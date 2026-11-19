using HealthZone.Services;

namespace HealthZone.Tests;

// ─── Izvori podataka za ProcjenaCekanjaServiceTests (TDD F1-A) ──────────
public static class ProcjenaCekanjaServicePodaci
{
    public static IEnumerable<object[]> NevalidnaPozicija() =>
        CsvLoader.Ucitaj("ProcjenaCekanjaService_nevalidnaPozicija.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), r[2] });

    public static IEnumerable<object[]> NevalidnoTrajanje() =>
        CsvLoader.Ucitaj("ProcjenaCekanjaService_nevalidnoTrajanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), r[2] });

    private static IEnumerable<object[]> Procjene(Func<int, bool> uslovPozicije) =>
        XmlLoader.Ucitaj("ProcjenaCekanjaService_procjena.xml", "scenarij")
                 .Where(x => uslovPozicije((int)x.Element("pozicija")!))
                 .Select(x => new object[]
                 {
                     (int)x.Element("pozicija")!,
                     (int)x.Element("trajanjePregledaMin")!,
                     (int)x.Element("ocekivaneMinute")!
                 });

    public static IEnumerable<object[]> SveProcjene() => Procjene(p => true);

    // Granični slučaj: pacijent koji je prvi na listi.
    public static IEnumerable<object[]> PrviNaListi() => Procjene(p => p == 1);
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class ProcjenaCekanjaServiceTests
{
    private ProcjenaCekanjaService _servis = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // Jednom za cijelu klasu: provjera da su CSV/XML fajlovi kopirani uz testove.
        Assert.IsTrue(Directory.Exists(Path.Combine(AppContext.BaseDirectory, "TestData")),
            "Folder TestData nije kopiran u output.");
    }

    [TestInitialize]
    public void Setup() => _servis = new ProcjenaCekanjaService();

    [TestCleanup]
    public void Cleanup() => _servis = null!;

    [TestMethod]
    [DynamicData(nameof(ProcjenaCekanjaServicePodaci.NevalidnaPozicija), typeof(ProcjenaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("TDD"), TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public void IzracunajMinuteCekanja_PozicijaManjaOdJedan_BacaException(int pozicija, int trajanjePregledaMin,
        string ocekivanaPoruka)
    {
        // Act
        var izuzetak = Assert.ThrowsException<Exception>(
            () => _servis.IzracunajMinuteCekanja(pozicija, trajanjePregledaMin));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(ProcjenaCekanjaServicePodaci.NevalidnoTrajanje), typeof(ProcjenaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("TDD"), TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public void IzracunajMinuteCekanja_TrajanjeNijePozitivno_BacaException(int pozicija, int trajanjePregledaMin,
        string ocekivanaPoruka)
    {
        // Act
        var izuzetak = Assert.ThrowsException<Exception>(
            () => _servis.IzracunajMinuteCekanja(pozicija, trajanjePregledaMin));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(ProcjenaCekanjaServicePodaci.SveProcjene), typeof(ProcjenaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("TDD"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Timeout(2000)]
    public void IzracunajMinuteCekanja_ValidniPodaci_VracaMinuteCekanja(int pozicija, int trajanjePregledaMin,
        int ocekivaneMinute)
    {
        // Act
        var rezultat = _servis.IzracunajMinuteCekanja(pozicija, trajanjePregledaMin);

        // Assert
        Assert.AreEqual(ocekivaneMinute, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(ProcjenaCekanjaServicePodaci.PrviNaListi), typeof(ProcjenaCekanjaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("TDD"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void IzracunajMinuteCekanja_PrviNaListi_CekaNulaMinuta(int pozicija, int trajanjePregledaMin,
        int ocekivaneMinute)
    {
        // Act
        var rezultat = _servis.IzracunajMinuteCekanja(pozicija, trajanjePregledaMin);

        // Assert
        Assert.AreEqual(ocekivaneMinute, rezultat);
    }
}
