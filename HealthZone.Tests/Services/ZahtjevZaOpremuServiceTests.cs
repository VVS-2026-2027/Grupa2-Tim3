using System.Xml.Linq;
using HealthZone.Models;
using HealthZone.Repositories;
using HealthZone.Services;
using Moq;

namespace HealthZone.Tests;

[TestClass]
public class ZahtjevZaOpremuServiceTests
{
    private Mock<IZahtjevZaOpremuRepository> _zahtjevRepo = null!;
    private Mock<IKorisnikRepository> _korisnikRepo = null!;
    private ZahtjevZaOpremuService _servis = null!;
    private List<ZahtjevZaOpremu> _baza = null!;

    [TestInitialize]
    public void Setup()
    {
        // "Baza" zahtjeva se čita iz XML-a za svaki test ispočetka, pa testovi ne utiču jedan na drugi
        // (npr. test koji odobri zahtjev ne mijenja status zahtjeva u drugom testu).
        _baza = XmlLoader.Ucitaj("ZahtjevZaOpremuService_zahtjevi.xml", "zahtjev").Select(NapraviZahtjev).ToList();

        _zahtjevRepo = new Mock<IZahtjevZaOpremuRepository>();
        _zahtjevRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(_baza);
        _zahtjevRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
                    .ReturnsAsync((int id) => _baza.FirstOrDefault(z => z.ZahtjevId == id));

        _korisnikRepo = new Mock<IKorisnikRepository>();

        _servis = new ZahtjevZaOpremuService(_zahtjevRepo.Object, _korisnikRepo.Object);
    }

    // ───────────── Pomoćne metode za pretvaranje XML-a u objekte ─────────────

    private static ZahtjevZaOpremu NapraviZahtjev(XElement x) => new ZahtjevZaOpremu
    {
        ZahtjevId = (int)x.Element("id")!,
        Naziv = (string)x.Element("naziv")!,
        Opis = string.Empty,
        DoktorID = (string)x.Element("doktorId")!,
        Status = Enum.Parse<Status>((string)x.Element("status")!),
        Kategorija = Enum.Parse<KategorijaOpreme>((string)x.Element("kategorija")!),
        Hitnost = Enum.Parse<Hitnost>((string)x.Element("hitnost")!)
    };

    private static Korisnik NapraviKorisnika(XElement x) => new Korisnik
    {
        Id = (string)x.Element("id")!,
        Specijalizacija = string.IsNullOrEmpty((string)x.Element("specijalizacija")!)
            ? null
            : Enum.Parse<Specijalizacija>((string)x.Element("specijalizacija")!)
    };

    // ───────────── Izvori podataka (CSV i XML) ─────────────

    public static IEnumerable<object[]> NevalidanZahtjev() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuService_nevalidanZahtjev.csv")
                 .Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> NoviZahtjev() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuService_noviZahtjev.csv")
                 .Select(r => new object[] { r[0], r[1], Enum.Parse<Status>(r[2]) });

