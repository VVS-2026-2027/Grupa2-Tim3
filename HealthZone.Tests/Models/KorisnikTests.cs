using System.ComponentModel.DataAnnotations;
using HealthZone.Models;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za KorisnikTests ──────────────────
public static class KorisnikPodaci
{
    // Poruke svih validacijskih grešaka modela, redom kojim su svojstva definisana.
    public static List<string> Greske(object model)
    {
        var rezultati = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), rezultati, true);
        return rezultati.Select(r => r.ErrorMessage!).ToList();
    }

    public static IEnumerable<object[]> Validacija() =>
        CsvLoader.Ucitaj("Korisnik_validacija.csv").Select(r => new object[]
        {
            r[0], r[1], r[2].Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
        });

    public static IEnumerable<object[]> MedicinskiPodaci() =>
        CsvLoader.Ucitaj("Korisnik_podaci.csv").Select(r => new object[] { r[0], r[1], r[2] });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class KorisnikTests
{
    private Korisnik _korisnik = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // Jednom za cijelu klasu: provjera da su CSV fajlovi kopirani uz testove.
        Assert.IsTrue(Directory.Exists(Path.Combine(AppContext.BaseDirectory, "TestData")),
            "Folder TestData nije kopiran u output.");
    }

    [TestInitialize]
    public void Setup() => _korisnik = new Korisnik();

    [TestCleanup]
    public void Cleanup() => _korisnik = null!;

    [TestMethod]
    [DynamicData(nameof(KorisnikPodaci.Validacija), typeof(KorisnikPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Validacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public void Validacija_RazlicitoImeIPrezime_VracaOcekivaneGreske(string ime, string prezime,
        List<string> ocekivaneGreske)
    {
        // Arrange
        _korisnik.Ime = ime;
        _korisnik.Prezime = prezime;

        // Act
        var greske = KorisnikPodaci.Greske(_korisnik);

        // Assert
        CollectionAssert.AreEqual(ocekivaneGreske, greske);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikPodaci.MedicinskiPodaci), typeof(KorisnikPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void BrojKartona_PostavljenaVrijednost_VracaIstuVrijednost(string brojKartona, string prioritet,
        string specijalizacija)
    {
        // Act
        _korisnik.BrojKartona = brojKartona;

        // Assert
        Assert.AreEqual(brojKartona, _korisnik.BrojKartona);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikPodaci.MedicinskiPodaci), typeof(KorisnikPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Prioritet_PostavljenaVrijednost_VracaIstuVrijednost(string brojKartona, string prioritet,
        string specijalizacija)
    {
        // Act
        _korisnik.Prioritet = Enum.Parse<Prioritet>(prioritet);

        // Assert
        Assert.AreEqual(Enum.Parse<Prioritet>(prioritet), _korisnik.Prioritet);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikPodaci.MedicinskiPodaci), typeof(KorisnikPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Specijalizacija_PostavljenaVrijednost_VracaIstuVrijednost(string brojKartona, string prioritet,
        string specijalizacija)
    {
        // Act
        _korisnik.Specijalizacija = Enum.Parse<Specijalizacija>(specijalizacija);

        // Assert
        Assert.AreEqual(Enum.Parse<Specijalizacija>(specijalizacija), _korisnik.Specijalizacija);
    }
}
