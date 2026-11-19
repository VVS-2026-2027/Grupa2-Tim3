using HealthZone.Data;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za SeedDataTests ──────────────────
public static class SeedDataPodaci
{
    public static Mock<RoleManager<IdentityRole>> NapraviRoleManager()
        => new(Mock.Of<IRoleStore<IdentityRole>>(), null!, null!, null!, null!);

    // IServiceProvider koji na zahtjev za RoleManager vraća zadani mock.
    public static Mock<IServiceProvider> NapraviServiceProvider(RoleManager<IdentityRole> roleManager)
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(RoleManager<IdentityRole>))).Returns(roleManager);
        return provider;
    }

    public static IEnumerable<object[]> StanjaBaze() =>
        XmlLoader.Ucitaj("SeedData_postojeceUloge.xml", "scenarij").Select(x => new object[]
        {
            ((string?)x.Element("postojece") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries),
            (int)x.Element("ocekivaniBrojNovih")!
        });

    public static IEnumerable<object[]> StandardneUloge() =>
        CsvLoader.Ucitaj("SeedData_standardneUloge.csv").Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> PostojeceUloge() =>
        CsvLoader.Ucitaj("SeedData_postojecaUloga.csv").Select(r => new object[] { r[0], bool.Parse(r[1]) });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class SeedDataTests
{
    private Mock<RoleManager<IdentityRole>> _roleManager = null!;
    private Mock<IServiceProvider> _serviceProvider = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // Jednom za cijelu klasu: provjera da su CSV/XML fajlovi kopirani uz testove.
        Assert.IsTrue(Directory.Exists(Path.Combine(AppContext.BaseDirectory, "TestData")),
            "Folder TestData nije kopiran u output.");
    }

    [TestInitialize]
    public void Setup()
    {
        // RoleExistsAsync bez Setup-a vraća false, tj. uloga ne postoji.
        _roleManager = SeedDataPodaci.NapraviRoleManager();
        _roleManager.Setup(r => r.CreateAsync(It.IsAny<IdentityRole>())).ReturnsAsync(IdentityResult.Success);
        _serviceProvider = SeedDataPodaci.NapraviServiceProvider(_roleManager.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _roleManager.Reset();
        _serviceProvider.Reset();
    }

    [TestMethod]
    [DynamicData(nameof(SeedDataPodaci.StanjaBaze), typeof(SeedDataPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task SeedRolesAsync_DioUlogaVecPostoji_KreiraSamoNedostajuce(string[] postojece, int ocekivaniBrojNovih)
    {
        // Arrange
        _roleManager.Setup(r => r.RoleExistsAsync(It.Is<string>(u => postojece.Contains(u)))).ReturnsAsync(true);

        // Act
        await SeedData.SeedRolesAsync(_serviceProvider.Object);

        // Assert
        _roleManager.Verify(r => r.CreateAsync(It.IsAny<IdentityRole>()), Times.Exactly(ocekivaniBrojNovih));
    }

    [TestMethod]
    [DynamicData(nameof(SeedDataPodaci.StandardneUloge), typeof(SeedDataPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task SeedRolesAsync_UlogaNePostoji_KreiraUlogu(string uloga)
    {
        // Act
        await SeedData.SeedRolesAsync(_serviceProvider.Object);

        // Assert
        _roleManager.Verify(r => r.CreateAsync(It.Is<IdentityRole>(i => i.Name == uloga)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(SeedDataPodaci.PostojeceUloge), typeof(SeedDataPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task SeedRolesAsync_UlogaVecPostoji_NeKreiraJePonovo(string uloga, bool postoji)
    {
        // Arrange
        _roleManager.Setup(r => r.RoleExistsAsync(uloga)).ReturnsAsync(postoji);

        // Act
        await SeedData.SeedRolesAsync(_serviceProvider.Object);

        // Assert
        _roleManager.Verify(r => r.CreateAsync(It.Is<IdentityRole>(i => i.Name == uloga)), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(SeedDataPodaci.StandardneUloge), typeof(SeedDataPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task SeedRolesAsync_ZaSvakuUlogu_ProvjeravaDaLiPostoji(string uloga)
    {
        // Act
        await SeedData.SeedRolesAsync(_serviceProvider.Object);

        // Assert
        _roleManager.Verify(r => r.RoleExistsAsync(uloga), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(SeedDataPodaci.StandardneUloge), typeof(SeedDataPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task SeedRolesAsync_PokretanjeSeeda_UzimaRoleManagerIzServisa(string uloga)
    {
        // Act
        await SeedData.SeedRolesAsync(_serviceProvider.Object);

        // Assert
        _serviceProvider.Verify(p => p.GetService(typeof(RoleManager<IdentityRole>)), Times.Once);
    }
}
