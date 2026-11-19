using System.Xml.Linq;
using HealthZone.Models;
using HealthZone.Repositories;
using HealthZone.Services;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za NotifikacijaServiceTests ────────
public static class NotifikacijaServicePodaci
{
    private static IEnumerable<string[]> UcitajKreiranje() =>
        CsvLoader.Ucitaj("NotifikacijaService_kreiranje.csv");

    public static IEnumerable<object[]> NepostojeciTerminCsv() =>
        UcitajKreiranje()
            .Where(r => r[0] == "999")
            .Select(r => new object[] { int.Parse(r[0]), r[1], Enum.Parse<Status>(r[2]), r[3] });

    public static IEnumerable<object[]> PostojeciTerminCsv() =>
        UcitajKreiranje()
            .Where(r => r[0] == "1")
            .Select(r => new object[] { int.Parse(r[0]), r[1], Enum.Parse<Status>(r[2]) });

    public static IEnumerable<object[]> FilterXml() =>
        XmlLoader.Ucitaj("NotifikacijaService_filter.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("pacijentId")!,
                     Enum.Parse<Status>((string)x.Element("status")!),
                     (int)x.Element("ocekivanoNotifikacija")!
                 });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class NotifikacijaServiceTests
{
    private static List<XElement> _podaciFilter = null!;

    private Mock<INotifikacijaRepository> _notifikacijaRepo = null!;
    private Mock<ITerminRepository> _terminRepo = null!;
    private NotifikacijaService _servis = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _podaciFilter = XmlLoader.Ucitaj("NotifikacijaService_filter.xml", "scenarij").ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        _notifikacijaRepo = new Mock<INotifikacijaRepository>();
        _terminRepo = new Mock<ITerminRepository>();

        _servis = new NotifikacijaService(
            _notifikacijaRepo.Object,
            _terminRepo.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _notifikacijaRepo.Reset();
        _terminRepo.Reset();
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.PostojeciTerminCsv), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetByIdAsync_PostojeciId_VracaNotifikaciju(int terminId, string poruka, Status status)
    {
        // Arrange
        var notif = new Notifikacija { NotifikacijaId = 1, TerminID = terminId, Poruka = poruka };
        _notifikacijaRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(notif);

        // Act
        var rezultat = await _servis.GetByIdAsync(1);

        // Assert
        Assert.AreEqual(notif, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.PostojeciTerminCsv), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetAllAsync_PostojeNotifikacije_VracaSve(int terminId, string poruka, Status status)
    {
        // Arrange
        var lista = new List<Notifikacija> { new Notifikacija { TerminID = terminId } };
        _notifikacijaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(lista);

        // Act
        var rezultat = await _servis.GetAllAsync();

        // Assert
        Assert.AreEqual(1, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.PostojeciTerminCsv), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public void Update_ValidnaNotifikacija_PozivaUpdateURepou(int terminId, string poruka, Status status)
    {
        // Arrange
        var notif = new Notifikacija { NotifikacijaId = 1, TerminID = terminId };

        // Act
        _servis.Update(notif);

        // Assert
        _notifikacijaRepo.Verify(r => r.Update(notif), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.PostojeciTerminCsv), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public void Delete_ValidnaNotifikacija_PozivaDeleteURepou(int terminId, string poruka, Status status)
    {
        // Arrange
        var notif = new Notifikacija { NotifikacijaId = 1, TerminID = terminId };

        // Act
        _servis.Delete(notif);

        // Assert
        _notifikacijaRepo.Verify(r => r.Delete(notif), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.PostojeciTerminCsv), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task SaveChangesAsync_PozivMetode_VracaBrojIzmjena(int terminId, string poruka, Status status)
    {
        // Arrange
        _notifikacijaRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var rezultat = await _servis.SaveChangesAsync();

        // Assert
        Assert.AreEqual(1, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.NepostojeciTerminCsv), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ramiz Plancic")]
    [Priority(1)]
    public async Task AddAsync_TerminNePostoji_BacaException(int terminId, string poruka, Status status, string ocekivanaPoruka)
    {
        // Arrange
        _terminRepo.Setup(r => r.GetByIdAsync(terminId)).ReturnsAsync((Termin?)null);
        var notif = new Notifikacija { TerminID = terminId, Poruka = poruka, status = status };

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(notif));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.PostojeciTerminCsv), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task AddAsync_TerminPostoji_DodajeURepozitorij(int terminId, string poruka, Status status)
    {
        // Arrange
        _terminRepo.Setup(r => r.GetByIdAsync(terminId)).ReturnsAsync(new Termin { TerminId = terminId });
        var notif = new Notifikacija { TerminID = terminId, Poruka = poruka, status = status };

        // Act
        await _servis.AddAsync(notif);

        // Assert
        _notifikacijaRepo.Verify(r => r.AddAsync(notif), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.PostojeciTerminCsv), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task AddAsync_TerminPostoji_PozivaSaveChanges(int terminId, string poruka, Status status)
    {
        // Arrange
        _terminRepo.Setup(r => r.GetByIdAsync(terminId)).ReturnsAsync(new Termin { TerminId = terminId });
        var notif = new Notifikacija { TerminID = terminId, Poruka = poruka, status = status };

        // Act
        await _servis.AddAsync(notif);

        // Assert
        _notifikacijaRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.PostojeciTerminCsv), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task KreirajNotifikacijuAsync_ValidniPodaci_DodajeURepozitorij(int terminId, string poruka, Status status)
    {
        // Arrange & Act
        await _servis.KreirajNotifikacijuAsync(terminId, poruka, status);

        // Assert
        _notifikacijaRepo.Verify(r => r.AddAsync(It.Is<Notifikacija>(n => n.TerminID == terminId && n.Poruka == poruka && n.status == status)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.PostojeciTerminCsv), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task KreirajNotifikacijuAsync_ValidniPodaci_PozivaSaveChanges(int terminId, string poruka, Status status)
    {
        // Arrange & Act
        await _servis.KreirajNotifikacijuAsync(terminId, poruka, status);

        // Assert
        _notifikacijaRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.PostojeciTerminCsv), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetNotifikacijeZaTerminAsync_PostojeNotifikacije_VracaSortirane(int terminId, string poruka, Status status)
    {
        // Arrange
        var n1 = new Notifikacija { TerminID = terminId, DatumSlanja = DateTime.Today.AddDays(-1) };
        var n2 = new Notifikacija { TerminID = terminId, DatumSlanja = DateTime.Today };
        var n3 = new Notifikacija { TerminID = 999, DatumSlanja = DateTime.Today };
        _notifikacijaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Notifikacija> { n1, n2, n3 });

        // Act
        var rezultat = (await _servis.GetNotifikacijeZaTerminAsync(terminId)).ToList();

        // Assert
        Assert.AreEqual(2, rezultat.Count);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.FilterXml), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetNotifikacijePacijentaAsync_PostojeNotifikacije_VracaNotifikacijePacijenta(string pacijentId, Status status, int ocekivano)
    {
        // Arrange
        var t1 = new Termin { PacijentID = pacijentId };
        var t2 = new Termin { PacijentID = "drugi_pac" };
        var notifikacije = new List<Notifikacija>
        {
            new Notifikacija { Termin = t1, DatumSlanja = DateTime.Today },
            new Notifikacija { Termin = t2, DatumSlanja = DateTime.Today }
        };
        _notifikacijaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(notifikacije);

        // Act
        var rezultat = (await _servis.GetNotifikacijePacijentaAsync(pacijentId)).ToList();

        // Assert
        Assert.AreEqual(ocekivano, rezultat.Count);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaServicePodaci.FilterXml), typeof(NotifikacijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetNotifikacijePoStatusuAsync_FiltriranjePoStatusu_VracaTacneNotifikacije(string pacijentId, Status status, int ocekivano)
    {
        // Arrange
        var notifikacije = new List<Notifikacija>
        {
            new Notifikacija { status = status },
            new Notifikacija { status = Status.Otkazan }
        };
        _notifikacijaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(notifikacije);

        // Act
        var rezultat = (await _servis.GetNotifikacijePoStatusuAsync(status)).ToList();

        // Assert
        Assert.AreEqual(ocekivano, rezultat.Count);
    }
}
