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

    [TestMethod]
    [DynamicData(nameof(OtkazivanjeTerminaPodaci.NedozvoljenoOtkazivanje), typeof(OtkazivanjeTerminaPodaci), DynamicDataSourceType.Method)]
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
    [DynamicData(nameof(OtkazivanjeTerminaPodaci.DozvoljenoOtkazivanje), typeof(OtkazivanjeTerminaPodaci), DynamicDataSourceType.Method)]
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
