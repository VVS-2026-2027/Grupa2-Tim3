using System.Xml.Linq;
using HealthZone.Data;
using HealthZone.Models;
using HealthZone.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za KorisnikNaListiRepositoryTests ─
public static class KorisnikNaListiRepositoryPodaci
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

    public static ListaCekanja NapraviListu(XElement x) => new()
    {
        ListaId = (int)x.Attribute("id")!,
        DoktorID = (string)x.Attribute("doktorId")!
    };

    public static KorisnikNaListi NapraviStavku(XElement x) =>
        NapraviStavku((int)x.Attribute("id")!, (int)x.Attribute("listaId")!, (string)x.Attribute("korisnikId")!,
                      (int)x.Attribute("pomakDana")!);

    public static KorisnikNaListi NapraviStavku(int id, int listaId, string korisnikId, int pomakDana) => new()
    {
        KorisnikNaListiID = id,
        ListaID = listaId,
        KorisnikID = korisnikId,
        Datum = DateTime.Today.AddDays(pomakDana)
    };

    // Upisuje korisnike, liste i stavke iz XML "baze" u InMemory bazu, u posebnoj instanci konteksta.
    // Repozitorij koristi Include (pacijent, lista i doktor liste), pa povezani zapisi moraju postojati.
    public static void NapuniBazu(string imeBaze, XElement baza)
    {
        using var kontekst = NovaBaza(imeBaze);
        kontekst.Users.AddRange(baza.Elements("korisnik").Select(NapraviKorisnika));
        kontekst.ListaCekanja.AddRange(baza.Elements("lista").Select(NapraviListu));
        kontekst.KorisnikNaListi.AddRange(baza.Elements("stavka").Select(NapraviStavku));
        kontekst.SaveChanges();
    }

    // ── izvori podataka ──

    public static IEnumerable<object[]> SveStavke() =>
        XmlLoader.Ucitaj("KorisnikNaListiRepository_baza.xml", "baza").Select(b => new object[]
        {
            b.Elements("stavka").Select(s => (int)s.Attribute("id")!).ToArray()
        });

    private static IEnumerable<string[]> Postojece() => CsvLoader.Ucitaj("KorisnikNaListiRepository_postojece.csv");

    public static IEnumerable<object[]> PostojeceStavke() => Postojece().Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> PacijentiStavki() => Postojece().Select(r => new object[] { int.Parse(r[0]), r[2] });

    public static IEnumerable<object[]> DoktoriStavki() => Postojece().Select(r => new object[] { int.Parse(r[0]), r[3] });

    public static IEnumerable<object[]> NepostojeceStavke() =>
        CsvLoader.Ucitaj("KorisnikNaListiRepository_nepostojece.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> NoveStavke() =>
        CsvLoader.Ucitaj("KorisnikNaListiRepository_nove.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), r[2], int.Parse(r[3]) });

    public static IEnumerable<object[]> Izmjene() =>
        CsvLoader.Ucitaj("KorisnikNaListiRepository_izmjena.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1], int.Parse(r[2]) });

    public static IEnumerable<object[]> Brisanje() =>
        CsvLoader.Ucitaj("KorisnikNaListiRepository_brisanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), r[2] });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("KorisnikNaListiRepository_snimanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), r[2], int.Parse(r[3]), int.Parse(r[4]) });
}

// ─── Testovi ────────────────────────────────────────────────────────────
// Repozitorij radi nad EF Core InMemory bazom (Fake): svaki test dobija svoju bazu, napunjenu iz XML-a.
// Stanje nakon snimanja se provjerava u novoj instanci konteksta, pa test vidi šta je stvarno upisano.
[TestClass]
public class KorisnikNaListiRepositoryTests
{
    private static XElement _baza = null!;   // samo za čitanje

