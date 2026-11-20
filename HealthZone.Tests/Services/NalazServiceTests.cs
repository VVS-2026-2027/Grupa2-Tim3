using System.Net.Mail;
using System.Xml.Linq;
using HealthZone.Models;
using HealthZone.Repositories;
using HealthZone.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za NalazServiceTests ──────────────
public static class NalazServicePodaci
{
    // ── nalazi za print i email (NalazService_nalazi.xml) ──

    private static IEnumerable<XElement> NalaziXml() => XmlLoader.Ucitaj("NalazService_nalazi.xml", "nalaz");

    public static string FormatDatuma()
        => (string)XmlLoader.Ucitaj("NalazService_nalazi.xml", "nalazi").Single().Attribute("formatDatuma")!;

    // Nalaz s pacijentom i terminom (ako ih XML ima), svaki put novi objekat.
    public static Nalaz NapraviNalaz(int id)
    {
        var x = NalaziXml().Single(n => (int)n.Attribute("id")! == id);
        var p = x.Element("pacijent");
        var t = x.Element("termin");
        return new Nalaz
        {
            NalazId = id,
            Opis = (string)x.Attribute("opis")!,
            Dijagnoza = (string)x.Attribute("dijagnoza")!,
            Terapija = (string)x.Attribute("terapija")!,
            Pacijent = p is null ? null : new Korisnik
            {
                Ime = (string)p.Attribute("ime")!,
                Prezime = (string)p.Attribute("prezime")!,
                BrojKartona = (string)p.Attribute("brojKartona")!,
                Email = (string)p.Attribute("email")!
            },
            Termin = t is null ? null : new Termin
            {
                Datum = DateTime.Today.AddDays((int)t.Attribute("pomakDana")!),
                Vrijeme = TimeOnly.Parse((string)t.Attribute("vrijeme")!)
            }
        };
    }

    private static IEnumerable<object[]> Idevi(Func<XElement, bool> uslov)
        => NalaziXml().Where(uslov).Select(n => new object[] { (int)n.Attribute("id")! });

    public static IEnumerable<object[]> NalaziSPacijentom() => Idevi(n => n.Element("pacijent") != null);
    public static IEnumerable<object[]> NalaziBezPacijenta() => Idevi(n => n.Element("pacijent") == null);
    public static IEnumerable<object[]> NalaziSTerminom() => Idevi(n => n.Element("termin") != null);
    public static IEnumerable<object[]> NalaziSTerapijom() => Idevi(n => (string)n.Attribute("terapija")! != string.Empty);
    public static IEnumerable<object[]> NalaziBezTerapije() =>
        NalaziXml().Where(n => (string)n.Attribute("terapija")! == string.Empty)
                   .Select(n => new object[] { (int)n.Attribute("id")!, (string)n.Attribute("oznakaTerapije")! });
    public static IEnumerable<object[]> SviNalazi() => Idevi(n => true);

    // ── SMTP konfiguracija (NalazService_konfiguracija.xml) ──

    private static XElement KonfiguracijaXml(string naziv)
        => XmlLoader.Ucitaj("NalazService_konfiguracija.xml", "scenarij").Single(s => (string)s.Attribute("naziv")! == naziv);

    // Prava (in-memory) konfiguracija sa samo onim Smtp* ključevima koje scenarij ima.
    public static IConfiguration Konfiguracija(string naziv)
    {
        var kljucevi = KonfiguracijaXml(naziv).Elements()
            .Where(e => e.Name.LocalName.StartsWith("Smtp"))
            .ToDictionary(e => "EmailSettings:" + e.Name.LocalName, e => (string?)e.Value);
        return new ConfigurationBuilder().AddInMemoryCollection(kljucevi).Build();
    }

    public static string Ocekivano(string naziv, string element) => (string)KonfiguracijaXml(naziv).Element(element)!;

    // Očekivane SMTP postavke za zadanu konfiguraciju.
    public static string OcekivaniHost(string naziv) => Ocekivano(naziv, "ocekivaniHost");
    public static int OcekivaniPort(string naziv) => int.Parse(Ocekivano(naziv, "ocekivaniPort"));
    public static string OcekivaniKorisnik(string naziv) => Ocekivano(naziv, "SmtpUser");
    public static string OcekivanaLozinka(string naziv) => Ocekivano(naziv, "SmtpPass");
    public static bool OcekivaniSsl(string naziv) => bool.Parse(Ocekivano(naziv, "ocekivaniSsl"));

