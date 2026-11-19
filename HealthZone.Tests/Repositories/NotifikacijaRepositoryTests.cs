using System.Xml.Linq;
using HealthZone.Data;
using HealthZone.Models;
using HealthZone.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za NotifikacijaRepositoryTests ────
public static class NotifikacijaRepositoryPodaci
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
public class NotifikacijaRepositoryTests
{
    private static List<XElement> _podaciXml = null!;

    private ApplicationDbContext _context = null!;
    private NotifikacijaRepository _repo = null!;

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
        _repo = new NotifikacijaRepository(_context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaRepositoryPodaci.ZapisiCsv), typeof(NotifikacijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Fake"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task AddAsync_I_GetByIdAsync_SpremaI_DohvataNotifikaciju(int id, string naziv, int ocekivano)
    {
        // Arrange
        var notif = new Notifikacija
        {
            NotifikacijaId = id,
            Poruka = naziv,
            DatumSlanja = DateTime.Now,
            status = Status.NaCekanju,
            TerminID = 1
        };

        // Act
        await _repo.AddAsync(notif);
        await _repo.SaveChangesAsync();
        var dohvaceno = await _repo.GetByIdAsync(id);

        // Assert
        Assert.IsNotNull(dohvaceno);
        Assert.AreEqual(id, dohvaceno.NotifikacijaId);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaRepositoryPodaci.ZapisiCsv), typeof(NotifikacijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Fake"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task GetAllAsync_PostojeNotifikacije_VracaSve(int id, string naziv, int ocekivano)
    {
        // Arrange
        var n1 = new Notifikacija { NotifikacijaId = id, Poruka = "N1", TerminID = 1 };
        var n2 = new Notifikacija { NotifikacijaId = id + 10, Poruka = "N2", TerminID = 2 };
        await _repo.AddAsync(n1);
        await _repo.AddAsync(n2);
        await _repo.SaveChangesAsync();

        // Act
        var sve = await _repo.GetAllAsync();

        // Assert
        Assert.AreEqual(2, sve.Count());
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaRepositoryPodaci.ZapisiXml), typeof(NotifikacijaRepositoryPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Fake"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Update_I_Delete_AzuriraIObriseNotifikaciju(string doktorId, string korisnikId, int ocekivano)
    {
        // Arrange
        var notif = new Notifikacija { NotifikacijaId = 20, Poruka = "Prije izmjene", TerminID = 1 };
        await _repo.AddAsync(notif);
        await _repo.SaveChangesAsync();

        // Act - Update
        notif.Poruka = "Nakon izmjene";
        _repo.Update(notif);
        await _repo.SaveChangesAsync();
        var azurirano = await _repo.GetByIdAsync(20);

        // Assert - Update
        Assert.AreEqual("Nakon izmjene", azurirano?.Poruka);

        // Act - Delete
        _repo.Delete(azurirano!);
        await _repo.SaveChangesAsync();
        var obrisano = await _repo.GetByIdAsync(20);

        // Assert - Delete
        Assert.IsNull(obrisano);
    }
}
