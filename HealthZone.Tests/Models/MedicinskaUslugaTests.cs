using System.ComponentModel.DataAnnotations;
using HealthZone.Models;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za MedicinskaUslugaTests ──────────
public static class MedicinskaUslugaPodaci
{
    // ── pomoćne metode ──

    // Vraća poruke svih validacijskih grešaka modela ([Required], [Range]), redom kojim su svojstva definisana.
    public static List<string> Greske(object model)
    {
        var rezultati = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), rezultati, true);
        return rezultati.Select(r => r.ErrorMessage!).ToList();
    }

    public static List<string> Poruke(string? s) =>
        (s ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();

    // ── izvori podataka (XML) ──

    // Element koji nedostaje znači da polje nije uneseno (null).
    public static IEnumerable<object[]> Validacija() =>
        XmlLoader.Ucitaj("MedicinskaUsluga_validacija.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string?)x.Element("naziv")!, (string?)x.Element("opis")!, (decimal)x.Element("cijena")!,
                     Poruke((string?)x.Element("ocekivaneGreske"))
                 });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class MedicinskaUslugaTests
{
    private static string _folderPodataka = null!;

    private MedicinskaUsluga _usluga = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // Jednom za cijelu klasu: provjera da su XML fajlovi kopirani uz testove.
        _folderPodataka = Path.Combine(AppContext.BaseDirectory, "TestData");
        Assert.IsTrue(Directory.Exists(_folderPodataka), "Folder TestData nije kopiran u output.");
    }

    [TestInitialize]
    public void Setup()
    {
        _usluga = new MedicinskaUsluga();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _usluga = null!;
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaPodaci.Validacija), typeof(MedicinskaUslugaPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public void Validacija_RazlicitiPodaci_VracaOcekivaneGreske(string? naziv, string? opis, decimal cijena, List<string> ocekivaneGreske)
    {
        // Arrange
        _usluga.Naziv = naziv!;
        _usluga.Opis = opis!;
        _usluga.Cijena = cijena;

        // Act
        var greske = MedicinskaUslugaPodaci.Greske(_usluga);

        // Assert
        CollectionAssert.AreEqual(ocekivaneGreske, greske);
    }
}
