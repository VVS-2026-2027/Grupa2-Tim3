using System.Globalization;
using HealthZone.Models;
using HealthZone.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za PodsjetnikHostedServiceTests ───
public static class PodsjetnikHostedServicePodaci
{
    // ── pomoćne metode ──

    // Termin čiji početak je pomaknut za zadani broj sati od trenutnog vremena.
    public static Termin NapraviTermin(int terminId, Status status, double pomakSati)
    {
        var vrijeme = DateTime.Now.AddHours(pomakSati);
        return new Termin { TerminId = terminId, Status = status, Datum = vrijeme.Date, Vrijeme = TimeOnly.FromDateTime(vrijeme) };
    }

    // Lanac zamjenskih objekata: IServiceScopeFactory → IServiceScope → IServiceProvider → ITerminService.
    public static Mock<IServiceScopeFactory> NapraviScopeFactory(ITerminService terminServis)
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(ITerminService))).Returns(terminServis);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(provider.Object);

        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);
        return factory;
    }

    // Pokreće pozadinski servis i čeka da se zaustavi (test otkaže token čim servis pročita termine).
    // StartAsync i ExecuteTask mogu završiti kao otkazani, pa se čeka preko WhenAny, koji ne baca izuzetak.
    public static async Task PokreniJedanKrug(PodsjetnikHostedService servis, CancellationToken token)
    {
        var pokretanje = servis.StartAsync(token);
        await Task.WhenAny(pokretanje, Task.Delay(TimeSpan.FromSeconds(5)));
        await Task.WhenAny(servis.ExecuteTask ?? Task.CompletedTask, Task.Delay(TimeSpan.FromSeconds(5)));
    }

    // Zamjena za Task.Delay: zabilježi koliko bi servis čekao i odmah nastavi, kao da je vrijeme isteklo.
    public static Func<TimeSpan, CancellationToken, Task> CekanjeKojeOdmahIstekne(List<TimeSpan> zabiljezenaCekanja) =>
        (trajanje, token) =>
        {
            zabiljezenaCekanja.Add(trajanje);
            return Task.CompletedTask;
        };

    // Zamjena za Task.Delay: zabilježi koliko bi servis čekao i zaustavi servis dok čeka
    // (kao da se aplikacija ugasila prije 08:00).
    public static Func<TimeSpan, CancellationToken, Task> CekanjeTokomKojegSeServisZaustavlja(
        List<TimeSpan> zabiljezenaCekanja, CancellationTokenSource cts) =>
        (trajanje, token) =>
        {
            zabiljezenaCekanja.Add(trajanje);
            cts.Cancel();
            return Task.CompletedTask;
        };

    // ── izvori podataka (CSV) ──
    // Kolona "akcija" kaže šta servis treba uraditi s terminom: otkazi, aktiviraj ili nista.

    private static IEnumerable<string[]> Termini() => CsvLoader.Ucitaj("PodsjetnikHostedService_termini.csv");

    private static object[] Red(string[] r) =>
        new object[] { int.Parse(r[0]), Enum.Parse<Status>(r[1]), double.Parse(r[2], CultureInfo.InvariantCulture) };

    public static IEnumerable<object[]> ZaOtkazivanje() => Termini().Where(r => r[3] == "otkazi").Select(Red);

    public static IEnumerable<object[]> BezOtkazivanja() => Termini().Where(r => r[3] != "otkazi").Select(Red);

    public static IEnumerable<object[]> ZaAktivaciju() => Termini().Where(r => r[3] == "aktiviraj").Select(Red);

    public static IEnumerable<object[]> BezAktivacije() => Termini().Where(r => r[3] != "aktiviraj").Select(Red);

    // ── izvori podataka (XML) ──

    public static IEnumerable<object[]> Greske() =>
        XmlLoader.Ucitaj("PodsjetnikHostedService_greske.xml", "greska")
                 .Select(x => new object[] { (string)x.Element("poruka")! });

    // Greške koje baci slanje podsjetnika (npr. pad SMTP servera).
    public static IEnumerable<object[]> GreskeSlanja() =>
        XmlLoader.Ucitaj("PodsjetnikHostedService_greskeSlanja.xml", "greska")
                 .Select(x => new object[] { (string)x.Element("poruka")! });

    // ── izvori podataka za petlju podsjetnika (CSV) ──
    // Trenutno vrijeme je broj sati od ponoći, a očekivano čekanje je broj sati do 08:00 sljedećeg dana.

    private static IEnumerable<string[]> Cekanje() => CsvLoader.Ucitaj("PodsjetnikHostedService_cekanje.csv");

    public static IEnumerable<object[]> Cekanja() =>
        Cekanje().Select(r => new object[]
        {
            double.Parse(r[0], CultureInfo.InvariantCulture),
            double.Parse(r[1], CultureInfo.InvariantCulture)
        });

    public static IEnumerable<object[]> TrenutnaVremena() =>
        Cekanje().Select(r => new object[] { double.Parse(r[0], CultureInfo.InvariantCulture) });
}

