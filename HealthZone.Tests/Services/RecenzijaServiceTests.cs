using System.Globalization;
using System.Xml.Linq;
using HealthZone.Models;
using HealthZone.Repositories;
using HealthZone.Services;
using Moq;

namespace HealthZone.Tests;

[TestClass]
public class RecenzijaServiceTests
{
    private Mock<IRecenzijaRepository> _recenzijaRepo = null!;
    private Mock<IKorisnikRepository> _korisnikRepo = null!;
    private Mock<ITerminRepository> _terminRepo = null!;
    private RecenzijaService _servis = null!;
    private List<Recenzija> _recenzije = null!;

    [TestInitialize]
    public void Setup()
    {
        // "Baza" (recenzije, korisnici, termini) se čita iz XML-a za svaki test ispočetka,
        // pa testovi ne utiču jedan na drugi.
        _recenzije = XmlLoader.Ucitaj("RecenzijaService_recenzije.xml", "recenzija").Select(NapraviRecenziju).ToList();
        var korisnici = XmlLoader.Ucitaj("RecenzijaService_korisnici.xml", "korisnik").Select(NapraviKorisnika).ToList();
        var termini = XmlLoader.Ucitaj("RecenzijaService_termini.xml", "termin").Select(NapraviTermin).ToList();

        _recenzijaRepo = new Mock<IRecenzijaRepository>();
        _recenzijaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(_recenzije);
        _recenzijaRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
                      .ReturnsAsync((int id) => _recenzije.FirstOrDefault(r => r.RecenzijaId == id));

        _korisnikRepo = new Mock<IKorisnikRepository>();
        _korisnikRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(korisnici);

        _terminRepo = new Mock<ITerminRepository>();
        _terminRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(termini);

        _servis = new RecenzijaService(_recenzijaRepo.Object, _korisnikRepo.Object, _terminRepo.Object);
    }

    // ───────────── Pomoćne metode za pretvaranje podataka u objekte ─────────────

    private static Recenzija NapraviRecenziju(XElement x) => new Recenzija
    {
        RecenzijaId = (int)x.Element("id")!,
        PacijentID = (string)x.Element("pacijentId")!,
        DoktorID = (string)x.Element("doktorId")!,
        OcjenaLjubaznosti = (int)x.Element("ljubaznost")!,
        OcjenaProfesionalnosti = (int)x.Element("profesionalnost")!,
        OcjenaUsluge = (int)x.Element("usluga")!
    };

    private static Korisnik NapraviKorisnika(XElement x) => new Korisnik
    {
        Id = (string)x.Element("id")!,
        Specijalizacija = string.IsNullOrEmpty((string)x.Element("specijalizacija")!)
            ? null
            : Enum.Parse<Specijalizacija>((string)x.Element("specijalizacija")!),
        Prioritet = string.IsNullOrEmpty((string)x.Element("prioritet")!)
            ? null
            : Enum.Parse<Prioritet>((string)x.Element("prioritet")!)
    };

    private static Termin NapraviTermin(XElement x) => new Termin
    {
        PacijentID = (string)x.Element("pacijentId")!,
        DoktorID = (string)x.Element("doktorId")!,
        Status = Enum.Parse<Status>((string)x.Element("status")!)
    };

    private static int[] Ocjene(string s) =>
        s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();

    // Od tri liste ocjena iz CSV-a (npr. "5,4,5") pravi recenzije za jednog doktora.
    private static List<Recenzija> RecenzijeIzOcjena(string doktorId, string ljubaznost, string profesionalnost, string usluga) =>
        Ocjene(ljubaznost)
            .Select((l, i) => new Recenzija
            {
                DoktorID = doktorId,
                OcjenaLjubaznosti = l,
                OcjenaProfesionalnosti = Ocjene(profesionalnost)[i],
                OcjenaUsluge = Ocjene(usluga)[i]
            })
            .ToList();

