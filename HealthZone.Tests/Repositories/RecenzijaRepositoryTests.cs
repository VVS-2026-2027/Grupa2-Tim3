using System.Xml.Linq;
using HealthZone.Data;
using HealthZone.Models;
using HealthZone.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za RecenzijaRepositoryTests ───────
public static class RecenzijaRepositoryPodaci
{
    // ── pomoćne metode ──

    public static ApplicationDbContext NovaBaza(string imeBaze) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(imeBaze).Options);

    public static Korisnik NapraviKorisnika(XElement x) => new()
    {
        Id = (string)x.Attribute("id")!,
        Ime = (string)x.Attribute("ime")!,
        Prezime = (string)x.Attribute("prezime")!
    };

    public static Recenzija NapraviRecenziju(XElement x) => new()
    {
        RecenzijaId = (int)x.Attribute("id")!,
        PacijentID = (string)x.Attribute("pacijent")!,
        DoktorID = (string)x.Attribute("doktor")!,
        OcjenaLjubaznosti = (int)x.Attribute("ljubaznost")!,
        OcjenaProfesionalnosti = (int)x.Attribute("profesionalnost")!,
        OcjenaUsluge = (int)x.Attribute("usluga")!,
        Komentar = (string?)x.Attribute("komentar")
    };

    public static Recenzija NapraviRecenziju(int id, string pacijentId, string doktorId, int ljubaznost, int profesionalnost, int usluga) => new()
    {
        RecenzijaId = id,
        PacijentID = pacijentId,
        DoktorID = doktorId,
        OcjenaLjubaznosti = ljubaznost,
        OcjenaProfesionalnosti = profesionalnost,
        OcjenaUsluge = usluga
    };

    // Upisuje korisnike (doktore i pacijente) i recenzije iz XML "baze" u InMemory bazu (posebna instanca konteksta).
    public static void NapuniBazu(string imeBaze, XElement baza)
    {
        using var kontekst = NovaBaza(imeBaze);
        kontekst.Users.AddRange(baza.Elements("korisnik").Select(NapraviKorisnika));
        kontekst.Recenzija.AddRange(baza.Elements("recenzija").Select(NapraviRecenziju));
        kontekst.SaveChanges();
    }

    // ── izvori podataka (XML) ──

    public static IEnumerable<object[]> SveRecenzije() =>
        XmlLoader.Ucitaj("RecenzijaRepository_baza.xml", "baza").Select(b => new object[]
        {
            b.Elements("recenzija").Select(r => (int)r.Attribute("id")!).ToArray()
        });

    // ── izvori podataka (CSV) ──

    public static IEnumerable<object[]> PostojeceRecenzije() =>
        CsvLoader.Ucitaj("RecenzijaRepository_postojeca.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> NepostojeceRecenzije() =>
        CsvLoader.Ucitaj("RecenzijaRepository_nepostojeca.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> NoveRecenzije() =>
        CsvLoader.Ucitaj("RecenzijaRepository_nova.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1], r[2], int.Parse(r[3]), int.Parse(r[4]), int.Parse(r[5]) });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("RecenzijaRepository_snimanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1], r[2], int.Parse(r[3]), int.Parse(r[4]), int.Parse(r[5]), int.Parse(r[6]) });

    public static IEnumerable<object[]> Izmjene() =>
        CsvLoader.Ucitaj("RecenzijaRepository_izmjena.csv").Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), r[2] });

    public static IEnumerable<object[]> Brisanje() =>
        CsvLoader.Ucitaj("RecenzijaRepository_brisanje.csv").Select(r => new object[] { int.Parse(r[0]) });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class RecenzijaRepositoryTests
{
    private static XElement _baza = null!;   // samo za čitanje

    private string _imeBaze = null!;
    private ApplicationDbContext _context = null!;
    private RecenzijaRepository _repozitorij = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // XML "baza" (korisnici i recenzije) se čita jednom; svaki test je upisuje u svoju InMemory bazu.
        _baza = XmlLoader.Ucitaj("RecenzijaRepository_baza.xml", "baza").Single();
    }

    [TestInitialize]
    public void Setup()
    {
        // Svaki test dobija svoju praznu bazu (novo ime), pa su testovi nezavisni.
        _imeBaze = Guid.NewGuid().ToString();
        RecenzijaRepositoryPodaci.NapuniBazu(_imeBaze, _baza);
        _context = RecenzijaRepositoryPodaci.NovaBaza(_imeBaze);
        _repozitorij = new RecenzijaRepository(_context);
    }

    [TestCleanup]
    public void Cleanup() => _context.Dispose();

    // ─── ČITANJE ──────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(RecenzijaRepositoryPodaci.PostojeceRecenzije), typeof(RecenzijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task GetByIdAsync_RecenzijaPostoji_UcitavaDoktora(int id, string ocekivanoImeDoktora, string ocekivanoImePacijenta)
    {
        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat?.Doktor);
        Assert.AreEqual(ocekivanoImeDoktora, rezultat.Doktor.Ime);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaRepositoryPodaci.PostojeceRecenzije), typeof(RecenzijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetByIdAsync_RecenzijaPostoji_UcitavaPacijenta(int id, string ocekivanoImeDoktora, string ocekivanoImePacijenta)
    {
        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat?.Pacijent);
        Assert.AreEqual(ocekivanoImePacijenta, rezultat.Pacijent.Ime);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaRepositoryPodaci.NepostojeceRecenzije), typeof(RecenzijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetByIdAsync_RecenzijaNePostoji_VracaNull(int id)
    {
        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNull(rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaRepositoryPodaci.SveRecenzije), typeof(RecenzijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetAllAsync_BazaImaRecenzije_VracaSveRecenzije(int[] ocekivaniId)
    {
        // Act
        var rezultat = await _repozitorij.GetAllAsync();

        // Assert
        CollectionAssert.AreEquivalent(ocekivaniId, rezultat.Select(r => r.RecenzijaId).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaRepositoryPodaci.SveRecenzije), typeof(RecenzijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetAllAsync_BazaImaRecenzije_UcitavaDoktoraIPacijentaZaSvaku(int[] ocekivaniId)
    {
        // Act
        var rezultat = await _repozitorij.GetAllAsync();

        // Assert
        Assert.IsTrue(rezultat.All(r => r.Doktor != null && r.Pacijent != null));
    }

    // ─── DODAVANJE ────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(RecenzijaRepositoryPodaci.NoveRecenzije), typeof(RecenzijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task AddAsync_NovaRecenzija_OznacavaJeZaDodavanje(int id, string pacijentId, string doktorId, int ljubaznost, int profesionalnost, int usluga)
    {
        // Arrange
        var recenzija = RecenzijaRepositoryPodaci.NapraviRecenziju(id, pacijentId, doktorId, ljubaznost, profesionalnost, usluga);

        // Act
        await _repozitorij.AddAsync(recenzija);

        // Assert
        Assert.AreEqual(EntityState.Added, _context.Entry(recenzija).State);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaRepositoryPodaci.NoveRecenzije), typeof(RecenzijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task AddAsync_BezSnimanja_RecenzijaNijeUBazi(int id, string pacijentId, string doktorId, int ljubaznost, int profesionalnost, int usluga)
    {
        // Arrange
        var recenzija = RecenzijaRepositoryPodaci.NapraviRecenziju(id, pacijentId, doktorId, ljubaznost, profesionalnost, usluga);

        // Act
        await _repozitorij.AddAsync(recenzija);

        // Assert
        using var provjera = RecenzijaRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNull(await provjera.Recenzija.FindAsync(id));
    }

    // ─── IZMJENA I BRISANJE ───────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(RecenzijaRepositoryPodaci.Izmjene), typeof(RecenzijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Update_PostojecaRecenzija_OznacavaJeKaoIzmijenjenu(int id, int novaOcjenaUsluge, string noviKomentar)
    {
        // Arrange
        var recenzija = (await _repozitorij.GetByIdAsync(id))!;
        recenzija.OcjenaUsluge = novaOcjenaUsluge;

        // Act
        _repozitorij.Update(recenzija);

        // Assert
        Assert.AreEqual(EntityState.Modified, _context.Entry(recenzija).State);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaRepositoryPodaci.Brisanje), typeof(RecenzijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Delete_PostojecaRecenzija_OznacavaJeZaBrisanje(int id)
    {
        // Arrange
        var recenzija = (await _repozitorij.GetByIdAsync(id))!;

        // Act
        _repozitorij.Delete(recenzija);

        // Assert
        Assert.AreEqual(EntityState.Deleted, _context.Entry(recenzija).State);
    }

    // ─── SNIMANJE ─────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(RecenzijaRepositoryPodaci.Snimanje), typeof(RecenzijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task SaveChangesAsync_NakonDodavanja_VracaBrojPromjena(int id, string pacijentId, string doktorId,
        int ljubaznost, int profesionalnost, int usluga, int ocekivaniBroj)
    {
        // Arrange
        await _repozitorij.AddAsync(RecenzijaRepositoryPodaci.NapraviRecenziju(id, pacijentId, doktorId, ljubaznost, profesionalnost, usluga));

        // Act
        var rezultat = await _repozitorij.SaveChangesAsync();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaRepositoryPodaci.NoveRecenzije), typeof(RecenzijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public async Task SaveChangesAsync_NakonDodavanja_RecenzijaJeUBazi(int id, string pacijentId, string doktorId, int ljubaznost, int profesionalnost, int usluga)
    {
        // Arrange
        await _repozitorij.AddAsync(RecenzijaRepositoryPodaci.NapraviRecenziju(id, pacijentId, doktorId, ljubaznost, profesionalnost, usluga));

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = RecenzijaRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNotNull(await provjera.Recenzija.FindAsync(id));
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaRepositoryPodaci.Izmjene), typeof(RecenzijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonIzmjene_BazaImaNovePodatke(int id, int novaOcjenaUsluge, string noviKomentar)
    {
        // Arrange
        var recenzija = (await _repozitorij.GetByIdAsync(id))!;
        recenzija.OcjenaUsluge = novaOcjenaUsluge;
        recenzija.Komentar = noviKomentar;
        _repozitorij.Update(recenzija);

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = RecenzijaRepositoryPodaci.NovaBaza(_imeBaze);
        var izBaze = (await provjera.Recenzija.FindAsync(id))!;
        Assert.AreEqual(novaOcjenaUsluge, izBaze.OcjenaUsluge);
        Assert.AreEqual(noviKomentar, izBaze.Komentar);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaRepositoryPodaci.Brisanje), typeof(RecenzijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonBrisanja_RecenzijaNijeUBazi(int id)
    {
        // Arrange
        _repozitorij.Delete((await _repozitorij.GetByIdAsync(id))!);

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = RecenzijaRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNull(await provjera.Recenzija.FindAsync(id));
    }
}
