using System.Globalization;
using HealthZone.Models;
using HealthZone.Services;

namespace HealthZone.Tests;

[TestClass]
public class OtkazivanjeTerminaServiceTests
{
    private OtkazivanjeTerminaService _servis = null!;
    private DateTime _sada;

    [TestInitialize]
    public void Setup()
    {
        _servis = new OtkazivanjeTerminaService();
        _sada = DateTime.Now;
    }

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

    [TestMethod]
    [DynamicData(nameof(NedozvoljenoOtkazivanje), DynamicDataSourceType.Method)]
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
    [DynamicData(nameof(DozvoljenoOtkazivanje), DynamicDataSourceType.Method)]
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