    private string _imeBaze = null!;
    private ApplicationDbContext _context = null!;
    private KorisnikNaListiRepository _repozitorij = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _baza = XmlLoader.Ucitaj("KorisnikNaListiRepository_baza.xml", "baza").Single();
    }

    [TestInitialize]
    public void Setup()
    {
        _imeBaze = Guid.NewGuid().ToString();
        KorisnikNaListiRepositoryPodaci.NapuniBazu(_imeBaze, _baza);
        _context = KorisnikNaListiRepositoryPodaci.NovaBaza(_imeBaze);
        _repozitorij = new KorisnikNaListiRepository(_context);
    }

    [TestCleanup]
    public void Cleanup() => _context.Dispose();

    // ─── ČITANJE ──────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiRepositoryPodaci.PostojeceStavke), typeof(KorisnikNaListiRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task GetByIdAsync_StavkaPostoji_VracaStavku(int id, string ocekivaniKorisnikId)
    {
        // Arrange (baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivaniKorisnikId, rezultat.KorisnikID);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiRepositoryPodaci.PacijentiStavki), typeof(KorisnikNaListiRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetByIdAsync_StavkaPostoji_UcitavaPacijenta(int id, string ocekivanoImePacijenta)
    {
        // Arrange (baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.AreEqual(ocekivanoImePacijenta, rezultat?.Korisnik?.Ime);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiRepositoryPodaci.DoktoriStavki), typeof(KorisnikNaListiRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetByIdAsync_StavkaPostoji_UcitavaListuSaDoktorom(int id, string ocekivanoImeDoktora)
    {
        // Arrange (baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.AreEqual(ocekivanoImeDoktora, rezultat?.Lista?.Doktor?.Ime);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiRepositoryPodaci.NepostojeceStavke), typeof(KorisnikNaListiRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetByIdAsync_StavkaNePostoji_VracaNull(int id)
    {
        // Arrange (baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetByIdAsync(id);

        // Assert
        Assert.IsNull(rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiRepositoryPodaci.SveStavke), typeof(KorisnikNaListiRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task GetAllAsync_BazaImaStavke_VracaSveStavke(int[] ocekivaniIds)
    {
        // Arrange (baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetAllAsync();

        // Assert
        CollectionAssert.AreEquivalent(ocekivaniIds, rezultat.Select(s => s.KorisnikNaListiID).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiRepositoryPodaci.SveStavke), typeof(KorisnikNaListiRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetAllAsync_BazaImaStavke_UcitavaPacijentaZaSvakuStavku(int[] ocekivaniIds)
    {
        // Arrange (baza je napunjena u Setup)

        // Act
        var rezultat = await _repozitorij.GetAllAsync();

        // Assert
        Assert.AreEqual(ocekivaniIds.Length, rezultat.Count(s => s.Korisnik != null));
    }

    // ─── DODAVANJE ────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiRepositoryPodaci.NoveStavke), typeof(KorisnikNaListiRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_NovaStavka_OznacavaJeZaDodavanje(int id, int listaId, string korisnikId, int pomakDana)
    {
        // Arrange
        var stavka = KorisnikNaListiRepositoryPodaci.NapraviStavku(id, listaId, korisnikId, pomakDana);

        // Act
        await _repozitorij.AddAsync(stavka);

        // Assert
        Assert.AreEqual(EntityState.Added, _context.Entry(stavka).State);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiRepositoryPodaci.NoveStavke), typeof(KorisnikNaListiRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_BezSnimanja_StavkaNijeUBazi(int id, int listaId, string korisnikId, int pomakDana)
    {
        // Arrange
        var stavka = KorisnikNaListiRepositoryPodaci.NapraviStavku(id, listaId, korisnikId, pomakDana);

        // Act
        await _repozitorij.AddAsync(stavka);

        // Assert
        using var provjera = KorisnikNaListiRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNull(await provjera.KorisnikNaListi.FindAsync(id));
    }

    // ─── IZMJENA I BRISANJE ───────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiRepositoryPodaci.Izmjene), typeof(KorisnikNaListiRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Update_PostojecaStavka_OznacavaJeKaoIzmijenjenu(int id, string korisnikId, int novaListaId)
    {
        // Arrange
        var stavka = KorisnikNaListiRepositoryPodaci.NapraviStavku(id, novaListaId, korisnikId, default);

        // Act
        _repozitorij.Update(stavka);

        // Assert
        Assert.AreEqual(EntityState.Modified, _context.Entry(stavka).State);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiRepositoryPodaci.Brisanje), typeof(KorisnikNaListiRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Delete_PostojecaStavka_OznacavaJeZaBrisanje(int id, int listaId, string korisnikId)
    {
        // Arrange
        var stavka = KorisnikNaListiRepositoryPodaci.NapraviStavku(id, listaId, korisnikId, default);

        // Act
        _repozitorij.Delete(stavka);

        // Assert
        Assert.AreEqual(EntityState.Deleted, _context.Entry(stavka).State);
    }

    // ─── SNIMANJE ─────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiRepositoryPodaci.Snimanje), typeof(KorisnikNaListiRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task SaveChangesAsync_NakonDodavanja_VracaBrojPromjena(int id, int listaId, string korisnikId,
        int pomakDana, int ocekivaniBroj)
    {
        // Arrange
        await _repozitorij.AddAsync(KorisnikNaListiRepositoryPodaci.NapraviStavku(id, listaId, korisnikId, pomakDana));

        // Act
        var rezultat = await _repozitorij.SaveChangesAsync();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiRepositoryPodaci.NoveStavke), typeof(KorisnikNaListiRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonDodavanja_StavkaJeUBazi(int id, int listaId, string korisnikId, int pomakDana)
    {
        // Arrange
        await _repozitorij.AddAsync(KorisnikNaListiRepositoryPodaci.NapraviStavku(id, listaId, korisnikId, pomakDana));

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = KorisnikNaListiRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNotNull(await provjera.KorisnikNaListi.FindAsync(id));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiRepositoryPodaci.Izmjene), typeof(KorisnikNaListiRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NakonIzmjene_BazaImaNovuListu(int id, string korisnikId, int novaListaId)
    {
        // Arrange
        _repozitorij.Update(KorisnikNaListiRepositoryPodaci.NapraviStavku(id, novaListaId, korisnikId, default));

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = KorisnikNaListiRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.AreEqual(novaListaId, (await provjera.KorisnikNaListi.FindAsync(id))!.ListaID);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiRepositoryPodaci.Brisanje), typeof(KorisnikNaListiRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task SaveChangesAsync_NakonBrisanja_StavkaNijeUBazi(int id, int listaId, string korisnikId)
    {
        // Arrange
        _repozitorij.Delete(KorisnikNaListiRepositoryPodaci.NapraviStavku(id, listaId, korisnikId, default));

        // Act
        await _repozitorij.SaveChangesAsync();

        // Assert
        using var provjera = KorisnikNaListiRepositoryPodaci.NovaBaza(_imeBaze);
        Assert.IsNull(await provjera.KorisnikNaListi.FindAsync(id));
    }
}
