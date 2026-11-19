using System.Globalization;
using System.Xml.Linq;
using HealthZone.Models;
using HealthZone.Repositories;
using HealthZone.Services;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za MedicinskaUslugaServiceTests ───
public static class MedicinskaUslugaServicePodaci
{
    // ── pomoćne metode ──

    public static MedicinskaUsluga NapraviUslugu(XElement x) => new()
    {
        UslugaId = (int)x.Element("id")!,
        Naziv = (string)x.Element("naziv")!,
        Vrsta = Enum.Parse<Specijalizacija>((string)x.Element("vrsta")!),
        Opis = string.Empty,
        Cijena = (decimal)x.Element("cijena")!
    };

    public static decimal Decimalni(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);

    public static int[] Idjevi(string s) => s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();

    // ── izvori podataka (CSV) ──

    public static IEnumerable<object[]> NevalidnaUsluga() =>
        CsvLoader.Ucitaj("MedicinskaUslugaService_nevalidnaUsluga.csv")
                 .Select(r => new object[] { r[0], Decimalni(r[1]), r[2] });

    public static IEnumerable<object[]> Duplikat() =>
        CsvLoader.Ucitaj("MedicinskaUslugaService_duplikat.csv")
                 .Select(r => new object[] { r[0], Decimalni(r[1]), r[2] });

    public static IEnumerable<object[]> NovaUsluga() =>
        CsvLoader.Ucitaj("MedicinskaUslugaService_novaUsluga.csv")
                 .Select(r => new object[] { r[0], Decimalni(r[1]) });

    public static IEnumerable<object[]> PoVrsti() =>
        CsvLoader.Ucitaj("MedicinskaUslugaService_poVrsti.csv")
                 .Select(r => new object[] { Enum.Parse<Specijalizacija>(r[0]), Idjevi(r[1]) });

