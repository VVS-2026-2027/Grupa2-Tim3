using HealthZone.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za HealthZoneIdentityDbContextTests
public static class HealthZoneIdentityDbContextPodaci
{
    public static HealthZoneIdentityDbContext NovaBaza(string imeBaze) => new(
        new DbContextOptionsBuilder<HealthZoneIdentityDbContext>().UseInMemoryDatabase(imeBaze).Options);

    public static IEnumerable<object[]> Korisnici() =>
        CsvLoader.Ucitaj("HealthZoneIdentityDbContext_korisnici.csv").Select(r => new object[] { r[0], r[1] });

    public static IEnumerable<object[]> IdentityTipovi() =>
        CsvLoader.Ucitaj("HealthZoneIdentityDbContext_tipovi.csv").Select(r => new object[] { r[0] });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class HealthZoneIdentityDbContextTests
{
    private string _imeBaze = null!;
    private HealthZoneIdentityDbContext _context = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // Jednom za cijelu klasu: provjera da su CSV fajlovi kopirani uz testove.
        Assert.IsTrue(Directory.Exists(Path.Combine(AppContext.BaseDirectory, "TestData")),
            "Folder TestData nije kopiran u output.");
    }

    [TestInitialize]
    public void Setup()
    {
        _imeBaze = Guid.NewGuid().ToString();
        _context = HealthZoneIdentityDbContextPodaci.NovaBaza(_imeBaze);
    }

    [TestCleanup]
    public void Cleanup() => _context.Dispose();

    [TestMethod]
    [DynamicData(nameof(HealthZoneIdentityDbContextPodaci.IdentityTipovi), typeof(HealthZoneIdentityDbContextPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void OnModelCreating_IdentityKonfiguracija_SadrziIdentityEntitet(string tip)
    {
        // Act
        var entitet = _context.Model.FindEntityType(tip);

        // Assert
        Assert.IsNotNull(entitet);
    }

    [TestMethod]
    [DynamicData(nameof(HealthZoneIdentityDbContextPodaci.Korisnici), typeof(HealthZoneIdentityDbContextPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task SaveChangesAsync_NoviIdentityKorisnik_KorisnikJeUBazi(string id, string korisnickoIme)
    {
        // Arrange
        _context.Users.Add(new IdentityUser { Id = id, UserName = korisnickoIme });

        // Act
        await _context.SaveChangesAsync();

        // Assert
        using var provjera = HealthZoneIdentityDbContextPodaci.NovaBaza(_imeBaze);
        Assert.IsNotNull(await provjera.Users.FindAsync(id));
    }
}
