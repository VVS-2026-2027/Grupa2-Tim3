using System.ComponentModel.DataAnnotations;
using HealthZone.Models;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za RecenzijaTests ─────────────────
public static class RecenzijaPodaci
{
    // ── pomoćne metode ──

    // Vraća poruke svih validacijskih grešaka modela ([Required], [Range]), redom kojim su svojstva definisana.
    public static List<string> Greske(object model)
    {
        var rezultati = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), rezultati, true);
        return rezultati.Select(r => r.ErrorMessage!).ToList();
    }

    // ── izvori podataka (CSV) ──

    // Ocjene i poruke koje validacija treba vratiti (razdvojene znakom |, prazno = validno).
    public static IEnumerable<object[]> Validacija() =>
        CsvLoader.Ucitaj("Recenzija_validacija.csv")
                 .Select(r => new object[]
                 {
                     r[0], r[1], int.Parse(r[2]), int.Parse(r[3]), int.Parse(r[4]),
                     r[5].Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
                 });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class RecenzijaTests
{
    private static string _folderPodataka = null!;

    private Recenzija _recenzija = null!;

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
        _recenzija = new Recenzija();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _recenzija = null!;
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaPodaci.Validacija), typeof(RecenzijaPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public void Validacija_RazliciteOcjene_VracaOcekivaneGreske(
        string pacijentId, string doktorId, int ljubaznost, int profesionalnost, int usluga, List<string> ocekivaneGreske)
    {
        // Arrange
        _recenzija.PacijentID = pacijentId;
        _recenzija.DoktorID = doktorId;
        _recenzija.OcjenaLjubaznosti = ljubaznost;
        _recenzija.OcjenaProfesionalnosti = profesionalnost;
        _recenzija.OcjenaUsluge = usluga;

        // Act
        var greske = RecenzijaPodaci.Greske(_recenzija);

        // Assert
        CollectionAssert.AreEqual(ocekivaneGreske, greske);
    }
}
