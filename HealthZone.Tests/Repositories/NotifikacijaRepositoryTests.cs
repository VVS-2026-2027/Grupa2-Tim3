using System.Xml.Linq;
using HealthZone.Data;
using HealthZone.Models;
using HealthZone.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za NotifikacijaRepositoryTests ────
public static class NotifikacijaRepositoryPodaci
{
    // ── pomoćne metode ──

    public static ApplicationDbContext NovaBaza(string imeBaze) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(imeBaze).Options);

    public static Notifikacija NapraviNotifikaciju(XElement x) => new()
    {
        NotifikacijaId = (int)x.Attribute("id")!,
        TerminID = (int)x.Attribute("terminId")!,
        Poruka = (string)x.Attribute("poruka")!,
        status = Enum.Parse<Status>((string)x.Attribute("status")!),
        DatumSlanja = DateTime.Now.AddHours((double)x.Attribute("pomakSati")!)
    };

    public static Notifikacija NapraviNotifikaciju(int id, int terminId, string poruka) =>
        new() { NotifikacijaId = id, TerminID = terminId, Poruka = poruka, DatumSlanja = DateTime.Now };

    // Upisuje notifikacije iz XML "baze" u InMemory bazu, u posebnoj instanci konteksta.
    public static void NapuniBazu(string imeBaze, XElement baza)
    {
        using var kontekst = NovaBaza(imeBaze);
        kontekst.Notifikacija.AddRange(baza.Elements("notifikacija").Select(NapraviNotifikaciju));
        kontekst.SaveChanges();
    }

    // ── izvori podataka ──

    public static IEnumerable<object[]> SveNotifikacije() =>
        XmlLoader.Ucitaj("NotifikacijaRepository_baza.xml", "baza").Select(b => new object[]
        {
            b.Elements("notifikacija").Select(n => (int)n.Attribute("id")!).ToArray()
        });

    private static IEnumerable<string[]> Postojece() => CsvLoader.Ucitaj("NotifikacijaRepository_postojece.csv");

    public static IEnumerable<object[]> PostojecePoruke() => Postojece().Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> PostojeciTermini() => Postojece().Select(r => new object[] { int.Parse(r[0]), int.Parse(r[2]) });

    public static IEnumerable<object[]> NepostojeceNotifikacije() =>
        CsvLoader.Ucitaj("NotifikacijaRepository_nepostojece.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> NoveNotifikacije() =>
        CsvLoader.Ucitaj("NotifikacijaRepository_nove.csv").Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), r[2] });

    public static IEnumerable<object[]> Izmjene() =>
        CsvLoader.Ucitaj("NotifikacijaRepository_izmjena.csv").Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), r[2] });

    public static IEnumerable<object[]> Brisanje() =>
        CsvLoader.Ucitaj("NotifikacijaRepository_brisanje.csv").Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), r[2] });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("NotifikacijaRepository_snimanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), r[2], int.Parse(r[3]) });
}

// ─── Testovi ────────────────────────────────────────────────────────────
// Repozitorij radi nad EF Core InMemory bazom (Fake): svaki test dobija svoju bazu, napunjenu iz XML-a.
// Stanje nakon snimanja se provjerava u novoj instanci konteksta.
[TestClass]
public class NotifikacijaRepositoryTests
{
    private static XElement _baza = null!;   // samo za čitanje

