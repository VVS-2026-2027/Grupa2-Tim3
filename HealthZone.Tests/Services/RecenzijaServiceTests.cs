using System.Globalization;
using System.Xml.Linq;
using HealthZone.Models;
using HealthZone.Repositories;
using HealthZone.Services;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za RecenzijaServiceTests ──────────
public static class RecenzijaServicePodaci
{
    // ── pomoćne metode ──

    public static Recenzija NapraviRecenziju(XElement x) => new()
    {
        RecenzijaId = (int)x.Element("id")!,
        PacijentID = (string)x.Element("pacijentId")!,
        DoktorID = (string)x.Element("doktorId")!,
        OcjenaLjubaznosti = (int)x.Element("ljubaznost")!,
        OcjenaProfesionalnosti = (int)x.Element("profesionalnost")!,
        OcjenaUsluge = (int)x.Element("usluga")!
    };

    public static Korisnik NapraviKorisnika(XElement x) => new()
    {
        Id = (string)x.Element("id")!,
        Specijalizacija = EnumIliNull<Specijalizacija>((string?)x.Element("specijalizacija")),
        Prioritet = EnumIliNull<Prioritet>((string?)x.Element("prioritet"))
    };

    public static Termin NapraviTermin(XElement x) => new()
    {
        PacijentID = (string)x.Element("pacijentId")!,
        DoktorID = (string)x.Element("doktorId")!,
        Status = Enum.Parse<Status>((string)x.Element("status")!)
    };

    public static T? EnumIliNull<T>(string? s) where T : struct, Enum
        => string.IsNullOrEmpty(s) ? null : Enum.Parse<T>(s);

    public static int[] Brojevi(string s) =>
        s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();

    public static double Broj(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    // Od tri liste ocjena iz CSV-a (npr. "5,4,5") pravi recenzije za jednog doktora.
    public static List<Recenzija> RecenzijeIzOcjena(string doktorId, string ljubaznost, string profesionalnost, string usluga) =>
        Brojevi(ljubaznost)
            .Select((l, i) => new Recenzija
            {
                DoktorID = doktorId,
                OcjenaLjubaznosti = l,
                OcjenaProfesionalnosti = Brojevi(profesionalnost)[i],
                OcjenaUsluge = Brojevi(usluga)[i]
            })
            .ToList();

    public static Recenzija NapraviNovuRecenziju(string pacijentId, string doktorId, int ljubaznost, int profesionalnost, int usluga) => new()
    {
        PacijentID = pacijentId,
        DoktorID = doktorId,
        OcjenaLjubaznosti = ljubaznost,
        OcjenaProfesionalnosti = profesionalnost,
        OcjenaUsluge = usluga
    };

    // ── izvori podataka (CSV) ──

    public static IEnumerable<object[]> ProsjecneOcjene() =>
        CsvLoader.Ucitaj("RecenzijaService_prosjecneOcjene.csv")
                 .Select(r => new object[]
                 {
                     r[0],
                     RecenzijeIzOcjena(r[0], r[1], r[2], r[3]),
                     (Broj(r[4]), Broj(r[5]), Broj(r[6]), Broj(r[7]))
                 });

    public static IEnumerable<object[]> NevalidneOcjene() =>
        CsvLoader.Ucitaj("RecenzijaService_nevalidneOcjene.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), int.Parse(r[2]), r[3] });

    public static IEnumerable<object[]> BezTermina() =>
        CsvLoader.Ucitaj("RecenzijaService_bezTermina.csv")
                 .Select(r => new object[] { r[0], r[1], int.Parse(r[2]), r[3] });

    public static IEnumerable<object[]> VecOcijenjen() =>
        CsvLoader.Ucitaj("RecenzijaService_vecOcijenjen.csv")
                 .Select(r => new object[] { r[0], r[1], int.Parse(r[2]), r[3] });

    public static IEnumerable<object[]> NovaRecenzija() =>
        CsvLoader.Ucitaj("RecenzijaService_novaRecenzija.csv")
                 .Select(r => new object[] { r[0], r[1], int.Parse(r[2]), int.Parse(r[3]), int.Parse(r[4]) });

    public static IEnumerable<object[]> RecenzijeDoktora() =>
        CsvLoader.Ucitaj("RecenzijaService_recenzijeDoktora.csv")
                 .Select(r => new object[] { r[0], Brojevi(r[1]) });

    public static IEnumerable<object[]> BrojKorisnika() =>
        CsvLoader.Ucitaj("RecenzijaService_brojevi.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), int.Parse(r[2]) });

    public static IEnumerable<object[]> PostojecaRecenzija() =>
        CsvLoader.Ucitaj("RecenzijaService_postojecaRecenzija.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("RecenzijaService_snimanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]) });