    // Ime i prezime pacijenta onako kako se ispisuje na printu nalaza.
    public static string ImeIPrezime(Korisnik pacijent) => $"{pacijent.Ime} {pacijent.Prezime}";

    // Novi nalaz za zadani termin; opis, dijagnoza i terapija su iz prvog nalaza u XML-u.
    public static Nalaz NoviNalazZaTermin(int terminId)
    {
        var nalaz = NapraviNalaz((int)NalaziXml().First().Attribute("id")!);
        nalaz.TerminID = terminId;
        return nalaz;
    }

    // ── nalazi po pacijentu i terminu (NalazService_nalaziPacijenata.xml) ──

    public static List<Nalaz> NalaziPacijenata() =>
        XmlLoader.Ucitaj("NalazService_nalaziPacijenata.xml", "nalaz").Select(x => new Nalaz
        {
            NalazId = (int)x.Attribute("id")!,
            PacijentID = (string)x.Attribute("pacijent")!,
            TerminID = (int?)x.Attribute("termin") ?? default,
            Termin = x.Attribute("pomakDana") is null
                ? null
                : new Termin { Datum = DateTime.Today.AddDays((int)x.Attribute("pomakDana")!) }
        }).ToList();

    public static IEnumerable<object[]> RedoslijedPoPacijentu() =>
        XmlLoader.Ucitaj("NalazService_nalaziPacijenata.xml", "scenarij").Select(x => new object[]
        {
            (string)x.Attribute("pacijent")!,
            ((string)x.Attribute("ocekivaniRedoslijed")!).Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse).ToArray()
        });

    // ── CSV izvori ──

    public static IEnumerable<object[]> BrojeviPromjena() =>
        CsvLoader.Ucitaj("NalazService_snimanje.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> PraznaPolja() =>
        CsvLoader.Ucitaj("NalazService_praznaPolja.csv").Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> TerminNePostoji() =>
        CsvLoader.Ucitaj("NalazService_terminNePostoji.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> NeaktivniTermini() =>
        CsvLoader.Ucitaj("NalazService_neaktivanTermin.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> PostojeciNalazZaTermin() =>
        CsvLoader.Ucitaj("NalazService_postojiNalaz.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2], r[3] });

    public static IEnumerable<object[]> ValidniNalazi() =>
        CsvLoader.Ucitaj("NalazService_validanNalaz.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1], r[2], r[3], r[4], r[5], int.Parse(r[6]) });

    public static IEnumerable<object[]> NalazZaTermin() =>
        CsvLoader.Ucitaj("NalazService_nalazZaTermin.csv").Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]) });

    public static IEnumerable<object[]> TerminBezNalaza() =>
        CsvLoader.Ucitaj("NalazService_bezNalazaZaTermin.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> NepostojeciNalazi() =>
        CsvLoader.Ucitaj("NalazService_nepostojeciNalaz.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> PacijentBezEmaila() =>
        CsvLoader.Ucitaj("NalazService_bezEmaila.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> NepotpunaKonfiguracija() =>
        CsvLoader.Ucitaj("NalazService_greskaKonfiguracije.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> Slanje() =>
        CsvLoader.Ucitaj("NalazService_slanje.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class NalazServiceTests
{
    private static string _formatDatuma = null!;   // samo za čitanje

    private Mock<INalazRepository> _nalazRepo = null!;
    private Mock<ITerminRepository> _terminRepo = null!;
    private Mock<ISmtpSender> _smtp = null!;
    private IConfiguration _konfiguracija = null!;   // Fake: prava in-memory konfiguracija
    private NalazService _servis = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _formatDatuma = NalazServicePodaci.FormatDatuma();
    }

    [TestInitialize]
    public void Setup()
    {
        _nalazRepo = new Mock<INalazRepository>();
        _terminRepo = new Mock<ITerminRepository>();
        _smtp = new Mock<ISmtpSender>();
        _konfiguracija = NalazServicePodaci.Konfiguracija("potpuna");
        _servis = new NalazService(_nalazRepo.Object, _terminRepo.Object, _konfiguracija, _smtp.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _nalazRepo.Reset();
        _terminRepo.Reset();
        _smtp.Reset();
    }

    // ─── PROSLJEĐIVANJE REPOZITORIJU ──────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.SviNalazi), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetByIdAsync_NalazPostoji_VracaNalazIzRepozitorija(int id)
    {
        // Arrange
        var nalaz = NalazServicePodaci.NapraviNalaz(id);
        _nalazRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(nalaz);

        // Act
        var rezultat = await _servis.GetByIdAsync(id);

        // Assert
        Assert.AreSame(nalaz, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.RedoslijedPoPacijentu), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetAllAsync_RepozitorijImaNalaze_VracaSveNalaze(string pacijentId, int[] ocekivaniRedoslijed)
    {
        // Arrange
        var nalazi = NalazServicePodaci.NalaziPacijenata();
        _nalazRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(nalazi);

        // Act
        var rezultat = await _servis.GetAllAsync();

        // Assert
        Assert.AreSame(nalazi, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.SviNalazi), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Update_ProslijedjenNalaz_PozivaRepozitorij(int id)
    {
        // Arrange
        var nalaz = NalazServicePodaci.NapraviNalaz(id);

        // Act
        _servis.Update(nalaz);

        // Assert
        _nalazRepo.Verify(r => r.Update(nalaz), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.SviNalazi), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Delete_ProslijedjenNalaz_PozivaRepozitorij(int id)
    {
        // Arrange
        var nalaz = NalazServicePodaci.NapraviNalaz(id);

        // Act
        _servis.Delete(nalaz);

        // Assert
        _nalazRepo.Verify(r => r.Delete(nalaz), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.BrojeviPromjena), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task SaveChangesAsync_RepozitorijSnimi_VracaBrojPromjena(int brojPromjena)
    {
        // Arrange
        _nalazRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(brojPromjena);

        // Act
        var rezultat = await _servis.SaveChangesAsync();

        // Assert
        Assert.AreEqual(brojPromjena, rezultat);
    }

    // ─── DODAVANJE NALAZA ─────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.PraznaPolja), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task AddAsync_PraznoObaveznoPolje_BacaException(string dijagnoza, string opis, string ocekivanaPoruka)
    {
        // Arrange
        var nalaz = new Nalaz { Dijagnoza = dijagnoza, Opis = opis };

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(nalaz));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.PraznaPolja), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_PraznoObaveznoPolje_NeTraziTermin(string dijagnoza, string opis, string ocekivanaPoruka)
    {
        // Arrange
        var nalaz = new Nalaz { Dijagnoza = dijagnoza, Opis = opis };

        // Act
        await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(nalaz));

        // Assert
        _terminRepo.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.TerminNePostoji), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_TerminNePostoji_BacaException(int terminId, string ocekivanaPoruka)
    {
        // Arrange
        var nalaz = NalazServicePodaci.NoviNalazZaTermin(terminId);
        _terminRepo.Setup(r => r.GetByIdAsync(terminId)).ReturnsAsync((Termin?)null);

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(nalaz));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.NeaktivniTermini), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task AddAsync_TerminNijeAktivan_BacaException(int terminId, string status, string ocekivanaPoruka)
    {
        // Arrange
        var nalaz = NalazServicePodaci.NoviNalazZaTermin(terminId);
        _terminRepo.Setup(r => r.GetByIdAsync(terminId))
                   .ReturnsAsync(new Termin { TerminId = terminId, Status = Enum.Parse<Status>(status) });

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(nalaz));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.PostojeciNalazZaTermin), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_TerminVecImaNalaz_BacaException(int terminId, string pacijentId, string status,
        string ocekivanaPoruka)
    {
        // Arrange
        var nalaz = NalazServicePodaci.NoviNalazZaTermin(terminId);
        _terminRepo.Setup(r => r.GetByIdAsync(terminId)).ReturnsAsync(
            new Termin { TerminId = terminId, PacijentID = pacijentId, Status = Enum.Parse<Status>(status) });
        _nalazRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Nalaz> { new() { TerminID = terminId } });

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(nalaz));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.PostojeciNalazZaTermin), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_TerminVecImaNalaz_NeDodajeNoviNalaz(int terminId, string pacijentId, string status,
        string ocekivanaPoruka)
    {
        // Arrange
        var nalaz = NalazServicePodaci.NoviNalazZaTermin(terminId);
        _terminRepo.Setup(r => r.GetByIdAsync(terminId)).ReturnsAsync(
            new Termin { TerminId = terminId, PacijentID = pacijentId, Status = Enum.Parse<Status>(status) });
        _nalazRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Nalaz> { new() { TerminID = terminId } });

        // Act
        await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(nalaz));

        // Assert
        _nalazRepo.Verify(r => r.AddAsync(It.IsAny<Nalaz>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.ValidniNalazi), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task AddAsync_ValidanNalaz_PreuzimaPacijentaIzTermina(int terminId, string pacijentTermina,
        string status, string opis, string dijagnoza, string terapija, int terminDrugogNalaza)
    {
        // Arrange
        var nalaz = new Nalaz { TerminID = terminId, Opis = opis, Dijagnoza = dijagnoza, Terapija = terapija };
        _terminRepo.Setup(r => r.GetByIdAsync(terminId)).ReturnsAsync(
            new Termin { TerminId = terminId, PacijentID = pacijentTermina, Status = Enum.Parse<Status>(status) });
        _nalazRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Nalaz> { new() { TerminID = terminDrugogNalaza } });

        // Act
        await _servis.AddAsync(nalaz);

        // Assert
        Assert.AreEqual(pacijentTermina, nalaz.PacijentID);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.ValidniNalazi), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task AddAsync_ValidanNalaz_DodajeNalazURepozitorij(int terminId, string pacijentTermina,
        string status, string opis, string dijagnoza, string terapija, int terminDrugogNalaza)
    {
        // Arrange
        var nalaz = new Nalaz { TerminID = terminId, Opis = opis, Dijagnoza = dijagnoza, Terapija = terapija };
        _terminRepo.Setup(r => r.GetByIdAsync(terminId)).ReturnsAsync(
            new Termin { TerminId = terminId, PacijentID = pacijentTermina, Status = Enum.Parse<Status>(status) });
        _nalazRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Nalaz> { new() { TerminID = terminDrugogNalaza } });

        // Act
        await _servis.AddAsync(nalaz);

        // Assert
        _nalazRepo.Verify(r => r.AddAsync(nalaz), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.ValidniNalazi), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_ValidanNalaz_SnimaPromjene(int terminId, string pacijentTermina,
        string status, string opis, string dijagnoza, string terapija, int terminDrugogNalaza)
    {
        // Arrange
        var nalaz = new Nalaz { TerminID = terminId, Opis = opis, Dijagnoza = dijagnoza, Terapija = terapija };
        _terminRepo.Setup(r => r.GetByIdAsync(terminId)).ReturnsAsync(
            new Termin { TerminId = terminId, PacijentID = pacijentTermina, Status = Enum.Parse<Status>(status) });
        _nalazRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Nalaz> { new() { TerminID = terminDrugogNalaza } });

        // Act
        await _servis.AddAsync(nalaz);

        // Assert
        _nalazRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ─── NALAZI PO PACIJENTU I TERMINU ────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.RedoslijedPoPacijentu), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task GetNalaziPacijentaAsync_PacijentImaNalaze_VracaNjegoveOdNajnovijeg(string pacijentId,
        int[] ocekivaniRedoslijed)
    {
        // Arrange
        _nalazRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(NalazServicePodaci.NalaziPacijenata());

        // Act
        var rezultat = await _servis.GetNalaziPacijentaAsync(pacijentId);

        // Assert
        CollectionAssert.AreEqual(ocekivaniRedoslijed, rezultat.Select(n => n.NalazId).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.NalazZaTermin), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetNalazZaTerminAsync_TerminImaNalaz_VracaTajNalaz(int terminId, int ocekivaniNalaz)
    {
        // Arrange
        _nalazRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(NalazServicePodaci.NalaziPacijenata());

        // Act
        var rezultat = await _servis.GetNalazZaTerminAsync(terminId);

        // Assert
        Assert.AreEqual(ocekivaniNalaz, rezultat!.NalazId);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.TerminBezNalaza), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetNalazZaTerminAsync_TerminNemaNalaz_VracaNull(int terminId)
    {
        // Arrange
        _nalazRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(NalazServicePodaci.NalaziPacijenata());

        // Act
        var rezultat = await _servis.GetNalazZaTerminAsync(terminId);

        // Assert
        Assert.IsNull(rezultat);
    }

    // ─── HTML ZA PRINT ────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.NepostojeciNalazi), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GenerirajHtmlZaPrintAsync_NalazNePostoji_BacaException(int id, string ocekivanaPoruka)
    {
        // Arrange
        _nalazRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Nalaz?)null);

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.GenerirajHtmlZaPrintAsync(id));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.NalaziSPacijentom), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task GenerirajHtmlZaPrintAsync_NalazSPacijentom_PrikazujeImeIPrezime(int id)
    {
        // Arrange
        var nalaz = NalazServicePodaci.NapraviNalaz(id);
        _nalazRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(nalaz);

        // Act
        var html = await _servis.GenerirajHtmlZaPrintAsync(id);

        // Assert
        StringAssert.Contains(html, NalazServicePodaci.ImeIPrezime(nalaz.Pacijent!));
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.NalaziSPacijentom), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GenerirajHtmlZaPrintAsync_NalazSPacijentom_PrikazujeBrojKartona(int id)
    {
        // Arrange
        var nalaz = NalazServicePodaci.NapraviNalaz(id);
        _nalazRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(nalaz);

        // Act
        var html = await _servis.GenerirajHtmlZaPrintAsync(id);

        // Assert
        StringAssert.Contains(html, nalaz.Pacijent!.BrojKartona);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.NalaziSTerminom), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GenerirajHtmlZaPrintAsync_NalazSTerminom_PrikazujeDatumPregleda(int id)
    {
        // Arrange
        var nalaz = NalazServicePodaci.NapraviNalaz(id);
        _nalazRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(nalaz);

        // Act
        var html = await _servis.GenerirajHtmlZaPrintAsync(id);

        // Assert
        StringAssert.Contains(html, nalaz.Termin!.Datum.ToString(_formatDatuma));
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.SviNalazi), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GenerirajHtmlZaPrintAsync_NalazPostoji_PrikazujeDijagnozu(int id)
    {
        // Arrange
        var nalaz = NalazServicePodaci.NapraviNalaz(id);
        _nalazRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(nalaz);

        // Act
        var html = await _servis.GenerirajHtmlZaPrintAsync(id);

        // Assert
        StringAssert.Contains(html, nalaz.Dijagnoza);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.SviNalazi), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GenerirajHtmlZaPrintAsync_NalazPostoji_PrikazujeOpis(int id)
    {
        // Arrange
        var nalaz = NalazServicePodaci.NapraviNalaz(id);
        _nalazRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(nalaz);

        // Act
        var html = await _servis.GenerirajHtmlZaPrintAsync(id);

        // Assert
        StringAssert.Contains(html, nalaz.Opis);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.NalaziSTerapijom), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GenerirajHtmlZaPrintAsync_NalazSTerapijom_PrikazujeTerapiju(int id)
    {
        // Arrange
        var nalaz = NalazServicePodaci.NapraviNalaz(id);
        _nalazRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(nalaz);

        // Act
        var html = await _servis.GenerirajHtmlZaPrintAsync(id);

        // Assert
        StringAssert.Contains(html, nalaz.Terapija);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.NalaziBezTerapije), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GenerirajHtmlZaPrintAsync_NalazBezTerapije_NemaSekcijeTerapije(int id, string oznakaTerapije)
    {
        // Arrange
        _nalazRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(NalazServicePodaci.NapraviNalaz(id));

        // Act
        var html = await _servis.GenerirajHtmlZaPrintAsync(id);

        // Assert
        Assert.IsFalse(html.Contains(oznakaTerapije));
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.NalaziBezPacijenta), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GenerirajHtmlZaPrintAsync_NalazBezPacijentaITermina_IpakGeneriseHtml(int id)
    {
        // Arrange
        var nalaz = NalazServicePodaci.NapraviNalaz(id);
        _nalazRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(nalaz);

        // Act
        var html = await _servis.GenerirajHtmlZaPrintAsync(id);

        // Assert
        StringAssert.Contains(html, nalaz.Dijagnoza);
    }

    // ─── SLANJE NALAZA NA EMAIL ───────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.NepostojeciNalazi), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PosaljiNaEmailAsync_NalazNePostoji_BacaException(int id, string ocekivanaPoruka)
    {
        // Arrange
        _nalazRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Nalaz?)null);

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.PosaljiNaEmailAsync(id));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.PacijentBezEmaila), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task PosaljiNaEmailAsync_PacijentBezEmaila_BacaException(int id, string opis, string ocekivanaPoruka)
    {
        // Arrange
        _nalazRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(NalazServicePodaci.NapraviNalaz(id));

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.PosaljiNaEmailAsync(id));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message, opis);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.NepotpunaKonfiguracija), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Fake"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PosaljiNaEmailAsync_NepotpunaSmtpKonfiguracija_BacaException(int id, string konfiguracija,
        string ocekivanaPoruka)
    {
        // Arrange
        _nalazRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(NalazServicePodaci.NapraviNalaz(id));
        var servis = new NalazService(_nalazRepo.Object, _terminRepo.Object,
            NalazServicePodaci.Konfiguracija(konfiguracija), _smtp.Object);

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => servis.PosaljiNaEmailAsync(id));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.NepotpunaKonfiguracija), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Fake"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PosaljiNaEmailAsync_NepotpunaSmtpKonfiguracija_NeSaljeMail(int id, string konfiguracija,
        string ocekivanaPoruka)
    {
        // Arrange
        _nalazRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(NalazServicePodaci.NapraviNalaz(id));
        var servis = new NalazService(_nalazRepo.Object, _terminRepo.Object,
            NalazServicePodaci.Konfiguracija(konfiguracija), _smtp.Object);

        // Act
        await Assert.ThrowsExceptionAsync<Exception>(() => servis.PosaljiNaEmailAsync(id));

        // Assert
        _smtp.Verify(s => s.PosaljiAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.Slanje), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task PosaljiNaEmailAsync_IspravnaKonfiguracija_SaljeNaEmailPacijenta(int id, string konfiguracija,
        string ocekivaniNaslov)
    {
        // Arrange
        var nalaz = NalazServicePodaci.NapraviNalaz(id);
        _nalazRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(nalaz);
        var servis = new NalazService(_nalazRepo.Object, _terminRepo.Object,
            NalazServicePodaci.Konfiguracija(konfiguracija), _smtp.Object);

        // Act
        await servis.PosaljiNaEmailAsync(id);

        // Assert
        _smtp.Verify(s => s.PosaljiAsync(It.Is<MailMessage>(m => m.To.Single().Address == nalaz.Pacijent!.Email),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.Slanje), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PosaljiNaEmailAsync_IspravnaKonfiguracija_ImaOcekivaniNaslov(int id, string konfiguracija,
        string ocekivaniNaslov)
    {
        // Arrange
        _nalazRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(NalazServicePodaci.NapraviNalaz(id));
        var servis = new NalazService(_nalazRepo.Object, _terminRepo.Object,
            NalazServicePodaci.Konfiguracija(konfiguracija), _smtp.Object);

        // Act
        await servis.PosaljiNaEmailAsync(id);

        // Assert
        _smtp.Verify(s => s.PosaljiAsync(It.Is<MailMessage>(m => m.Subject == ocekivaniNaslov),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.Slanje), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PosaljiNaEmailAsync_IspravnaKonfiguracija_TijeloJeHtmlNalaza(int id, string konfiguracija,
        string ocekivaniNaslov)
    {
        // Arrange
        var nalaz = NalazServicePodaci.NapraviNalaz(id);
        _nalazRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(nalaz);
        var servis = new NalazService(_nalazRepo.Object, _terminRepo.Object,
            NalazServicePodaci.Konfiguracija(konfiguracija), _smtp.Object);

        // Act
        await servis.PosaljiNaEmailAsync(id);

        // Assert
        _smtp.Verify(s => s.PosaljiAsync(It.Is<MailMessage>(m => m.IsBodyHtml && m.Body.Contains(nalaz.Dijagnoza)),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NalazServicePodaci.Slanje), typeof(NalazServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task PosaljiNaEmailAsync_IspravnaKonfiguracija_KoristiSmtpPostavke(int id, string konfiguracija,
        string ocekivaniNaslov)
    {
        // Arrange
        _nalazRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(NalazServicePodaci.NapraviNalaz(id));
        var servis = new NalazService(_nalazRepo.Object, _terminRepo.Object,
            NalazServicePodaci.Konfiguracija(konfiguracija), _smtp.Object);
        var host = NalazServicePodaci.OcekivaniHost(konfiguracija);
        var port = NalazServicePodaci.OcekivaniPort(konfiguracija);
        var korisnik = NalazServicePodaci.OcekivaniKorisnik(konfiguracija);
        var lozinka = NalazServicePodaci.OcekivanaLozinka(konfiguracija);
        var ssl = NalazServicePodaci.OcekivaniSsl(konfiguracija);

        // Act
        await servis.PosaljiNaEmailAsync(id);

        // Assert
        _smtp.Verify(s => s.PosaljiAsync(It.IsAny<MailMessage>(), host, port, korisnik, lozinka, ssl), Times.Once);
    }
}
