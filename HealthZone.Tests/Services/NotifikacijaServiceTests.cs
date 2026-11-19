using System.Xml.Linq;
using HealthZone.Models;
using HealthZone.Repositories;
using HealthZone.Services;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za NotifikacijaServiceTests ───────
public static class NotifikacijaServicePodaci
{
    // ── pomoćne metode ──

    // Notifikacija iz XML "baze". Ako nema atributa pacijentId, termin nije učitan (Termin je null).
    public static Notifikacija NapraviNotifikaciju(XElement x) => new()
    {
        NotifikacijaId = (int)x.Attribute("id")!,
        TerminID = (int)x.Attribute("terminId")!,
        Poruka = (string)x.Attribute("poruka")!,
        status = Enum.Parse<Status>((string)x.Attribute("status")!),
        DatumSlanja = DateTime.Now.AddHours((double)x.Attribute("pomakSati")!),
        Termin = x.Attribute("pacijentId") == null
            ? null!
            : new Termin { TerminId = (int)x.Attribute("terminId")!, PacijentID = (string)x.Attribute("pacijentId")! }
    };

    public static List<Notifikacija> NapraviBazu(IEnumerable<XElement> baza) => baza.Select(NapraviNotifikaciju).ToList();

    public static Notifikacija IzBaze(IEnumerable<XElement> baza, int id) =>
        NapraviBazu(baza).Single(n => n.NotifikacijaId == id);

    public static int[] Ids(string lista) =>
        lista.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();

    // ── izvori podataka ──

    public static IEnumerable<object[]> BrojUBazi() =>
        XmlLoader.Ucitaj("NotifikacijaService_baza.xml", "baza")
                 .Select(b => new object[] { b.Elements("notifikacija").Count() });

    public static IEnumerable<object[]> PoTerminu() =>
        CsvLoader.Ucitaj("NotifikacijaService_poTerminu.csv").Select(r => new object[] { int.Parse(r[0]), Ids(r[1]) });

    public static IEnumerable<object[]> PoPacijentu() =>
        XmlLoader.Ucitaj("NotifikacijaService_poPacijentu.xml", "scenarij").Select(x => new object[]
        {
            (string)x.Element("pacijentId")!,
            Ids((string)x.Element("ocekivaniIds")!)
        });

    public static IEnumerable<object[]> PoStatusu() =>
        CsvLoader.Ucitaj("NotifikacijaService_poStatusu.csv")
                 .Select(r => new object[] { Enum.Parse<Status>(r[0]), Ids(r[1]) });

