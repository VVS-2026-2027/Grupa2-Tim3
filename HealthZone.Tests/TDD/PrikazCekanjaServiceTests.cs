using HealthZone.Services;

namespace HealthZone.Tests;

// ─── Izvori podataka za PrikazCekanjaServiceTests (TDD F1-B) ────────────
public static class PrikazCekanjaServicePodaci
{
    public static IEnumerable<object[]> NegativneMinute() =>
        CsvLoader.Ucitaj("PrikazCekanjaService_negativno.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    // Svaki scenarij u XML-u ima atribut "pravilo", pa svaki test dobija samo scenarije svog pravila.
    private static IEnumerable<object[]> Formati(string pravilo) =>
        XmlLoader.Ucitaj("PrikazCekanjaService_formati.xml", "scenarij")
                 .Where(x => (string)x.Attribute("pravilo")! == pravilo)
                 .Select(x => new object[] { (int)x.Element("minute")!, (string)x.Element("ocekivano")! });

    public static IEnumerable<object[]> NulaMinuta() => Formati("nula");

    public static IEnumerable<object[]> ManjeOdSata() => Formati("minute");

    public static IEnumerable<object[]> CijeliSati() => Formati("sati");

    public static IEnumerable<object[]> SatiIMinute() => Formati("satiIMinute");
}

// ─── Testovi ────────────────────────────────────────────────────────────
// Pravila: negativno → izuzetak; 0 → "Vi ste sljedeći"; 1–59 → "<m> min";
// 60 i više → "<h> h <m> min", a kad su minute 0 samo "<h> h".
[TestClass]
public class PrikazCekanjaServiceTests
{
    private PrikazCekanjaService _servis = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // Jednom za cijelu klasu: provjera da su CSV/XML fajlovi kopirani uz testove.
        Assert.IsTrue(Directory.Exists(Path.Combine(AppContext.BaseDirectory, "TestData")),
            "Folder TestData nije kopiran u output.");
    }

    [TestInitialize]
    public void Setup() => _servis = new PrikazCekanjaService();

    [TestCleanup]
    public void Cleanup() => _servis = null!;

    [TestMethod]
    [DynamicData(nameof(PrikazCekanjaServicePodaci.NegativneMinute), typeof(PrikazCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("TDD"), TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public void FormatirajVrijemeCekanja_NegativneMinute_BacaException(int minute, string ocekivanaPoruka)
    {
        // Arrange (servis je napravljen u Setup)

        // Act
        var izuzetak = Assert.ThrowsException<Exception>(() => _servis.FormatirajVrijemeCekanja(minute));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(PrikazCekanjaServicePodaci.NulaMinuta), typeof(PrikazCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("TDD"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public void FormatirajVrijemeCekanja_NulaMinuta_VracaViSteSljedeci(int minute, string ocekivano)
    {
        // Arrange (servis je napravljen u Setup)

        // Act
        var rezultat = _servis.FormatirajVrijemeCekanja(minute);

        // Assert
        Assert.AreEqual(ocekivano, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(PrikazCekanjaServicePodaci.ManjeOdSata), typeof(PrikazCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("TDD"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void FormatirajVrijemeCekanja_ManjeOdSata_VracaSamoMinute(int minute, string ocekivano)
    {
        // Arrange (servis je napravljen u Setup)

        // Act
        var rezultat = _servis.FormatirajVrijemeCekanja(minute);

        // Assert
        Assert.AreEqual(ocekivano, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(PrikazCekanjaServicePodaci.CijeliSati), typeof(PrikazCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("TDD"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void FormatirajVrijemeCekanja_CijeliSati_VracaSamoSate(int minute, string ocekivano)
    {
        // Arrange (servis je napravljen u Setup)

        // Act
        var rezultat = _servis.FormatirajVrijemeCekanja(minute);

        // Assert
        Assert.AreEqual(ocekivano, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(PrikazCekanjaServicePodaci.SatiIMinute), typeof(PrikazCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("TDD"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Timeout(2000)]
    public void FormatirajVrijemeCekanja_SatiIMinute_VracaSateIMinute(int minute, string ocekivano)
    {
        // Arrange (servis je napravljen u Setup)

        // Act
        var rezultat = _servis.FormatirajVrijemeCekanja(minute);

        // Assert
        Assert.AreEqual(ocekivano, rezultat);
    }
}
