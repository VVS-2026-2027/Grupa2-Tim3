using System.Xml.Linq;
using HealthZone.Models;
using HealthZone.Repositories;
using HealthZone.Services;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za ZahtjevZaOpremuServiceTests ────
public static class ZahtjevZaOpremuServicePodaci
{
    // ── pomoćne metode ──

    public static ZahtjevZaOpremu NapraviZahtjev(XElement x) => new()
    {
        ZahtjevId = (int)x.Element("id")!,
        Naziv = (string)x.Element("naziv")!,
        Opis = string.Empty,
        DoktorID = (string)x.Element("doktorId")!,
        Status = Enum.Parse<Status>((string)x.Element("status")!),
        Kategorija = Enum.Parse<KategorijaOpreme>((string)x.Element("kategorija")!),
        Hitnost = Enum.Parse<Hitnost>((string)x.Element("hitnost")!)
    };

    public static Korisnik NapraviKorisnika(XElement x) => new()
    {
        Id = (string)x.Element("id")!,
        Specijalizacija = string.IsNullOrEmpty((string)x.Element("specijalizacija")!)
            ? null
            : Enum.Parse<Specijalizacija>((string)x.Element("specijalizacija")!)
    };

    public static int[] Idjevi(string? s) =>
        (s ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();

    // ── izvori podataka (CSV) ──

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
                 .Select(r => new object[] { r[0], Idjevi(r[1]) });

    public static IEnumerable<object[]> PostojeciZahtjev() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuService_postojeciZahtjev.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuService_snimanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> SviZahtjevi() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuService_sviZahtjevi.csv")
                 .Select(r => new object[] { int.Parse(r[0]) });

    // ── izvori podataka (XML) ──

    public static IEnumerable<object[]> PoStatusu() =>
        XmlLoader.Ucitaj("ZahtjevZaOpremuService_poStatusu.xml", "filter")
                 .Select(x => new object[] { Enum.Parse<Status>((string)x.Element("status")!), Idjevi((string?)x.Element("ocekivaniId")) });

    public static IEnumerable<object[]> PoKategoriji() =>
        XmlLoader.Ucitaj("ZahtjevZaOpremuService_poKategoriji.xml", "filter")
                 .Select(x => new object[] { Enum.Parse<KategorijaOpreme>((string)x.Element("kategorija")!), Idjevi((string?)x.Element("ocekivaniId")) });

    public static IEnumerable<object[]> PoHitnosti() =>
        XmlLoader.Ucitaj("ZahtjevZaOpremuService_poHitnosti.xml", "scenarij")
                 .Select(x => new object[] { Idjevi((string?)x.Element("ocekivaniRedoslijedId")) });

    public static IEnumerable<object[]> Korisnici() =>
        XmlLoader.Ucitaj("ZahtjevZaOpremuService_korisnici.xml", "scenarij")
                 .Select(x => new object[] { x.Elements("korisnik").Select(NapraviKorisnika).ToList(), (int)x.Element("ocekivaniBrojDoktora")! });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class ZahtjevZaOpremuServiceTests
{
    private static List<XElement> _bazaZahtjeva = null!;   // samo za čitanje

    private Mock<IZahtjevZaOpremuRepository> _zahtjevRepo = null!;
    private Mock<IKorisnikRepository> _korisnikRepo = null!;
    private ZahtjevZaOpremuService _servis = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // XML "baza" se čita jednom; objekti se iz nje prave ispočetka za svaki test,
        // pa test koji odobri zahtjev ne mijenja status zahtjeva u drugom testu.
        _bazaZahtjeva = XmlLoader.Ucitaj("ZahtjevZaOpremuService_zahtjevi.xml", "zahtjev").ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        var baza = _bazaZahtjeva.Select(ZahtjevZaOpremuServicePodaci.NapraviZahtjev).ToList();

        _zahtjevRepo = new Mock<IZahtjevZaOpremuRepository>();
        _zahtjevRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(baza);
        _zahtjevRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
                    .ReturnsAsync((int id) => baza.FirstOrDefault(z => z.ZahtjevId == id));

        _korisnikRepo = new Mock<IKorisnikRepository>();

        _servis = new ZahtjevZaOpremuService(_zahtjevRepo.Object, _korisnikRepo.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _zahtjevRepo.Reset();
        _korisnikRepo.Reset();
    }

    // ─── AddAsync ─────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.NevalidanZahtjev), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
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
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.NoviZahtjev), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
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
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.NoviZahtjev), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public async Task AddAsync_ValidanZahtjev_DodajeZahtjev(string naziv, string opis, Status ocekivaniStatus)
    {
        // Arrange
        var zahtjev = new ZahtjevZaOpremu { Naziv = naziv, Opis = opis };

        // Act
        await _servis.AddAsync(zahtjev);

        // Assert
        _zahtjevRepo.Verify(r => r.AddAsync(zahtjev), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.NoviZahtjev), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task AddAsync_ValidanZahtjev_SnimaPromjene(string naziv, string opis, Status ocekivaniStatus)
    {
        // Arrange
        var zahtjev = new ZahtjevZaOpremu { Naziv = naziv, Opis = opis };

        // Act
        await _servis.AddAsync(zahtjev);

        // Assert
        _zahtjevRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ─── OdobriZahtjevAsync ───────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.NepostojeciZahtjev), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
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
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.ObradjenZahtjev), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task OdobriZahtjevAsync_ZahtjevVecObradjen_BacaException(int id, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.OdobriZahtjevAsync(id));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.Odobravanje), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task OdobriZahtjevAsync_ZahtjevNaCekanju_AzuriraZahtjevSaStatusomPotvrdjen(int id, Status ocekivaniStatus)
    {
        // Act
        await _servis.OdobriZahtjevAsync(id);

        // Assert
        _zahtjevRepo.Verify(r => r.Update(It.Is<ZahtjevZaOpremu>(z => z.ZahtjevId == id && z.Status == ocekivaniStatus)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.Odobravanje), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task OdobriZahtjevAsync_ZahtjevNaCekanju_SnimaPromjene(int id, Status ocekivaniStatus)
    {
        // Act
        await _servis.OdobriZahtjevAsync(id);

        // Assert
        _zahtjevRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ─── OdbijZahtjevAsync ────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.NepostojeciZahtjev), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task OdbijZahtjevAsync_ZahtjevNePostoji_BacaException(int id, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.OdbijZahtjevAsync(id));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.ObradjenZahtjev), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task OdbijZahtjevAsync_ZahtjevVecObradjen_BacaException(int id, string ocekivanaPoruka)
    {
        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.OdbijZahtjevAsync(id));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.ObradjenZahtjev), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task OdbijZahtjevAsync_ZahtjevVecObradjen_NeSnimaPromjene(int id, string ocekivanaPoruka)
    {
        // Act
        await Assert.ThrowsExceptionAsync<Exception>(() => _servis.OdbijZahtjevAsync(id));

        // Assert
        _zahtjevRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.Odbijanje), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task OdbijZahtjevAsync_ZahtjevNaCekanju_AzuriraZahtjevSaStatusomOtkazan(int id, Status ocekivaniStatus)
    {
        // Act
        await _servis.OdbijZahtjevAsync(id);

        // Assert
        _zahtjevRepo.Verify(r => r.Update(It.Is<ZahtjevZaOpremu>(z => z.ZahtjevId == id && z.Status == ocekivaniStatus)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.Odbijanje), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task OdbijZahtjevAsync_ZahtjevNaCekanju_SnimaPromjene(int id, Status ocekivaniStatus)
    {
        // Act
        await _servis.OdbijZahtjevAsync(id);

        // Assert
        _zahtjevRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ─── Filtriranje ──────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.PoStatusu), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetZahtjeviPoStatusuAsync_ZadanStatus_VracaSamoZahtjeveTogStatusa(Status status, int[] ocekivaniId)
    {
        // Act
        var rezultat = await _servis.GetZahtjeviPoStatusuAsync(status);

        // Assert
        CollectionAssert.AreEquivalent(ocekivaniId, rezultat.Select(z => z.ZahtjevId).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.PoKategoriji), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetZahtjeviPoKategorijiAsync_ZadanaKategorija_VracaSamoZahtjeveTeKategorije(KategorijaOpreme kategorija, int[] ocekivaniId)
    {
        // Act
        var rezultat = await _servis.GetZahtjeviPoKategorijiAsync(kategorija);

        // Assert
        CollectionAssert.AreEquivalent(ocekivaniId, rezultat.Select(z => z.ZahtjevId).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.PoHitnosti), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetZahtjeviPoHitnostiAsync_ZahtjeviNaCekanju_VracaIhOdNajhitnijeg(int[] ocekivaniRedoslijedId)
    {
        // Act
        var rezultat = await _servis.GetZahtjeviPoHitnostiAsync();

        // Assert
        CollectionAssert.AreEqual(ocekivaniRedoslijedId, rezultat.Select(z => z.ZahtjevId).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.ZahtjeviDoktora), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetZahtjeviDoktoraAsync_ZadanDoktor_VracaSamoNjegoveZahtjeve(string doktorId, int[] ocekivaniId)
    {
        // Act
        var rezultat = await _servis.GetZahtjeviDoktoraAsync(doktorId);

        // Assert
        CollectionAssert.AreEquivalent(ocekivaniId, rezultat.Select(z => z.ZahtjevId).ToArray());
    }

    // ─── GetDoktoriAsync (IKorisnikRepository) ────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.Korisnici), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetDoktoriAsync_RazliciteUloge_VracaSamoKorisnikeSaSpecijalizacijom(List<Korisnik> korisnici, int ocekivaniBrojDoktora)
    {
        // Arrange
        _korisnikRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(korisnici);

        // Act
        var rezultat = await _servis.GetDoktoriAsync();

        // Assert
        Assert.AreEqual(ocekivaniBrojDoktora, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.Korisnici), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetDoktoriAsync_RazliciteUloge_CitaKorisnikeIzRepozitorija(List<Korisnik> korisnici, int ocekivaniBrojDoktora)
    {
        // Arrange
        _korisnikRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(korisnici);

        // Act
        await _servis.GetDoktoriAsync();

        // Assert
        _korisnikRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    // ─── Metode koje samo prosljeđuju repozitoriju ────────────────────────

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.PostojeciZahtjev), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetByIdAsync_ZahtjevPostoji_VracaZahtjevIzRepozitorija(int id, string ocekivaniNaziv)
    {
        // Act
        var rezultat = await _servis.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivaniNaziv, rezultat.Naziv);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.PostojeciZahtjev), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetByIdAsync_ZahtjevPostoji_PozivaRepozitorijSIstimId(int id, string ocekivaniNaziv)
    {
        // Act
        await _servis.GetByIdAsync(id);

        // Assert
        _zahtjevRepo.Verify(r => r.GetByIdAsync(id), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.SviZahtjevi), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetAllAsync_PostojeZahtjevi_VracaSveZahtjeve(int ocekivaniBroj)
    {
        // Act
        var rezultat = await _servis.GetAllAsync();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count());
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.SviZahtjevi), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetAllAsync_PostojeZahtjevi_PozivaRepozitorij(int ocekivaniBroj)
    {
        // Act
        await _servis.GetAllAsync();

        // Assert
        _zahtjevRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.PostojeciZahtjev), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void Update_PostojeciZahtjev_PozivaUpdateURepozitoriju(int id, string naziv)
    {
        // Arrange
        var zahtjev = new ZahtjevZaOpremu { ZahtjevId = id, Naziv = naziv };

        // Act
        _servis.Update(zahtjev);

        // Assert
        _zahtjevRepo.Verify(r => r.Update(zahtjev), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.PostojeciZahtjev), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void Delete_PostojeciZahtjev_PozivaDeleteURepozitoriju(int id, string naziv)
    {
        // Arrange
        var zahtjev = new ZahtjevZaOpremu { ZahtjevId = id, Naziv = naziv };

        // Act
        _servis.Delete(zahtjev);

        // Assert
        _zahtjevRepo.Verify(r => r.Delete(zahtjev), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.Snimanje), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task SaveChangesAsync_RepozitorijSnimiPromjene_VracaBrojPromjena(int brojPromjena)
    {
        // Arrange
        _zahtjevRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(brojPromjena);

        // Act
        var rezultat = await _servis.SaveChangesAsync();

        // Assert
        Assert.AreEqual(brojPromjena, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuServicePodaci.Snimanje), typeof(ZahtjevZaOpremuServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task SaveChangesAsync_Poziv_PozivaRepozitorij(int brojPromjena)
    {
        // Arrange
        _zahtjevRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(brojPromjena);

        // Act
        await _servis.SaveChangesAsync();

        // Assert
        _zahtjevRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
