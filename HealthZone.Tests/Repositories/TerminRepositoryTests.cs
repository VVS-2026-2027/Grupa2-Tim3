using System.Xml.Linq;
using HealthZone.Data;
using HealthZone.Models;
using HealthZone.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za TerminRepositoryTests ──────────
public static class TerminRepositoryPodaci
{
    // ── pomoćne metode ──

    public static ApplicationDbContext NovaBaza(string imeBaze) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(imeBaze).Options);

    public static Termin NapraviTermin(int id, string doktorId, string pacijentId, int uslugaId, int pomakDana, int sat,
        Status status) => new()
    {
        TerminId = id,
        DoktorID = doktorId,
        PacijentID = pacijentId,
        UslugaID = uslugaId,
        Datum = DateTime.Today.AddDays(pomakDana),
        Vrijeme = new TimeOnly(sat, 0),
        Status = status
    };

    // Upisuje korisnike (doktore i pacijente), usluge i termine iz XML "baze" u InMemory bazu zadanog imena.
    public static void NapuniBazu(string imeBaze, XElement baza)
    {
        using var kontekst = NovaBaza(imeBaze);
        kontekst.Users.AddRange(baza.Descendants("korisnik").Select(k => new Korisnik
        {
            Id = (string)k.Attribute("id")!,
            Ime = (string)k.Attribute("ime")!,
            Prezime = (string)k.Attribute("prezime")!
        }));
        kontekst.MedicinskaUsluga.AddRange(baza.Descendants("usluga").Select(u => new MedicinskaUsluga
        {
            UslugaId = (int)u.Attribute("id")!,
            Naziv = (string)u.Attribute("naziv")!,
            Opis = (string)u.Attribute("opis")!,
            Vrsta = Enum.Parse<Specijalizacija>((string)u.Attribute("vrsta")!),
            Cijena = (decimal)u.Attribute("cijena")!
        }));
        kontekst.Termin.AddRange(baza.Descendants("termin").Select(t => NapraviTermin(
            (int)t.Attribute("id")!,
            (string)t.Attribute("doktor")!,
            (string)t.Attribute("pacijent")!,
            (int)t.Attribute("usluga")!,
            (int)t.Attribute("pomakDana")!,
            (int)t.Attribute("sat")!,
            Enum.Parse<Status>((string)t.Attribute("status")!))));
        kontekst.SaveChanges();
    }

    // ── izvori podataka (XML) ──

    public static IEnumerable<object[]> SviTermini() =>
        XmlLoader.Ucitaj("TerminRepository_baza.xml", "baza").Select(b => new object[]
        {
            b.Descendants("termin").Select(t => (int)t.Attribute("id")!).ToArray()
        });

    // ── izvori podataka (CSV) ──

    public static IEnumerable<object[]> PostojeciTermini() =>
        CsvLoader.Ucitaj("TerminRepository_postojeci.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2], r[3] });

    public static IEnumerable<object[]> NepostojeciTermini() =>
        CsvLoader.Ucitaj("TerminRepository_nepostojeci.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> NoviTermini() =>
        CsvLoader.Ucitaj("TerminRepository_novi.csv").Select(r => new object[]
        {
            int.Parse(r[0]), r[1], r[2], int.Parse(r[3]), int.Parse(r[4]), int.Parse(r[5]), Enum.Parse<Status>(r[6])
        });

    public static IEnumerable<object[]> Izmjene() =>
        CsvLoader.Ucitaj("TerminRepository_izmjena.csv").Select(r => new object[] { int.Parse(r[0]), Enum.Parse<Status>(r[1]) });

    public static IEnumerable<object[]> Brisanje() =>
        CsvLoader.Ucitaj("TerminRepository_brisanje.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("TerminRepository_snimanje.csv").Select(r => new object[]
        {
            int.Parse(r[0]), r[1], r[2], int.Parse(r[3]), int.Parse(r[4]), int.Parse(r[5]), Enum.Parse<Status>(r[6]),
            int.Parse(r[7])
        });
}

// ─── Testovi ────────────────────────────────────────────────────────────
// Kategorija "Fake": umjesto SQL Servera koristi se EF Core InMemory baza.
[TestClass]
public class TerminRepositoryTests
{
    private static XElement _baza = null!;   // samo za čitanje

    private string _imeBaze = null!;
    private ApplicationDbContext _context = null!;
    private TerminRepository _repozitorij = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // XML "baza" se čita jednom; svaki test je upisuje u svoju InMemory bazu.
        _baza = XmlLoader.Ucitaj("TerminRepository_baza.xml", "baza").Single();
    }

    [TestInitialize]
    public void Setup()
    {
        _imeBaze = Guid.NewGuid().ToString();
        TerminRepositoryPodaci.NapuniBazu(_imeBaze, _baza);
        _context = TerminRepositoryPodaci.NovaBaza(_imeBaze);
        _repozitorij = new TerminRepository(_context);
    }

    [TestCleanup]
    public void Cleanup() => _context.Dispose();

    // ─── ČITANJE ──────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(TerminRepositoryPodaci.PostojeciTermini), typeof(TerminRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task GetByIdAsync_TerminPostoji_UcitavaDoktora(int id, string ocekivaniDoktor, string ocekivaniPacijent,
        string ocekivanaUsluga)
    {
        // Arrange (InMemory baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivaniDoktor, rezultat.Doktor!.Id);
    }

    [TestMethod]
    [DynamicData(nameof(TerminRepositoryPodaci.PostojeciTermini), typeof(TerminRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetByIdAsync_TerminPostoji_UcitavaPacijenta(int id, string ocekivaniDoktor, string ocekivaniPacijent,
        string ocekivanaUsluga)
    {
        // Arrange (InMemory baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivaniPacijent, rezultat.Pacijent!.Id);
    }

    [TestMethod]
    [DynamicData(nameof(TerminRepositoryPodaci.PostojeciTermini), typeof(TerminRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetByIdAsync_TerminPostoji_UcitavaUslugu(int id, string ocekivaniDoktor, string ocekivaniPacijent,
        string ocekivanaUsluga)
    {
        // Arrange (InMemory baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivanaUsluga, rezultat.Usluga!.Naziv);
    }

    [TestMethod]
    [DynamicData(nameof(TerminRepositoryPodaci.NepostojeciTermini), typeof(TerminRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetByIdAsync_TerminNePostoji_VracaNull(int id)
    {
        // Arrange (InMemory baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNull(rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(TerminRepositoryPodaci.SviTermini), typeof(TerminRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task GetAllAsync_BazaImaTermine_VracaSveTermine(int[] ocekivaniIds)
    {
        // Arrange (InMemory baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetAllAsync();

        // Assert
        CollectionAssert.AreEquivalent(ocekivaniIds, rezultat.Select(t => t.TerminId).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(TerminRepositoryPodaci.SviTermini), typeof(TerminRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetAllAsync_BazaImaTermine_UcitavaDoktorePacijenteIUsluge(int[] ocekivaniIds)
    {
        // Arrange (InMemory baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetAllAsync();

        // Assert
        Assert.IsTrue(rezultat.All(t => t.Doktor != null && t.Pacijent != null && t.Usluga != null));
    }

    // ─── DODAVANJE, IZMJENA, BRISANJE ─────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(TerminRepositoryPodaci.NoviTermini), typeof(TerminRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task AddAsync_NoviTermin_OznacavaGaZaDodavanje(int id, string doktorId, string pacijentId, int uslugaId,
        int pomakDana, int sat, Status status)
    {
        // Arrange
        var termin = TerminRepositoryPodaci.NapraviTermin(id, doktorId, pacijentId, uslugaId, pomakDana, sat, status);

        // Act
        await _repozitorij.AddAsync(termin);

        // Assert
        Assert.AreEqual(EntityState.Added, _context.Entry(termin).State);
    }

    [TestMethod]
    [DynamicData(nameof(TerminRepositoryPodaci.NoviTermini), typeof(TerminRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task AddAsync_BezSnimanja_TerminNijeUBazi(int id, string doktorId, string pacijentId, int uslugaId,
        int pomakDana, int sat, Status status)
    {
        // Arrange
        var termin = TerminRepositoryPodaci.NapraviTermin(id, doktorId, pacijentId, uslugaId, pomakDana, sat, status);

        // Act
        await _repozitorij.AddAsync(termin);

        // Assert
        using var provjera = TerminRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNull(await provjera.Termin.FindAsync(id));
    }

    [TestMethod]
    [DynamicData(nameof(TerminRepositoryPodaci.Izmjene), typeof(TerminRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Update_PostojeciTermin_OznacavaGaKaoIzmijenjen(int id, Status noviStatus)
    {
        // Arrange
        var termin = (await _repozitorij.GetByIdAsync(id))!;
        termin.Status = noviStatus;

        // Act
        _repozitorij.Update(termin);

        // Assert
        Assert.AreEqual(EntityState.Modified, _context.Entry(termin).State);
    }

    [TestMethod]
    [DynamicData(nameof(TerminRepositoryPodaci.Brisanje), typeof(TerminRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Delete_PostojeciTermin_OznacavaGaZaBrisanje(int id)
    {
        // Arrange
        var termin = (await _repozitorij.GetByIdAsync(id))!;

        // Act
        _repozitorij.Delete(termin);

        // Assert
        Assert.AreEqual(EntityState.Deleted, _context.Entry(termin).State);
    }

    // ─── SNIMANJE ─────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(TerminRepositoryPodaci.Snimanje), typeof(TerminRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task SaveChangesAsync_NakonDodavanja_VracaBrojPromjena(int id, string doktorId, string pacijentId,
        int uslugaId, int pomakDana, int sat, Status status, int ocekivaniBroj)
    {
        // Arrange
        await _repozitorij.AddAsync(TerminRepositoryPodaci.NapraviTermin(id, doktorId, pacijentId, uslugaId, pomakDana, sat, status));

        // Act
        var rezultat = await _repozitorij.SaveChangesAsync();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(TerminRepositoryPodaci.NoviTermini), typeof(TerminRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonDodavanja_TerminJeUBazi(int id, string doktorId, string pacijentId, int uslugaId,
        int pomakDana, int sat, Status status)
    {
        // Arrange
        await _repozitorij.AddAsync(TerminRepositoryPodaci.NapraviTermin(id, doktorId, pacijentId, uslugaId, pomakDana, sat, status));

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = TerminRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNotNull(await provjera.Termin.FindAsync(id));
    }

    [TestMethod]
    [DynamicData(nameof(TerminRepositoryPodaci.Izmjene), typeof(TerminRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonIzmjene_BazaImaNoviStatus(int id, Status noviStatus)
    {
        // Arrange
        var termin = (await _repozitorij.GetByIdAsync(id))!;
        termin.Status = noviStatus;
        _repozitorij.Update(termin);

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = TerminRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.AreEqual(noviStatus, (await provjera.Termin.FindAsync(id))!.Status);
    }

    [TestMethod]
    [DynamicData(nameof(TerminRepositoryPodaci.Brisanje), typeof(TerminRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonBrisanja_TerminNijeUBazi(int id)
    {
        // Arrange
        _repozitorij.Delete((await _repozitorij.GetByIdAsync(id))!);

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = TerminRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNull(await provjera.Termin.FindAsync(id));
    }
}
