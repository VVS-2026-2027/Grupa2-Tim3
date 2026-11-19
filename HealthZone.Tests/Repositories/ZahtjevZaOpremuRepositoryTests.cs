using System.Xml.Linq;
using HealthZone.Data;
using HealthZone.Models;
using HealthZone.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za ZahtjevZaOpremuRepositoryTests ─
public static class ZahtjevZaOpremuRepositoryPodaci
{
    // ── pomoćne metode ──

    public static ApplicationDbContext NovaBaza(string imeBaze) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(imeBaze).Options);

    public static Korisnik NapraviDoktora(XElement x) => new()
    {
        Id = (string)x.Attribute("id")!,
        Ime = (string)x.Attribute("ime")!,
        Prezime = (string)x.Attribute("prezime")!
    };

    public static ZahtjevZaOpremu NapraviZahtjev(XElement x) => new()
    {
        ZahtjevId = (int)x.Attribute("id")!,
        Naziv = (string)x.Attribute("naziv")!,
        Opis = (string)x.Attribute("opis")!,
        DoktorID = (string)x.Attribute("doktor")!,
        Status = Enum.Parse<Status>((string)x.Attribute("status")!),
        VrstaZahtjeva = Enum.Parse<VrstaZahtjeva>((string)x.Attribute("vrsta")!),
        Kategorija = Enum.Parse<KategorijaOpreme>((string)x.Attribute("kategorija")!),
        Hitnost = Enum.Parse<Hitnost>((string)x.Attribute("hitnost")!)
    };

    public static ZahtjevZaOpremu NapraviZahtjev(int id, string naziv, string opis, string doktorId) => new()
    {
        ZahtjevId = id, Naziv = naziv, Opis = opis, DoktorID = doktorId
    };

    // Upisuje doktore i zahtjeve iz XML "baze" u InMemory bazu zadanog imena (posebna instanca konteksta).
    public static void NapuniBazu(string imeBaze, XElement baza)
    {
        using var kontekst = NovaBaza(imeBaze);
        kontekst.Users.AddRange(baza.Elements("doktor").Select(NapraviDoktora));
        kontekst.ZahtjevZaOpremu.AddRange(baza.Elements("zahtjev").Select(NapraviZahtjev));
        kontekst.SaveChanges();
    }

    // ── izvori podataka (XML) ──

    // Očekivani redoslijed id-jeva iz GetAllAsync (od najnovijeg zahtjeva).
    public static IEnumerable<object[]> Redoslijed() =>
        XmlLoader.Ucitaj("ZahtjevZaOpremuRepository_baza.xml", "baza").Select(b => new object[]
        {
            ((string)b.Element("ocekivaniRedoslijed")!).Split(',').Select(int.Parse).ToArray()
        });

    // ── izvori podataka (CSV) ──

    public static IEnumerable<object[]> PostojeciZahtjevi() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuRepository_postojeci.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> NepostojeciZahtjevi() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuRepository_nepostojeci.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> NoviZahtjevi() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuRepository_novi.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2], r[3] });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuRepository_snimanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1], r[2], r[3], int.Parse(r[4]) });

    public static IEnumerable<object[]> Izmjene() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuRepository_izmjena.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1], Enum.Parse<Status>(r[2]) });

    public static IEnumerable<object[]> Brisanje() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuRepository_brisanje.csv").Select(r => new object[] { int.Parse(r[0]) });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class ZahtjevZaOpremuRepositoryTests
{
    private static XElement _baza = null!;   // samo za čitanje

    private string _imeBaze = null!;
    private ApplicationDbContext _context = null!;
    private ZahtjevZaOpremuRepository _repozitorij = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // XML "baza" (doktori i zahtjevi) se čita jednom; svaki test je upisuje u svoju InMemory bazu.
        _baza = XmlLoader.Ucitaj("ZahtjevZaOpremuRepository_baza.xml", "baza").Single();
    }

    [TestInitialize]
    public void Setup()
    {
        // Svaki test dobija svoju praznu bazu (novo ime), pa su testovi nezavisni.
        _imeBaze = Guid.NewGuid().ToString();
        ZahtjevZaOpremuRepositoryPodaci.NapuniBazu(_imeBaze, _baza);
        _context = ZahtjevZaOpremuRepositoryPodaci.NovaBaza(_imeBaze);
        _repozitorij = new ZahtjevZaOpremuRepository(_context);
    }

    [TestCleanup]
    public void Cleanup() => _context.Dispose();

    // ─── ČITANJE ──────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuRepositoryPodaci.PostojeciZahtjevi), typeof(ZahtjevZaOpremuRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task GetByIdAsync_ZahtjevPostoji_VracaZahtjev(int id, string ocekivaniNaziv, string ocekivanoImeDoktora)
    {
        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivaniNaziv, rezultat.Naziv);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuRepositoryPodaci.PostojeciZahtjevi), typeof(ZahtjevZaOpremuRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetByIdAsync_ZahtjevPostoji_UcitavaDoktora(int id, string ocekivaniNaziv, string ocekivanoImeDoktora)
    {
        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat?.Doktor);
        Assert.AreEqual(ocekivanoImeDoktora, rezultat.Doktor.Ime);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuRepositoryPodaci.NepostojeciZahtjevi), typeof(ZahtjevZaOpremuRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetByIdAsync_ZahtjevNePostoji_VracaNull(int id)
    {
        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNull(rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuRepositoryPodaci.Redoslijed), typeof(ZahtjevZaOpremuRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetAllAsync_BazaImaZahtjeve_VracaIhOdNajnovijeg(int[] ocekivaniRedoslijed)
    {
        // Act
        var rezultat = await _repozitorij.GetAllAsync();

        // Assert
        CollectionAssert.AreEqual(ocekivaniRedoslijed, rezultat.Select(z => z.ZahtjevId).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuRepositoryPodaci.Redoslijed), typeof(ZahtjevZaOpremuRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetAllAsync_BazaImaZahtjeve_UcitavaDoktoraZaSvakiZahtjev(int[] ocekivaniRedoslijed)
    {
        // Act
        var rezultat = await _repozitorij.GetAllAsync();

        // Assert
        Assert.IsTrue(rezultat.All(z => z.Doktor != null));
    }

    // ─── DODAVANJE ────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuRepositoryPodaci.NoviZahtjevi), typeof(ZahtjevZaOpremuRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task AddAsync_NoviZahtjev_OznacavaGaZaDodavanje(int id, string naziv, string opis, string doktorId)
    {
        // Arrange
        var zahtjev = ZahtjevZaOpremuRepositoryPodaci.NapraviZahtjev(id, naziv, opis, doktorId);

        // Act
        await _repozitorij.AddAsync(zahtjev);

        // Assert
        Assert.AreEqual(EntityState.Added, _context.Entry(zahtjev).State);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuRepositoryPodaci.NoviZahtjevi), typeof(ZahtjevZaOpremuRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task AddAsync_BezSnimanja_ZahtjevNijeUBazi(int id, string naziv, string opis, string doktorId)
    {
        // Arrange
        var zahtjev = ZahtjevZaOpremuRepositoryPodaci.NapraviZahtjev(id, naziv, opis, doktorId);

        // Act
        await _repozitorij.AddAsync(zahtjev);

        // Assert
        using var provjera = ZahtjevZaOpremuRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNull(await provjera.ZahtjevZaOpremu.FindAsync(id));
    }

    // ─── IZMJENA I BRISANJE ───────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuRepositoryPodaci.Izmjene), typeof(ZahtjevZaOpremuRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Update_PostojeciZahtjev_OznacavaGaKaoIzmijenjen(int id, string noviNaziv, Status noviStatus)
    {
        // Arrange
        var zahtjev = (await _repozitorij.GetByIdAsync(id))!;
        zahtjev.Naziv = noviNaziv;

        // Act
        _repozitorij.Update(zahtjev);

        // Assert
        Assert.AreEqual(EntityState.Modified, _context.Entry(zahtjev).State);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuRepositoryPodaci.Brisanje), typeof(ZahtjevZaOpremuRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Delete_PostojeciZahtjev_OznacavaGaZaBrisanje(int id)
    {
        // Arrange
        var zahtjev = (await _repozitorij.GetByIdAsync(id))!;

        // Act
        _repozitorij.Delete(zahtjev);

        // Assert
        Assert.AreEqual(EntityState.Deleted, _context.Entry(zahtjev).State);
    }

    // ─── SNIMANJE ─────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuRepositoryPodaci.Snimanje), typeof(ZahtjevZaOpremuRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task SaveChangesAsync_NakonDodavanja_VracaBrojPromjena(int id, string naziv, string opis, string doktorId, int ocekivaniBroj)
    {
        // Arrange
        await _repozitorij.AddAsync(ZahtjevZaOpremuRepositoryPodaci.NapraviZahtjev(id, naziv, opis, doktorId));

        // Act
        var rezultat = await _repozitorij.SaveChangesAsync();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuRepositoryPodaci.NoviZahtjevi), typeof(ZahtjevZaOpremuRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public async Task SaveChangesAsync_NakonDodavanja_ZahtjevJeUBazi(int id, string naziv, string opis, string doktorId)
    {
        // Arrange
        await _repozitorij.AddAsync(ZahtjevZaOpremuRepositoryPodaci.NapraviZahtjev(id, naziv, opis, doktorId));

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = ZahtjevZaOpremuRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNotNull(await provjera.ZahtjevZaOpremu.FindAsync(id));
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuRepositoryPodaci.Izmjene), typeof(ZahtjevZaOpremuRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonIzmjene_BazaImaNovePodatke(int id, string noviNaziv, Status noviStatus)
    {
        // Arrange
        var zahtjev = (await _repozitorij.GetByIdAsync(id))!;
        zahtjev.Naziv = noviNaziv;
        zahtjev.Status = noviStatus;
        _repozitorij.Update(zahtjev);

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = ZahtjevZaOpremuRepositoryPodaci.NovaBaza(_imeBaze);
        var izBaze = (await provjera.ZahtjevZaOpremu.FindAsync(id))!;
        Assert.AreEqual(noviNaziv, izBaze.Naziv);
        Assert.AreEqual(noviStatus, izBaze.Status);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuRepositoryPodaci.Brisanje), typeof(ZahtjevZaOpremuRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonBrisanja_ZahtjevNijeUBazi(int id)
    {
        // Arrange
        _repozitorij.Delete((await _repozitorij.GetByIdAsync(id))!);

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = ZahtjevZaOpremuRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNull(await provjera.ZahtjevZaOpremu.FindAsync(id));
    }
}