    public static IEnumerable<object[]> NepostojeciTermin() =>
        CsvLoader.Ucitaj("NotifikacijaService_nepostojeciTermin.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> NepostojeciTerminBezPoruke() =>
        CsvLoader.Ucitaj("NotifikacijaService_nepostojeciTermin.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> Dodavanje() =>
        CsvLoader.Ucitaj("NotifikacijaService_dodavanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1], Enum.Parse<Status>(r[2]) });

    public static IEnumerable<object[]> Kreiranje() =>
        XmlLoader.Ucitaj("NotifikacijaService_kreiranje.xml", "scenarij").Select(x => new object[]
        {
            (int)x.Element("terminId")!,
            (string)x.Element("poruka")!,
            Enum.Parse<Status>((string)x.Element("status")!)
        });

    public static IEnumerable<object[]> Postojece() =>
        CsvLoader.Ucitaj("NotifikacijaService_postojece.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> PostojeciIds() =>
        CsvLoader.Ucitaj("NotifikacijaService_postojece.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("NotifikacijaService_snimanje.csv").Select(r => new object[] { int.Parse(r[0]) });
}

// ─── Testovi ────────────────────────────────────────────────────────────
// Oba repozitorija su zamijenjena Moq objektima: INotifikacijaRepository vraća notifikacije iz XML "baze",
// a ITerminRepository kaže da li termin postoji. Prava baza se ne koristi.
[TestClass]
public class NotifikacijaServiceTests
{
    private static List<XElement> _baza = null!;   // samo za čitanje

    private Mock<INotifikacijaRepository> _notifikacijaRepo = null!;
    private Mock<ITerminRepository> _terminRepo = null!;
    private NotifikacijaService _servis = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _baza = XmlLoader.Ucitaj("NotifikacijaService_baza.xml", "notifikacija").ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        _notifikacijaRepo = new Mock<INotifikacijaRepository>();
        _terminRepo = new Mock<ITerminRepository>();
        _notifikacijaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(NotifikacijaServicePodaci.NapraviBazu(_baza));
        _servis = new NotifikacijaService(_notifikacijaRepo.Object, _terminRepo.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _notifikacijaRepo.Reset();
        _terminRepo.Reset();
    }

    // ─── FILTRIRANJE ──────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.PoTerminu), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task GetNotifikacijeZaTerminAsync_ZadanTermin_VracaSamoNotifikacijeTogTermina(int terminId, int[] ocekivaniIds)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetNotifikacijeZaTerminAsync(terminId);

        // Assert
        CollectionAssert.AreEquivalent(ocekivaniIds, rezultat.Select(n => n.NotifikacijaId).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.PoTerminu), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetNotifikacijeZaTerminAsync_ZadanTermin_SortiraOdNajnovije(int terminId, int[] ocekivaniIds)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetNotifikacijeZaTerminAsync(terminId);

        // Assert
        CollectionAssert.AreEqual(ocekivaniIds, rezultat.Select(n => n.NotifikacijaId).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.PoPacijentu), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task GetNotifikacijePacijentaAsync_ZadanPacijent_VracaNjegoveNotifikacijeOdNajnovije(string pacijentId, int[] ocekivaniIds)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetNotifikacijePacijentaAsync(pacijentId);

        // Assert
        CollectionAssert.AreEqual(ocekivaniIds, rezultat.Select(n => n.NotifikacijaId).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.PoStatusu), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetNotifikacijePoStatusuAsync_ZadanStatus_VracaSamoNotifikacijeTogStatusa(Status status, int[] ocekivaniIds)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetNotifikacijePoStatusuAsync(status);

        // Assert
        CollectionAssert.AreEquivalent(ocekivaniIds, rezultat.Select(n => n.NotifikacijaId).ToArray());
    }

    // ─── DODAVANJE ────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.NepostojeciTermin), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task AddAsync_TerminNePostoji_BacaException(int terminId, string poruka, string ocekivanaPoruka)
    {
        // Arrange
        _terminRepo.Setup(r => r.GetByIdAsync(terminId)).ReturnsAsync((Termin?)null);
        var notifikacija = new Notifikacija { TerminID = terminId, Poruka = poruka };

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(notifikacija));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.NepostojeciTerminBezPoruke), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_TerminNePostoji_NeDodajeNotifikaciju(int terminId, string poruka)
    {
        // Arrange
        _terminRepo.Setup(r => r.GetByIdAsync(terminId)).ReturnsAsync((Termin?)null);
        var notifikacija = new Notifikacija { TerminID = terminId, Poruka = poruka };

        // Act
        await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(notifikacija));

        // Assert
        _notifikacijaRepo.Verify(r => r.AddAsync(It.IsAny<Notifikacija>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.Dodavanje), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task AddAsync_TerminPostoji_DodajeNotifikacijuURepozitorij(int terminId, string poruka, Status status)
    {
        // Arrange
        _terminRepo.Setup(r => r.GetByIdAsync(terminId)).ReturnsAsync(new Termin { TerminId = terminId });
        var notifikacija = new Notifikacija { TerminID = terminId, Poruka = poruka, status = status };

        // Act
        await _servis.AddAsync(notifikacija);

        // Assert
        _notifikacijaRepo.Verify(r => r.AddAsync(notifikacija), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.Dodavanje), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_TerminPostoji_SnimaPromjene(int terminId, string poruka, Status status)
    {
        // Arrange
        _terminRepo.Setup(r => r.GetByIdAsync(terminId)).ReturnsAsync(new Termin { TerminId = terminId });
        var notifikacija = new Notifikacija { TerminID = terminId, Poruka = poruka, status = status };

        // Act
        await _servis.AddAsync(notifikacija);

        // Assert
        _notifikacijaRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.Dodavanje), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_TerminPostoji_PostavljaTrenutniDatumSlanja(int terminId, string poruka, Status status)
    {
        // Arrange
        _terminRepo.Setup(r => r.GetByIdAsync(terminId)).ReturnsAsync(new Termin { TerminId = terminId });
        var notifikacija = new Notifikacija { TerminID = terminId, Poruka = poruka, status = status };
        var prije = DateTime.Now;

        // Act
        await _servis.AddAsync(notifikacija);

        // Assert
        Assert.IsTrue(notifikacija.DatumSlanja >= prije);
    }

    // ─── KREIRANJE NOTIFIKACIJE ZA TERMIN ─────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.Kreiranje), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task KreirajNotifikacijuAsync_IspravniPodaci_DodajeNotifikacijuSaTimPodacima(int terminId, string poruka, Status status)
    {
        // Arrange (servis ne čita ništa iz repozitorija, samo dodaje)

        // Act
        await _servis.KreirajNotifikacijuAsync(terminId, poruka, status);

        // Assert
        _notifikacijaRepo.Verify(r => r.AddAsync(It.Is<Notifikacija>(n =>
            n.TerminID == terminId && n.Poruka == poruka && n.status == status)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.Kreiranje), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task KreirajNotifikacijuAsync_IspravniPodaci_SnimaPromjene(int terminId, string poruka, Status status)
    {
        // Arrange (servis ne čita ništa iz repozitorija, samo dodaje)

        // Act
        await _servis.KreirajNotifikacijuAsync(terminId, poruka, status);

        // Assert
        _notifikacijaRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ─── PROSLJEĐIVANJE REPOZITORIJU ──────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.Postojece), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetByIdAsync_NotifikacijaPostoji_VracaNotifikacijuIzRepozitorija(int id, string ocekivanaPoruka)
    {
        // Arrange
        _notifikacijaRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(NotifikacijaServicePodaci.IzBaze(_baza, id));

        // Act
        var rezultat = await _servis.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivanaPoruka, rezultat.Poruka);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.BrojUBazi), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetAllAsync_RepozitorijImaNotifikacije_VracaSveNotifikacije(int ocekivaniBroj)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetAllAsync();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.PostojeciIds), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Update_PostojecaNotifikacija_ProsljedjujeRepozitoriju(int id)
    {
        // Arrange
        var notifikacija = NotifikacijaServicePodaci.IzBaze(_baza, id);

        // Act
        _servis.Update(notifikacija);

        // Assert
        _notifikacijaRepo.Verify(r => r.Update(notifikacija), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.PostojeciIds), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Delete_PostojecaNotifikacija_ProsljedjujeRepozitoriju(int id)
    {
        // Arrange
        var notifikacija = NotifikacijaServicePodaci.IzBaze(_baza, id);

        // Act
        _servis.Delete(notifikacija);

        // Assert
        _notifikacijaRepo.Verify(r => r.Delete(notifikacija), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.Snimanje), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task SaveChangesAsync_RepozitorijSnimiPromjene_VracaBrojPromjena(int brojPromjena)
    {
        // Arrange
        _notifikacijaRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(brojPromjena);

        // Act
        var rezultat = await _servis.SaveChangesAsync();

        // Assert
        Assert.AreEqual(brojPromjena, rezultat);
    }
}
