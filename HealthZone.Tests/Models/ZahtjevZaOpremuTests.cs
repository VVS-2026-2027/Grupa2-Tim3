using System.ComponentModel.DataAnnotations;
using HealthZone.Models;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za ZahtjevZaOpremuTests ───────────
public static class ZahtjevZaOpremuPodaci
{
    // ── pomoćne metode ──

    // Vraća poruke svih validacijskih grešaka modela ([Required] i sl.), redom kojim su svojstva definisana.
    public static List<string> Greske(object model)
    {
        var rezultati = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), rezultati, true);
        return rezultati.Select(r => r.ErrorMessage!).ToList();
    }

    public static List<string> Poruke(string? s) =>
        (s ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();

    // ── izvori podataka (CSV) ──

    public static IEnumerable<object[]> Doktor() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremu_doktor.csv")
                 .Select(r => new object[] { r[0], r[1], Enum.Parse<VrstaZahtjeva>(r[2]) });

    // ── izvori podataka (XML) ──

    // Element koji nedostaje znači da polje nije uneseno (null).
    public static IEnumerable<object[]> Validacija() =>
        XmlLoader.Ucitaj("ZahtjevZaOpremu_validacija.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string?)x.Element("naziv")!, (string?)x.Element("opis")!, (string?)x.Element("doktorId")!,
                     Poruke((string?)x.Element("ocekivaneGreske"))
                 });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class ZahtjevZaOpremuTests
{
    private static string _folderPodataka = null!;

    private ZahtjevZaOpremu _zahtjev = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // Jednom za cijelu klasu: provjera da su CSV i XML fajlovi kopirani uz testove.
        _folderPodataka = Path.Combine(AppContext.BaseDirectory, "TestData");
        Assert.IsTrue(Directory.Exists(_folderPodataka), "Folder TestData nije kopiran u output.");
    }

    [TestInitialize]
    public void Setup()
    {
        _zahtjev = new ZahtjevZaOpremu();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _zahtjev = null!;
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuPodaci.Validacija), typeof(ZahtjevZaOpremuPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public void Validacija_RazlicitiPodaci_VracaOcekivaneGreske(string? naziv, string? opis, string? doktorId, List<string> ocekivaneGreske)
    {
        // Arrange
        _zahtjev.Naziv = naziv!;
        _zahtjev.Opis = opis!;
        _zahtjev.DoktorID = doktorId!;

        // Act
        var greske = ZahtjevZaOpremuPodaci.Greske(_zahtjev);

        // Assert
        CollectionAssert.AreEqual(ocekivaneGreske, greske);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuPodaci.Doktor), typeof(ZahtjevZaOpremuPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void Doktor_PostavljenDoktor_CuvaIstogDoktora(string doktorId, string ime, VrstaZahtjeva vrsta)
    {
        // Arrange
        var doktor = new Korisnik { Id = doktorId, Ime = ime };

        // Act
        _zahtjev.Doktor = doktor;

        // Assert
        Assert.AreSame(doktor, _zahtjev.Doktor);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuPodaci.Doktor), typeof(ZahtjevZaOpremuPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void VrstaZahtjeva_PostavljenaVrsta_CuvaPostavljenuVrstu(string doktorId, string ime, VrstaZahtjeva vrsta)
    {
        // Act
        _zahtjev.VrstaZahtjeva = vrsta;

        // Assert
        Assert.AreEqual(vrsta, _zahtjev.VrstaZahtjeva);
    }
}
