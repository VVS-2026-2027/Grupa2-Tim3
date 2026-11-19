using System.Globalization;
using System.Xml.Linq;
using HealthZone.Models;
using HealthZone.Repositories;
using HealthZone.Services;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za TerminServiceTests ─────────────
// Izvori za [DynamicData] čitaju CSV/XML iz TestData, a ostale metode od tih podataka prave objekte modela.
public static class TerminServicePodaci
{
    // ───────────── CSV ─────────────

    public static IEnumerable<object[]> NevalidnoVrijeme() =>
        CsvLoader.Ucitaj("TerminService_nevalidnoVrijeme.csv")
                 .Select(r => new object[] { int.Parse(r[0]), double.Parse(r[1], CultureInfo.InvariantCulture), r[2] });

    public static IEnumerable<object[]> TerminNePostoji() =>
        CsvLoader.Ucitaj("TerminService_terminNePostoji.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> GreskaEmaila() =>
        CsvLoader.Ucitaj("TerminService_greskaEmaila.csv")
                 .Select(r => new object[] { r[0], r[1], int.Parse(r[2]), int.Parse(r[3]), r[4] });

    public static IEnumerable<object[]> Otkazivanje() =>
        CsvLoader.Ucitaj("TerminService_otkazivanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> OtkazivanjeStatus() =>
        CsvLoader.Ucitaj("TerminService_otkazivanjeStatus.csv")
                 .Select(r => new object[] { int.Parse(r[0]), Enum.Parse<Status>(r[1]) });

    public static IEnumerable<object[]> PromjenaTermina() =>
        CsvLoader.Ucitaj("TerminService_promjenaTermina.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), int.Parse(r[2]) });

    public static IEnumerable<object[]> PromjenaTerminaStatus() =>
        CsvLoader.Ucitaj("TerminService_promjenaTermina.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), int.Parse(r[2]), Enum.Parse<Status>(r[3]) });

    public static IEnumerable<object[]> Podsjetnici() =>
        CsvLoader.Ucitaj("TerminService_podsjetnici.csv")
                 .Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> PodsjetniciGreska() =>
        CsvLoader.Ucitaj("TerminService_podsjetnici.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> SlobodniTermini() =>
        CsvLoader.Ucitaj("TerminService_slobodniTermini.csv")
                 .Select(r => new object[] { r[0], int.Parse(r[1]), int.Parse(r[2]) });

    public static IEnumerable<object[]> TerminiDoktoraNaDan() =>
        CsvLoader.Ucitaj("TerminService_terminiDoktoraNaDan.csv")
                 .Select(r => new object[] { r[0], int.Parse(r[1]), int.Parse(r[2]) });

    public static IEnumerable<object[]> TerminiDoktora() =>
        CsvLoader.Ucitaj("TerminService_terminiDoktora.csv")
                 .Select(r => new object[] { r[0], int.Parse(r[1]) });

    public static IEnumerable<object[]> Dostupnost() =>
        CsvLoader.Ucitaj("TerminService_dostupnost.csv")
                 .Select(r => new object[] { r[0], int.Parse(r[1]), int.Parse(r[2]), bool.Parse(r[3]) });

    public static IEnumerable<object[]> PostojeciTermin() =>
        CsvLoader.Ucitaj("TerminService_postojeciTermin.csv")
                 .Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("TerminService_snimanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> NotifikacijaPodaci() =>
        CsvLoader.Ucitaj("TerminService_notifikacija.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1], Enum.Parse<Status>(r[2]) });

    // ───────────── XML ─────────────

    public static IEnumerable<object[]> Zakazivanje() =>
        XmlLoader.Ucitaj("TerminService_zakazivanje.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("doktorId")!, (string)x.Element("pacijentEmail")!,
                     (int)x.Element("pomakDana")!, (int)x.Element("sat")!
                 });

    public static IEnumerable<object[]> ZakazivanjeStatus() =>
        XmlLoader.Ucitaj("TerminService_zakazivanje.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("doktorId")!, (string)x.Element("pacijentEmail")!,
                     (int)x.Element("pomakDana")!, (int)x.Element("sat")!,
                     Enum.Parse<Status>((string)x.Element("ocekivaniStatus")!)
                 });

    public static IEnumerable<object[]> ZakazivanjeEmail() =>
        XmlLoader.Ucitaj("TerminService_zakazivanje.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("doktorId")!, (string)x.Element("pacijentEmail")!,
                     (int)x.Element("pomakDana")!, (int)x.Element("sat")!,
                     (int)x.Element("ocekivaniBrojMailova")!
                 });

    public static IEnumerable<object[]> NedostupanZakazivanje() =>
        XmlLoader.Ucitaj("TerminService_nedostupan.xml", "zakazivanje")
                 .Select(x => new object[]
                 {
                     (string)x.Element("doktorId")!, (int)x.Element("pomakDana")!,
                     (int)x.Element("sat")!, (string)x.Element("poruka")!
                 });

    public static IEnumerable<object[]> NedostupanPromjena() =>
        XmlLoader.Ucitaj("TerminService_nedostupan.xml", "promjena")
                 .Select(x => new object[]
                 {
                     (int)x.Element("terminId")!, (int)x.Element("pomakDana")!,
                     (int)x.Element("sat")!, (string)x.Element("poruka")!
                 });

    public static IEnumerable<object[]> OtkazanPotvrda() => OtkazanTermin("potvrda");
    public static IEnumerable<object[]> OtkazanOtkazivanje() => OtkazanTermin("otkazivanje");
    public static IEnumerable<object[]> OtkazanPromjena() => OtkazanTermin("promjena");

    private static IEnumerable<object[]> OtkazanTermin(string element) =>
        XmlLoader.Ucitaj("TerminService_otkazanTermin.xml", element)
                 .Select(x => new object[] { (int)x.Element("terminId")!, (string)x.Element("poruka")! });

    public static IEnumerable<object[]> StatusPotvrda() => Statusi("potvrda");
    public static IEnumerable<object[]> StatusAktivacija() => Statusi("aktivacija");

    private static IEnumerable<object[]> Statusi(string element) =>
        XmlLoader.Ucitaj("TerminService_statusi.xml", element)
                 .Select(x => new object[]
                 {
                     (int)x.Element("terminId")!, Enum.Parse<Status>((string)x.Element("ocekivaniStatus")!)
                 });

    public static IEnumerable<object[]> KandidatiNaListi() =>
        XmlLoader.Ucitaj("TerminService_listaCekanja.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (int)x.Element("terminId")!,
                     x.Elements("kandidat").Select(NapraviKandidata).ToList(),
                     (string)x.Element("ocekivaniEmail")!
                 });

    public static IEnumerable<object[]> BrojTerminaPacijenta() =>
        XmlLoader.Ucitaj("TerminService_terminiPacijenta.xml", "scenarij")
                 .Select(x => new object[] { (string)x.Element("pacijentId")!, (int)x.Element("ocekivaniBroj")! });

    public static IEnumerable<object[]> NajnovijiTerminPacijenta() =>
        XmlLoader.Ucitaj("TerminService_terminiPacijenta.xml", "scenarij")
                 .Select(x => new object[] { (string)x.Element("pacijentId")!, (int)x.Element("ocekivaniPrviId")! });

    public static IEnumerable<object[]> BrojTermina() =>
        CsvLoader.Ucitaj("TerminService_brojTermina.csv")
                 .Select(r => new object[] { int.Parse(r[0]) });

    // ───────────── Pomoćne metode (pravljenje objekata) ─────────────

    // Pravi novu listu termina iz XML elemenata ("baza" koju vraća mock ITerminRepository).
    public static List<Termin> NapraviBazu(IEnumerable<XElement> elementi) =>
        elementi.Select(NapraviTermin).ToList();

    // Vraća termin s traženim ID-jem iz "baze".
    public static Termin Pronadji(IEnumerable<Termin> baza, int terminId) =>
        baza.Single(t => t.TerminId == terminId);

    public static Termin NapraviTermin(XElement x) => new Termin
    {
        TerminId = (int)x.Element("id")!,
        DoktorID = (string)x.Element("doktorId")!,
        Doktor = x.Element("doktorIme") is null
            ? null
            : new Korisnik { Ime = (string)x.Element("doktorIme")!, Prezime = (string)x.Element("doktorPrezime")! },
        PacijentID = (string)x.Element("pacijentId")!,
        Pacijent = new Korisnik { Email = PrazanUNull((string)x.Element("email")!) },
        Datum = DateTime.Today.AddDays((int)x.Element("pomakDana")!),
        Vrijeme = new TimeOnly((int)x.Element("sat")!, 0),
        Status = Enum.Parse<Status>((string)x.Element("status")!)
    };

    // Novi termin koji pacijent želi zakazati (još nije u "bazi").
    public static Termin NoviTermin(string doktorId, string pacijentEmail, int pomakDana, int sat) => new Termin
    {
        DoktorID = doktorId,
        Datum = DateTime.Today.AddDays(pomakDana),
        Vrijeme = new TimeOnly(sat, 0),
        Pacijent = new Korisnik { Email = PrazanUNull(pacijentEmail) }
    };

    // Termin čiji je početak pomaknut za zadani broj sati od trenutnog vremena.
    public static Termin TerminSaPomakom(int terminId, double pomakSati)
    {
        var vrijeme = DateTime.Now.AddHours(pomakSati);
        return new Termin { TerminId = terminId, Datum = vrijeme.Date, Vrijeme = TimeOnly.FromDateTime(vrijeme) };
    }

    public static KorisnikNaListi NapraviKandidata(XElement x) => new KorisnikNaListi
    {
        Datum = DateTime.Today.AddDays((int)x.Element("pomakDana")!),
        Lista = new ListaCekanja { DoktorID = (string)x.Element("doktorId")! },
        Korisnik = new Korisnik
        {
            Email = (string)x.Element("email")!,
            Prioritet = Enum.Parse<Prioritet>((string)x.Element("prioritet")!)
        }
    };

    // Prazan string iz CSV/XML-a znači da pacijent nema email.
    public static string? PrazanUNull(string tekst) => string.IsNullOrEmpty(tekst) ? null : tekst;
}

// ─── Testovi ────────────────────────────────────────────────────────────
// Kategorije zamjenskih objekata:
//  - "Stub": mock samo vraća pripremljene podatke (Setup), a test provjerava rezultat ili stanje (Assert).
//  - "Mock": test provjerava interakciju, tj. da je servis pozvao zavisnost tačno onoliko puta koliko treba (Verify).
[TestClass]
public class TerminServiceTests
{
    // XML "baze" termina čita se jednom za cijelu klasu.
    private static List<XElement> _terminiXml = null!;

    private Mock<ITerminRepository> _terminRepo = null!;
    private Mock<INotifikacijaRepository> _notifikacijaRepo = null!;
    private Mock<IEmailService> _email = null!;
    private Mock<IKorisnikNaListiRepository> _listaRepo = null!;
    private TerminService _servis = null!;
    private List<Termin> _baza = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _terminiXml = XmlLoader.Ucitaj("TerminService_termini.xml", "termin").ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        // Svaki test dobija svoju kopiju "baze", pa izmjene u jednom testu ne utiču na druge.
        _baza = TerminServicePodaci.NapraviBazu(_terminiXml);

        _terminRepo = new Mock<ITerminRepository>();
        _terminRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(_baza);
        _terminRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
                   .ReturnsAsync((int id) => _baza.FirstOrDefault(t => t.TerminId == id));

        _notifikacijaRepo = new Mock<INotifikacijaRepository>();
        _email = new Mock<IEmailService>();
        _listaRepo = new Mock<IKorisnikNaListiRepository>();
        _listaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<KorisnikNaListi>());

        _servis = new TerminService(_terminRepo.Object, _notifikacijaRepo.Object,
                                    _email.Object, _listaRepo.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Poništavamo podešavanja i zabilježene pozive mockova nakon svakog testa.
        _terminRepo.Reset();
        _notifikacijaRepo.Reset();
        _email.Reset();
        _listaRepo.Reset();
    }

    // ═════════════════════════ ZakaziTerminAsync ═════════════════════════

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.NevalidnoVrijeme), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task ZakaziTerminAsync_NevalidnoVrijeme_BacaException(int terminId, double pomakSati, string ocekivanaPoruka)
    {
        // Arrange
        var termin = TerminServicePodaci.TerminSaPomakom(terminId, pomakSati);

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.ZakaziTerminAsync(termin));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.NedostupanZakazivanje), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task ZakaziTerminAsync_TerminZauzet_BacaException(string doktorId, int pomakDana, int sat, string ocekivanaPoruka)
    {
        // Arrange
        var termin = TerminServicePodaci.NoviTermin(doktorId, string.Empty, pomakDana, sat);

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.ZakaziTerminAsync(termin));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.ZakazivanjeStatus), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task ZakaziTerminAsync_SlobodanTermin_PostavljaStatusNaCekanju(
        string doktorId, string pacijentEmail, int pomakDana, int sat, Status ocekivaniStatus)
    {
        // Arrange
        var termin = TerminServicePodaci.NoviTermin(doktorId, pacijentEmail, pomakDana, sat);

        // Act
        await _servis.ZakaziTerminAsync(termin);

        // Assert
        Assert.AreEqual(ocekivaniStatus, termin.Status);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.Zakazivanje), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task ZakaziTerminAsync_SlobodanTermin_SpremaTerminURepozitorij(string doktorId, string pacijentEmail, int pomakDana, int sat)
    {
        // Arrange
        var termin = TerminServicePodaci.NoviTermin(doktorId, pacijentEmail, pomakDana, sat);

        // Act
        await _servis.ZakaziTerminAsync(termin);

        // Assert
        _terminRepo.Verify(r => r.AddAsync(termin), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.Zakazivanje), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task ZakaziTerminAsync_SlobodanTermin_KreiraNotifikaciju(string doktorId, string pacijentEmail, int pomakDana, int sat)
    {
        // Arrange
        var termin = TerminServicePodaci.NoviTermin(doktorId, pacijentEmail, pomakDana, sat);

        // Act
        await _servis.ZakaziTerminAsync(termin);

        // Assert
        _notifikacijaRepo.Verify(r => r.AddAsync(It.IsAny<Notifikacija>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.ZakazivanjeEmail), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task ZakaziTerminAsync_SlobodanTermin_SaljePotvrduSamoAkoPacijentImaEmail(
        string doktorId, string pacijentEmail, int pomakDana, int sat, int ocekivaniBrojMailova)
    {
        // Arrange
        var termin = TerminServicePodaci.NoviTermin(doktorId, pacijentEmail, pomakDana, sat);

        // Act
        await _servis.ZakaziTerminAsync(termin);

        // Assert
        _email.Verify(e => e.PošaljiPotvrduTerminaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<TimeOnly>(), It.IsAny<string>(), It.IsAny<int>()),
            Times.Exactly(ocekivaniBrojMailova));
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.GreskaEmaila), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task ZakaziTerminAsync_SlanjeEmailaNeUspije_TerminJeIpakZakazan(
        string doktorId, string pacijentEmail, int pomakDana, int sat, string porukaGreske)
    {
        // Arrange
        var termin = TerminServicePodaci.NoviTermin(doktorId, pacijentEmail, pomakDana, sat);
        _email.Setup(e => e.PošaljiPotvrduTerminaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<TimeOnly>(), It.IsAny<string>(), It.IsAny<int>()))
              .ThrowsAsync(new Exception(porukaGreske));

        // Act
        await _servis.ZakaziTerminAsync(termin);

        // Assert
        _terminRepo.Verify(r => r.AddAsync(termin), Times.Once);
    }

    // ═════════════════════════ OznaciKaoPotvrdjenAsync ═════════════════════════

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.TerminNePostoji), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OznaciKaoPotvrdjenAsync_TerminNePostoji_BacaException(int terminId, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.OznaciKaoPotvrdjenAsync(terminId));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.OtkazanPotvrda), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OznaciKaoPotvrdjenAsync_TerminOtkazan_BacaException(int terminId, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.OznaciKaoPotvrdjenAsync(terminId));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.StatusPotvrda), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OznaciKaoPotvrdjenAsync_TerminPostoji_SpremaStatusPotvrdjen(int terminId, Status ocekivaniStatus)
    {
        // Act
        await _servis.OznaciKaoPotvrdjenAsync(terminId);

        // Assert
        _terminRepo.Verify(r => r.Update(It.Is<Termin>(t => t.TerminId == terminId && t.Status == ocekivaniStatus)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.PostojeciTermin), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OznaciKaoPotvrdjenAsync_TerminPostoji_KreiraNotifikaciju(int terminId)
    {
        // Act
        await _servis.OznaciKaoPotvrdjenAsync(terminId);

        // Assert
        _notifikacijaRepo.Verify(r => r.AddAsync(It.Is<Notifikacija>(n => n.TerminID == terminId)), Times.Once);
    }

    // ═════════════════════════ OznaciKaoAktivanAsync ═════════════════════════

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.TerminNePostoji), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OznaciKaoAktivanAsync_TerminNePostoji_BacaException(int terminId, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.OznaciKaoAktivanAsync(terminId));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.StatusAktivacija), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OznaciKaoAktivanAsync_TerminPostoji_SpremaStatusAktivan(int terminId, Status ocekivaniStatus)
    {
        // Act
        await _servis.OznaciKaoAktivanAsync(terminId);

        // Assert
        _terminRepo.Verify(r => r.Update(It.Is<Termin>(t => t.TerminId == terminId && t.Status == ocekivaniStatus)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.PostojeciTermin), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OznaciKaoAktivanAsync_TerminPostoji_SnimaPromjene(int terminId)
    {
        // Act
        await _servis.OznaciKaoAktivanAsync(terminId);

        // Assert
        _terminRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ═════════════════════════ OtkaziTerminAsync ═════════════════════════

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.TerminNePostoji), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OtkaziTerminAsync_TerminNePostoji_BacaException(int terminId, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.OtkaziTerminAsync(terminId));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.OtkazanOtkazivanje), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OtkaziTerminAsync_TerminVecOtkazan_BacaException(int terminId, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.OtkaziTerminAsync(terminId));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.OtkazivanjeStatus), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task OtkaziTerminAsync_TerminPostoji_PostavljaStatusOtkazan(int terminId, Status ocekivaniStatus)
    {
        // Act
        await _servis.OtkaziTerminAsync(terminId);

        // Assert
        Assert.AreEqual(ocekivaniStatus, TerminServicePodaci.Pronadji(_baza, terminId).Status);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.PostojeciTermin), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OtkaziTerminAsync_PacijentImaEmail_SaljeEmailOOtkazu(int terminId)
    {
        // Act
        await _servis.OtkaziTerminAsync(terminId);

        // Assert
        _email.Verify(e => e.PošaljiOtkazTerminaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<TimeOnly>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.KandidatiNaListi), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task OtkaziTerminAsync_PostojeKandidatiNaListi_ObavjestavaPrvogPoPrioritetuIDatumu(
        int terminId, List<KorisnikNaListi> kandidati, string ocekivaniEmail)
    {
        // Arrange
        _listaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(kandidati);

        // Act
        await _servis.OtkaziTerminAsync(terminId);

        // Assert
        _email.Verify(e => e.PošaljiObavještenjeListe(ocekivaniEmail, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<TimeOnly>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.PostojeciTermin), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OtkaziTerminAsync_ListaCekanjaPrazna_NeSaljeObavjestenjeListe(int terminId)
    {
        // Act
        await _servis.OtkaziTerminAsync(terminId);

        // Assert
        _email.Verify(e => e.PošaljiObavještenjeListe(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<TimeOnly>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.Otkazivanje), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OtkaziTerminAsync_EmailIListaCekanjaNeUspiju_TerminJeIpakOtkazan(int terminId, string porukaGreske)
    {
        // Arrange
        _email.Setup(e => e.PošaljiOtkazTerminaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<TimeOnly>()))
              .ThrowsAsync(new Exception(porukaGreske));
        _listaRepo.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception(porukaGreske));

        // Act
        await _servis.OtkaziTerminAsync(terminId);

        // Assert
        _terminRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ═════════════════════════ PromijeniTerminAsync ═════════════════════════

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.TerminNePostoji), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniTerminAsync_TerminNePostoji_BacaException(int terminId, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(
            () => _servis.PromijeniTerminAsync(terminId, default, default));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.OtkazanPromjena), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniTerminAsync_TerminOtkazan_BacaException(int terminId, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(
            () => _servis.PromijeniTerminAsync(terminId, default, default));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.NevalidnoVrijeme), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniTerminAsync_NevalidnoNovoVrijeme_BacaException(int terminId, double pomakSati, string ocekivanaPoruka)
    {
        // Arrange
        var novi = TerminServicePodaci.TerminSaPomakom(terminId, pomakSati);

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(
            () => _servis.PromijeniTerminAsync(terminId, novi.Datum, novi.Vrijeme));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.NedostupanPromjena), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniTerminAsync_NoviTerminZauzet_BacaException(int terminId, int pomakDana, int sat, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(
            () => _servis.PromijeniTerminAsync(terminId, DateTime.Today.AddDays(pomakDana), new TimeOnly(sat, 0)));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.PromjenaTermina), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniTerminAsync_NoviTerminSlobodan_MijenjaDatum(int terminId, int pomakDana, int sat)
    {
        // Arrange
        var noviDatum = DateTime.Today.AddDays(pomakDana);

        // Act
        await _servis.PromijeniTerminAsync(terminId, noviDatum, new TimeOnly(sat, 0));

        // Assert
        Assert.AreEqual(noviDatum, TerminServicePodaci.Pronadji(_baza, terminId).Datum);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.PromjenaTermina), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniTerminAsync_NoviTerminSlobodan_MijenjaVrijeme(int terminId, int pomakDana, int sat)
    {
        // Arrange
        var novoVrijeme = new TimeOnly(sat, 0);

        // Act
        await _servis.PromijeniTerminAsync(terminId, DateTime.Today.AddDays(pomakDana), novoVrijeme);

        // Assert
        Assert.AreEqual(novoVrijeme, TerminServicePodaci.Pronadji(_baza, terminId).Vrijeme);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.PromjenaTerminaStatus), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniTerminAsync_NoviTerminSlobodan_PostavljaStatusPotvrdjen(int terminId, int pomakDana, int sat, Status ocekivaniStatus)
    {
        // Act
        await _servis.PromijeniTerminAsync(terminId, DateTime.Today.AddDays(pomakDana), new TimeOnly(sat, 0));

        // Assert
        Assert.AreEqual(ocekivaniStatus, TerminServicePodaci.Pronadji(_baza, terminId).Status);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.PromjenaTermina), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniTerminAsync_NoviTerminSlobodan_SpremaIzmjenuURepozitorij(int terminId, int pomakDana, int sat)
    {
        // Act
        await _servis.PromijeniTerminAsync(terminId, DateTime.Today.AddDays(pomakDana), new TimeOnly(sat, 0));

        // Assert
        _terminRepo.Verify(r => r.Update(It.Is<Termin>(t => t.TerminId == terminId)), Times.Once);
    }

    // ═════════════════════════ Dostupnost i pregledi ═════════════════════════

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.Dostupnost), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task IsTerminDostupanAsync_RazliciteKombinacije_VracaOcekivano(string doktorId, int pomakDana, int sat, bool ocekivano)
    {
        // Act
        var rezultat = await _servis.IsTerminDostupanAsync(doktorId, DateTime.Today.AddDays(pomakDana), new TimeOnly(sat, 0));

        // Assert
        Assert.AreEqual(ocekivano, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.SlobodniTermini), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Timeout(2000)]
    public async Task GetSlobodniTerminiAsync_DoktorImaZauzeteTermine_VracaSamoSlobodne(string doktorId, int pomakDana, int ocekivaniBroj)
    {
        // Act
        var rezultat = await _servis.GetSlobodniTerminiAsync(doktorId, DateTime.Today.AddDays(pomakDana));

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.TerminiDoktoraNaDan), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetTerminiDoktoraAsync_SaDatumom_VracaTermineSamoTogDana(string doktorId, int pomakDana, int ocekivaniBroj)
    {
        // Act
        var rezultat = await _servis.GetTerminiDoktoraAsync(doktorId, DateTime.Today.AddDays(pomakDana));

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.TerminiDoktora), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetTerminiDoktoraAsync_BezDatuma_VracaSveTermineDoktora(string doktorId, int ocekivaniBroj)
    {
        // Act
        var rezultat = await _servis.GetTerminiDoktoraAsync(doktorId);

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.BrojTerminaPacijenta), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetTerminiPacijentaAsync_PacijentImaTermine_VracaSveNjegoveTermine(string pacijentId, int ocekivaniBroj)
    {
        // Act
        var rezultat = await _servis.GetTerminiPacijentaAsync(pacijentId);

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.NajnovijiTerminPacijenta), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetTerminiPacijentaAsync_PacijentImaTermine_PrviJeNajnoviji(string pacijentId, int ocekivaniPrviId)
    {
        // Act
        var rezultat = await _servis.GetTerminiPacijentaAsync(pacijentId);

        // Assert
        Assert.AreEqual(ocekivaniPrviId, rezultat.First().TerminId);
    }

    // ═════════════════════════ PošaljiPodsjetnikeSutraAsync ═════════════════════════

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.Podsjetnici), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task PosaljiPodsjetnikeSutraAsync_PostojeSutrasnjiPotvrdjeniTermini_SaljeMailZaSvaki(int ocekivaniBroj)
    {
        // Act
        await _servis.PošaljiPodsjetnikeSutraAsync();

        // Assert
        _email.Verify(e => e.PošaljiPodsjetnikAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<TimeOnly>(), It.IsAny<string>()), Times.Exactly(ocekivaniBroj));
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.PodsjetniciGreska), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PosaljiPodsjetnikeSutraAsync_SlanjeNeUspije_NastavljaSaOstalimTerminima(int ocekivaniBroj, string porukaGreske)
    {
        // Arrange
        _email.Setup(e => e.PošaljiPodsjetnikAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<TimeOnly>(), It.IsAny<string>()))
              .ThrowsAsync(new Exception(porukaGreske));

        // Act
        await _servis.PošaljiPodsjetnikeSutraAsync();

        // Assert
        _email.Verify(e => e.PošaljiPodsjetnikAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<TimeOnly>(), It.IsAny<string>()), Times.Exactly(ocekivaniBroj));
    }

    // ═════════════════════════ KreirajNotifikacijuAsync ═════════════════════════

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.NotifikacijaPodaci), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task KreirajNotifikacijuAsync_IspravniPodaci_SpremaNotifikacijuSaTacnimPodacima(int terminId, string poruka, Status status)
    {
        // Act
        await _servis.KreirajNotifikacijuAsync(terminId, poruka, status);

        // Assert
        _notifikacijaRepo.Verify(r => r.AddAsync(It.Is<Notifikacija>(n =>
            n.TerminID == terminId && n.Poruka == poruka && n.status == status)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.NotifikacijaPodaci), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task KreirajNotifikacijuAsync_IspravniPodaci_SnimaPromjene(int terminId, string poruka, Status status)
    {
        // Act
        await _servis.KreirajNotifikacijuAsync(terminId, poruka, status);

        // Assert
        _notifikacijaRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ═════════════════════════ Jednostavne metode (prosljeđuju poziv repozitoriju) ═════════════════════════

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.PostojeciTermin), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetByIdAsync_TerminPostoji_VracaTerminSaTimId(int terminId)
    {
        // Act
        var rezultat = await _servis.GetByIdAsync(terminId);

        // Assert
        Assert.AreEqual(terminId, rezultat!.TerminId);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.PostojeciTermin), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetByIdAsync_TerminPostoji_PozivaRepozitorij(int terminId)
    {
        // Act
        await _servis.GetByIdAsync(terminId);

        // Assert
        _terminRepo.Verify(r => r.GetByIdAsync(terminId), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.BrojTermina), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetAllAsync_RepozitorijImaTermine_VracaSveTermine(int ocekivaniBroj)
    {
        // Act
        var rezultat = await _servis.GetAllAsync();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.PostojeciTermin), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task AddAsync_NoviTermin_ProsljedjujeRepozitoriju(int terminId)
    {
        // Arrange
        var termin = new Termin { TerminId = terminId };

        // Act
        await _servis.AddAsync(termin);

        // Assert
        _terminRepo.Verify(r => r.AddAsync(termin), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.PostojeciTermin), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public void Update_PostojeciTermin_ProsljedjujeRepozitoriju(int terminId)
    {
        // Arrange
        var termin = new Termin { TerminId = terminId };

        // Act
        _servis.Update(termin);

        // Assert
        _terminRepo.Verify(r => r.Update(termin), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.PostojeciTermin), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public void Delete_PostojeciTermin_ProsljedjujeRepozitoriju(int terminId)
    {
        // Arrange
        var termin = new Termin { TerminId = terminId };

        // Act
        _servis.Delete(termin);

        // Assert
        _terminRepo.Verify(r => r.Delete(termin), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminServicePodaci.Snimanje), typeof(TerminServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task SaveChangesAsync_RepozitorijSnimiPromjene_VracaBrojPromjena(int brojPromjena)
    {
        // Arrange
        _terminRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(brojPromjena);

        // Act
        var rezultat = await _servis.SaveChangesAsync();

        // Assert
        Assert.AreEqual(brojPromjena, rezultat);
    }
}
