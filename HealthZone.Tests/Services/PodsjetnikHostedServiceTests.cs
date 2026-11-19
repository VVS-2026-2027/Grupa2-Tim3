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
}

// ─── Testovi ────────────────────────────────────────────────────────────
// Servis ima dvije petlje: podsjetnike šalje svaki dan u 08:00, a statuse termina provjerava svake minute.
// Testovi pokreću servis, puštaju jedan krug provjere statusa i zaustavljaju ga otkazivanjem tokena.
[TestClass]
public class PodsjetnikHostedServiceTests
{
    private static TimeSpan _maksimalnoTrajanje;   // samo za čitanje

    private Mock<ITerminService> _terminServis = null!;
    private Mock<ILogger<PodsjetnikHostedService>> _logger = null!;
    private CancellationTokenSource _cts = null!;
    private PodsjetnikHostedService _servis = null!;

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
    }

    [TestCleanup]
    public void Cleanup()
    {
        _servis.Dispose();
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
}
