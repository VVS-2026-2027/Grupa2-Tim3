using System.ComponentModel.DataAnnotations;
using HealthZone.Models;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za KorisnikNaListiTests ───────────
public static class KorisnikNaListiPodaci
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
        CsvLoader.Ucitaj("KorisnikNaListi_validacija.csv")
                 .Select(r => new object[] { r[0], int.Parse(r[1]), int.Parse(r[2]), Lista(r[3]) });

    private static IEnumerable<System.Xml.Linq.XElement> Veze() => XmlLoader.Ucitaj("KorisnikNaListi_veze.xml", "stavka");

    public static IEnumerable<object[]> Liste() =>
        Veze().Select(x => new object[] { (int)x.Element("listaId")!, (string)x.Element("doktorId")! });

    public static IEnumerable<object[]> Pacijenti() =>
        Veze().Select(x => new object[] { (string)x.Element("korisnikId")! });
}

// ─── Testovi ────────────────────────────────────────────────────────────
// Model ima validacijske atribute ([Required]), pa se provjerava koje poruke Validator vraća.
[TestClass]
public class KorisnikNaListiTests
{
    private KorisnikNaListi _stavka = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // Jednom za cijelu klasu: provjera da su CSV/XML fajlovi kopirani uz testove.
        Assert.IsTrue(Directory.Exists(Path.Combine(AppContext.BaseDirectory, "TestData")),
            "Folder TestData nije kopiran u output.");
    }

    [TestInitialize]
    public void Setup() => _stavka = new KorisnikNaListi();

    [TestCleanup]
    public void Cleanup() => _stavka = null!;

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiPodaci.Validacija), typeof(KorisnikNaListiPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Validacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public void Validacija_RazlicitPacijent_VracaOcekivaneGreske(string korisnikId, int listaId, int pomakDana,
        List<string> ocekivaneGreske)
    {
        // Arrange
        _stavka.KorisnikID = korisnikId;
        _stavka.ListaID = listaId;
        _stavka.Datum = DateTime.Today.AddDays(pomakDana);

        // Act
        var greske = KorisnikNaListiPodaci.Greske(_stavka);

        // Assert
        CollectionAssert.AreEqual(ocekivaneGreske, greske);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiPodaci.Liste), typeof(KorisnikNaListiPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Lista_PostavljenaLista_DostupnaKrozNavigaciju(int listaId, string doktorId)
    {
        // Arrange
        var lista = new ListaCekanja { ListaId = listaId, DoktorID = doktorId };

        // Act
        _stavka.Lista = lista;

        // Assert
        Assert.AreSame(lista, _stavka.Lista);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiPodaci.Pacijenti), typeof(KorisnikNaListiPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Korisnik_PostavljenPacijent_DostupanKrozNavigaciju(string korisnikId)
    {
        // Arrange
        var pacijent = new Korisnik { Id = korisnikId };

        // Act
        _stavka.Korisnik = pacijent;

        // Assert
        Assert.AreSame(pacijent, _stavka.Korisnik);
    }
}
