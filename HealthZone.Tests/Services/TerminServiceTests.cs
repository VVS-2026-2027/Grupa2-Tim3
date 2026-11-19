using System.Globalization;
using System.Xml.Linq;
using HealthZone.Models;
using HealthZone.Repositories;
using HealthZone.Services;
using Moq;

namespace HealthZone.Tests;

[TestClass]
public class TerminServiceTests
{
    private Mock<ITerminRepository> _terminRepo = null!;
    private Mock<INotifikacijaRepository> _notifikacijaRepo = null!;
    private Mock<IEmailService> _email = null!;
    private Mock<IKorisnikNaListiRepository> _listaRepo = null!;
    private TerminService _servis = null!;
    private List<Termin> _baza = null!;

    [TestInitialize]
    public void Setup()
    {
        // "Baza" termina se čita iz XML-a za svaki test ispočetka, pa testovi ne utiču jedan na drugi.
        _baza = XmlLoader.Ucitaj("TerminService_termini.xml", "termin").Select(NapraviTermin).ToList();

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

    // ───────────── Pomoćne metode za pretvaranje XML-a u objekte ─────────────

    private static Termin NapraviTermin(XElement x) => new Termin
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

    private static KorisnikNaListi NapraviKandidata(XElement x) => new KorisnikNaListi
    {
        Datum = DateTime.Today.AddDays((int)x.Element("pomakDana")!),
        Lista = new ListaCekanja { DoktorID = (string)x.Element("doktorId")! },
        Korisnik = new Korisnik
        {
            Email = (string)x.Element("email")!,
            Prioritet = Enum.Parse<Prioritet>((string)x.Element("prioritet")!)
        }
    };

    private static string? PrazanUNull(string tekst) => string.IsNullOrEmpty(tekst) ? null : tekst;

    // ───────────── Izvori podataka: CSV ─────────────

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

    public static IEnumerable<object[]> PromjenaTermina() =>
        CsvLoader.Ucitaj("TerminService_promjenaTermina.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), int.Parse(r[2]) });

    public static IEnumerable<object[]> Podsjetnici() =>
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

    // ───────────── Izvori podataka: XML ─────────────

    public static IEnumerable<object[]> Zakazivanje() =>
        XmlLoader.Ucitaj("TerminService_zakazivanje.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("doktorId")!,
                     (string)x.Element("pacijentEmail")!,
                     (int)x.Element("pomakDana")!,
                     (int)x.Element("sat")!,
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

    public static IEnumerable<object[]> TerminiPacijenta() =>
        XmlLoader.Ucitaj("TerminService_terminiPacijenta.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("pacijentId")!, (int)x.Element("ocekivaniBroj")!, (int)x.Element("ocekivaniPrviId")!
                 });

    // ═════════════════════════ ZakaziTerminAsync ═════════════════════════

    [TestMethod]
    [DynamicData(nameof(NevalidnoVrijeme), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task ZakaziTerminAsync_NevalidnoVrijeme_BacaException(int terminId, double pomakSati, string ocekivanaPoruka)
    {
        // Arrange
        var vrijeme = DateTime.Now.AddHours(pomakSati);
        var termin = new Termin { TerminId = terminId, Datum = vrijeme.Date, Vrijeme = TimeOnly.FromDateTime(vrijeme) };

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.ZakaziTerminAsync(termin));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(NedostupanZakazivanje), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task ZakaziTerminAsync_TerminZauzet_BacaException(string doktorId, int pomakDana, int sat, string ocekivanaPoruka)
    {
        // Arrange
        var termin = new Termin { DoktorID = doktorId, Datum = DateTime.Today.AddDays(pomakDana), Vrijeme = new TimeOnly(sat, 0) };

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.ZakaziTerminAsync(termin));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
        _terminRepo.Verify(r => r.AddAsync(It.IsAny<Termin>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(Zakazivanje), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task ZakaziTerminAsync_SlobodanTermin_SpremaTerminISaljePotvrdu(
        string doktorId, string pacijentEmail, int pomakDana, int sat, int ocekivaniBrojMailova)
    {
        // Arrange
        var termin = new Termin
        {
            DoktorID = doktorId,
            Datum = DateTime.Today.AddDays(pomakDana),
            Vrijeme = new TimeOnly(sat, 0),
            Pacijent = new Korisnik { Email = PrazanUNull(pacijentEmail) }
        };

        // Act
        await _servis.ZakaziTerminAsync(termin);

        // Assert
        Assert.AreEqual(Status.NaCekanju, termin.Status);
        _terminRepo.Verify(r => r.AddAsync(termin), Times.Once);
        _terminRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        _notifikacijaRepo.Verify(r => r.AddAsync(It.IsAny<Notifikacija>()), Times.Once);
        _email.Verify(e => e.PošaljiPotvrduTerminaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<TimeOnly>(), It.IsAny<string>(), It.IsAny<int>()),
            Times.Exactly(ocekivaniBrojMailova));
    }

    [TestMethod]
    [DynamicData(nameof(GreskaEmaila), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task ZakaziTerminAsync_SlanjeEmailaNeUspije_TerminJeIpakZakazan(
        string doktorId, string pacijentEmail, int pomakDana, int sat, string porukaGreske)
    {
        // Arrange
        var termin = new Termin
        {
            DoktorID = doktorId,
            Datum = DateTime.Today.AddDays(pomakDana),
            Vrijeme = new TimeOnly(sat, 0),
            Pacijent = new Korisnik { Email = pacijentEmail }
        };
        _email.Setup(e => e.PošaljiPotvrduTerminaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<TimeOnly>(), It.IsAny<string>(), It.IsAny<int>()))
              .ThrowsAsync(new Exception(porukaGreske));

        // Act
        await _servis.ZakaziTerminAsync(termin);

        // Assert
        _terminRepo.Verify(r => r.AddAsync(termin), Times.Once);
    }

    // ═════════════════════════ OznaciKaoPotvrdjenAsync / OznaciKaoAktivanAsync ═════════════════════════

    [TestMethod]
    [DynamicData(nameof(TerminNePostoji), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OznaciKaoPotvrdjenAsync_TerminNePostoji_BacaException(int terminId, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.OznaciKaoPotvrdjenAsync(terminId));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(OtkazanPotvrda), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OznaciKaoPotvrdjenAsync_TerminOtkazan_BacaException(int terminId, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.OznaciKaoPotvrdjenAsync(terminId));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(StatusPotvrda), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OznaciKaoPotvrdjenAsync_TerminPostoji_MijenjaStatusIKreiraNotifikaciju(int terminId, Status ocekivaniStatus)
    {
        // Act
        await _servis.OznaciKaoPotvrdjenAsync(terminId);

        // Assert
        _terminRepo.Verify(r => r.Update(It.Is<Termin>(t => t.TerminId == terminId && t.Status == ocekivaniStatus)), Times.Once);
        _notifikacijaRepo.Verify(r => r.AddAsync(It.Is<Notifikacija>(n => n.TerminID == terminId)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminNePostoji), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OznaciKaoAktivanAsync_TerminNePostoji_BacaException(int terminId, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.OznaciKaoAktivanAsync(terminId));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(StatusAktivacija), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OznaciKaoAktivanAsync_TerminPostoji_MijenjaStatusUAktivan(int terminId, Status ocekivaniStatus)
    {
        // Act
        await _servis.OznaciKaoAktivanAsync(terminId);

        // Assert
        _terminRepo.Verify(r => r.Update(It.Is<Termin>(t => t.TerminId == terminId && t.Status == ocekivaniStatus)), Times.Once);
        _terminRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ═════════════════════════ OtkaziTerminAsync ═════════════════════════

    [TestMethod]
    [DynamicData(nameof(TerminNePostoji), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OtkaziTerminAsync_TerminNePostoji_BacaException(int terminId, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.OtkaziTerminAsync(terminId));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(OtkazanOtkazivanje), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OtkaziTerminAsync_TerminVecOtkazan_BacaException(int terminId, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.OtkaziTerminAsync(terminId));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(KandidatiNaListi), DynamicDataSourceType.Method)]
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
        _email.Verify(e => e.PošaljiOtkazTerminaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<TimeOnly>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(PostojeciTermin), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OtkaziTerminAsync_ListaCekanjaPrazna_NeSaljeObavjestenjeListe(int terminId)
    {
        // Act
        await _servis.OtkaziTerminAsync(terminId);

        // Assert
        _terminRepo.Verify(r => r.Update(It.Is<Termin>(t => t.TerminId == terminId && t.Status == Status.Otkazan)), Times.Once);
        _email.Verify(e => e.PošaljiObavještenjeListe(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<TimeOnly>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(Otkazivanje), DynamicDataSourceType.Method)]
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
        _listaRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    // ═════════════════════════ PromijeniTerminAsync ═════════════════════════

    [TestMethod]
    [DynamicData(nameof(TerminNePostoji), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniTerminAsync_TerminNePostoji_BacaException(int terminId, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(
            () => _servis.PromijeniTerminAsync(terminId, DateTime.Today, TimeOnly.MinValue));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(OtkazanPromjena), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniTerminAsync_TerminOtkazan_BacaException(int terminId, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(
            () => _servis.PromijeniTerminAsync(terminId, DateTime.Today, TimeOnly.MinValue));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(NevalidnoVrijeme), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniTerminAsync_NevalidnoNovoVrijeme_BacaException(int terminId, double pomakSati, string ocekivanaPoruka)
    {
        // Arrange
        var novoVrijeme = DateTime.Now.AddHours(pomakSati);

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(
            () => _servis.PromijeniTerminAsync(terminId, novoVrijeme.Date, TimeOnly.FromDateTime(novoVrijeme)));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(NedostupanPromjena), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
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
    [DynamicData(nameof(PromjenaTermina), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniTerminAsync_NoviTerminSlobodan_MijenjaDatumIVrijeme(int terminId, int pomakDana, int sat)
    {
        // Arrange
        var noviDatum = DateTime.Today.AddDays(pomakDana);
        var novoVrijeme = new TimeOnly(sat, 0);

        // Act
        await _servis.PromijeniTerminAsync(terminId, noviDatum, novoVrijeme);

        // Assert
        _terminRepo.Verify(r => r.Update(It.Is<Termin>(t =>
            t.TerminId == terminId && t.Datum == noviDatum && t.Vrijeme == novoVrijeme && t.Status == Status.Potvrdjen)),
            Times.Once);
    }

    // ═════════════════════════ Dostupnost i pregledi ═════════════════════════

    [TestMethod]
    [DynamicData(nameof(Dostupnost), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task IsTerminDostupanAsync_RazliciteKombinacije_VracaOcekivano(string doktorId, int pomakDana, int sat, bool ocekivano)
    {
        // Act
        var rezultat = await _servis.IsTerminDostupanAsync(doktorId, DateTime.Today.AddDays(pomakDana), new TimeOnly(sat, 0));

        // Assert
        Assert.AreEqual(ocekivano, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(SlobodniTermini), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
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
    [DynamicData(nameof(TerminiDoktoraNaDan), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetTerminiDoktoraAsync_SaDatumom_VracaTermineSamoTogDana(string doktorId, int pomakDana, int ocekivaniBroj)
    {
        // Act
        var rezultat = await _servis.GetTerminiDoktoraAsync(doktorId, DateTime.Today.AddDays(pomakDana));

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(TerminiDoktora), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetTerminiDoktoraAsync_BezDatuma_VracaSveTermineDoktora(string doktorId, int ocekivaniBroj)
    {
        // Act
        var rezultat = await _servis.GetTerminiDoktoraAsync(doktorId);

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(TerminiPacijenta), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetTerminiPacijentaAsync_PacijentImaTermine_VracaIhOdNajnovijeg(string pacijentId, int ocekivaniBroj, int ocekivaniPrviId)
    {
        // Act
        var rezultat = (await _servis.GetTerminiPacijentaAsync(pacijentId)).ToList();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count);
        Assert.AreEqual(ocekivaniPrviId, rezultat.First().TerminId);
    }

    // ═════════════════════════ PošaljiPodsjetnikeSutraAsync ═════════════════════════

    [TestMethod]
    [DynamicData(nameof(Podsjetnici), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task PosaljiPodsjetnikeSutraAsync_PostojeSutrasnjiPotvrdjeniTermini_SaljeMailZaSvaki(int ocekivaniBroj, string porukaGreske)
    {
        // Act
        await _servis.PošaljiPodsjetnikeSutraAsync();

        // Assert
        _email.Verify(e => e.PošaljiPodsjetnikAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<TimeOnly>(), It.IsAny<string>()), Times.Exactly(ocekivaniBroj));
    }

    [TestMethod]
    [DynamicData(nameof(Podsjetnici), DynamicDataSourceType.Method)]
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
    [DynamicData(nameof(NotifikacijaPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task KreirajNotifikacijuAsync_IspravniPodaci_SpremaNotifikacijuURepozitorij(int terminId, string poruka, Status status)
    {
        // Act
        await _servis.KreirajNotifikacijuAsync(terminId, poruka, status);

        // Assert
        _notifikacijaRepo.Verify(r => r.AddAsync(It.Is<Notifikacija>(n =>
            n.TerminID == terminId && n.Poruka == poruka && n.status == status)), Times.Once);
        _notifikacijaRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ═════════════════════════ Jednostavne metode (prosljeđuju poziv repozitoriju) ═════════════════════════

    [TestMethod]
    [DynamicData(nameof(PostojeciTermin), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetByIdAsync_TerminPostoji_VracaTerminIzRepozitorija(int terminId)
    {
        // Act
        var rezultat = await _servis.GetByIdAsync(terminId);

        // Assert
        Assert.AreEqual(terminId, rezultat!.TerminId);
        _terminRepo.Verify(r => r.GetByIdAsync(terminId), Times.Once);
    }

    [TestMethod]
    [TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetAllAsync_RepozitorijImaTermine_VracaSveTermine()
    {
        // Act
        var rezultat = await _servis.GetAllAsync();

        // Assert
        Assert.AreEqual(_baza.Count, rezultat.Count());
        _terminRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(PostojeciTermin), DynamicDataSourceType.Method)]
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
    [DynamicData(nameof(PostojeciTermin), DynamicDataSourceType.Method)]
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
    [DynamicData(nameof(PostojeciTermin), DynamicDataSourceType.Method)]
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
    [DynamicData(nameof(Snimanje), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
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
