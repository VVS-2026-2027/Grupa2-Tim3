using System.Xml.Linq;
using HealthZone.Data;
using HealthZone.Models;
using HealthZone.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za KorisnikNaListiRepositoryTests ─
public static class KorisnikNaListiRepositoryPodaci
{
    public static IEnumerable<object[]> ZapisiCsv() =>
        CsvLoader.Ucitaj("Repozitorij_podaci.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1], int.Parse(r[2]) });

    public static IEnumerable<object[]> ZapisiXml() =>
        XmlLoader.Ucitaj("Repozitorij_zapisi.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("doktorId")!,
                     (string)x.Element("korisnikId")!,
                     (int)x.Element("ocekivanoDodano")!
                 });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class KorisnikNaListiRepositoryTests
{
    private static List<XElement> _podaciXml = null!;

    private ApplicationDbContext _context = null!;
    private KorisnikNaListiRepository _repo = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _podaciXml = XmlLoader.Ucitaj("Repozitorij_zapisi.xml", "scenarij").ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(opcije);
        _repo = new KorisnikNaListiRepository(_context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiRepositoryPodaci.ZapisiCsv), typeof(KorisnikNaListiRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Fake"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task AddAsync_I_GetByIdAsync_SpremaI_DohvataStavku(int id, string naziv, int ocekivano)
    {
        // Arrange
        var stavka = new KorisnikNaListi
        {
            KorisnikNaListiID = id,
            Datum = DateTime.Now,
            ListaID = 1,
            KorisnikID = "k1"
        };

        // Act
        await _repo.AddAsync(stavka);
        await _repo.SaveChangesAsync();
        var dohvaceno = await _repo.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(dohvaceno);
        Assert.AreEqual(id, dohvaceno.KorisnikNaListiID);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiRepositoryPodaci.ZapisiCsv), typeof(KorisnikNaListiRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Fake"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetAllAsync_PostojeStavke_VracaKolekciju(int id, string naziv, int ocekivano)
    {
        // Arrange
        var s1 = new KorisnikNaListi { KorisnikNaListiID = id, Datum = DateTime.Now, ListaID = 1, KorisnikID = "k1" };
        var s2 = new KorisnikNaListi { KorisnikNaListiID = id + 10, Datum = DateTime.Now, ListaID = 1, KorisnikID = "k2" };
        await _repo.AddAsync(s1);
        await _repo.AddAsync(s2);
        await _repo.SaveChangesAsync();

        // Act
        var sve = await _repo.GetAllAsync();

        // Assert
        Assert.AreEqual(2, sve.Count());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiRepositoryPodaci.ZapisiXml), typeof(KorisnikNaListiRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Fake"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Update_I_Delete_AzuriraIObriseStavku(string doktorId, string korisnikId, int ocekivano)
    {
        // Arrange
        var stavka = new KorisnikNaListi { KorisnikNaListiID = 5, Datum = DateTime.Now, ListaID = 1, KorisnikID = korisnikId };
        await _repo.AddAsync(stavka);
        await _repo.SaveChangesAsync();

        // Act - Update
        stavka.ListaID = 2;
        _repo.Update(stavka);
        await _repo.SaveChangesAsync();
        var azurirano = await _repo.GetByIdAsync(5);

        // Assert - Update
        Assert.AreEqual(2, azurirano?.ListaID);

        // Act - Delete
        _repo.Delete(azurirano!);
        await _repo.SaveChangesAsync();
        var obrisano = await _repo.GetByIdAsync(5);

        // Assert - Delete
        Assert.IsNull(obrisano);
    }
}
