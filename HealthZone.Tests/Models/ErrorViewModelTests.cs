using HealthZone.Models;

namespace HealthZone.Tests;

// ─── Izvori podataka za ErrorViewModelTests ─────────────────────────────
public static class ErrorViewModelPodaci
{
    // RequestId i da li se on treba prikazati. Prazan string se ne prikazuje, a svaki neprazan (i razmak) se prikazuje.
    public static IEnumerable<object[]> ShowRequestId() =>
        CsvLoader.Ucitaj("ErrorViewModel_showRequestId.csv")
                 .Select(r => new object[] { r[0], bool.Parse(r[1]) });

    // RequestId uopšte nije postavljen (null).
    public static IEnumerable<object[]> Nepostavljen() =>
        CsvLoader.Ucitaj("ErrorViewModel_nepostavljen.csv")
                 .Select(r => new object[] { bool.Parse(r[0]) });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class ErrorViewModelTests
{
    private static string _folderPodataka = null!;

    private ErrorViewModel _model = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // Jednom za cijelu klasu: provjera da su CSV fajlovi kopirani uz testove.
        _folderPodataka = Path.Combine(AppContext.BaseDirectory, "TestData");
        Assert.IsTrue(Directory.Exists(_folderPodataka), "Folder TestData nije kopiran u output.");
    }

    [TestInitialize]
    public void Setup()
    {
        _model = new ErrorViewModel();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _model = null!;
    }

    [TestMethod]
    [DynamicData(nameof(ErrorViewModelPodaci.ShowRequestId), typeof(ErrorViewModelPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public void ShowRequestId_RazliciteVrijednostiRequestId_VracaDaLiSePrikazuje(string requestId, bool ocekivanoPrikazivanje)
    {
        // Arrange
        _model.RequestId = requestId;

        // Act
        var rezultat = _model.ShowRequestId;

        // Assert
        Assert.AreEqual(ocekivanoPrikazivanje, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(ErrorViewModelPodaci.Nepostavljen), typeof(ErrorViewModelPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void ShowRequestId_RequestIdNijePostavljen_NePrikazuje(bool ocekivanoPrikazivanje)
    {
        // Act
        var rezultat = _model.ShowRequestId;

        // Assert
        Assert.AreEqual(ocekivanoPrikazivanje, rezultat);
    }
}
