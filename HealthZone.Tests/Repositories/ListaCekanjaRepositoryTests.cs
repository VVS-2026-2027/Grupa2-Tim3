using System.Xml.Linq;
using HealthZone.Data;
using HealthZone.Models;
using HealthZone.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za ListaCekanjaRepositoryTests ────
public static class ListaCekanjaRepositoryPodaci
{
    // ── pomoćne metode ──

    public static ApplicationDbContext NovaBaza(string imeBaze) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(imeBaze).Options);

    public static Korisnik NapraviKorisnika(XElement x) => new()
    {
        Id = (string)x.Attribute("id")!,
        Ime = (string)x.Attribute("ime")!,
        Prezime = (string)x.Attribute("prezime")!,
        Email = (string)x.Attribute("email")!,
        UserName = (string)x.Attribute("email")!
    };

    public static ListaCekanja NapraviListu(XElement x) =>
        NapraviListu((int)x.Attribute("id")!, (string)x.Attribute("doktorId")!);

    public static ListaCekanja NapraviListu(int id, string doktorId) => new() { ListaId = id, DoktorID = doktorId };

    // Upisuje doktore i liste iz XML "baze" u InMemory bazu, u posebnoj instanci konteksta.
    // Repozitorij uključuje doktora liste (Include), pa doktori moraju postojati u bazi.
    public static void NapuniBazu(string imeBaze, XElement baza)
    {
        using var kontekst = NovaBaza(imeBaze);
        kontekst.Users.AddRange(baza.Elements("korisnik").Select(NapraviKorisnika));
        kontekst.ListaCekanja.AddRange(baza.Elements("lista").Select(NapraviListu));
        kontekst.SaveChanges();
    }

    // ── izvori podataka ──

    public static IEnumerable<object[]> SveListe() =>
        XmlLoader.Ucitaj("ListaCekanjaRepository_baza.xml", "baza").Select(b => new object[]
        {
            b.Elements("lista").Select(l => (int)l.Attribute("id")!).ToArray()
        });

    private static IEnumerable<string[]> Postojece() => CsvLoader.Ucitaj("ListaCekanjaRepository_postojece.csv");

    public static IEnumerable<object[]> PostojeceListe() => Postojece().Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> DoktoriListi() => Postojece().Select(r => new object[] { int.Parse(r[0]), r[2] });

    public static IEnumerable<object[]> NepostojeceListe() =>
        CsvLoader.Ucitaj("ListaCekanjaRepository_nepostojece.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> NoveListe() =>
        CsvLoader.Ucitaj("ListaCekanjaRepository_nove.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> Izmjene() =>
        CsvLoader.Ucitaj("ListaCekanjaRepository_izmjena.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> Brisanje() =>
        CsvLoader.Ucitaj("ListaCekanjaRepository_brisanje.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("ListaCekanjaRepository_snimanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1], int.Parse(r[2]) });
}

// ─── Testovi ────────────────────────────────────────────────────────────
// Repozitorij radi nad EF Core InMemory bazom (Fake): svaki test dobija svoju bazu, napunjenu iz XML-a.
// Stanje nakon snimanja se provjerava u novoj instanci konteksta.
[TestClass]
public class ListaCekanjaRepositoryTests
{
    private static XElement _baza = null!;   // samo za čitanje

