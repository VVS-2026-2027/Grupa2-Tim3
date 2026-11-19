using System.Xml.Linq;
using HealthZone.Models;
using HealthZone.Repositories;
using HealthZone.Services;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za ListaCekanjaServiceTests ────────
public static class ListaCekanjaServicePodaci
{
    private static IEnumerable<string[]> UcitajGreske() =>
        CsvLoader.Ucitaj("ListaCekanjaService_greske.csv");

    public static IEnumerable<object[]> PostojecaListaCsv() =>
        UcitajGreske()
            .Where(r => r[0] == "d1")
            .Select(r => new object[] { r[0], int.Parse(r[1]), r[2] });

    public static IEnumerable<object[]> NepostojecaListaCsv() =>
        UcitajGreske()
            .Where(r => r[0] == "d2")
            .Select(r => new object[] { r[0], int.Parse(r[1]), r[2] });

    public static IEnumerable<object[]> DoktoriXml() =>
        XmlLoader.Ucitaj("ListaCekanjaService_doktori.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("doktorId")!,
                     (string)x.Element("doktorIme")!,
                     (string)x.Element("doktorPrezime")!,
                     Enum.Parse<Specijalizacija>((string)x.Element("specijalizacija")!),
                     (int)x.Element("ocekivanoDoktora")!
                 });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class ListaCekanjaServiceTests
{
    private static List<XElement> _podaciDoktori = null!;

    private Mock<IListaCekanjaRepository> _listaCekanjaRepo = null!;
    private Mock<IKorisnikRepository> _korisnikRepo = null!;
    private ListaCekanjaService _servis = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _podaciDoktori = XmlLoader.Ucitaj("ListaCekanjaService_doktori.xml", "scenarij").ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        _listaCekanjaRepo = new Mock<IListaCekanjaRepository>();
        _korisnikRepo = new Mock<IKorisnikRepository>();

        _servis = new ListaCekanjaService(
            _listaCekanjaRepo.Object,
            _korisnikRepo.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _listaCekanjaRepo.Reset();
        _korisnikRepo.Reset();
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojecaListaCsv), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetByIdAsync_PostojeciId_VracaListu(string doktorId, int listaId, string poruka)
    {
        // Arrange
        var ocekivana = new ListaCekanja { ListaId = listaId, DoktorID = doktorId };
        _listaCekanjaRepo.Setup(r => r.GetByIdAsync(listaId)).ReturnsAsync(ocekivana);

        // Act
        var rezultat = await _servis.GetByIdAsync(listaId);

        // Assert
        Assert.AreEqual(ocekivana, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojecaListaCsv), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetAllAsync_PostojeListe_VracaKolekciju(string doktorId, int listaId, string poruka)
    {
        // Arrange
        var liste = new List<ListaCekanja> { new ListaCekanja { ListaId = listaId, DoktorID = doktorId } };
        _listaCekanjaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(liste);

        // Act
        var rezultat = await _servis.GetAllAsync();

        // Assert
        Assert.AreEqual(1, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojecaListaCsv), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public void Update_ValidnaLista_PozivaUpdateURepou(string doktorId, int listaId, string poruka)
    {
        // Arrange
        var lista = new ListaCekanja { ListaId = listaId, DoktorID = doktorId };

        // Act
        _servis.Update(lista);

        // Assert
        _listaCekanjaRepo.Verify(r => r.Update(lista), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojecaListaCsv), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public void Delete_ValidnaLista_PozivaDeleteURepou(string doktorId, int listaId, string poruka)
    {
        // Arrange
        var lista = new ListaCekanja { ListaId = listaId, DoktorID = doktorId };

        // Act
        _servis.Delete(lista);

        // Assert
        _listaCekanjaRepo.Verify(r => r.Delete(lista), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojecaListaCsv), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task SaveChangesAsync_PozivMetode_VracaBrojIzmjena(string doktorId, int listaId, string poruka)
    {
        // Arrange
        _listaCekanjaRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var rezultat = await _servis.SaveChangesAsync();

        // Assert
        Assert.AreEqual(1, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojecaListaCsv), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ramiz Plancic")]
    [Priority(1)]
    public async Task AddAsync_DoktorVecImaListu_BacaException(string doktorId, int listaId, string ocekivanaPoruka)
    {
        // Arrange
        var postojeca = new List<ListaCekanja> { new ListaCekanja { ListaId = listaId, DoktorID = doktorId } };
        _listaCekanjaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(postojeca);

        var nova = new ListaCekanja { DoktorID = doktorId };

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(nova));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojecaListaCsv), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task AddAsync_NoviDoktor_DodajeListuURepozitorij(string doktorId, int listaId, string opis)
    {
        // Arrange
        _listaCekanjaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ListaCekanja>());
        var nova = new ListaCekanja { DoktorID = "novi_doktor" };

        // Act
        await _servis.AddAsync(nova);

        // Assert
        _listaCekanjaRepo.Verify(r => r.AddAsync(nova), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojecaListaCsv), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task AddAsync_NoviDoktor_PozivaSaveChanges(string doktorId, int listaId, string opis)
    {
        // Arrange
        _listaCekanjaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ListaCekanja>());
        var nova = new ListaCekanja { DoktorID = "novi_doktor" };

        // Act
        await _servis.AddAsync(nova);

        // Assert
        _listaCekanjaRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojecaListaCsv), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetListaZaDoktoraAsync_DoktorImaListu_VracaListu(string doktorId, int listaId, string poruka)
    {
        // Arrange
        var liste = new List<ListaCekanja> { new ListaCekanja { ListaId = listaId, DoktorID = doktorId } };
        _listaCekanjaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(liste);

        // Act
        var rezultat = await _servis.GetListaZaDoktoraAsync(doktorId);

        // Assert
        Assert.AreEqual(listaId, rezultat?.ListaId);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojecaListaCsv), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetListaZaDoktoraAsync_DoktorNemaListu_VracaNull(string doktorId, int listaId, string poruka)
    {
        // Arrange
        _listaCekanjaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ListaCekanja>());

        // Act
        var rezultat = await _servis.GetListaZaDoktoraAsync("nepostojeci_doc");

        // Assert
        Assert.IsNull(resultado(rezultat));

        static object? resultado(object? x) => x;
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojecaListaCsv), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetIliKreirajListuAsync_ListaPostoji_VracaPostojecu(string doktorId, int listaId, string poruka)
    {
        // Arrange
        var postojeca = new ListaCekanja { ListaId = listaId, DoktorID = doktorId };
        _listaCekanjaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ListaCekanja> { postojeca });

        // Act
        var rezultat = await _servis.GetIliKreirajListuAsync(doktorId);

        // Assert
        Assert.AreEqual(postojeca, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojecaListaCsv), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetIliKreirajListuAsync_ListaNePostoji_DodajeNovu(string doktorId, int listaId, string poruka)
    {
        // Arrange
        _listaCekanjaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ListaCekanja>());

        // Act
        await _servis.GetIliKreirajListuAsync(doktorId);

        // Assert
        _listaCekanjaRepo.Verify(r => r.AddAsync(It.Is<ListaCekanja>(l => l.DoktorID == doktorId)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojecaListaCsv), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetIliKreirajListuAsync_ListaNePostoji_PozivaSaveChanges(string doktorId, int listaId, string poruka)
    {
        // Arrange
        _listaCekanjaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ListaCekanja>());

        // Act
        await _servis.GetIliKreirajListuAsync(doktorId);

        // Assert
        _listaCekanjaRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.NepostojecaListaCsv), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task ObrisiListuAsync_ListaNePostoji_BacaException(string doktorId, int listaId, string ocekivanaPoruka)
    {
        // Arrange
        _listaCekanjaRepo.Setup(r => r.GetByIdAsync(listaId)).ReturnsAsync((ListaCekanja?)null);

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.ObrisiListuAsync(listaId));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojecaListaCsv), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task ObrisiListuAsync_ListaPostoji_PozivaDelete(string doktorId, int listaId, string poruka)
    {
        // Arrange
        var lista = new ListaCekanja { ListaId = listaId, DoktorID = doktorId };
        _listaCekanjaRepo.Setup(r => r.GetByIdAsync(listaId)).ReturnsAsync(lista);

        // Act
        await _servis.ObrisiListuAsync(listaId);

        // Assert
        _listaCekanjaRepo.Verify(r => r.Delete(lista), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojecaListaCsv), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task ObrisiListuAsync_ListaPostoji_PozivaSaveChanges(string doktorId, int listaId, string poruka)
    {
        // Arrange
        var lista = new ListaCekanja { ListaId = listaId, DoktorID = doktorId };
        _listaCekanjaRepo.Setup(r => r.GetByIdAsync(listaId)).ReturnsAsync(lista);

        // Act
        await _servis.ObrisiListuAsync(listaId);

        // Assert
        _listaCekanjaRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.DoktoriXml), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetDoktoriAsync_PostojeKorisnici_VracaDoktoreSaSpecijalizacijom(
        string doktorId, string ime, string prezime, Specijalizacija specijalizacija, int ocekivanoDoktora)
    {
        // Arrange
        var korisnici = new List<Korisnik>
        {
            new Korisnik { Id = doktorId, Ime = ime, Prezime = prezime, Specijalizacija = specijalizacija },
            new Korisnik { Id = "pacijent1", Ime = "Neko", Specijalizacija = null }
        };
        _korisnikRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(korisnici);

        // Act
        var rezultat = (await _servis.GetDoktoriAsync()).ToList();

        // Assert
        Assert.AreEqual(ocekivanoDoktora, rezultat.Count);
    }
}