    private string _imeBaze = null!;
    private ApplicationDbContext _context = null!;
    private NotifikacijaRepository _repozitorij = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _baza = XmlLoader.Ucitaj("NotifikacijaRepository_baza.xml", "baza").Single();
    }

    [TestInitialize]
    public void Setup()
    {
        _imeBaze = Guid.NewGuid().ToString();
        NotifikacijaRepositoryPodaci.NapuniBazu(_imeBaze, _baza);
        _context = NotifikacijaRepositoryPodaci.NovaBaza(_imeBaze);
        _repozitorij = new NotifikacijaRepository(_context);
    }

    [TestCleanup]
    public void Cleanup() => _context.Dispose();

    // ─── ČITANJE ──────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NotifikacijaRepositoryPodaci.PostojecePoruke), typeof(NotifikacijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task GetByIdAsync_NotifikacijaPostoji_VracaNotifikacijuSaPorukom(int id, string ocekivanaPoruka)
    {
        // Arrange (baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivanaPoruka, rezultat.Poruka);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaRepositoryPodaci.PostojeciTermini), typeof(NotifikacijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetByIdAsync_NotifikacijaPostoji_VracaTerminNotifikacije(int id, int ocekivaniTerminId)
    {
        // Arrange (baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.AreEqual(ocekivaniTerminId, rezultat?.TerminID);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaRepositoryPodaci.NepostojeceNotifikacije), typeof(NotifikacijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetByIdAsync_NotifikacijaNePostoji_VracaNull(int id)
    {
        // Arrange (baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNull(rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaRepositoryPodaci.SveNotifikacije), typeof(NotifikacijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task GetAllAsync_BazaImaNotifikacije_VracaSveNotifikacije(int[] ocekivaniIds)
    {
        // Arrange (baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetAllAsync();

        // Assert
        CollectionAssert.AreEquivalent(ocekivaniIds, rezultat.Select(n => n.NotifikacijaId).ToArray());
    }

    // ─── DODAVANJE ────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NotifikacijaRepositoryPodaci.NoveNotifikacije), typeof(NotifikacijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_NovaNotifikacija_OznacavaJeZaDodavanje(int id, int terminId, string poruka)
    {
        // Arrange
        var notifikacija = NotifikacijaRepositoryPodaci.NapraviNotifikaciju(id, terminId, poruka);

        // Act
        await _repozitorij.AddAsync(notifikacija);

        // Assert
        Assert.AreEqual(EntityState.Added, _context.Entry(notifikacija).State);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaRepositoryPodaci.NoveNotifikacije), typeof(NotifikacijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_BezSnimanja_NotifikacijaNijeUBazi(int id, int terminId, string poruka)
    {
        // Arrange
        var notifikacija = NotifikacijaRepositoryPodaci.NapraviNotifikaciju(id, terminId, poruka);

        // Act
        await _repozitorij.AddAsync(notifikacija);

        // Assert
        using var provjera = NotifikacijaRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNull(await provjera.Notifikacija.FindAsync(id));
    }

    // ─── IZMJENA I BRISANJE ───────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NotifikacijaRepositoryPodaci.Izmjene), typeof(NotifikacijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Update_PostojecaNotifikacija_OznacavaJeKaoIzmijenjenu(int id, int terminId, string novaPoruka)
    {
        // Arrange
        var notifikacija = NotifikacijaRepositoryPodaci.NapraviNotifikaciju(id, terminId, novaPoruka);

        // Act
        _repozitorij.Update(notifikacija);

        // Assert
        Assert.AreEqual(EntityState.Modified, _context.Entry(notifikacija).State);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaRepositoryPodaci.Brisanje), typeof(NotifikacijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Delete_PostojecaNotifikacija_OznacavaJeZaBrisanje(int id, int terminId, string poruka)
    {
        // Arrange
        var notifikacija = NotifikacijaRepositoryPodaci.NapraviNotifikaciju(id, terminId, poruka);

        // Act
        _repozitorij.Delete(notifikacija);

        // Assert
        Assert.AreEqual(EntityState.Deleted, _context.Entry(notifikacija).State);
    }

    // ─── SNIMANJE ─────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NotifikacijaRepositoryPodaci.Snimanje), typeof(NotifikacijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task SaveChangesAsync_NakonDodavanja_VracaBrojPromjena(int id, int terminId, string poruka, int ocekivaniBroj)
    {
        // Arrange
        await _repozitorij.AddAsync(NotifikacijaRepositoryPodaci.NapraviNotifikaciju(id, terminId, poruka));

        // Act
        var rezultat = await _repozitorij.SaveChangesAsync();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaRepositoryPodaci.NoveNotifikacije), typeof(NotifikacijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonDodavanja_NotifikacijaJeUBazi(int id, int terminId, string poruka)
    {
        // Arrange
        await _repozitorij.AddAsync(NotifikacijaRepositoryPodaci.NapraviNotifikaciju(id, terminId, poruka));

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = NotifikacijaRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNotNull(await provjera.Notifikacija.FindAsync(id));
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaRepositoryPodaci.Izmjene), typeof(NotifikacijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonIzmjene_BazaImaNovuPoruku(int id, int terminId, string novaPoruka)
    {
        // Arrange
        _repozitorij.Update(NotifikacijaRepositoryPodaci.NapraviNotifikaciju(id, terminId, novaPoruka));

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = NotifikacijaRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.AreEqual(novaPoruka, (await provjera.Notifikacija.FindAsync(id))!.Poruka);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaRepositoryPodaci.Brisanje), typeof(NotifikacijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task SaveChangesAsync_NakonBrisanja_NotifikacijaNijeUBazi(int id, int terminId, string poruka)
    {
        // Arrange
        _repozitorij.Delete(NotifikacijaRepositoryPodaci.NapraviNotifikaciju(id, terminId, poruka));

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = NotifikacijaRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNull(await provjera.Notifikacija.FindAsync(id));
    }
}
