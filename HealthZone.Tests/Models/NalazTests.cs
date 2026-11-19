using System.ComponentModel.DataAnnotations;
using HealthZone.Models;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za NalazTests ─────────────────────
public static class NalazPodaci
{
    // Poruke svih validacijskih grešaka modela, redom kojim su svojstva definisana.
    public static List<string> Greske(object model)
    {
        var rezultati = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), rezultati, true);
        return rezultati.Select(r => r.ErrorMessage!).ToList();
    }

    public static IEnumerable<object[]> Validacija() =>
        CsvLoader.Ucitaj("Nalaz_validacija.csv").Select(r => new object[]
        {
            r[0], r[1], r[2], r[3], r[4].Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
        });

    public static IEnumerable<object[]> Veze() =>
        CsvLoader.Ucitaj("Nalaz_veze.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class NalazTests
{
    private Nalaz _nalaz = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // Jednom za cijelu klasu: provjera da su CSV fajlovi kopirani uz testove.
        Assert.IsTrue(Directory.Exists(Path.Combine(AppContext.BaseDirectory, "TestData")),
            "Folder TestData nije kopiran u output.");
    }

    [TestInitialize]
    public void Setup() => _nalaz = new Nalaz();

    [TestCleanup]
    public void Cleanup() => _nalaz = null!;

    [TestMethod]
    [DynamicData(nameof(NalazPodaci.Validacija), typeof(NalazPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Validacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public void Validacija_RazlicitaPolja_VracaOcekivaneGreske(string opis, string dijagnoza, string terapija,
        string pacijentId, List<string> ocekivaneGreske)
    {
        // Arrange
        _nalaz.Opis = opis;
        _nalaz.Dijagnoza = dijagnoza;
        _nalaz.Terapija = terapija;
        _nalaz.PacijentID = pacijentId;

        // Act
        var greske = NalazPodaci.Greske(_nalaz);

        // Assert
        CollectionAssert.AreEqual(ocekivaneGreske, greske);
    }

    [TestMethod]
    [DynamicData(nameof(NalazPodaci.Veze), typeof(NalazPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Termin_PostavljenTermin_DostupanKrozNavigaciju(int terminId, string pacijentId)
    {
        // Act
        _nalaz.Termin = new Termin { TerminId = terminId };

        // Assert
        Assert.AreEqual(terminId, _nalaz.Termin.TerminId);
    }

    [TestMethod]
    [DynamicData(nameof(NalazPodaci.Veze), typeof(NalazPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Pacijent_PostavljenPacijent_DostupanKrozNavigaciju(int terminId, string pacijentId)
    {
        // Act
        _nalaz.Pacijent = new Korisnik { Id = pacijentId };

        // Assert
        Assert.AreEqual(pacijentId, _nalaz.Pacijent.Id);
    }

    [TestMethod]
    [DynamicData(nameof(NalazPodaci.Veze), typeof(NalazPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void TerminID_PostavljenId_VracaIstuVrijednost(int terminId, string pacijentId)
    {
        // Act
        _nalaz.TerminID = terminId;

        // Assert
        Assert.AreEqual(terminId, _nalaz.TerminID);
    }
}
