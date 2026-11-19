using HealthZone.Models;

namespace HealthZone.Tests;

[TestClass]
public class ErrorViewModelTests
{
    // Podaci iz CSV-a: RequestId i da li se on treba prikazati.
    // Prazan string se ne prikazuje, a svaki neprazan (i razmak) se prikazuje.
    public static IEnumerable<object[]> ShowRequestId() =>
        CsvLoader.Ucitaj("ErrorViewModel_showRequestId.csv")
                 .Select(r => new object[] { r[0], bool.Parse(r[1]) });

    [TestMethod]
    [DynamicData(nameof(ShowRequestId), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public void ShowRequestId_RazliciteVrijednostiRequestId_VracaDaLiSePrikazuje(string requestId, bool ocekivanoPrikazivanje)
    {
        // Arrange
        var model = new ErrorViewModel { RequestId = requestId };

        // Act
        var rezultat = model.ShowRequestId;

        // Assert
        Assert.AreEqual(ocekivanoPrikazivanje, rezultat);
    }

    [TestMethod]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void ShowRequestId_RequestIdNijePostavljen_VracaFalse()
    {
        // Arrange
        var model = new ErrorViewModel();

        // Act
        var rezultat = model.ShowRequestId;

        // Assert
        Assert.IsFalse(rezultat);
    }
}
