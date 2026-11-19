using System.Xml.Linq;
using HealthZone.Models;
using HealthZone.Repositories;
using HealthZone.Services;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za KorisnikNaListiServiceTests ─────
public static class KorisnikNaListiServicePodaci
{
    public static IEnumerable<object[]> VecNaListiCsv() =>
        CsvLoader.Ucitaj("KorisnikNaListiService_dodavanje.csv")
                 .Select(r => new object[] { r[0], int.Parse(r[1]), r[2] });

    public static IEnumerable<object[]> PozicijeCsv() =>
        CsvLoader.Ucitaj("KorisnikNaListiService_pozicije.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1], int.Parse(r[2]) });

    public static IEnumerable<object[]> SortiranjeXml() =>
        XmlLoader.Ucitaj("KorisnikNaListiService_sortiranje.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (int)x.Element("listaId")!,
                     (string)x.Element("ocekivaniPrviPacijent")!,
                     (int)x.Element("ukupnoPacijenata")!
                 });

    public static IEnumerable<object[]> TerminiXml() =>
        XmlLoader.Ucitaj("KorisnikNaListiService_termini.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("doktorId")!,
                     (int)x.Element("ocekivaniBrojPacijenata")!
                 });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class KorisnikNaListiServiceTests
{
    private static List<XElement> _podaciSortiranje = null!;

    private Mock<IKorisnikNaListiRepository> _korisnikNaListiRepo = null!;
    private Mock<IListaCekanjaRepository> _listaCekanjaRepo = null!;
    private Mock<IKorisnikRepository> _korisnikRepo = null!;
    private Mock<ITerminService> _terminService = null!;
    private KorisnikNaListiService _servis = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _podaciSortiranje = XmlLoader.Ucitaj("KorisnikNaListiService_sortiranje.xml", "scenarij").ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        _korisnikNaListiRepo = new Mock<IKorisnikNaListiRepository>();
        _listaCekanjaRepo = new Mock<IListaCekanjaRepository>();
        _korisnikRepo = new Mock<IKorisnikRepository>();
        _terminService = new Mock<ITerminService>();

        _servis = new KorisnikNaListiService(
            _korisnikNaListiRepo.Object,
            _listaCekanjaRepo.Object,
            _korisnikRepo.Object,
            _terminService.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _korisnikNaListiRepo.Reset();
        _listaCekanjaRepo.Reset();
        _korisnikRepo.Reset();
        _terminService.Reset();
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.VecNaListiCsv), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ramiz Plancic")]
    [Priority(1)]
    public async Task AddAsync_PacijentVecNaListi_BacaException(string korisnikId, int listaId, string ocekivanaPoruka)
    {
        // Arrange
        var postojeci = new List<KorisnikNaListi>
        {
            new KorisnikNaListi { KorisnikID = korisnikId, ListaID = listaId }
        };
        _korisnikNaListiRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(postojeci);

        var novi = new KorisnikNaListi { KorisnikID = korisnikId, ListaID = listaId };

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(novi));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.VecNaListiCsv), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task AddAsync_NoviPacijent_DodajeURepozitorij(string korisnikId, int listaId, string opis)
    {
        // Arrange
        _korisnikNaListiRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<KorisnikNaListi>());
        var novi = new KorisnikNaListi { KorisnikID = "drugi_pacijent", ListaID = listaId };

        // Act
        await _servis.AddAsync(novi);

        // Assert
        _korisnikNaListiRepo.Verify(r => r.AddAsync(novi), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.VecNaListiCsv), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task AddAsync_NoviPacijent_PozivaSaveChanges(string korisnikId, int listaId, string opis)
    {
        // Arrange
        _korisnikNaListiRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<KorisnikNaListi>());
        var novi = new KorisnikNaListi { KorisnikID = "drugi_pacijent", ListaID = listaId };

        // Act
        await _servis.AddAsync(novi);

        // Assert
        _korisnikNaListiRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PozicijeCsv), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetByIdAsync_PozivMetode_VracaStavkuIzRepozitorija(int listaId, string korisnikId, int ocekivanaPozicija)
    {
        // Arrange
        var ocekivani = new KorisnikNaListi { KorisnikNaListiID = listaId, KorisnikID = korisnikId };
        _korisnikNaListiRepo.Setup(r => r.GetByIdAsync(listaId)).ReturnsAsync(ocekivani);

        // Act
        var rezultat = await _servis.GetByIdAsync(listaId);

        // Assert
        Assert.AreEqual(ocekivani, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PozicijeCsv), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetAllAsync_PostojeStavke_VracaKolekciju(int listaId, string korisnikId, int pozicija)
    {
        // Arrange
        var lista = new List<KorisnikNaListi> { new KorisnikNaListi { KorisnikID = korisnikId } };
        _korisnikNaListiRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(lista);

        // Act
        var rezultat = await _servis.GetAllAsync();

        // Assert
        Assert.AreEqual(1, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PozicijeCsv), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public void Update_ValidanZapis_PozivaUpdateURepou(int listaId, string korisnikId, int pozicija)
    {
        // Arrange
        var stavka = new KorisnikNaListi { KorisnikNaListiID = listaId, KorisnikID = korisnikId };

        // Act
        _servis.Update(stavka);

        // Assert
        _korisnikNaListiRepo.Verify(r => r.Update(stavka), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PozicijeCsv), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public void Delete_ValidanZapis_PozivaDeleteURepou(int listaId, string korisnikId, int pozicija)
    {
        // Arrange
        var stavka = new KorisnikNaListi { KorisnikNaListiID = listaId, KorisnikID = korisnikId };

        // Act
        _servis.Delete(stavka);

        // Assert
        _korisnikNaListiRepo.Verify(r => r.Delete(stavka), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PozicijeCsv), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task SaveChangesAsync_PozivMetode_VracaBrojIzmjena(int listaId, string korisnikId, int pozicija)
    {
        // Arrange
        _korisnikNaListiRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var rezultat = await _servis.SaveChangesAsync();

        // Assert
        Assert.AreEqual(1, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.VecNaListiCsv), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task UkloniSListeAsync_NepostojecaStavka_BacaException(string korisnikId, int listaId, string poruka)
    {
        // Arrange
        _korisnikNaListiRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((KorisnikNaListi?)null);

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.UkloniSListeAsync(999));

        // Assert
        Assert.AreEqual("Stavka nije pronađena na listi čekanja.", izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.VecNaListiCsv), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task UkloniSListeAsync_PostojecaStavka_PozivaDelete(string korisnikId, int listaId, string poruka)
    {
        // Arrange
        var stavka = new KorisnikNaListi { KorisnikNaListiID = 10 };
        _korisnikNaListiRepo.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(stavka);

        // Act
        await _servis.UkloniSListeAsync(10);

        // Assert
        _korisnikNaListiRepo.Verify(r => r.Delete(stavka), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.VecNaListiCsv), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task UkloniSListeAsync_PostojecaStavka_PozivaSaveChanges(string korisnikId, int listaId, string poruka)
    {
        // Arrange
        var stavka = new KorisnikNaListi { KorisnikNaListiID = 10 };
        _korisnikNaListiRepo.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(stavka);

        // Act
        await _servis.UkloniSListeAsync(10);

        // Assert
        _korisnikNaListiRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.SortiranjeXml), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetSortiranaListaAsync_RazlicitiPrioriteti_SortiraIspravno(int listaId, string ocekivaniPrvi, int ukupno)
    {
        // Arrange
        var lista = new List<KorisnikNaListi>
        {
            new KorisnikNaListi { ListaID = listaId, KorisnikID = "pacijent_nizak", Datum = DateTime.Today.AddDays(-5), Korisnik = new Korisnik { Prioritet = Prioritet.Nizak } },
            new KorisnikNaListi { ListaID = listaId, KorisnikID = ocekivaniPrvi, Datum = DateTime.Today, Korisnik = new Korisnik { Prioritet = Prioritet.Visok } },
            new KorisnikNaListi { ListaID = listaId, KorisnikID = "pacijent_srednji", Datum = DateTime.Today.AddDays(-2), Korisnik = new Korisnik { Prioritet = Prioritet.Srednji } }
        };
        _korisnikNaListiRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(lista);

        // Act
        var sortirano = (await _servis.GetSortiranaListaAsync(listaId)).ToList();

        // Assert
        Assert.AreEqual(ocekivaniPrvi, sortirano.First().KorisnikID);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.SortiranjeXml), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetSljedeciPacijentAsync_PostojePacijenti_VracaPrvog(int listaId, string ocekivaniPrvi, int ukupno)
    {
        // Arrange
        var lista = new List<KorisnikNaListi>
        {
            new KorisnikNaListi { ListaID = listaId, KorisnikID = ocekivaniPrvi, Datum = DateTime.Today, Korisnik = new Korisnik { Prioritet = Prioritet.Visok } }
        };
        _korisnikNaListiRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(lista);

        // Act
        var sljedeci = await _servis.GetSljedeciPacijentAsync(listaId);

        // Assert
        Assert.AreEqual(ocekivaniPrvi, sljedeci?.KorisnikID);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.SortiranjeXml), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetSljedeciPacijentAsync_PraznaLista_VracaNull(int listaId, string ocekivaniPrvi, int ukupno)
    {
        // Arrange
        _korisnikNaListiRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<KorisnikNaListi>());

        // Act
        var sljedeci = await _servis.GetSljedeciPacijentAsync(listaId);

        // Assert
        Assert.IsNull(sljedeci);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PozicijeCsv), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetPozicijaAsync_KorisnikNaListi_VracaIspravnuPoziciju(int listaId, string korisnikId, int ocekivanaPozicija)
    {
        // Arrange
        var lista = new List<KorisnikNaListi>
        {
            new KorisnikNaListi { ListaID = listaId, KorisnikID = "p1", Datum = DateTime.Today, Korisnik = new Korisnik { Prioritet = Prioritet.Visok } },
            new KorisnikNaListi { ListaID = listaId, KorisnikID = "p2", Datum = DateTime.Today.AddDays(1), Korisnik = new Korisnik { Prioritet = Prioritet.Nizak } }
        };
        _korisnikNaListiRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(lista);

        // Act
        var pozicija = await _servis.GetPozicijaAsync(listaId, korisnikId);

        // Assert
        Assert.AreEqual(ocekivanaPozicija, pozicija);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PozicijeCsv), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetListeKorisnikaAsync_KorisnikImaListe_VracaNjegoveStavke(int listaId, string korisnikId, int pozicija)
    {
        // Arrange
        var stavke = new List<KorisnikNaListi>
        {
            new KorisnikNaListi { KorisnikID = korisnikId, ListaID = 1 },
            new KorisnikNaListi { KorisnikID = "drugi", ListaID = 2 }
        };
        _korisnikNaListiRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(stavke);

        // Act
        var rezultat = await _servis.GetListeKorisnikaAsync(korisnikId);

        // Assert
        Assert.AreEqual(1, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PozicijeCsv), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetBrojNaListiAsync_PostojePacijenti_VracaTacanBroj(int listaId, string korisnikId, int pozicija)
    {
        // Arrange
        var stavke = new List<KorisnikNaListi>
        {
            new KorisnikNaListi { ListaID = listaId },
            new KorisnikNaListi { ListaID = listaId }
        };
        _korisnikNaListiRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(stavke);

        // Act
        var broj = await _servis.GetBrojNaListiAsync(listaId);

        // Assert
        Assert.AreEqual(2, broj);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.TerminiXml), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetPacijentiSaTerminomAsync_DoktorImaTermine_VracaJedinstvenePacijente(string doktorId, int ocekivaniBroj)
    {
        // Arrange
        var p1 = new Korisnik { Id = "pac1" };
        var p2 = new Korisnik { Id = "pac2" };
        var termini = new List<Termin>
        {
            new Termin { DoktorID = doktorId, Pacijent = p1 },
            new Termin { DoktorID = doktorId, Pacijent = p1 },
            new Termin { DoktorID = doktorId, Pacijent = p2 },
            new Termin { DoktorID = "drugi_doktor", Pacijent = p1 }
        };
        _terminService.Setup(t => t.GetAllAsync()).ReturnsAsync(termini);

        // Act
        var pacijenti = await _servis.GetPacijentiSaTerminomAsync(doktorId);

        // Assert
        Assert.AreEqual(ocekivaniBroj, pacijenti.Count());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PozicijeCsv), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetListeCekanjaAsync_PozivMetode_VracaSveListe(int listaId, string korisnikId, int pozicija)
    {
        // Arrange
        var liste = new List<ListaCekanja> { new ListaCekanja { ListaId = 1 } };
        _listaCekanjaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(liste);

        // Act
        var rezultat = await _servis.GetListeCekanjaAsync();

        // Assert
        Assert.AreEqual(1, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PozicijeCsv), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetKorisniciAsync_PozivMetode_VracaSveKorisnike(int listaId, string korisnikId, int pozicija)
    {
        // Arrange
        var korisnici = new List<Korisnik> { new Korisnik { Id = "k1" } };
        _korisnikRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(korisnici);

        // Act
        var rezultat = await _servis.GetKorisniciAsync();

        // Assert
        Assert.AreEqual(1, rezultat.Count());
    }
}
