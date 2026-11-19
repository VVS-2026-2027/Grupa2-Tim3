using System.Globalization;
using System.Xml.Linq;
using HealthZone.Data;
using HealthZone.Models;
using HealthZone.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za MedicinskaUslugaRepositoryTests ─
public static class MedicinskaUslugaRepositoryPodaci
{
    // ── pomoćne metode ──

    public static ApplicationDbContext NovaBaza(string imeBaze) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(imeBaze).Options);

    public static decimal Decimalni(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);

    public static MedicinskaUsluga NapraviUslugu(XElement x) => new()
    {
        UslugaId = (int)x.Attribute("id")!,
        Naziv = (string)x.Attribute("naziv")!,
        Vrsta = Enum.Parse<Specijalizacija>((string)x.Attribute("vrsta")!),
        Opis = (string)x.Attribute("opis")!,
        Cijena = Decimalni((string)x.Attribute("cijena")!)
    };

    public static MedicinskaUsluga NapraviUslugu(int id, string naziv, Specijalizacija vrsta, string opis, decimal cijena) => new()
    {
        UslugaId = id, Naziv = naziv, Vrsta = vrsta, Opis = opis, Cijena = cijena
    };

    // Upisuje usluge iz XML "baze" u InMemory bazu zadanog imena (posebna instanca konteksta).
    public static void NapuniBazu(string imeBaze, XElement baza)
    {
        using var kontekst = NovaBaza(imeBaze);
        kontekst.MedicinskaUsluga.AddRange(baza.Elements("usluga").Select(NapraviUslugu));
        kontekst.SaveChanges();
    }

    // ── izvori podataka (XML) ──

    public static IEnumerable<object[]> SveUsluge() =>
        XmlLoader.Ucitaj("MedicinskaUslugaRepository_baza.xml", "baza").Select(b => new object[]
        {
            b.Elements("usluga").Select(u => (int)u.Attribute("id")!).ToArray()
        });

    // ── izvori podataka (CSV) ──

    public static IEnumerable<object[]> PostojeceUsluge() =>
        CsvLoader.Ucitaj("MedicinskaUslugaRepository_postojeca.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> NepostojeceUsluge() =>
        CsvLoader.Ucitaj("MedicinskaUslugaRepository_nepostojeca.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> NoveUsluge() =>
        CsvLoader.Ucitaj("MedicinskaUslugaRepository_nova.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1], Enum.Parse<Specijalizacija>(r[2]), r[3], Decimalni(r[4]) });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("MedicinskaUslugaRepository_snimanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1], Enum.Parse<Specijalizacija>(r[2]), r[3], Decimalni(r[4]), int.Parse(r[5]) });

    public static IEnumerable<object[]> Izmjene() =>
        CsvLoader.Ucitaj("MedicinskaUslugaRepository_izmjena.csv").Select(r => new object[] { int.Parse(r[0]), r[1], Decimalni(r[2]) });

    public static IEnumerable<object[]> Brisanje() =>
        CsvLoader.Ucitaj("MedicinskaUslugaRepository_brisanje.csv").Select(r => new object[] { int.Parse(r[0]) });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class MedicinskaUslugaRepositoryTests
{
    private static XElement _baza = null!;   // samo za čitanje

    private string _imeBaze = null!;
    private ApplicationDbContext _context = null!;
    private MedicinskaUslugaRepository _repozitorij = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // XML "baza" usluga se čita jednom; svaki test je upisuje u svoju InMemory bazu.
        _baza = XmlLoader.Ucitaj("MedicinskaUslugaRepository_baza.xml", "baza").Single();
    }

    [TestInitialize]
    public void Setup()
    {
        // Svaki test dobija svoju praznu bazu (novo ime), pa su testovi nezavisni.
        _imeBaze = Guid.NewGuid().ToString();
        MedicinskaUslugaRepositoryPodaci.NapuniBazu(_imeBaze, _baza);
        _context = MedicinskaUslugaRepositoryPodaci.NovaBaza(_imeBaze);
        _repozitorij = new MedicinskaUslugaRepository(_context);
    }

    [TestCleanup]
    public void Cleanup() => _context.Dispose();

    // ─── ČITANJE ──────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaRepositoryPodaci.PostojeceUsluge), typeof(MedicinskaUslugaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task GetByIdAsync_UslugaPostoji_VracaUslugu(int id, string ocekivaniNaziv)
    {
        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivaniNaziv, rezultat.Naziv);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaRepositoryPodaci.NepostojeceUsluge), typeof(MedicinskaUslugaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetByIdAsync_UslugaNePostoji_VracaNull(int id)
    {
        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNull(rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaRepositoryPodaci.SveUsluge), typeof(MedicinskaUslugaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task GetAllAsync_BazaImaUsluge_VracaSveUsluge(int[] ocekivaniId)
    {
        // Act
        var rezultat = await _repozitorij.GetAllAsync();

        // Assert
        CollectionAssert.AreEquivalent(ocekivaniId, rezultat.Select(u => u.UslugaId).ToArray());
    }

    // ─── DODAVANJE ────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaRepositoryPodaci.NoveUsluge), typeof(MedicinskaUslugaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task AddAsync_NovaUsluga_OznacavaJeZaDodavanje(int id, string naziv, Specijalizacija vrsta, string opis, decimal cijena)
    {
        // Arrange
        var usluga = MedicinskaUslugaRepositoryPodaci.NapraviUslugu(id, naziv, vrsta, opis, cijena);

        // Act
        await _repozitorij.AddAsync(usluga);

        // Assert
        Assert.AreEqual(EntityState.Added, _context.Entry(usluga).State);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaRepositoryPodaci.NoveUsluge), typeof(MedicinskaUslugaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task AddAsync_BezSnimanja_UslugaNijeUBazi(int id, string naziv, Specijalizacija vrsta, string opis, decimal cijena)
    {
        // Arrange
        var usluga = MedicinskaUslugaRepositoryPodaci.NapraviUslugu(id, naziv, vrsta, opis, cijena);

        // Act
        await _repozitorij.AddAsync(usluga);

        // Assert
        using var provjera = MedicinskaUslugaRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNull(await provjera.MedicinskaUsluga.FindAsync(id));
    }

    // ─── IZMJENA I BRISANJE ───────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaRepositoryPodaci.Izmjene), typeof(MedicinskaUslugaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Update_PostojecaUsluga_OznacavaJeKaoIzmijenjenu(int id, string noviNaziv, decimal novaCijena)
    {
        // Arrange
        var usluga = (await _repozitorij.GetByIdAsync(id))!;
        usluga.Naziv = noviNaziv;

        // Act
        _repozitorij.Update(usluga);

        // Assert
        Assert.AreEqual(EntityState.Modified, _context.Entry(usluga).State);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaRepositoryPodaci.Brisanje), typeof(MedicinskaUslugaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Delete_PostojecaUsluga_OznacavaJeZaBrisanje(int id)
    {
        // Arrange
        var usluga = (await _repozitorij.GetByIdAsync(id))!;

        // Act
        _repozitorij.Delete(usluga);

        // Assert
        Assert.AreEqual(EntityState.Deleted, _context.Entry(usluga).State);
    }

    // ─── SNIMANJE ─────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaRepositoryPodaci.Snimanje), typeof(MedicinskaUslugaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task SaveChangesAsync_NakonDodavanja_VracaBrojPromjena(int id, string naziv, Specijalizacija vrsta, string opis,
        decimal cijena, int ocekivaniBroj)
    {
        // Arrange
        await _repozitorij.AddAsync(MedicinskaUslugaRepositoryPodaci.NapraviUslugu(id, naziv, vrsta, opis, cijena));

        // Act
        var rezultat = await _repozitorij.SaveChangesAsync();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaRepositoryPodaci.NoveUsluge), typeof(MedicinskaUslugaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public async Task SaveChangesAsync_NakonDodavanja_UslugaJeUBazi(int id, string naziv, Specijalizacija vrsta, string opis, decimal cijena)
    {
        // Arrange
        await _repozitorij.AddAsync(MedicinskaUslugaRepositoryPodaci.NapraviUslugu(id, naziv, vrsta, opis, cijena));

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = MedicinskaUslugaRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNotNull(await provjera.MedicinskaUsluga.FindAsync(id));
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaRepositoryPodaci.Izmjene), typeof(MedicinskaUslugaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonIzmjene_BazaImaNovePodatke(int id, string noviNaziv, decimal novaCijena)
    {
        // Arrange
        var usluga = (await _repozitorij.GetByIdAsync(id))!;
        usluga.Naziv = noviNaziv;
        usluga.Cijena = novaCijena;
        _repozitorij.Update(usluga);

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = MedicinskaUslugaRepositoryPodaci.NovaBaza(_imeBaze);
        var izBaze = (await provjera.MedicinskaUsluga.FindAsync(id))!;
        Assert.AreEqual(noviNaziv, izBaze.Naziv);
        Assert.AreEqual(novaCijena, izBaze.Cijena);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaRepositoryPodaci.Brisanje), typeof(MedicinskaUslugaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonBrisanja_UslugaNijeUBazi(int id)
    {
        // Arrange
        _repozitorij.Delete((await _repozitorij.GetByIdAsync(id))!);

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = MedicinskaUslugaRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNull(await provjera.MedicinskaUsluga.FindAsync(id));
    }
}