// ─── Testovi ────────────────────────────────────────────────────────────
// Servis ima dvije petlje: podsjetnike šalje svaki dan u 08:00, a statuse termina provjerava svake minute.
// Testovi pokreću servis, puštaju jedan krug provjere statusa i zaustavljaju ga otkazivanjem tokena.
// Za petlju podsjetnika se koristi drugi konstruktor, kojem se proslijede zamjene za Task.Delay i DateTime.Now,
// pa test ne mora čekati do 08:00 (_servisSaSatom).
[TestClass]
public class PodsjetnikHostedServiceTests
{
    private static TimeSpan _maksimalnoTrajanje;   // samo za čitanje

    private Mock<ITerminService> _terminServis = null!;
    private Mock<ILogger<PodsjetnikHostedService>> _logger = null!;
    private CancellationTokenSource _cts = null!;
    private PodsjetnikHostedService _servis = null!;

    // Za petlju podsjetnika: servis sa zamijenjenim čekanjem i trenutnim vremenom.
    private PodsjetnikHostedService _servisSaSatom = null!;
    private List<TimeSpan> _zabiljezenaCekanja = null!;
    private Func<TimeSpan, CancellationToken, Task> _cekanje = null!;
    private DateTime _trenutnoVrijeme;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _maksimalnoTrajanje = TimeSpan.FromSeconds(5);
    }

    [TestInitialize]
    public void Setup()
    {
        _terminServis = new Mock<ITerminService>();
        _logger = new Mock<ILogger<PodsjetnikHostedService>>();
        _cts = new CancellationTokenSource(_maksimalnoTrajanje);
        var scopeFactory = PodsjetnikHostedServicePodaci.NapraviScopeFactory(_terminServis.Object);
        _servis = new PodsjetnikHostedService(scopeFactory.Object, _logger.Object);

        // Test u Arrange po potrebi promijeni _cekanje i _trenutnoVrijeme, a servis ih čita preko lambdi.
        _zabiljezenaCekanja = new List<TimeSpan>();
        _trenutnoVrijeme = DateTime.Now;
        _cekanje = PodsjetnikHostedServicePodaci.CekanjeKojeOdmahIstekne(_zabiljezenaCekanja);
        _servisSaSatom = new PodsjetnikHostedService(scopeFactory.Object, _logger.Object,
            (trajanje, token) => _cekanje(trajanje, token), () => _trenutnoVrijeme);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _servis.Dispose();
        _servisSaSatom.Dispose();
        _cts.Dispose();
        _terminServis.Reset();
        _logger.Reset();
    }

    // ─── AUTOMATSKO OTKAZIVANJE ───────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(PodsjetnikHostedServicePodaci.ZaOtkazivanje), typeof(PodsjetnikHostedServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    [Timeout(10000)]
    public async Task StatusLoop_NepotvrdjenTerminZa12SatiIliManje_OtkazujeTermin(int terminId, Status status, double pomakSati)
    {
        // Arrange
        var termin = PodsjetnikHostedServicePodaci.NapraviTermin(terminId, status, pomakSati);
        _terminServis.Setup(s => s.GetAllAsync()).Callback(() => _cts.Cancel()).ReturnsAsync(new List<Termin> { termin });

        // Act
        await PodsjetnikHostedServicePodaci.PokreniJedanKrug(_servis, _cts.Token);

        // Assert
        _terminServis.Verify(s => s.OtkaziTerminAsync(terminId), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(PodsjetnikHostedServicePodaci.BezOtkazivanja), typeof(PodsjetnikHostedServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Timeout(10000)]
    public async Task StatusLoop_TerminNijeNepotvrdjenIliJeDaleko_NeOtkazujeTermin(int terminId, Status status, double pomakSati)
    {
        // Arrange
        var termin = PodsjetnikHostedServicePodaci.NapraviTermin(terminId, status, pomakSati);
        _terminServis.Setup(s => s.GetAllAsync()).Callback(() => _cts.Cancel()).ReturnsAsync(new List<Termin> { termin });

        // Act
        await PodsjetnikHostedServicePodaci.PokreniJedanKrug(_servis, _cts.Token);

        // Assert
        _terminServis.Verify(s => s.OtkaziTerminAsync(It.IsAny<int>()), Times.Never);
    }

    // ─── AUTOMATSKA AKTIVACIJA ────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(PodsjetnikHostedServicePodaci.ZaAktivaciju), typeof(PodsjetnikHostedServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    [Timeout(10000)]
    public async Task StatusLoop_PotvrdjenTerminJePoceo_OznacavaGaKaoAktivan(int terminId, Status status, double pomakSati)
    {
        // Arrange
        var termin = PodsjetnikHostedServicePodaci.NapraviTermin(terminId, status, pomakSati);
        _terminServis.Setup(s => s.GetAllAsync()).Callback(() => _cts.Cancel()).ReturnsAsync(new List<Termin> { termin });

        // Act
        await PodsjetnikHostedServicePodaci.PokreniJedanKrug(_servis, _cts.Token);

        // Assert
        _terminServis.Verify(s => s.OznaciKaoAktivanAsync(terminId), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(PodsjetnikHostedServicePodaci.BezAktivacije), typeof(PodsjetnikHostedServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Timeout(10000)]
    public async Task StatusLoop_TerminNijePotvrdjenIliNijePoceo_NeOznacavaGaKaoAktivan(int terminId, Status status, double pomakSati)
    {
        // Arrange
        var termin = PodsjetnikHostedServicePodaci.NapraviTermin(terminId, status, pomakSati);
        _terminServis.Setup(s => s.GetAllAsync()).Callback(() => _cts.Cancel()).ReturnsAsync(new List<Termin> { termin });

        // Act
        await PodsjetnikHostedServicePodaci.PokreniJedanKrug(_servis, _cts.Token);

        // Assert
        _terminServis.Verify(s => s.OznaciKaoAktivanAsync(It.IsAny<int>()), Times.Never);
    }

    // ─── GREŠKE ───────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(PodsjetnikHostedServicePodaci.Greske), typeof(PodsjetnikHostedServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Timeout(10000)]
    public async Task StatusLoop_ServisBaciIzuzetak_ZapisujeGreskuULog(string poruka)
    {
        // Arrange
        _terminServis.Setup(s => s.GetAllAsync()).Callback(() => _cts.Cancel()).ThrowsAsync(new Exception(poruka));

        // Act
        await PodsjetnikHostedServicePodaci.PokreniJedanKrug(_servis, _cts.Token);

        // Assert
        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.Is<Exception>(e => e.Message == poruka),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(PodsjetnikHostedServicePodaci.Greske), typeof(PodsjetnikHostedServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Timeout(10000)]
    public async Task StatusLoop_ServisBaciIzuzetak_ServisSeZaustavljaBezPada(string poruka)
    {
        // Arrange
        _terminServis.Setup(s => s.GetAllAsync()).Callback(() => _cts.Cancel()).ThrowsAsync(new Exception(poruka));

        // Act
        await PodsjetnikHostedServicePodaci.PokreniJedanKrug(_servis, _cts.Token);

        // Assert
        Assert.IsFalse(_servis.ExecuteTask!.IsFaulted);
    }

    [TestMethod]
    [DynamicData(nameof(PodsjetnikHostedServicePodaci.ZaOtkazivanje), typeof(PodsjetnikHostedServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Timeout(10000)]
    public async Task StatusLoop_JedanKrugProvjere_CitaTermineJednom(int terminId, Status status, double pomakSati)
    {
        // Arrange
        var termin = PodsjetnikHostedServicePodaci.NapraviTermin(terminId, status, pomakSati);
        _terminServis.Setup(s => s.GetAllAsync()).Callback(() => _cts.Cancel()).ReturnsAsync(new List<Termin> { termin });

        // Act
        await PodsjetnikHostedServicePodaci.PokreniJedanKrug(_servis, _cts.Token);

        // Assert
        _terminServis.Verify(s => s.GetAllAsync(), Times.Once);
    }

    // ─── SLANJE PODSJETNIKA (svaki dan u 08:00) ───────────────────────────

    [TestMethod]
    [DynamicData(nameof(PodsjetnikHostedServicePodaci.Cekanja), typeof(PodsjetnikHostedServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    [Timeout(10000)]
    public async Task PodsjetnikLoop_TrenutnoVrijeme_CekaDoOsamSatiSljedecegDana(double satiOdPonoci, double ocekivaniSatiCekanja)
    {
        // Arrange
        _trenutnoVrijeme = DateTime.Today.AddHours(satiOdPonoci);
        _cekanje = PodsjetnikHostedServicePodaci.CekanjeTokomKojegSeServisZaustavlja(_zabiljezenaCekanja, _cts);

        // Act
        await PodsjetnikHostedServicePodaci.PokreniJedanKrug(_servisSaSatom, _cts.Token);

        // Assert
        Assert.AreEqual(TimeSpan.FromHours(ocekivaniSatiCekanja), _zabiljezenaCekanja.First());
    }

    [TestMethod]
    [DynamicData(nameof(PodsjetnikHostedServicePodaci.TrenutnaVremena), typeof(PodsjetnikHostedServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Timeout(10000)]
    public async Task PodsjetnikLoop_ServisZaustavljenTokomCekanja_NeSaljePodsjetnike(double satiOdPonoci)
    {
        // Arrange
        _trenutnoVrijeme = DateTime.Today.AddHours(satiOdPonoci);
        _cekanje = PodsjetnikHostedServicePodaci.CekanjeTokomKojegSeServisZaustavlja(_zabiljezenaCekanja, _cts);

        // Act
        await PodsjetnikHostedServicePodaci.PokreniJedanKrug(_servisSaSatom, _cts.Token);

        // Assert
        _terminServis.Verify(s => s.PošaljiPodsjetnikeSutraAsync(), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(PodsjetnikHostedServicePodaci.TrenutnaVremena), typeof(PodsjetnikHostedServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    [Timeout(10000)]
    public async Task PodsjetnikLoop_DocekanoVrijemeSlanja_SaljePodsjetnike(double satiOdPonoci)
    {
        // Arrange
        _trenutnoVrijeme = DateTime.Today.AddHours(satiOdPonoci);
        _terminServis.Setup(s => s.PošaljiPodsjetnikeSutraAsync()).Callback(() => _cts.Cancel()).Returns(Task.CompletedTask);

        // Act
        await PodsjetnikHostedServicePodaci.PokreniJedanKrug(_servisSaSatom, _cts.Token);

        // Assert
        _terminServis.Verify(s => s.PošaljiPodsjetnikeSutraAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(PodsjetnikHostedServicePodaci.GreskeSlanja), typeof(PodsjetnikHostedServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Timeout(10000)]
    public async Task PodsjetnikLoop_SlanjeBaciIzuzetak_ZapisujeGreskuULog(string poruka)
    {
        // Arrange
        _terminServis.Setup(s => s.PošaljiPodsjetnikeSutraAsync()).Callback(() => _cts.Cancel()).ThrowsAsync(new Exception(poruka));

        // Act
        await PodsjetnikHostedServicePodaci.PokreniJedanKrug(_servisSaSatom, _cts.Token);

        // Assert
        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.Is<Exception>(e => e.Message == poruka),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(PodsjetnikHostedServicePodaci.GreskeSlanja), typeof(PodsjetnikHostedServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Timeout(10000)]
    public async Task PodsjetnikLoop_SlanjeBaciIzuzetak_ServisSeZaustavljaBezPada(string poruka)
    {
        // Arrange
        _terminServis.Setup(s => s.PošaljiPodsjetnikeSutraAsync()).Callback(() => _cts.Cancel()).ThrowsAsync(new Exception(poruka));

        // Act
        await PodsjetnikHostedServicePodaci.PokreniJedanKrug(_servisSaSatom, _cts.Token);

        // Assert
        Assert.IsTrue(_servisSaSatom.ExecuteTask!.IsCompletedSuccessfully);
    }
}
