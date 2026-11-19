using System.Xml.Linq;
using HealthZone.Models;
using HealthZone.Repositories;
using HealthZone.Services;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za ListaCekanjaServiceTests ───────
public static class ListaCekanjaServicePodaci
{
    // ── pomoćne metode ──

    public static ListaCekanja NapraviListu(XElement x) => new()
    {
        ListaId = (int)x.Attribute("id")!,
        DoktorID = (string)x.Attribute("doktorId")!
    };

    public static List<ListaCekanja> NapraviBazu(IEnumerable<XElement> baza) => baza.Select(NapraviListu).ToList();

    public static ListaCekanja IzBaze(IEnumerable<XElement> baza, int listaId) =>
        NapraviBazu(baza).Single(l => l.ListaId == listaId);

    // Korisnik je doktor ako ima specijalizaciju; prazna specijalizacija znači pacijent.
    public static List<Korisnik> NapraviKorisnike(string[] ids, string[] specijalizacije) =>
        ids.Zip(specijalizacije, (id, s) => new Korisnik
        {
            Id = id,
            Specijalizacija = string.IsNullOrEmpty(s) ? null : Enum.Parse<Specijalizacija>(s)
        }).ToList();

    private static string[] Lista(string vrijednost) => vrijednost.Split(',', StringSplitOptions.RemoveEmptyEntries);

    // ── izvori podataka ──

    public static IEnumerable<object[]> BrojUBazi() =>
        XmlLoader.Ucitaj("ListaCekanjaService_baza.xml", "baza").Select(b => new object[] { b.Elements("lista").Count() });

    public static IEnumerable<object[]> PostojeceListe() =>
        CsvLoader.Ucitaj("ListaCekanjaService_postojecaLista.csv").Select(r => new object[] { r[0], int.Parse(r[1]) });

    public static IEnumerable<object[]> DoktoriSaListom() =>
        CsvLoader.Ucitaj("ListaCekanjaService_postojecaLista.csv").Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> Duplikati() =>
        CsvLoader.Ucitaj("ListaCekanjaService_duplikat.csv").Select(r => new object[] { r[0], r[1] });

