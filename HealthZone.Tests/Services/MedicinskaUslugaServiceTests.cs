using System.Globalization;
using System.Xml.Linq;
using HealthZone.Models;
using HealthZone.Repositories;
using HealthZone.Services;
using Moq;

namespace HealthZone.Tests;

[TestClass]
public class MedicinskaUslugaServiceTests
{
    private Mock<IMedicinskaUslugaRepository> _repo = null!;
    private MedicinskaUslugaService _servis = null!;
    private List<MedicinskaUsluga> _baza = null!;

    [TestInitialize]
    public void Setup()
    {
        // "Baza" usluga se čita iz XML-a za svaki test ispočetka, pa testovi ne utiču jedan na drugi.
        _baza = XmlLoader.Ucitaj("MedicinskaUslugaService_usluge.xml", "usluga").Select(NapraviUslugu).ToList();

        _repo = new Mock<IMedicinskaUslugaRepository>();
        _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(_baza);
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync((int id) => _baza.FirstOrDefault(u => u.UslugaId == id));

        _servis = new MedicinskaUslugaService(_repo.Object);
    }

    // ───────────── Pomoćne metode za pretvaranje podataka u objekte ─────────────

    private static MedicinskaUsluga NapraviUslugu(XElement x) => new MedicinskaUsluga
    {
        UslugaId = (int)x.Element("id")!,
        Naziv = (string)x.Element("naziv")!,
        Vrsta = Enum.Parse<Specijalizacija>((string)x.Element("vrsta")!),
        Opis = string.Empty,
        Cijena = (decimal)x.Element("cijena")!
    };

    private static decimal Decimalni(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);

    // ───────────── Izvori podataka (CSV i XML) ─────────────

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
                 .Select(r => new object[] { Enum.Parse<Specijalizacija>(r[0]), int.Parse(r[1]) });

    public static IEnumerable<object[]> Pretrage() =>
        XmlLoader.Ucitaj("MedicinskaUslugaService_pretraga.xml", "pretraga")
                 .Select(x => new object[] { (string)x.Element("naziv")!, (int)x.Element("brojRezultata")! });

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

    // ───────────── AddAsync ─────────────

    [TestMethod]
    [DynamicData(nameof(NevalidnaUsluga), DynamicDataSourceType.Method)]
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
    [DynamicData(nameof(NevalidnaUsluga), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task AddAsync_NevalidnaUsluga_NeSnimaURepozitorij(string naziv, decimal cijena, string ocekivanaPoruka)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { Naziv = naziv, Cijena = cijena };

        // Act
        await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(usluga));

        // Assert
        _repo.Verify(r => r.AddAsync(It.IsAny<MedicinskaUsluga>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(Duplikat), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
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
    [DynamicData(nameof(NovaUsluga), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public async Task AddAsync_ValidnaNovaUsluga_DodajeISnimaUslugu(string naziv, decimal cijena)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { Naziv = naziv, Cijena = cijena };

        // Act
        await _servis.AddAsync(usluga);

        // Assert
        _repo.Verify(r => r.AddAsync(usluga), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ───────────── GetUslugePoVrstiAsync ─────────────

    [TestMethod]
    [DynamicData(nameof(PoVrsti), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetUslugePoVrstiAsync_PostojeUslugeTeVrste_VracaSamoUslugeTeVrste(Specijalizacija vrsta, int ocekivaniBroj)
    {
        // Act
        var rezultat = (await _servis.GetUslugePoVrstiAsync(vrsta)).ToList();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count);
        Assert.IsTrue(rezultat.All(u => u.Vrsta == vrsta));
    }

    // ───────────── PretraziAsync ─────────────

    [TestMethod]
    [DynamicData(nameof(Pretrage), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task PretraziAsync_DioNaziva_VracaUslugeKojeSadrzeNazivBezObziraNaVelicinuSlova(string naziv, int brojRezultata)
    {
        // Act
        var rezultat = await _servis.PretraziAsync(naziv);

        // Assert
        Assert.AreEqual(brojRezultata, rezultat.Count());
        _repo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    // ───────────── Metode koje samo prosljeđuju repozitoriju ─────────────

    [TestMethod]
    [DynamicData(nameof(PostojecaUsluga), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetByIdAsync_UslugaPostoji_VracaUsluguIzRepozitorija(int id, string ocekivaniNaziv)
    {
        // Act
        var rezultat = await _servis.GetByIdAsync(id);

        // Assert
        Assert.AreEqual(ocekivaniNaziv, rezultat!.Naziv);
        _repo.Verify(r => r.GetByIdAsync(id), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NepostojecaUsluga), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetByIdAsync_UslugaNePostoji_VracaNull(int id)
    {
        // Act
        var rezultat = await _servis.GetByIdAsync(id);

        // Assert
        Assert.IsNull(rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(SveUsluge), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetAllAsync_PostojeUsluge_VracaSveUsluge(int ocekivaniBroj)
    {
        // Act
        var rezultat = await _servis.GetAllAsync();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count());
        _repo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(PostojecaUsluga), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Update_PostojecaUsluga_PozivaUpdateURepozitoriju(int id, string ocekivaniNaziv)
    {
        // Arrange
        var usluga = await _servis.GetByIdAsync(id);

        // Act
        _servis.Update(usluga!);

        // Assert
        _repo.Verify(r => r.Update(It.Is<MedicinskaUsluga>(u => u.Naziv == ocekivaniNaziv)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(PostojecaUsluga), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Delete_PostojecaUsluga_PozivaDeleteURepozitoriju(int id, string ocekivaniNaziv)
    {
        // Arrange
        var usluga = await _servis.GetByIdAsync(id);

        // Act
        _servis.Delete(usluga!);

        // Assert
        _repo.Verify(r => r.Delete(It.Is<MedicinskaUsluga>(u => u.Naziv == ocekivaniNaziv)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(Snimanje), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task SaveChangesAsync_RepozitorijSnimiPromjene_VracaBrojPromjena(int brojPromjena)
    {
        // Arrange
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(brojPromjena);

        // Act
        var rezultat = await _servis.SaveChangesAsync();

        // Assert
        Assert.AreEqual(brojPromjena, rezultat);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