    // ── izvori podataka (XML) ──

    public static IEnumerable<object[]> TopDoktori() =>
        XmlLoader.Ucitaj("RecenzijaService_topDoktori.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (int)x.Element("top")!,
                     ((string)x.Element("ocekivaniRedoslijed")!).Split(',', StringSplitOptions.RemoveEmptyEntries)
                 });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class RecenzijaServiceTests
{
    // XML "baza" (samo za čitanje): recenzije, korisnici i termini.
    private static List<XElement> _bazaRecenzija = null!;
    private static List<XElement> _bazaKorisnika = null!;
    private static List<XElement> _bazaTermina = null!;

    private Mock<IRecenzijaRepository> _recenzijaRepo = null!;
    private Mock<IKorisnikRepository> _korisnikRepo = null!;
    private Mock<ITerminRepository> _terminRepo = null!;
    private RecenzijaService _servis = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _bazaRecenzija = XmlLoader.Ucitaj("RecenzijaService_recenzije.xml", "recenzija").ToList();
        _bazaKorisnika = XmlLoader.Ucitaj("RecenzijaService_korisnici.xml", "korisnik").ToList();
        _bazaTermina = XmlLoader.Ucitaj("RecenzijaService_termini.xml", "termin").ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        var recenzije = _bazaRecenzija.Select(RecenzijaServicePodaci.NapraviRecenziju).ToList();

        _recenzijaRepo = new Mock<IRecenzijaRepository>();
        _recenzijaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(recenzije);
        _recenzijaRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
                      .ReturnsAsync((int id) => recenzije.FirstOrDefault(r => r.RecenzijaId == id));

        _korisnikRepo = new Mock<IKorisnikRepository>();
        _korisnikRepo.Setup(r => r.GetAllAsync())
                     .ReturnsAsync(_bazaKorisnika.Select(RecenzijaServicePodaci.NapraviKorisnika).ToList());

        _terminRepo = new Mock<ITerminRepository>();
        _terminRepo.Setup(r => r.GetAllAsync())
                   .ReturnsAsync(_bazaTermina.Select(RecenzijaServicePodaci.NapraviTermin).ToList());

        _servis = new RecenzijaService(_recenzijaRepo.Object, _korisnikRepo.Object, _terminRepo.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _recenzijaRepo.Reset();
        _korisnikRepo.Reset();
        _terminRepo.Reset();
    }

    // ─── AddAsync: neispravne ocjene ──────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.NevalidneOcjene), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task AddAsync_OcjenaVanOpsega_BacaException(int ljubaznost, int profesionalnost, int usluga, string ocekivanaPoruka)
    {
        // Arrange
        var recenzija = new Recenzija
        {
            OcjenaLjubaznosti = ljubaznost,
            OcjenaProfesionalnosti = profesionalnost,
            OcjenaUsluge = usluga
        };

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(recenzija));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.NevalidneOcjene), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task AddAsync_OcjenaVanOpsega_NeCitaTermine(int ljubaznost, int profesionalnost, int usluga, string ocekivanaPoruka)
    {
        // Arrange
        var recenzija = new Recenzija
        {
            OcjenaLjubaznosti = ljubaznost,
            OcjenaProfesionalnosti = profesionalnost,
            OcjenaUsluge = usluga
        };

        // Act
        await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(recenzija));

        // Assert
        _terminRepo.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.NevalidneOcjene), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task AddAsync_OcjenaVanOpsega_NeDodajeRecenziju(int ljubaznost, int profesionalnost, int usluga, string ocekivanaPoruka)
    {
        // Arrange
        var recenzija = new Recenzija
        {
            OcjenaLjubaznosti = ljubaznost,
            OcjenaProfesionalnosti = profesionalnost,
            OcjenaUsluge = usluga
        };

        // Act
        await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(recenzija));

        // Assert
        _recenzijaRepo.Verify(r => r.AddAsync(It.IsAny<Recenzija>()), Times.Never);
    }

    // ─── AddAsync: termin i postojeća recenzija ───────────────────────────

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.BezTermina), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task AddAsync_PacijentNemaAktivanTermin_BacaException(string pacijentId, string doktorId, int validnaOcjena, string ocekivanaPoruka)
    {
        // Arrange
        var recenzija = RecenzijaServicePodaci.NapraviNovuRecenziju(pacijentId, doktorId, validnaOcjena, validnaOcjena, validnaOcjena);

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(recenzija));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.VecOcijenjen), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task AddAsync_PacijentVecOcijenioDoktora_BacaException(string pacijentId, string doktorId, int validnaOcjena, string ocekivanaPoruka)
    {
        // Arrange
        var recenzija = RecenzijaServicePodaci.NapraviNovuRecenziju(pacijentId, doktorId, validnaOcjena, validnaOcjena, validnaOcjena);

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(recenzija));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    // ─── AddAsync: ispravna recenzija ─────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.NovaRecenzija), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task AddAsync_ValidnaRecenzija_ProvjeravaTerminePacijenta(string pacijentId, string doktorId, int ljubaznost, int profesionalnost, int usluga)
    {
        // Arrange
        var recenzija = RecenzijaServicePodaci.NapraviNovuRecenziju(pacijentId, doktorId, ljubaznost, profesionalnost, usluga);

        // Act
        await _servis.AddAsync(recenzija);

        // Assert
        _terminRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.NovaRecenzija), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public async Task AddAsync_ValidnaRecenzija_DodajeRecenziju(string pacijentId, string doktorId, int ljubaznost, int profesionalnost, int usluga)
    {
        // Arrange
        var recenzija = RecenzijaServicePodaci.NapraviNovuRecenziju(pacijentId, doktorId, ljubaznost, profesionalnost, usluga);

        // Act
        await _servis.AddAsync(recenzija);

        // Assert
        _recenzijaRepo.Verify(r => r.AddAsync(recenzija), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.NovaRecenzija), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task AddAsync_ValidnaRecenzija_SnimaPromjene(string pacijentId, string doktorId, int ljubaznost, int profesionalnost, int usluga)
    {
        // Arrange
        var recenzija = RecenzijaServicePodaci.NapraviNovuRecenziju(pacijentId, doktorId, ljubaznost, profesionalnost, usluga);

        // Act
        await _servis.AddAsync(recenzija);

        // Assert
        _recenzijaRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ─── Recenzije i prosjeci ─────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.RecenzijeDoktora), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetRecenzijeDoktoraAsync_ZadanDoktor_VracaSamoNjegoveRecenzije(string doktorId, int[] ocekivaniId)
    {
        // Act
        var rezultat = await _servis.GetRecenzijeDoktoraAsync(doktorId);

        // Assert
        CollectionAssert.AreEquivalent(ocekivaniId, rezultat.Select(r => r.RecenzijaId).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.ProsjecneOcjene), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task GetProsjecneOcjeneAsync_OcjeneDoktora_VracaZaokruzeneProsjeke(
        string doktorId, List<Recenzija> recenzije,
        (double Ljubaznost, double Profesionalnost, double Usluga, double Ukupno) ocekivano)
    {
        // Arrange
        _recenzijaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(recenzije);

        // Act
        var rezultat = await _servis.GetProsjecneOcjeneAsync(doktorId);

        // Assert
        Assert.AreEqual(ocekivano, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.ProsjecneOcjene), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetProsjecneOcjeneAsync_OcjeneDoktora_CitaRecenzijeIzRepozitorija(
        string doktorId, List<Recenzija> recenzije,
        (double Ljubaznost, double Profesionalnost, double Usluga, double Ukupno) ocekivano)
    {
        // Arrange
        _recenzijaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(recenzije);

        // Act
        await _servis.GetProsjecneOcjeneAsync(doktorId);

        // Assert
        _recenzijaRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.TopDoktori), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetTopDoktoriAsync_ZadanBroj_VracaDoktoreOdNajboljeOcjene(int top, string[] ocekivaniRedoslijed)
    {
        // Act
        var rezultat = await _servis.GetTopDoktoriAsync(top);

        // Assert
        CollectionAssert.AreEqual(ocekivaniRedoslijed, rezultat.Select(d => d.Id).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.TopDoktori), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetTopDoktoriAsync_ZadanBroj_CitaKorisnikeIzRepozitorija(int top, string[] ocekivaniRedoslijed)
    {
        // Act
        await _servis.GetTopDoktoriAsync(top);

        // Assert
        _korisnikRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    // ─── Doktori i pacijenti (IKorisnikRepository) ────────────────────────

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.BrojKorisnika), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetDoktoriAsync_RazliciteUloge_VracaSamoKorisnikeSaSpecijalizacijom(int ocekivaniBrojDoktora, int ocekivaniBrojPacijenata, int ocekivaniBrojRecenzija)
    {
        // Act
        var rezultat = await _servis.GetDoktoriAsync();

        // Assert
        Assert.AreEqual(ocekivaniBrojDoktora, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.BrojKorisnika), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetDoktoriAsync_Poziv_CitaKorisnikeIzRepozitorija(int ocekivaniBrojDoktora, int ocekivaniBrojPacijenata, int ocekivaniBrojRecenzija)
    {
        // Act
        await _servis.GetDoktoriAsync();

        // Assert
        _korisnikRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.BrojKorisnika), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetPacijentiAsync_RazliciteUloge_VracaSamoKorisnikeSaPrioritetom(int ocekivaniBrojDoktora, int ocekivaniBrojPacijenata, int ocekivaniBrojRecenzija)
    {
        // Act
        var rezultat = await _servis.GetPacijentiAsync();

        // Assert
        Assert.AreEqual(ocekivaniBrojPacijenata, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.BrojKorisnika), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetPacijentiAsync_Poziv_CitaKorisnikeIzRepozitorija(int ocekivaniBrojDoktora, int ocekivaniBrojPacijenata, int ocekivaniBrojRecenzija)
    {
        // Act
        await _servis.GetPacijentiAsync();

        // Assert
        _korisnikRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    // ─── Metode koje samo prosljeđuju repozitoriju ────────────────────────

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.PostojecaRecenzija), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetByIdAsync_RecenzijaPostoji_VracaRecenzijuIzRepozitorija(int id, string ocekivaniDoktorId)
    {
        // Act
        var rezultat = await _servis.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivaniDoktorId, rezultat.DoktorID);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.PostojecaRecenzija), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetByIdAsync_RecenzijaPostoji_PozivaRepozitorijSIstimId(int id, string ocekivaniDoktorId)
    {
        // Act
        await _servis.GetByIdAsync(id);

        // Assert
        _recenzijaRepo.Verify(r => r.GetByIdAsync(id), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.BrojKorisnika), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetAllAsync_PostojeRecenzije_VracaSveRecenzije(int ocekivaniBrojDoktora, int ocekivaniBrojPacijenata, int ocekivaniBrojRecenzija)
    {
        // Act
        var rezultat = await _servis.GetAllAsync();

        // Assert
        Assert.AreEqual(ocekivaniBrojRecenzija, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.BrojKorisnika), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetAllAsync_PostojeRecenzije_PozivaRepozitorij(int ocekivaniBrojDoktora, int ocekivaniBrojPacijenata, int ocekivaniBrojRecenzija)
    {
        // Act
        await _servis.GetAllAsync();

        // Assert
        _recenzijaRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.PostojecaRecenzija), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void Update_PostojecaRecenzija_PozivaUpdateURepozitoriju(int id, string doktorId)
    {
        // Arrange
        var recenzija = new Recenzija { RecenzijaId = id, DoktorID = doktorId };

        // Act
        _servis.Update(recenzija);

        // Assert
        _recenzijaRepo.Verify(r => r.Update(recenzija), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.PostojecaRecenzija), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void Delete_PostojecaRecenzija_PozivaDeleteURepozitoriju(int id, string doktorId)
    {
        // Arrange
        var recenzija = new Recenzija { RecenzijaId = id, DoktorID = doktorId };

        // Act
        _servis.Delete(recenzija);

        // Assert
        _recenzijaRepo.Verify(r => r.Delete(recenzija), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.Snimanje), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task SaveChangesAsync_RepozitorijSnimiPromjene_VracaBrojPromjena(int brojPromjena)
    {
        // Arrange
        _recenzijaRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(brojPromjena);

        // Act
        var rezultat = await _servis.SaveChangesAsync();

        // Assert
        Assert.AreEqual(brojPromjena, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaServicePodaci.Snimanje), typeof(RecenzijaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task SaveChangesAsync_Poziv_PozivaRepozitorij(int brojPromjena)
    {
        // Arrange
        _recenzijaRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(brojPromjena);

        // Act
        await _servis.SaveChangesAsync();

        // Assert
        _recenzijaRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
