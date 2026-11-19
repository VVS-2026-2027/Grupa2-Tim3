using System.Xml.Linq;
using HealthZone.Data;
using HealthZone.Models;
using HealthZone.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za NalazRepositoryTests ───────────
public static class NalazRepositoryPodaci
{
    // ── pomoćne metode ──

    public static ApplicationDbContext NovaBaza(string imeBaze) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(imeBaze).Options);

    public static Nalaz NapraviNalaz(int id, int terminId, string pacijentId, string opis, string dijagnoza,
        string terapija) => new()
    {
        NalazId = id, TerminID = terminId, PacijentID = pacijentId, Opis = opis, Dijagnoza = dijagnoza, Terapija = terapija
    };

    // Upisuje pacijente, usluge, termine i nalaze iz XML "baze" u InMemory bazu zadanog imena.
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
        kontekst.Termin.AddRange(baza.Descendants("termin").Select(t => new Termin
        {
            TerminId = (int)t.Attribute("id")!,
            DoktorID = (string)t.Attribute("doktor")!,
            PacijentID = (string)t.Attribute("pacijent")!,
            UslugaID = (int)t.Attribute("usluga")!,
            Datum = DateTime.Today.AddDays((int)t.Attribute("pomakDana")!)
        }));
        kontekst.Nalaz.AddRange(baza.Descendants("nalaz").Select(n => NapraviNalaz(
            (int)n.Attribute("id")!,
            (int)n.Attribute("termin")!,
            (string)n.Attribute("pacijent")!,
            (string)n.Attribute("opis")!,
            (string)n.Attribute("dijagnoza")!,
            (string)n.Attribute("terapija")!)));
        kontekst.SaveChanges();
    }

    // ── izvori podataka ──

    public static IEnumerable<object[]> SviNalazi() =>
        XmlLoader.Ucitaj("NalazRepository_baza.xml", "baza").Select(b => new object[]
        {
            b.Descendants("nalaz").Select(n => (int)n.Attribute("id")!).ToArray()
        });

    public static IEnumerable<object[]> PostojeciNalazi() =>
        CsvLoader.Ucitaj("NalazRepository_postojeci.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> NepostojeciNalazi() =>
        CsvLoader.Ucitaj("NalazRepository_nepostojeci.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> NoviNalazi() =>
        CsvLoader.Ucitaj("NalazRepository_novi.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), r[2], r[3], r[4], r[5] });

    public static IEnumerable<object[]> Izmjene() =>
        CsvLoader.Ucitaj("NalazRepository_izmjena.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> Brisanje() =>
        CsvLoader.Ucitaj("NalazRepository_brisanje.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("NalazRepository_snimanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), r[2], r[3], r[4], r[5], int.Parse(r[6]) });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class NalazRepositoryTests
{
    private static XElement _baza = null!;   // samo za čitanje

    private string _imeBaze = null!;
    private ApplicationDbContext _context = null!;
    private NalazRepository _repozitorij = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // XML "baza" se čita jednom; svaki test je upisuje u svoju InMemory bazu.
        _baza = XmlLoader.Ucitaj("NalazRepository_baza.xml", "baza").Single();
    }

    [TestInitialize]
    public void Setup()
    {
        _imeBaze = Guid.NewGuid().ToString();
        NalazRepositoryPodaci.NapuniBazu(_imeBaze, _baza);
        _context = NalazRepositoryPodaci.NovaBaza(_imeBaze);
        _repozitorij = new NalazRepository(_context);
    }

    [TestCleanup]
    public void Cleanup() => _context.Dispose();

    // ─── ČITANJE ──────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NalazRepositoryPodaci.PostojeciNalazi), typeof(NalazRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task GetByIdAsync_NalazPostoji_UcitavaPacijenta(int id, string ocekivaniPacijent, string ocekivanaUsluga)
    {
        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivaniPacijent, rezultat.Pacijent!.Id);
    }

    [TestMethod]
    [DynamicData(nameof(NalazRepositoryPodaci.PostojeciNalazi), typeof(NalazRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetByIdAsync_NalazPostoji_UcitavaTerminIUslugu(int id, string ocekivaniPacijent, string ocekivanaUsluga)
    {
        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivanaUsluga, rezultat.Termin!.Usluga!.Naziv);
    }

    [TestMethod]
    [DynamicData(nameof(NalazRepositoryPodaci.NepostojeciNalazi), typeof(NalazRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetByIdAsync_NalazNePostoji_VracaNull(int id)
    {
        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNull(rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(NalazRepositoryPodaci.SviNalazi), typeof(NalazRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetAllAsync_BazaImaNalaze_VracaSveNalaze(int[] ocekivaniIds)
    {
        // Act
        var rezultat = await _repozitorij.GetAllAsync();

        // Assert
        CollectionAssert.AreEquivalent(ocekivaniIds, rezultat.Select(n => n.NalazId).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(NalazRepositoryPodaci.SviNalazi), typeof(NalazRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetAllAsync_BazaImaNalaze_UcitavaUslugeSvihTermina(int[] ocekivaniIds)
    {
        // Act
        var rezultat = await _repozitorij.GetAllAsync();

        // Assert
        Assert.IsTrue(rezultat.All(n => n.Pacijent != null && n.Termin?.Usluga != null));
    }

    // ─── DODAVANJE, IZMJENA, BRISANJE ─────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NalazRepositoryPodaci.NoviNalazi), typeof(NalazRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task AddAsync_NoviNalaz_OznacavaGaZaDodavanje(int id, int terminId, string pacijentId, string opis,
        string dijagnoza, string terapija)
    {
        // Arrange
        var nalaz = NalazRepositoryPodaci.NapraviNalaz(id, terminId, pacijentId, opis, dijagnoza, terapija);

        // Act
        await _repozitorij.AddAsync(nalaz);

        // Assert
        Assert.AreEqual(EntityState.Added, _context.Entry(nalaz).State);
    }

    [TestMethod]
    [DynamicData(nameof(NalazRepositoryPodaci.NoviNalazi), typeof(NalazRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_BezSnimanja_NalazNijeUBazi(int id, int terminId, string pacijentId, string opis,
        string dijagnoza, string terapija)
    {
        // Arrange
        var nalaz = NalazRepositoryPodaci.NapraviNalaz(id, terminId, pacijentId, opis, dijagnoza, terapija);

        // Act
        await _repozitorij.AddAsync(nalaz);

        // Assert
        using var provjera = NalazRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNull(await provjera.Nalaz.FindAsync(id));
    }

    [TestMethod]
    [DynamicData(nameof(NalazRepositoryPodaci.Izmjene), typeof(NalazRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Update_PostojeciNalaz_OznacavaGaKaoIzmijenjen(int id, string novaDijagnoza)
    {
        // Arrange
        var nalaz = (await _repozitorij.GetByIdAsync(id))!;
        nalaz.Dijagnoza = novaDijagnoza;

        // Act
        _repozitorij.Update(nalaz);

        // Assert
        Assert.AreEqual(EntityState.Modified, _context.Entry(nalaz).State);
    }

    [TestMethod]
    [DynamicData(nameof(NalazRepositoryPodaci.Brisanje), typeof(NalazRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Delete_PostojeciNalaz_OznacavaGaZaBrisanje(int id)
    {
        // Arrange
        var nalaz = (await _repozitorij.GetByIdAsync(id))!;

        // Act
        _repozitorij.Delete(nalaz);

        // Assert
        Assert.AreEqual(EntityState.Deleted, _context.Entry(nalaz).State);
    }

    // ─── SNIMANJE ─────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NalazRepositoryPodaci.Snimanje), typeof(NalazRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task SaveChangesAsync_NakonDodavanja_VracaBrojPromjena(int id, int terminId, string pacijentId,
        string opis, string dijagnoza, string terapija, int ocekivaniBroj)
    {
        // Arrange
        await _repozitorij.AddAsync(NalazRepositoryPodaci.NapraviNalaz(id, terminId, pacijentId, opis, dijagnoza, terapija));

        // Act
        var rezultat = await _repozitorij.SaveChangesAsync();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(NalazRepositoryPodaci.NoviNalazi), typeof(NalazRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonDodavanja_NalazJeUBazi(int id, int terminId, string pacijentId, string opis,
        string dijagnoza, string terapija)
    {
        // Arrange
        await _repozitorij.AddAsync(NalazRepositoryPodaci.NapraviNalaz(id, terminId, pacijentId, opis, dijagnoza, terapija));

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = NalazRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNotNull(await provjera.Nalaz.FindAsync(id));
    }

    [TestMethod]
    [DynamicData(nameof(NalazRepositoryPodaci.Izmjene), typeof(NalazRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonIzmjene_BazaImaNovuDijagnozu(int id, string novaDijagnoza)
    {
        // Arrange
        var nalaz = (await _repozitorij.GetByIdAsync(id))!;
        nalaz.Dijagnoza = novaDijagnoza;
        _repozitorij.Update(nalaz);

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = NalazRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.AreEqual(novaDijagnoza, (await provjera.Nalaz.FindAsync(id))!.Dijagnoza);
    }

    [TestMethod]
    [DynamicData(nameof(NalazRepositoryPodaci.Brisanje), typeof(NalazRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonBrisanja_NalazNijeUBazi(int id)
    {
        // Arrange
        _repozitorij.Delete((await _repozitorij.GetByIdAsync(id))!);

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = NalazRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNull(await provjera.Nalaz.FindAsync(id));
    }
}