    public static IEnumerable<object[]> NepostojeciZahtjev() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuService_nepostojeciZahtjev.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> ObradjenZahtjev() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuService_obradjenZahtjev.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> Odobravanje() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuService_odobravanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]), Enum.Parse<Status>(r[1]) });

    public static IEnumerable<object[]> Odbijanje() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuService_odbijanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]), Enum.Parse<Status>(r[1]) });

    public static IEnumerable<object[]> ZahtjeviDoktora() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuService_zahtjeviDoktora.csv")
                 .Select(r => new object[] { r[0], int.Parse(r[1]) });

    public static IEnumerable<object[]> PostojeciZahtjev() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuService_postojeciZahtjev.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuService_snimanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> SviZahtjevi() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuService_sviZahtjevi.csv")
                 .Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> PoStatusu() =>
        XmlLoader.Ucitaj("ZahtjevZaOpremuService_poStatusu.xml", "filter")
                 .Select(x => new object[] { Enum.Parse<Status>((string)x.Element("status")!), (int)x.Element("ocekivaniBroj")! });

    public static IEnumerable<object[]> PoKategoriji() =>
        XmlLoader.Ucitaj("ZahtjevZaOpremuService_poKategoriji.xml", "filter")
                 .Select(x => new object[] { Enum.Parse<KategorijaOpreme>((string)x.Element("kategorija")!), (int)x.Element("ocekivaniBroj")! });

    public static IEnumerable<object[]> PoHitnosti() =>
        XmlLoader.Ucitaj("ZahtjevZaOpremuService_poHitnosti.xml", "scenarij")
                 .Select(x => new object[] { ((string)x.Element("ocekivaniRedoslijedId")!).Split(',').Select(int.Parse).ToArray() });

    public static IEnumerable<object[]> Korisnici() =>
        XmlLoader.Ucitaj("ZahtjevZaOpremuService_korisnici.xml", "scenarij")
                 .Select(x => new object[] { x.Elements("korisnik").Select(NapraviKorisnika).ToList(), (int)x.Element("ocekivaniBrojDoktora")! });

    // ───────────── AddAsync ─────────────

    [TestMethod]
    [DynamicData(nameof(NevalidanZahtjev), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task AddAsync_NedostajeNazivIliOpis_BacaException(string naziv, string opis, string ocekivanaPoruka)
    {
        // Arrange
        var zahtjev = new ZahtjevZaOpremu { Naziv = naziv, Opis = opis };

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(zahtjev));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(NoviZahtjev), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task AddAsync_ValidanZahtjev_PostavljaStatusNaCekanju(string naziv, string opis, Status ocekivaniStatus)
    {
        // Arrange
        var zahtjev = new ZahtjevZaOpremu { Naziv = naziv, Opis = opis, Status = Status.Aktivan };

        // Act
        await _servis.AddAsync(zahtjev);

        // Assert
        Assert.AreEqual(ocekivaniStatus, zahtjev.Status);
    }

    [TestMethod]
    [DynamicData(nameof(NoviZahtjev), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public async Task AddAsync_ValidanZahtjev_DodajeISnimaZahtjev(string naziv, string opis, Status ocekivaniStatus)
    {
        // Arrange
        var zahtjev = new ZahtjevZaOpremu { Naziv = naziv, Opis = opis };

        // Act
        await _servis.AddAsync(zahtjev);

        // Assert
        _zahtjevRepo.Verify(r => r.AddAsync(It.Is<ZahtjevZaOpremu>(z => z == zahtjev && z.Status == ocekivaniStatus)), Times.Once);
        _zahtjevRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ───────────── OdobriZahtjevAsync ─────────────

    [TestMethod]
    [DynamicData(nameof(NepostojeciZahtjev), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task OdobriZahtjevAsync_ZahtjevNePostoji_BacaException(int id, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.OdobriZahtjevAsync(id));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(ObradjenZahtjev), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task OdobriZahtjevAsync_ZahtjevVecObradjen_BacaException(int id, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.OdobriZahtjevAsync(id));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(Odobravanje), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task OdobriZahtjevAsync_ZahtjevNaCekanju_PostavljaPotvrdjenISnima(int id, Status ocekivaniStatus)
    {
        // Act
        await _servis.OdobriZahtjevAsync(id);

        // Assert
        _zahtjevRepo.Verify(r => r.Update(It.Is<ZahtjevZaOpremu>(z => z.ZahtjevId == id && z.Status == ocekivaniStatus)), Times.Once);
        _zahtjevRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ───────────── OdbijZahtjevAsync ─────────────

    [TestMethod]
    [DynamicData(nameof(NepostojeciZahtjev), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task OdbijZahtjevAsync_ZahtjevNePostoji_BacaException(int id, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.OdbijZahtjevAsync(id));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(ObradjenZahtjev), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task OdbijZahtjevAsync_ZahtjevVecObradjen_BacaExceptionINeSnima(int id, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.OdbijZahtjevAsync(id));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
        _zahtjevRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(Odbijanje), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task OdbijZahtjevAsync_ZahtjevNaCekanju_PostavljaOtkazanISnima(int id, Status ocekivaniStatus)
    {
        // Act
        await _servis.OdbijZahtjevAsync(id);

        // Assert
        _zahtjevRepo.Verify(r => r.Update(It.Is<ZahtjevZaOpremu>(z => z.ZahtjevId == id && z.Status == ocekivaniStatus)), Times.Once);
        _zahtjevRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ───────────── Filtriranje ─────────────

    [TestMethod]
    [DynamicData(nameof(PoStatusu), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetZahtjeviPoStatusuAsync_ZadanStatus_VracaSamoZahtjeveTogStatusa(Status status, int ocekivaniBroj)
    {
        // Act
        var rezultat = (await _servis.GetZahtjeviPoStatusuAsync(status)).ToList();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count);
        Assert.IsTrue(rezultat.All(z => z.Status == status));
    }

    [TestMethod]
    [DynamicData(nameof(PoKategoriji), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetZahtjeviPoKategorijiAsync_ZadanaKategorija_VracaSamoZahtjeveTeKategorije(KategorijaOpreme kategorija, int ocekivaniBroj)
    {
        // Act
        var rezultat = (await _servis.GetZahtjeviPoKategorijiAsync(kategorija)).ToList();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count);
        Assert.IsTrue(rezultat.All(z => z.Kategorija == kategorija));
    }

    [TestMethod]
    [DynamicData(nameof(PoHitnosti), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetZahtjeviPoHitnostiAsync_ZahtjeviNaCekanju_VracaIhOdNajhitnijeg(int[] ocekivaniRedoslijedId)
    {
        // Act
        var rezultat = await _servis.GetZahtjeviPoHitnostiAsync();

        // Assert
        CollectionAssert.AreEqual(ocekivaniRedoslijedId, rezultat.Select(z => z.ZahtjevId).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjeviDoktora), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetZahtjeviDoktoraAsync_ZadanDoktor_VracaSamoNjegoveZahtjeve(string doktorId, int ocekivaniBroj)
    {
        // Act
        var rezultat = (await _servis.GetZahtjeviDoktoraAsync(doktorId)).ToList();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count);
        Assert.IsTrue(rezultat.All(z => z.DoktorID == doktorId));
    }

    // ───────────── GetDoktoriAsync (koristi IKorisnikRepository) ─────────────

    [TestMethod]
    [DynamicData(nameof(Korisnici), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetDoktoriAsync_RazliciteUloge_VracaSamoKorisnikeSaSpecijalizacijom(List<Korisnik> korisnici, int ocekivaniBrojDoktora)
    {
        // Arrange
        _korisnikRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(korisnici);

        // Act
        var rezultat = (await _servis.GetDoktoriAsync()).ToList();

        // Assert
        Assert.AreEqual(ocekivaniBrojDoktora, rezultat.Count);
        _korisnikRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    // ───────────── Metode koje samo prosljeđuju repozitoriju ─────────────

    [TestMethod]
    [DynamicData(nameof(PostojeciZahtjev), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetByIdAsync_ZahtjevPostoji_VracaZahtjevIzRepozitorija(int id, string ocekivaniNaziv)
    {
        // Act
        var rezultat = await _servis.GetByIdAsync(id);

        // Assert
        Assert.AreEqual(ocekivaniNaziv, rezultat!.Naziv);
        _zahtjevRepo.Verify(r => r.GetByIdAsync(id), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(SviZahtjevi), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetAllAsync_PostojeZahtjevi_VracaSveZahtjeve(int ocekivaniBroj)
    {
        // Act
        var rezultat = await _servis.GetAllAsync();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count());
        _zahtjevRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(PostojeciZahtjev), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Update_PostojeciZahtjev_PozivaUpdateURepozitoriju(int id, string ocekivaniNaziv)
    {
        // Arrange
        var zahtjev = await _servis.GetByIdAsync(id);

        // Act
        _servis.Update(zahtjev!);

        // Assert
        _zahtjevRepo.Verify(r => r.Update(It.Is<ZahtjevZaOpremu>(z => z.Naziv == ocekivaniNaziv)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(PostojeciZahtjev), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Delete_PostojeciZahtjev_PozivaDeleteURepozitoriju(int id, string ocekivaniNaziv)
    {
        // Arrange
        var zahtjev = await _servis.GetByIdAsync(id);

        // Act
        _servis.Delete(zahtjev!);

        // Assert
        _zahtjevRepo.Verify(r => r.Delete(It.Is<ZahtjevZaOpremu>(z => z.Naziv == ocekivaniNaziv)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(Snimanje), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task SaveChangesAsync_RepozitorijSnimiPromjene_VracaBrojPromjena(int brojPromjena)
    {
        // Arrange
        _zahtjevRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(brojPromjena);

        // Act
        var rezultat = await _servis.SaveChangesAsync();

        // Assert
        Assert.AreEqual(brojPromjena, rezultat);
        _zahtjevRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
