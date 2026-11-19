using System.Globalization;
using HealthZone.Models;
using HealthZone.Services;

namespace HealthZone.Tests;

// ─── Izvori podataka za OtkazivanjeTerminaServiceTests (TDD F2-A) ─────────
public static class OtkazivanjeTerminaServicePodaci
{
    // CSV: termini koje nije dozvoljeno otkazati (već počeli ili počinju za manje od 2 sata).
    public static IEnumerable<object[]> NedozvoljenoOtkazivanje() =>
        CsvLoader.Ucitaj("OtkazivanjeTerminaService_nedozvoljeno.csv")
                 .Select(r => new object[] { double.Parse(r[0], CultureInfo.InvariantCulture), r[1] });

    // XML: termini koje je dozvoljeno otkazati i očekivani rezultat.
    public static IEnumerable<object[]> DozvoljenoOtkazivanje() =>
        XmlLoader.Ucitaj("OtkazivanjeTerminaService_dozvoljeno.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     double.Parse((string)x.Element("pomakSati")!, CultureInfo.InvariantCulture),
                     Enum.Parse<RezultatOtkazivanja>((string)x.Element("ocekivaniRezultat")!)
                 });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class OtkazivanjeTerminaServiceTests
{
    // Isto "sada" za sve testove u klasi, postavlja se jednom (samo za čitanje).
    private static DateTime _referentnoVrijeme;

    private OtkazivanjeTerminaService _servis = null!;
    private DateTime _sada;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _referentnoVrijeme = DateTime.Now;
    }

    [TestInitialize]
    public void Setup()
    {
        _servis = new OtkazivanjeTerminaService();
        _sada = _referentnoVrijeme;
    }

    [TestCleanup]
    public void Cleanup()
    {
        _servis = null!;
    }

    [TestMethod]
    [DynamicData(nameof(OtkazivanjeTerminaServicePodaci.NedozvoljenoOtkazivanje), typeof(OtkazivanjeTerminaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("TDD"), TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public void ProvjeriOtkazivanje_PrekasnoZaOtkazivanje_BacaException(double pomakSati, string ocekivanaPoruka)
    {
        // Arrange
        var pocetakTermina = _sada.AddHours(pomakSati);

        // Act
        var izuzetak = Assert.ThrowsException<Exception>(() => _servis.ProvjeriOtkazivanje(pocetakTermina, _sada));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(OtkazivanjeTerminaServicePodaci.DozvoljenoOtkazivanje), typeof(OtkazivanjeTerminaServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("TDD"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Timeout(2000)]
    public void ProvjeriOtkazivanje_DovoljnoVremenaDoTermina_VracaOcekivaniRezultat(double pomakSati, RezultatOtkazivanja ocekivaniRezultat)
    {
        // Arrange
        var pocetakTermina = _sada.AddHours(pomakSati);

        // Act
        var rezultat = _servis.ProvjeriOtkazivanje(pocetakTermina, _sada);

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat);
    }
}