    public static IEnumerable<object[]> PostojecaUsluga() =>
        CsvLoader.Ucitaj("MedicinskaUslugaService_postojecaUsluga.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> NepostojecaUsluga() =>
        CsvLoader.Ucitaj("MedicinskaUslugaService_nepostojecaUsluga.csv")
                 .Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("MedicinskaUslugaService_snimanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> SveUsluge() =>
        CsvLoader.Ucitaj("MedicinskaUslugaService_sveUsluge.csv")
                 .Select(r => new object[] { int.Parse(r[0]) });

    // ── izvori podataka (XML) ──

    public static IEnumerable<object[]> Pretrage() =>
        XmlLoader.Ucitaj("MedicinskaUslugaService_pretraga.xml", "pretraga")
                 .Select(x => new object[] { (string)x.Element("naziv")!, (int)x.Element("brojRezultata")! });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class MedicinskaUslugaServiceTests
{
    private static List<XElement> _bazaUsluga = null!;   // samo za čitanje

    private Mock<IMedicinskaUslugaRepository> _repo = null!;
    private MedicinskaUslugaService _servis = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // XML "baza" se čita jednom; objekti se iz nje prave ispočetka za svaki test.
        _bazaUsluga = XmlLoader.Ucitaj("MedicinskaUslugaService_usluge.xml", "usluga").ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        var baza = _bazaUsluga.Select(MedicinskaUslugaServicePodaci.NapraviUslugu).ToList();

        _repo = new Mock<IMedicinskaUslugaRepository>();
        _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(baza);
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync((int id) => baza.FirstOrDefault(u => u.UslugaId == id));

        _servis = new MedicinskaUslugaService(_repo.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _repo.Reset();
    }

    // ─── AddAsync ─────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaServicePodaci.NevalidnaUsluga), typeof(MedicinskaUslugaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task AddAsync_NevalidanNazivIliCijena_BacaException(string naziv, decimal cijena, string ocekivanaPoruka)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { Naziv = naziv, Cijena = cijena };

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(usluga));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaServicePodaci.NevalidnaUsluga), typeof(MedicinskaUslugaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task AddAsync_NevalidnaUsluga_NeDodajeURepozitorij(string naziv, decimal cijena, string ocekivanaPoruka)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { Naziv = naziv, Cijena = cijena };

        // Act
        await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(usluga));

        // Assert
        _repo.Verify(r => r.AddAsync(It.IsAny<MedicinskaUsluga>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaServicePodaci.Duplikat), typeof(MedicinskaUslugaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task AddAsync_NazivVecPostoji_BacaException(string naziv, decimal cijena, string ocekivanaPoruka)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { Naziv = naziv, Cijena = cijena };

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(usluga));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaServicePodaci.NovaUsluga), typeof(MedicinskaUslugaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public async Task AddAsync_ValidnaNovaUsluga_DodajeUslugu(string naziv, decimal cijena)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { Naziv = naziv, Cijena = cijena };

        // Act
        await _servis.AddAsync(usluga);

        // Assert
        _repo.Verify(r => r.AddAsync(usluga), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaServicePodaci.NovaUsluga), typeof(MedicinskaUslugaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task AddAsync_ValidnaNovaUsluga_SnimaPromjene(string naziv, decimal cijena)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { Naziv = naziv, Cijena = cijena };

        // Act
        await _servis.AddAsync(usluga);

        // Assert
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ─── GetUslugePoVrstiAsync ────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaServicePodaci.PoVrsti), typeof(MedicinskaUslugaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetUslugePoVrstiAsync_PostojeUslugeTeVrste_VracaSamoUslugeTeVrste(Specijalizacija vrsta, int[] ocekivaniId)
    {
        // Act
        var rezultat = await _servis.GetUslugePoVrstiAsync(vrsta);

        // Assert
        CollectionAssert.AreEquivalent(ocekivaniId, rezultat.Select(u => u.UslugaId).ToArray());
    }

    // ─── PretraziAsync ────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaServicePodaci.Pretrage), typeof(MedicinskaUslugaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task PretraziAsync_DioNaziva_VracaUslugeKojeSadrzeNazivBezObziraNaVelicinuSlova(string naziv, int brojRezultata)
    {
        // Act
        var rezultat = await _servis.PretraziAsync(naziv);

        // Assert
        Assert.AreEqual(brojRezultata, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaServicePodaci.Pretrage), typeof(MedicinskaUslugaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task PretraziAsync_DioNaziva_CitaUslugeIzRepozitorija(string naziv, int brojRezultata)
    {
        // Act
        await _servis.PretraziAsync(naziv);

        // Assert
        _repo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    // ─── Metode koje samo prosljeđuju repozitoriju ────────────────────────

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaServicePodaci.PostojecaUsluga), typeof(MedicinskaUslugaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetByIdAsync_UslugaPostoji_VracaUsluguIzRepozitorija(int id, string ocekivaniNaziv)
    {
        // Act
        var rezultat = await _servis.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivaniNaziv, rezultat.Naziv);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaServicePodaci.PostojecaUsluga), typeof(MedicinskaUslugaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetByIdAsync_UslugaPostoji_PozivaRepozitorijSIstimId(int id, string ocekivaniNaziv)
    {
        // Act
        await _servis.GetByIdAsync(id);

        // Assert
        _repo.Verify(r => r.GetByIdAsync(id), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaServicePodaci.NepostojecaUsluga), typeof(MedicinskaUslugaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetByIdAsync_UslugaNePostoji_VracaNull(int id)
    {
        // Act
        var rezultat = await _servis.GetByIdAsync(id);

        // Assert
        Assert.IsNull(rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaServicePodaci.SveUsluge), typeof(MedicinskaUslugaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetAllAsync_PostojeUsluge_VracaSveUsluge(int ocekivaniBroj)
    {
        // Act
        var rezultat = await _servis.GetAllAsync();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaServicePodaci.SveUsluge), typeof(MedicinskaUslugaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetAllAsync_PostojeUsluge_PozivaRepozitorij(int ocekivaniBroj)
    {
        // Act
        await _servis.GetAllAsync();

        // Assert
        _repo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaServicePodaci.PostojecaUsluga), typeof(MedicinskaUslugaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void Update_PostojecaUsluga_PozivaUpdateURepozitoriju(int id, string naziv)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { UslugaId = id, Naziv = naziv };

        // Act
        _servis.Update(usluga);

        // Assert
        _repo.Verify(r => r.Update(usluga), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaServicePodaci.PostojecaUsluga), typeof(MedicinskaUslugaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void Delete_PostojecaUsluga_PozivaDeleteURepozitoriju(int id, string naziv)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { UslugaId = id, Naziv = naziv };

        // Act
        _servis.Delete(usluga);

        // Assert
        _repo.Verify(r => r.Delete(usluga), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaServicePodaci.Snimanje), typeof(MedicinskaUslugaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task SaveChangesAsync_RepozitorijSnimiPromjene_VracaBrojPromjena(int brojPromjena)
    {
        // Arrange
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(brojPromjena);

        // Act
        var rezultat = await _servis.SaveChangesAsync();

        // Assert
        Assert.AreEqual(brojPromjena, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaServicePodaci.Snimanje), typeof(MedicinskaUslugaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task SaveChangesAsync_Poziv_PozivaRepozitorij(int brojPromjena)
    {
        // Arrange
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(brojPromjena);

        // Act
        await _servis.SaveChangesAsync();

        // Assert
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
