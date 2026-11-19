using System.Xml.Linq;
using HealthZone.Data;
using HealthZone.Models;
using HealthZone.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za KorisnikRepositoryTests ────────
public static class KorisnikRepositoryPodaci
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

    public static Korisnik NapraviKorisnika(string id, string ime, string prezime, string email) => new()
    {
        Id = id, Ime = ime, Prezime = prezime, Email = email, UserName = email
    };

    // Upisuje korisnike iz XML "baze" u InMemory bazu zadanog imena.
    public static void NapuniBazu(string imeBaze, XElement baza)
    {
        using var kontekst = NovaBaza(imeBaze);
        kontekst.Users.AddRange(baza.Elements("korisnik").Select(NapraviKorisnika));
        kontekst.SaveChanges();
    }

    // ── izvori podataka ──

    public static IEnumerable<object[]> SviKorisnici() =>
        XmlLoader.Ucitaj("KorisnikRepository_baza.xml", "baza").Select(b => new object[]
        {
            b.Elements("korisnik").Select(k => (string)k.Attribute("id")!).ToArray()
        });

    public static IEnumerable<object[]> PostojeciKorisnici() =>
        CsvLoader.Ucitaj("KorisnikRepository_postojeci.csv").Select(r => new object[] { r[0], r[1] });

    public static IEnumerable<object[]> NepostojeciKorisnici() =>
        CsvLoader.Ucitaj("KorisnikRepository_nepostojeci.csv").Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> NoviKorisnici() =>
        CsvLoader.Ucitaj("KorisnikRepository_novi.csv").Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> Izmjene() =>
        CsvLoader.Ucitaj("KorisnikRepository_izmjena.csv").Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> Brisanje() =>
        CsvLoader.Ucitaj("KorisnikRepository_brisanje.csv").Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("KorisnikRepository_snimanje.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], r[3], int.Parse(r[4]) });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class KorisnikRepositoryTests
{
    private static XElement _baza = null!;   // samo za čitanje

    private string _imeBaze = null!;
    private ApplicationDbContext _context = null!;
    private KorisnikRepository _repozitorij = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // XML "baza" korisnika se čita jednom; svaki test je upisuje u svoju InMemory bazu.
        _baza = XmlLoader.Ucitaj("KorisnikRepository_baza.xml", "baza").Single();
    }

    [TestInitialize]
    public void Setup()
    {
        _imeBaze = Guid.NewGuid().ToString();
        KorisnikRepositoryPodaci.NapuniBazu(_imeBaze, _baza);
        _context = KorisnikRepositoryPodaci.NovaBaza(_imeBaze);
        _repozitorij = new KorisnikRepository(_context);
    }

    [TestCleanup]
    public void Cleanup() => _context.Dispose();

    // ─── ČITANJE ──────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikRepositoryPodaci.PostojeciKorisnici), typeof(KorisnikRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task GetByIdAsync_KorisnikPostoji_VracaKorisnika(string id, string ocekivanoIme)
    {
        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivanoIme, rezultat.Ime);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikRepositoryPodaci.NepostojeciKorisnici), typeof(KorisnikRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetByIdAsync_KorisnikNePostoji_VracaNull(string id)
    {
        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNull(rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikRepositoryPodaci.SviKorisnici), typeof(KorisnikRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetAllAsync_BazaImaKorisnike_VracaSveKorisnike(string[] ocekivaniIds)
    {
        // Act
        var rezultat = await _repozitorij.GetAllAsync();

        // Assert
        CollectionAssert.AreEquivalent(ocekivaniIds, rezultat.Select(k => k.Id).ToArray());
    }

    // ─── DODAVANJE ────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikRepositoryPodaci.NoviKorisnici), typeof(KorisnikRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task AddAsync_NoviKorisnik_OznacavaGaZaDodavanje(string id, string ime, string prezime, string email)
    {
        // Arrange
        var korisnik = KorisnikRepositoryPodaci.NapraviKorisnika(id, ime, prezime, email);

        // Act
        await _repozitorij.AddAsync(korisnik);

        // Assert
        Assert.AreEqual(EntityState.Added, _context.Entry(korisnik).State);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikRepositoryPodaci.NoviKorisnici), typeof(KorisnikRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_BezSnimanja_KorisnikNijeUBazi(string id, string ime, string prezime, string email)
    {
        // Arrange
        var korisnik = KorisnikRepositoryPodaci.NapraviKorisnika(id, ime, prezime, email);

        // Act
        await _repozitorij.AddAsync(korisnik);

        // Assert
        using var provjera = KorisnikRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNull(await provjera.Users.FindAsync(id));
    }

    // ─── IZMJENA I BRISANJE ───────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikRepositoryPodaci.Izmjene), typeof(KorisnikRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Update_PostojeciKorisnik_OznacavaGaKaoIzmijenjen(string id, string novoIme, string novoPrezime)
    {
        // Arrange
        var korisnik = (await _repozitorij.GetByIdAsync(id))!;
        korisnik.Ime = novoIme;

        // Act
        _repozitorij.Update(korisnik);

        // Assert
        Assert.AreEqual(EntityState.Modified, _context.Entry(korisnik).State);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikRepositoryPodaci.Brisanje), typeof(KorisnikRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Delete_PostojeciKorisnik_OznacavaGaZaBrisanje(string id)
    {
        // Arrange
        var korisnik = (await _repozitorij.GetByIdAsync(id))!;

        // Act
        _repozitorij.Delete(korisnik);

        // Assert
        Assert.AreEqual(EntityState.Deleted, _context.Entry(korisnik).State);
    }

    // ─── SNIMANJE ─────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikRepositoryPodaci.Snimanje), typeof(KorisnikRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task SaveChangesAsync_NakonDodavanja_VracaBrojPromjena(string id, string ime, string prezime,
        string email, int ocekivaniBroj)
    {
        // Arrange
        await _repozitorij.AddAsync(KorisnikRepositoryPodaci.NapraviKorisnika(id, ime, prezime, email));

        // Act
        var rezultat = await _repozitorij.SaveChangesAsync();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikRepositoryPodaci.NoviKorisnici), typeof(KorisnikRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonDodavanja_KorisnikJeUBazi(string id, string ime, string prezime, string email)
    {
        // Arrange
        await _repozitorij.AddAsync(KorisnikRepositoryPodaci.NapraviKorisnika(id, ime, prezime, email));

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = KorisnikRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNotNull(await provjera.Users.FindAsync(id));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikRepositoryPodaci.Izmjene), typeof(KorisnikRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonIzmjene_BazaImaNovePodatke(string id, string novoIme, string novoPrezime)
    {
        // Arrange
        var korisnik = (await _repozitorij.GetByIdAsync(id))!;
        korisnik.Ime = novoIme;
        korisnik.Prezime = novoPrezime;
        _repozitorij.Update(korisnik);

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = KorisnikRepositoryPodaci.NovaBaza(_imeBaze);
        var izBaze = (await provjera.Users.FindAsync(id))!;
        Assert.AreEqual(novoIme, izBaze.Ime);
        Assert.AreEqual(novoPrezime, izBaze.Prezime);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikRepositoryPodaci.Brisanje), typeof(KorisnikRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonBrisanja_KorisnikNijeUBazi(string id)
    {
        // Arrange
        _repozitorij.Delete((await _repozitorij.GetByIdAsync(id))!);

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = KorisnikRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNull(await provjera.Users.FindAsync(id));
    }
}
