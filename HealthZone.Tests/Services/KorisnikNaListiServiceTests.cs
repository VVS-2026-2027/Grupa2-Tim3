using System.Xml.Linq;
using HealthZone.Models;
using HealthZone.Repositories;
using HealthZone.Services;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za KorisnikNaListiServiceTests ────
public static class KorisnikNaListiServicePodaci
{
    // ── pomoćne metode ──

    // Stavka liste čekanja iz XML "baze". Datum prijave je pomak u danima od danas.
    // Bez atributa prioritet pacijent nema prioritet, a sa bezKorisnika="true" pacijent nije učitan.
    public static KorisnikNaListi NapraviStavku(XElement x) => new()
    {
        KorisnikNaListiID = (int)x.Attribute("id")!,
        ListaID = (int)x.Attribute("listaId")!,
        KorisnikID = (string)x.Attribute("korisnikId")!,
        Datum = DateTime.Today.AddDays((int)x.Attribute("pomakDana")!),
        Korisnik = (bool?)x.Attribute("bezKorisnika") == true ? null : new Korisnik
        {
            Id = (string)x.Attribute("korisnikId")!,
            Prioritet = x.Attribute("prioritet") == null ? null : Enum.Parse<Prioritet>((string)x.Attribute("prioritet")!)
        }
    };

    public static List<KorisnikNaListi> NapraviBazu(IEnumerable<XElement> baza) => baza.Select(NapraviStavku).ToList();

    public static KorisnikNaListi IzBaze(IEnumerable<XElement> baza, int id) =>
        NapraviBazu(baza).Single(k => k.KorisnikNaListiID == id);

    // Termin iz XML-a. Bez atributa pacijentId termin nema učitanog pacijenta.
    public static Termin NapraviTermin(XElement x) => new()
    {
        TerminId = (int)x.Attribute("id")!,
        DoktorID = (string)x.Attribute("doktorId")!,
        PacijentID = (string?)x.Attribute("pacijentId") ?? string.Empty,
        Pacijent = x.Attribute("pacijentId") == null ? null : new Korisnik { Id = (string)x.Attribute("pacijentId")! }
    };

    public static List<Termin> NapraviTermine(IEnumerable<XElement> termini) => termini.Select(NapraviTermin).ToList();

    public static List<ListaCekanja> NapraviListe(int broj) =>
        Enumerable.Range(1, broj).Select(id => new ListaCekanja { ListaId = id }).ToList();

    public static List<Korisnik> NapraviKorisnike(int broj) =>
        Enumerable.Range(1, broj).Select(i => new Korisnik { Id = $"k-{i}" }).ToList();

    public static int[] Ids(string lista) =>
        lista.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();

    public static string[] Lista(string lista) => lista.Split(',', StringSplitOptions.RemoveEmptyEntries);

    // ── izvori podataka ──

    public static IEnumerable<object[]> BrojUBazi() =>
        XmlLoader.Ucitaj("KorisnikNaListiService_baza.xml", "baza").Select(b => new object[] { b.Elements("stavka").Count() });

    public static IEnumerable<object[]> Sortiranje() =>
        CsvLoader.Ucitaj("KorisnikNaListiService_sortiranje.csv").Select(r => new object[] { int.Parse(r[0]), Ids(r[1]) });

    public static IEnumerable<object[]> SljedeciPacijenti() =>
        XmlLoader.Ucitaj("KorisnikNaListiService_sljedeci.xml", "scenarij").Select(x => new object[]
        {
            (int)x.Element("listaId")!,
            (string)x.Element("ocekivaniKorisnikId")!
        });

    public static IEnumerable<object[]> PrazneListe() =>
        CsvLoader.Ucitaj("KorisnikNaListiService_praznaLista.csv").Select(r => new object[] { int.Parse(r[0]) });

    private static IEnumerable<string[]> Pozicije() => CsvLoader.Ucitaj("KorisnikNaListiService_pozicije.csv");

    public static IEnumerable<object[]> PacijentiNaListi() =>
        Pozicije().Where(r => r[2] != "-1").Select(r => new object[] { int.Parse(r[0]), r[1], int.Parse(r[2]) });

    public static IEnumerable<object[]> PacijentiVanListe() =>
        Pozicije().Where(r => r[2] == "-1").Select(r => new object[] { int.Parse(r[0]), r[1], int.Parse(r[2]) });