    private static double Broj(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    // ───────────── Izvori podataka (CSV i XML) ─────────────

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
                 .Select(r => new object[] { r[0], int.Parse(r[1]) });

    public static IEnumerable<object[]> Brojevi() =>
        CsvLoader.Ucitaj("RecenzijaService_brojevi.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), int.Parse(r[2]) });

    public static IEnumerable<object[]> PostojecaRecenzija() =>
        CsvLoader.Ucitaj("RecenzijaService_postojecaRecenzija.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("RecenzijaService_snimanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> TopDoktori() =>
        XmlLoader.Ucitaj("RecenzijaService_topDoktori.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (int)x.Element("top")!,
                     ((string)x.Element("ocekivaniRedoslijed")!).Split(',', StringSplitOptions.RemoveEmptyEntries)
                 });

    // ───────────── AddAsync ─────────────

    [TestMethod]
    [DynamicData(nameof(NevalidneOcjene), DynamicDataSourceType.Method)]
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
    [DynamicData(nameof(NevalidneOcjene), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task AddAsync_OcjenaVanOpsega_NeProvjeravaTermineNiSnima(int ljubaznost, int profesionalnost, int usluga, string ocekivanaPoruka)
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
        _recenzijaRepo.Verify(r => r.AddAsync(It.IsAny<Recenzija>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(BezTermina), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task AddAsync_PacijentNemaAktivanTermin_BacaException(string pacijentId, string doktorId, int validnaOcjena, string ocekivanaPoruka)
    {
        // Arrange
        var recenzija = new Recenzija
        {
            PacijentID = pacijentId,
            DoktorID = doktorId,
            OcjenaLjubaznosti = validnaOcjena,
            OcjenaProfesionalnosti = validnaOcjena,
            OcjenaUsluge = validnaOcjena
        };

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(recenzija));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(VecOcijenjen), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task AddAsync_PacijentVecOcijenioDoktora_BacaException(string pacijentId, string doktorId, int validnaOcjena, string ocekivanaPoruka)
    {
        // Arrange
        var recenzija = new Recenzija
        {
            PacijentID = pacijentId,
            DoktorID = doktorId,
            OcjenaLjubaznosti = validnaOcjena,
            OcjenaProfesionalnosti = validnaOcjena,
            OcjenaUsluge = validnaOcjena
        };

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(recenzija));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(NovaRecenzija), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public async Task AddAsync_ValidnaRecenzija_ProvjeravaTermineIDodajeRecenziju(string pacijentId, string doktorId, int ljubaznost, int profesionalnost, int usluga)
    {
        // Arrange
        var recenzija = new Recenzija
        {
            PacijentID = pacijentId,
            DoktorID = doktorId,
            OcjenaLjubaznosti = ljubaznost,
            OcjenaProfesionalnosti = profesionalnost,
            OcjenaUsluge = usluga
        };

        // Act
        await _servis.AddAsync(recenzija);

        // Assert
        _terminRepo.Verify(r => r.GetAllAsync(), Times.Once);
        _recenzijaRepo.Verify(r => r.AddAsync(recenzija), Times.Once);
        _recenzijaRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ───────────── Recenzije i prosjeci ─────────────

    [TestMethod]
    [DynamicData(nameof(RecenzijeDoktora), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetRecenzijeDoktoraAsync_ZadanDoktor_VracaSamoNjegoveRecenzije(string doktorId, int ocekivaniBroj)
    {
        // Act
        var rezultat = (await _servis.GetRecenzijeDoktoraAsync(doktorId)).ToList();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count);
        Assert.IsTrue(rezultat.All(r => r.DoktorID == doktorId));
    }

    [TestMethod]
    [DynamicData(nameof(ProsjecneOcjene), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
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
        _recenzijaRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TopDoktori), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetTopDoktoriAsync_ZadanBroj_VracaDoktoreOdNajboljeOcjene(int top, string[] ocekivaniRedoslijed)
    {
        // Act
        var rezultat = await _servis.GetTopDoktoriAsync(top);

        // Assert
        CollectionAssert.AreEqual(ocekivaniRedoslijed, rezultat.Select(d => d.Id).ToArray());
        _korisnikRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    // ───────────── Doktori i pacijenti (IKorisnikRepository) ─────────────

    [TestMethod]
    [DynamicData(nameof(Brojevi), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetDoktoriAsync_RazliciteUloge_VracaSamoKorisnikeSaSpecijalizacijom(int ocekivaniBrojDoktora, int ocekivaniBrojPacijenata, int ocekivaniBrojRecenzija)
    {
        // Act
        var rezultat = await _servis.GetDoktoriAsync();

        // Assert
        Assert.AreEqual(ocekivaniBrojDoktora, rezultat.Count());
        _korisnikRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(Brojevi), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetPacijentiAsync_RazliciteUloge_VracaSamoKorisnikeSaPrioritetom(int ocekivaniBrojDoktora, int ocekivaniBrojPacijenata, int ocekivaniBrojRecenzija)
    {
        // Act
        var rezultat = await _servis.GetPacijentiAsync();

        // Assert
        Assert.AreEqual(ocekivaniBrojPacijenata, rezultat.Count());
        _korisnikRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    // ───────────── Metode koje samo prosljeđuju repozitoriju ─────────────

    [TestMethod]
    [DynamicData(nameof(PostojecaRecenzija), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetByIdAsync_RecenzijaPostoji_VracaRecenzijuIzRepozitorija(int id, string ocekivaniDoktorId)
    {
        // Act
        var rezultat = await _servis.GetByIdAsync(id);

        // Assert
        Assert.AreEqual(ocekivaniDoktorId, rezultat!.DoktorID);
        _recenzijaRepo.Verify(r => r.GetByIdAsync(id), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(Brojevi), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetAllAsync_PostojeRecenzije_VracaSveRecenzije(int ocekivaniBrojDoktora, int ocekivaniBrojPacijenata, int ocekivaniBrojRecenzija)
    {
        // Act
        var rezultat = await _servis.GetAllAsync();

        // Assert
        Assert.AreEqual(ocekivaniBrojRecenzija, rezultat.Count());
        _recenzijaRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(PostojecaRecenzija), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Update_PostojecaRecenzija_PozivaUpdateURepozitoriju(int id, string ocekivaniDoktorId)
    {
        // Arrange
        var recenzija = await _servis.GetByIdAsync(id);

        // Act
        _servis.Update(recenzija!);

        // Assert
        _recenzijaRepo.Verify(r => r.Update(It.Is<Recenzija>(x => x.RecenzijaId == id && x.DoktorID == ocekivaniDoktorId)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(PostojecaRecenzija), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Delete_PostojecaRecenzija_PozivaDeleteURepozitoriju(int id, string ocekivaniDoktorId)
    {
        // Arrange
        var recenzija = await _servis.GetByIdAsync(id);

        // Act
        _servis.Delete(recenzija!);

        // Assert
        _recenzijaRepo.Verify(r => r.Delete(It.Is<Recenzija>(x => x.RecenzijaId == id && x.DoktorID == ocekivaniDoktorId)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(Snimanje), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task SaveChangesAsync_RepozitorijSnimiPromjene_VracaBrojPromjena(int brojPromjena)
    {
        // Arrange
        _recenzijaRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(brojPromjena);

        // Act
        var rezultat = await _servis.SaveChangesAsync();

        // Assert
        Assert.AreEqual(brojPromjena, rezultat);
        _recenzijaRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