    private string _imeBaze = null!;
    private ApplicationDbContext _context = null!;
    private ListaCekanjaRepository _repozitorij = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _baza = XmlLoader.Ucitaj("ListaCekanjaRepository_baza.xml", "baza").Single();
    }

    [TestInitialize]
    public void Setup()
    {
        _imeBaze = Guid.NewGuid().ToString();
        ListaCekanjaRepositoryPodaci.NapuniBazu(_imeBaze, _baza);
        _context = ListaCekanjaRepositoryPodaci.NovaBaza(_imeBaze);
        _repozitorij = new ListaCekanjaRepository(_context);
    }

    [TestCleanup]
    public void Cleanup() => _context.Dispose();

    // ─── ČITANJE ──────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaRepositoryPodaci.PostojeceListe), typeof(ListaCekanjaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task GetByIdAsync_ListaPostoji_VracaListu(int id, string ocekivaniDoktorId)
    {
        // Arrange (baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivaniDoktorId, rezultat.DoktorID);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaRepositoryPodaci.DoktoriListi), typeof(ListaCekanjaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetByIdAsync_ListaPostoji_UcitavaDoktora(int id, string ocekivanoImeDoktora)
    {
        // Arrange (baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.AreEqual(ocekivanoImeDoktora, rezultat?.Doktor?.Ime);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaRepositoryPodaci.NepostojeceListe), typeof(ListaCekanjaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetByIdAsync_ListaNePostoji_VracaNull(int id)
    {
        // Arrange (baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNull(rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaRepositoryPodaci.SveListe), typeof(ListaCekanjaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task GetAllAsync_BazaImaListe_VracaSveListe(int[] ocekivaniIds)
    {
        // Arrange (baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetAllAsync();

        // Assert
        CollectionAssert.AreEquivalent(ocekivaniIds, rezultat.Select(l => l.ListaId).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaRepositoryPodaci.SveListe), typeof(ListaCekanjaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetAllAsync_BazaImaListe_UcitavaDoktoraZaSvakuListu(int[] ocekivaniIds)
    {
        // Arrange (baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetAllAsync();

        // Assert
        Assert.AreEqual(ocekivaniIds.Length, rezultat.Count(l => l.Doktor != null));
    }

    // ─── DODAVANJE ────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaRepositoryPodaci.NoveListe), typeof(ListaCekanjaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_NovaLista_OznacavaJeZaDodavanje(int id, string doktorId)
    {
        // Arrange
        var lista = ListaCekanjaRepositoryPodaci.NapraviListu(id, doktorId);

        // Act
        await _repozitorij.AddAsync(lista);

        // Assert
        Assert.AreEqual(EntityState.Added, _context.Entry(lista).State);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaRepositoryPodaci.NoveListe), typeof(ListaCekanjaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_BezSnimanja_ListaNijeUBazi(int id, string doktorId)
    {
        // Arrange
        var lista = ListaCekanjaRepositoryPodaci.NapraviListu(id, doktorId);

        // Act
        await _repozitorij.AddAsync(lista);

        // Assert
        using var provjera = ListaCekanjaRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNull(await provjera.ListaCekanja.FindAsync(id));
    }

    // ─── IZMJENA I BRISANJE ───────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaRepositoryPodaci.Izmjene), typeof(ListaCekanjaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Update_PostojecaLista_OznacavaJeKaoIzmijenjenu(int id, string noviDoktorId)
    {
        // Arrange
        var lista = ListaCekanjaRepositoryPodaci.NapraviListu(id, noviDoktorId);

        // Act
        _repozitorij.Update(lista);

        // Assert
        Assert.AreEqual(EntityState.Modified, _context.Entry(lista).State);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaRepositoryPodaci.Brisanje), typeof(ListaCekanjaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Delete_PostojecaLista_OznacavaJeZaBrisanje(int id, string doktorId)
    {
        // Arrange
        var lista = ListaCekanjaRepositoryPodaci.NapraviListu(id, doktorId);

        // Act
        _repozitorij.Delete(lista);

        // Assert
        Assert.AreEqual(EntityState.Deleted, _context.Entry(lista).State);
    }

    // ─── SNIMANJE ─────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaRepositoryPodaci.Snimanje), typeof(ListaCekanjaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task SaveChangesAsync_NakonDodavanja_VracaBrojPromjena(int id, string doktorId, int ocekivaniBroj)
    {
        // Arrange
        await _repozitorij.AddAsync(ListaCekanjaRepositoryPodaci.NapraviListu(id, doktorId));

        // Act
        var rezultat = await _repozitorij.SaveChangesAsync();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaRepositoryPodaci.NoveListe), typeof(ListaCekanjaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonDodavanja_ListaJeUBazi(int id, string doktorId)
    {
        // Arrange
        await _repozitorij.AddAsync(ListaCekanjaRepositoryPodaci.NapraviListu(id, doktorId));

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = ListaCekanjaRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNotNull(await provjera.ListaCekanja.FindAsync(id));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaRepositoryPodaci.Izmjene), typeof(ListaCekanjaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonIzmjene_BazaImaNovogDoktora(int id, string noviDoktorId)
    {
        // Arrange
        _repozitorij.Update(ListaCekanjaRepositoryPodaci.NapraviListu(id, noviDoktorId));

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = ListaCekanjaRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.AreEqual(noviDoktorId, (await provjera.ListaCekanja.FindAsync(id))!.DoktorID);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaRepositoryPodaci.Brisanje), typeof(ListaCekanjaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task SaveChangesAsync_NakonBrisanja_ListaNijeUBazi(int id, string doktorId)
    {
        // Arrange
        _repozitorij.Delete(ListaCekanjaRepositoryPodaci.NapraviListu(id, doktorId));

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = ListaCekanjaRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNull(await provjera.ListaCekanja.FindAsync(id));
    }
}
