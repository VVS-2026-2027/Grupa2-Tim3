using System.ComponentModel.DataAnnotations;
using HealthZone.Models;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za ListaCekanjaTests ──────────────
public static class ListaCekanjaPodaci
{
    // Poruke svih validacijskih grešaka modela.
    public static List<string> Greske(object model)
    {
        var rezultati = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), rezultati, true);
        return rezultati.Select(r => r.ErrorMessage!).ToList();
    }

    public static List<string> Lista(string vrijednost) =>
        vrijednost.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();

    public static IEnumerable<object[]> Validacija() =>
        CsvLoader.Ucitaj("ListaCekanja_validacija.csv").Select(r => new object[] { r[0], Lista(r[1]) });

    public static IEnumerable<object[]> Doktori() =>
        XmlLoader.Ucitaj("ListaCekanja_doktori.xml", "doktor").Select(x => new object[]
        {
            (string)x.Element("id")!,
            (string)x.Element("ime")!,
            (string)x.Element("prezime")!
        });
}

// ─── Testovi ────────────────────────────────────────────────────────────
// Model ima validacijski atribut [Required] na doktoru liste.
[TestClass]
public class ListaCekanjaTests
{
    private ListaCekanja _lista = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // Jednom za cijelu klasu: provjera da su CSV/XML fajlovi kopirani uz testove.
        Assert.IsTrue(Directory.Exists(Path.Combine(AppContext.BaseDirectory, "TestData")),
            "Folder TestData nije kopiran u output.");
    }

    [TestInitialize]
    public void Setup() => _lista = new ListaCekanja();

    [TestCleanup]
    public void Cleanup() => _lista = null!;

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaPodaci.Validacija), typeof(ListaCekanjaPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Validacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public void Validacija_RazlicitDoktor_VracaOcekivaneGreske(string doktorId, List<string> ocekivaneGreske)
    {
        // Arrange
        _lista.DoktorID = doktorId;

        // Act
        var greske = ListaCekanjaPodaci.Greske(_lista);

        // Assert
        CollectionAssert.AreEqual(ocekivaneGreske, greske);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaPodaci.Doktori), typeof(ListaCekanjaPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Doktor_PostavljenDoktor_DostupanKrozNavigaciju(string id, string ime, string prezime)
    {
        // Arrange
        var doktor = new Korisnik { Id = id, Ime = ime, Prezime = prezime };

        // Act
        _lista.Doktor = doktor;

        // Assert
        Assert.AreSame(doktor, _lista.Doktor);
    }
}
