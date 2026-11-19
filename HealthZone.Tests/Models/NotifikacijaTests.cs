using System.ComponentModel.DataAnnotations;
using System.Globalization;
using HealthZone.Models;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za NotifikacijaTests ──────────────
public static class NotifikacijaPodaci
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
        XmlLoader.Ucitaj("Notifikacija_validacija.xml", "scenarij").Select(x => new object[]
        {
            (string)x.Element("poruka")!,
            (int)x.Element("terminId")!,
            Lista((string)x.Element("ocekivaneGreske")!)
        });

    private static IEnumerable<string[]> Podaci() => CsvLoader.Ucitaj("Notifikacija_podaci.csv");

    public static IEnumerable<object[]> Statusi() => Podaci().Select(r => new object[] { Enum.Parse<Status>(r[0]) });

    public static IEnumerable<object[]> Pomaci() =>
        Podaci().Select(r => new object[] { double.Parse(r[1], CultureInfo.InvariantCulture) });

    public static IEnumerable<object[]> Termini() => Podaci().Select(r => new object[] { int.Parse(r[2]) });
}

// ─── Testovi ────────────────────────────────────────────────────────────
// Model ima validacijski atribut [Required] na poruci; status, datum slanja i termin se samo čuvaju.
[TestClass]
public class NotifikacijaTests
{
    private Notifikacija _notifikacija = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // Jednom za cijelu klasu: provjera da su CSV/XML fajlovi kopirani uz testove.
        Assert.IsTrue(Directory.Exists(Path.Combine(AppContext.BaseDirectory, "TestData")),
            "Folder TestData nije kopiran u output.");
    }

    [TestInitialize]
    public void Setup() => _notifikacija = new Notifikacija();

    [TestCleanup]
    public void Cleanup() => _notifikacija = null!;

    [TestMethod]
    [DynamicData(nameof(NotifikacijaPodaci.Validacija), typeof(NotifikacijaPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Validacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public void Validacija_RazlicitaPoruka_VracaOcekivaneGreske(string poruka, int terminId, List<string> ocekivaneGreske)
    {
        // Arrange
        _notifikacija.Poruka = poruka;
        _notifikacija.TerminID = terminId;

        // Act
        var greske = NotifikacijaPodaci.Greske(_notifikacija);

        // Assert
        CollectionAssert.AreEqual(ocekivaneGreske, greske);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaPodaci.Statusi), typeof(NotifikacijaPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Status_PostavljenStatus_CuvaPostavljeniStatus(Status status)
    {
        // Arrange (nova notifikacija je napravljena u Setup)

        // Act
        _notifikacija.status = status;

        // Assert
        Assert.AreEqual(status, _notifikacija.status);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaPodaci.Pomaci), typeof(NotifikacijaPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void DatumSlanja_PostavljenDatum_CuvaPostavljeniDatum(double pomakSati)
    {
        // Arrange
        var datum = DateTime.Now.AddHours(pomakSati);

        // Act
        _notifikacija.DatumSlanja = datum;

        // Assert
        Assert.AreEqual(datum, _notifikacija.DatumSlanja);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaPodaci.Termini), typeof(NotifikacijaPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Termin_PostavljenTermin_DostupanKrozNavigaciju(int terminId)
    {
        // Arrange
        var termin = new Termin { TerminId = terminId };

        // Act
        _notifikacija.Termin = termin;

        // Assert
        Assert.AreSame(termin, _notifikacija.Termin);
    }
}