    public static IEnumerable<object[]> DoktoriBezListe() =>
        CsvLoader.Ucitaj("ListaCekanjaService_noviDoktor.csv").Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> NepostojeceListe() =>
        CsvLoader.Ucitaj("ListaCekanjaService_nepostojecaLista.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> NepostojeciIds() =>
        CsvLoader.Ucitaj("ListaCekanjaService_nepostojecaLista.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> PostojeciIds() =>
        CsvLoader.Ucitaj("ListaCekanjaService_postojeciId.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> PostojeciIdsSaDoktorom() =>
        CsvLoader.Ucitaj("ListaCekanjaService_postojeciId.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> Korisnici() =>
        XmlLoader.Ucitaj("ListaCekanjaService_doktori.xml", "scenarij").Select(x => new object[]
        {
            x.Elements("korisnik").Select(k => (string)k.Attribute("id")!).ToArray(),
            x.Elements("korisnik").Select(k => (string?)k.Attribute("specijalizacija") ?? string.Empty).ToArray(),
            Lista((string)x.Element("ocekivaniDoktori")!)
        });

    public static IEnumerable<object[]> KorisniciBezOcekivanja() =>
        XmlLoader.Ucitaj("ListaCekanjaService_doktori.xml", "scenarij").Select(x => new object[]
        {
            x.Elements("korisnik").Select(k => (string)k.Attribute("id")!).ToArray(),
            x.Elements("korisnik").Select(k => (string?)k.Attribute("specijalizacija") ?? string.Empty).ToArray()
        });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("ListaCekanjaService_snimanje.csv").Select(r => new object[] { int.Parse(r[0]) });
}

// ─── Testovi ────────────────────────────────────────────────────────────
// IListaCekanjaRepository i IKorisnikRepository su zamijenjeni Moq objektima.
// Repozitorij lista vraća liste iz XML "baze", pa servis može provjeriti ima li doktor već listu.
[TestClass]
public class ListaCekanjaServiceTests
{
    private static List<XElement> _baza = null!;   // samo za čitanje

    private Mock<IListaCekanjaRepository> _listaRepo = null!;
    private Mock<IKorisnikRepository> _korisnikRepo = null!;
    private ListaCekanjaService _servis = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _baza = XmlLoader.Ucitaj("ListaCekanjaService_baza.xml", "lista").ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        _listaRepo = new Mock<IListaCekanjaRepository>();
        _korisnikRepo = new Mock<IKorisnikRepository>();
        _listaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(ListaCekanjaServicePodaci.NapraviBazu(_baza));
        _servis = new ListaCekanjaService(_listaRepo.Object, _korisnikRepo.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _listaRepo.Reset();
        _korisnikRepo.Reset();
    }

    // ─── LISTA ZA DOKTORA ─────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojeceListe), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task GetListaZaDoktoraAsync_DoktorImaListu_VracaNjegovuListu(string doktorId, int ocekivanaListaId)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetListaZaDoktoraAsync(doktorId);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivanaListaId, rezultat.ListaId);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.DoktoriBezListe), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetListaZaDoktoraAsync_DoktorNemaListu_VracaNull(string doktorId)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetListaZaDoktoraAsync(doktorId);

        // Assert
        Assert.IsNull(rezultat);
    }

    // ─── DODAVANJE LISTE ──────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.Duplikati), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task AddAsync_DoktorVecImaListu_BacaException(string doktorId, string ocekivanaPoruka)
    {
        // Arrange
        var lista = new ListaCekanja { DoktorID = doktorId };

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(lista));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.DoktoriSaListom), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_DoktorVecImaListu_NeDodajeListu(string doktorId)
    {
        // Arrange
        var lista = new ListaCekanja { DoktorID = doktorId };

        // Act
        await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(lista));

        // Assert
        _listaRepo.Verify(r => r.AddAsync(It.IsAny<ListaCekanja>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.DoktoriBezListe), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task AddAsync_NoviDoktor_DodajeListuURepozitorij(string doktorId)
    {
        // Arrange
        var lista = new ListaCekanja { DoktorID = doktorId };

        // Act
        await _servis.AddAsync(lista);

        // Assert
        _listaRepo.Verify(r => r.AddAsync(lista), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.DoktoriBezListe), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_NoviDoktor_SnimaPromjene(string doktorId)
    {
        // Arrange
        var lista = new ListaCekanja { DoktorID = doktorId };

        // Act
        await _servis.AddAsync(lista);

        // Assert
        _listaRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ─── DOHVATI ILI KREIRAJ ──────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojeceListe), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetIliKreirajListuAsync_DoktorImaListu_VracaPostojecuListu(string doktorId, int ocekivanaListaId)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetIliKreirajListuAsync(doktorId);

        // Assert
        Assert.AreEqual(ocekivanaListaId, rezultat.ListaId);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.DoktoriSaListom), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetIliKreirajListuAsync_DoktorImaListu_NeKreiraNovuListu(string doktorId)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        await _servis.GetIliKreirajListuAsync(doktorId);

        // Assert
        _listaRepo.Verify(r => r.AddAsync(It.IsAny<ListaCekanja>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.DoktoriBezListe), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetIliKreirajListuAsync_DoktorNemaListu_VracaNovuListuZaDoktora(string doktorId)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetIliKreirajListuAsync(doktorId);

        // Assert
        Assert.AreEqual(doktorId, rezultat.DoktorID);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.DoktoriBezListe), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetIliKreirajListuAsync_DoktorNemaListu_DodajeListuURepozitorij(string doktorId)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        await _servis.GetIliKreirajListuAsync(doktorId);

        // Assert
        _listaRepo.Verify(r => r.AddAsync(It.Is<ListaCekanja>(l => l.DoktorID == doktorId)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.DoktoriBezListe), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetIliKreirajListuAsync_DoktorNemaListu_SnimaPromjene(string doktorId)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        await _servis.GetIliKreirajListuAsync(doktorId);

        // Assert
        _listaRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ─── BRISANJE LISTE ───────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.NepostojeceListe), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task ObrisiListuAsync_ListaNePostoji_BacaException(int listaId, string ocekivanaPoruka)
    {
        // Arrange
        _listaRepo.Setup(r => r.GetByIdAsync(listaId)).ReturnsAsync((ListaCekanja?)null);

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.ObrisiListuAsync(listaId));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.NepostojeciIds), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task ObrisiListuAsync_ListaNePostoji_NistaNeBrise(int listaId)
    {
        // Arrange
        _listaRepo.Setup(r => r.GetByIdAsync(listaId)).ReturnsAsync((ListaCekanja?)null);

        // Act
        await Assert.ThrowsExceptionAsync<Exception>(() => _servis.ObrisiListuAsync(listaId));

        // Assert
        _listaRepo.Verify(r => r.Delete(It.IsAny<ListaCekanja>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojeciIds), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task ObrisiListuAsync_ListaPostoji_BriseListu(int listaId)
    {
        // Arrange
        var lista = ListaCekanjaServicePodaci.IzBaze(_baza, listaId);
        _listaRepo.Setup(r => r.GetByIdAsync(listaId)).ReturnsAsync(lista);

        // Act
        await _servis.ObrisiListuAsync(listaId);

        // Assert
        _listaRepo.Verify(r => r.Delete(lista), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojeciIds), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task ObrisiListuAsync_ListaPostoji_SnimaPromjene(int listaId)
    {
        // Arrange
        _listaRepo.Setup(r => r.GetByIdAsync(listaId)).ReturnsAsync(ListaCekanjaServicePodaci.IzBaze(_baza, listaId));

        // Act
        await _servis.ObrisiListuAsync(listaId);

        // Assert
        _listaRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ─── DOKTORI ──────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.Korisnici), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetDoktoriAsync_RazliciteUloge_VracaSamoKorisnikeSaSpecijalizacijom(string[] ids,
        string[] specijalizacije, string[] ocekivaniDoktori)
    {
        // Arrange
        _korisnikRepo.Setup(r => r.GetAllAsync())
                     .ReturnsAsync(ListaCekanjaServicePodaci.NapraviKorisnike(ids, specijalizacije));

        // Act
        var rezultat = await _servis.GetDoktoriAsync();

        // Assert
        CollectionAssert.AreEquivalent(ocekivaniDoktori, rezultat.Select(k => k.Id).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.KorisniciBezOcekivanja), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetDoktoriAsync_Poziv_CitaKorisnikeIzRepozitorija(string[] ids, string[] specijalizacije)
    {
        // Arrange
        _korisnikRepo.Setup(r => r.GetAllAsync())
                     .ReturnsAsync(ListaCekanjaServicePodaci.NapraviKorisnike(ids, specijalizacije));

        // Act
        await _servis.GetDoktoriAsync();

        // Assert
        _korisnikRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    // ─── PROSLJEĐIVANJE REPOZITORIJU ──────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojeciIdsSaDoktorom), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetByIdAsync_ListaPostoji_VracaListuIzRepozitorija(int listaId, string ocekivaniDoktorId)
    {
        // Arrange
        _listaRepo.Setup(r => r.GetByIdAsync(listaId)).ReturnsAsync(ListaCekanjaServicePodaci.IzBaze(_baza, listaId));

        // Act
        var rezultat = await _servis.GetByIdAsync(listaId);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivaniDoktorId, rezultat.DoktorID);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.BrojUBazi), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetAllAsync_RepozitorijImaListe_VracaSveListe(int ocekivaniBroj)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetAllAsync();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojeciIds), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Update_PostojecaLista_ProsljedjujeRepozitoriju(int listaId)
    {
        // Arrange
        var lista = ListaCekanjaServicePodaci.IzBaze(_baza, listaId);

        // Act
        _servis.Update(lista);

        // Assert
        _listaRepo.Verify(r => r.Update(lista), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.PostojeciIds), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Delete_PostojecaLista_ProsljedjujeRepozitoriju(int listaId)
    {
        // Arrange
        var lista = ListaCekanjaServicePodaci.IzBaze(_baza, listaId);

        // Act
        _servis.Delete(lista);

        // Assert
        _listaRepo.Verify(r => r.Delete(lista), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaServicePodaci.Snimanje), typeof(ListaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task SaveChangesAsync_RepozitorijSnimiPromjene_VracaBrojPromjena(int brojPromjena)
    {
        // Arrange
        _listaRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(brojPromjena);

        // Act
        var rezultat = await _servis.SaveChangesAsync();

        // Assert
        Assert.AreEqual(brojPromjena, rezultat);
    }
}