    public static IEnumerable<object[]> BrojNaListi() =>
        CsvLoader.Ucitaj("KorisnikNaListiService_brojNaListi.csv").Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]) });

    public static IEnumerable<object[]> ListeKorisnika() =>
        XmlLoader.Ucitaj("KorisnikNaListiService_listeKorisnika.xml", "scenarij").Select(x => new object[]
        {
            (string)x.Element("korisnikId")!,
            Ids((string)x.Element("ocekivaneStavke")!)
        });

    public static IEnumerable<object[]> Duplikati() =>
        CsvLoader.Ucitaj("KorisnikNaListiService_duplikat.csv").Select(r => new object[] { r[0], int.Parse(r[1]), r[2] });

    public static IEnumerable<object[]> DuplikatiBezPoruke() =>
        CsvLoader.Ucitaj("KorisnikNaListiService_duplikat.csv").Select(r => new object[] { r[0], int.Parse(r[1]) });

    public static IEnumerable<object[]> NoveStavke() =>
        CsvLoader.Ucitaj("KorisnikNaListiService_novaStavka.csv").Select(r => new object[] { r[0], int.Parse(r[1]) });

    public static IEnumerable<object[]> NepostojeceStavke() =>
        CsvLoader.Ucitaj("KorisnikNaListiService_nepostojecaStavka.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> NepostojeciIds() =>
        CsvLoader.Ucitaj("KorisnikNaListiService_nepostojecaStavka.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> PostojeceStavke() =>
        CsvLoader.Ucitaj("KorisnikNaListiService_postojecaStavka.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> PostojeciIds() =>
        CsvLoader.Ucitaj("KorisnikNaListiService_postojecaStavka.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> PacijentiDoktora() =>
        CsvLoader.Ucitaj("KorisnikNaListiService_pacijentiDoktora.csv").Select(r => new object[] { r[0], Lista(r[1]) });

    public static IEnumerable<object[]> Doktori() =>
        CsvLoader.Ucitaj("KorisnikNaListiService_pacijentiDoktora.csv").Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> BrojListi() =>
        CsvLoader.Ucitaj("KorisnikNaListiService_listeCekanja.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> BrojKorisnika() =>
        CsvLoader.Ucitaj("KorisnikNaListiService_korisnici.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("KorisnikNaListiService_snimanje.csv").Select(r => new object[] { int.Parse(r[0]) });
}

// ─── Testovi ────────────────────────────────────────────────────────────
// Sva tri repozitorija i ITerminService su zamijenjeni Moq objektima. ITerminService je drugi servis,
// pa se pravi TerminService ne poziva: mock vraća termine iz XML-a (Setup) i provjerava se poziv (Verify).
[TestClass]
public class KorisnikNaListiServiceTests
{
    private static List<XElement> _baza = null!;      // samo za čitanje
    private static List<XElement> _termini = null!;   // samo za čitanje

    private Mock<IKorisnikNaListiRepository> _stavkeRepo = null!;
    private Mock<IListaCekanjaRepository> _listeRepo = null!;
    private Mock<IKorisnikRepository> _korisnikRepo = null!;
    private Mock<ITerminService> _terminServis = null!;
    private KorisnikNaListiService _servis = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _baza = XmlLoader.Ucitaj("KorisnikNaListiService_baza.xml", "stavka").ToList();
        _termini = XmlLoader.Ucitaj("KorisnikNaListiService_termini.xml", "termin").ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        _stavkeRepo = new Mock<IKorisnikNaListiRepository>();
        _listeRepo = new Mock<IListaCekanjaRepository>();
        _korisnikRepo = new Mock<IKorisnikRepository>();
        _terminServis = new Mock<ITerminService>();
        _stavkeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(KorisnikNaListiServicePodaci.NapraviBazu(_baza));
        _terminServis.Setup(s => s.GetAllAsync()).ReturnsAsync(KorisnikNaListiServicePodaci.NapraviTermine(_termini));
        _servis = new KorisnikNaListiService(_stavkeRepo.Object, _listeRepo.Object, _korisnikRepo.Object, _terminServis.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _stavkeRepo.Reset();
        _listeRepo.Reset();
        _korisnikRepo.Reset();
        _terminServis.Reset();
    }

    // ─── SORTIRANA LISTA, SLJEDEĆI PACIJENT I POZICIJA ────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.Sortiranje), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task GetSortiranaListaAsync_RazlicitiPrioriteti_SortiraPoPrioritetuPaDatumuPrijave(int listaId, int[] ocekivaniRedoslijed)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetSortiranaListaAsync(listaId);

        // Assert
        CollectionAssert.AreEqual(ocekivaniRedoslijed, rezultat.Select(k => k.KorisnikNaListiID).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.SljedeciPacijenti), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task GetSljedeciPacijentAsync_ListaImaPacijente_VracaPrvogPoPrioritetu(int listaId, string ocekivaniKorisnikId)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetSljedeciPacijentAsync(listaId);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivaniKorisnikId, rezultat.KorisnikID);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PrazneListe), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetSljedeciPacijentAsync_ListaPrazna_VracaNull(int listaId)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetSljedeciPacijentAsync(listaId);

        // Assert
        Assert.IsNull(rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PacijentiNaListi), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task GetPozicijaAsync_PacijentNaListi_VracaPozicijuOdJedan(int listaId, string korisnikId, int ocekivanaPozicija)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetPozicijaAsync(listaId, korisnikId);

        // Assert
        Assert.AreEqual(ocekivanaPozicija, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PacijentiVanListe), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetPozicijaAsync_PacijentNijeNaListi_VracaMinusJedan(int listaId, string korisnikId, int ocekivanaPozicija)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetPozicijaAsync(listaId, korisnikId);

        // Assert
        Assert.AreEqual(ocekivanaPozicija, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.BrojNaListi), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetBrojNaListiAsync_ZadanaLista_VracaBrojPacijenataNaNjoj(int listaId, int ocekivaniBroj)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetBrojNaListiAsync(listaId);

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.ListeKorisnika), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetListeKorisnikaAsync_ZadanPacijent_VracaSamoNjegoveStavke(string korisnikId, int[] ocekivaneStavke)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetListeKorisnikaAsync(korisnikId);

        // Assert
        CollectionAssert.AreEquivalent(ocekivaneStavke, rezultat.Select(k => k.KorisnikNaListiID).ToArray());
    }

    // ─── DODAVANJE NA LISTU ───────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.Duplikati), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task AddAsync_PacijentVecNaListi_BacaException(string korisnikId, int listaId, string ocekivanaPoruka)
    {
        // Arrange
        var stavka = new KorisnikNaListi { KorisnikID = korisnikId, ListaID = listaId };

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(stavka));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.DuplikatiBezPoruke), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_PacijentVecNaListi_NeDodajeStavku(string korisnikId, int listaId)
    {
        // Arrange
        var stavka = new KorisnikNaListi { KorisnikID = korisnikId, ListaID = listaId };

        // Act
        await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(stavka));

        // Assert
        _stavkeRepo.Verify(r => r.AddAsync(It.IsAny<KorisnikNaListi>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.NoveStavke), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task AddAsync_NoviPacijent_DodajeStavkuURepozitorij(string korisnikId, int listaId)
    {
        // Arrange
        var stavka = new KorisnikNaListi { KorisnikID = korisnikId, ListaID = listaId };

        // Act
        await _servis.AddAsync(stavka);

        // Assert
        _stavkeRepo.Verify(r => r.AddAsync(stavka), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.NoveStavke), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_NoviPacijent_SnimaPromjene(string korisnikId, int listaId)
    {
        // Arrange
        var stavka = new KorisnikNaListi { KorisnikID = korisnikId, ListaID = listaId };

        // Act
        await _servis.AddAsync(stavka);

        // Assert
        _stavkeRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.NoveStavke), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_NoviPacijent_PostavljaTrenutniDatumPrijave(string korisnikId, int listaId)
    {
        // Arrange
        var stavka = new KorisnikNaListi { KorisnikID = korisnikId, ListaID = listaId };
        var prije = DateTime.Now;

        // Act
        await _servis.AddAsync(stavka);

        // Assert
        Assert.IsTrue(stavka.Datum >= prije);
    }

    // ─── UKLANJANJE S LISTE ───────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.NepostojeceStavke), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task UkloniSListeAsync_StavkaNePostoji_BacaException(int id, string ocekivanaPoruka)
    {
        // Arrange
        _stavkeRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((KorisnikNaListi?)null);

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.UkloniSListeAsync(id));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.NepostojeciIds), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task UkloniSListeAsync_StavkaNePostoji_NistaNeBrise(int id)
    {
        // Arrange
        _stavkeRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((KorisnikNaListi?)null);

        // Act
        await Assert.ThrowsExceptionAsync<Exception>(() => _servis.UkloniSListeAsync(id));

        // Assert
        _stavkeRepo.Verify(r => r.Delete(It.IsAny<KorisnikNaListi>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PostojeciIds), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task UkloniSListeAsync_StavkaPostoji_BriseStavku(int id)
    {
        // Arrange
        var stavka = KorisnikNaListiServicePodaci.IzBaze(_baza, id);
        _stavkeRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(stavka);

        // Act
        await _servis.UkloniSListeAsync(id);

        // Assert
        _stavkeRepo.Verify(r => r.Delete(stavka), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PostojeciIds), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task UkloniSListeAsync_StavkaPostoji_SnimaPromjene(int id)
    {
        // Arrange
        _stavkeRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(KorisnikNaListiServicePodaci.IzBaze(_baza, id));

        // Act
        await _servis.UkloniSListeAsync(id);

        // Assert
        _stavkeRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ─── PACIJENTI S TERMINOM KOD DOKTORA (drugi servis je mock) ──────────

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PacijentiDoktora), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task GetPacijentiSaTerminomAsync_DoktorImaTermine_VracaJedinstvenePacijente(string doktorId, string[] ocekivaniPacijenti)
    {
        // Arrange (mock ITerminService vraća termine iz XML-a, pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetPacijentiSaTerminomAsync(doktorId);

        // Assert
        CollectionAssert.AreEquivalent(ocekivaniPacijenti, rezultat.Select(p => p.Id).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.Doktori), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetPacijentiSaTerminomAsync_Poziv_CitaTermineIzTerminServisa(string doktorId)
    {
        // Arrange (mock ITerminService vraća termine iz XML-a, pripremljeno u Setup)

        // Act
        await _servis.GetPacijentiSaTerminomAsync(doktorId);

        // Assert
        _terminServis.Verify(s => s.GetAllAsync(), Times.Once);
    }

    // ─── POMOĆNE METODE (liste i korisnici) ───────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.BrojListi), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetListeCekanjaAsync_PostojeListe_VracaListeIzRepozitorija(int brojListi)
    {
        // Arrange
        _listeRepo.Setup(r => r.GetAllAsync())
                  .ReturnsAsync(KorisnikNaListiServicePodaci.NapraviListe(brojListi));

        // Act
        var rezultat = await _servis.GetListeCekanjaAsync();

        // Assert
        Assert.AreEqual(brojListi, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.BrojListi), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetListeCekanjaAsync_Poziv_CitaRepozitorijListi(int brojListi)
    {
        // Arrange
        _listeRepo.Setup(r => r.GetAllAsync())
                  .ReturnsAsync(KorisnikNaListiServicePodaci.NapraviListe(brojListi));

        // Act
        await _servis.GetListeCekanjaAsync();

        // Assert
        _listeRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.BrojKorisnika), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetKorisniciAsync_PostojeKorisnici_VracaKorisnikeIzRepozitorija(int brojKorisnika)
    {
        // Arrange
        _korisnikRepo.Setup(r => r.GetAllAsync())
                     .ReturnsAsync(KorisnikNaListiServicePodaci.NapraviKorisnike(brojKorisnika));

        // Act
        var rezultat = await _servis.GetKorisniciAsync();

        // Assert
        Assert.AreEqual(brojKorisnika, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.BrojKorisnika), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetKorisniciAsync_Poziv_CitaRepozitorijKorisnika(int brojKorisnika)
    {
        // Arrange
        _korisnikRepo.Setup(r => r.GetAllAsync())
                     .ReturnsAsync(KorisnikNaListiServicePodaci.NapraviKorisnike(brojKorisnika));

        // Act
        await _servis.GetKorisniciAsync();

        // Assert
        _korisnikRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    // ─── PROSLJEĐIVANJE REPOZITORIJU ──────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PostojeceStavke), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetByIdAsync_StavkaPostoji_VracaStavkuIzRepozitorija(int id, string ocekivaniKorisnikId)
    {
        // Arrange
        _stavkeRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(KorisnikNaListiServicePodaci.IzBaze(_baza, id));

        // Act
        var rezultat = await _servis.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivaniKorisnikId, rezultat.KorisnikID);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.BrojUBazi), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetAllAsync_RepozitorijImaStavke_VracaSveStavke(int ocekivaniBroj)
    {
        // Arrange (repozitorij vraća XML "bazu", pripremljeno u Setup)

        // Act
        var rezultat = await _servis.GetAllAsync();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PostojeciIds), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Update_PostojecaStavka_ProsljedjujeRepozitoriju(int id)
    {
        // Arrange
        var stavka = KorisnikNaListiServicePodaci.IzBaze(_baza, id);

        // Act
        _servis.Update(stavka);

        // Assert
        _stavkeRepo.Verify(r => r.Update(stavka), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.PostojeciIds), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Delete_PostojecaStavka_ProsljedjujeRepozitoriju(int id)
    {
        // Arrange
        var stavka = KorisnikNaListiServicePodaci.IzBaze(_baza, id);

        // Act
        _servis.Delete(stavka);

        // Assert
        _stavkeRepo.Verify(r => r.Delete(stavka), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiServicePodaci.Snimanje), typeof(KorisnikNaListiServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task SaveChangesAsync_RepozitorijSnimiPromjene_VracaBrojPromjena(int brojPromjena)
    {
        // Arrange
        _stavkeRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(brojPromjena);

        // Act
        var rezultat = await _servis.SaveChangesAsync();

        // Assert
        Assert.AreEqual(brojPromjena, rezultat);
    }
}
