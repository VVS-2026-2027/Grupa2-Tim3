using System.Xml.Linq;
using HealthZone.Data;
using HealthZone.Models;
using HealthZone.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za ListaCekanjaRepositoryTests ────
public static class ListaCekanjaRepositoryPodaci
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
public class ListaCekanjaRepositoryTests
{
    private static List<XElement> _podaciXml = null!;

    private ApplicationDbContext _context = null!;
    private ListaCekanjaRepository _repo = null!;

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
        _repo = new ListaCekanjaRepository(_context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaRepositoryPodaci.ZapisiCsv), typeof(ListaCekanjaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Fake"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task AddAsync_I_GetByIdAsync_SpremaI_DohvataListu(int id, string naziv, int ocekivano)
    {
        // Arrange
        var lista = new ListaCekanja { ListaId = id, DoktorID = "doc1" };

        // Act
        await _repo.AddAsync(lista);
        await _repo.SaveChangesAsync();
        var dohvaceno = await _repo.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(dohvaceno);
        Assert.AreEqual(id, dohvaceno.ListaId);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaRepositoryPodaci.ZapisiCsv), typeof(ListaCekanjaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Fake"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetAllAsync_PostojeListe_VracaSveListe(int id, string naziv, int ocekivano)
    {
        // Arrange
        var l1 = new ListaCekanja { ListaId = id, DoktorID = "doc1" };
        var l2 = new ListaCekanja { ListaId = id + 10, DoktorID = "doc2" };
        await _repo.AddAsync(l1);
        await _repo.AddAsync(l2);
        await _repo.SaveChangesAsync();

        // Act
        var sve = await _repo.GetAllAsync();

        // Assert
        Assert.AreEqual(2, sve.Count());
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaRepositoryPodaci.ZapisiXml), typeof(ListaCekanjaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Fake"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Update_I_Delete_AzuriraIObriseListu(string doktorId, string korisnikId, int ocekivano)
    {
        // Arrange
        var lista = new ListaCekanja { ListaId = 15, DoktorID = doktorId };
        await _repo.AddAsync(lista);
        await _repo.SaveChangesAsync();

        // Act - Update
        lista.DoktorID = "novi_doktor";
        _repo.Update(lista);
        await _repo.SaveChangesAsync();
        var azurirano = await _repo.GetByIdAsync(15);

        // Assert - Update
        Assert.AreEqual("novi_doktor", azurirano?.DoktorID);

        // Act - Delete
        _repo.Delete(azurirano!);
        await _repo.SaveChangesAsync();
        var obrisano = await _repo.GetByIdAsync(15);

        // Assert - Delete
        Assert.IsNull(obrisano);
    }
}
