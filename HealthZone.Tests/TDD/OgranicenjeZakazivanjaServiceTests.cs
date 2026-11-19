using System.Globalization;
using HealthZone.Services;

namespace HealthZone.Tests;

[TestClass]
public class OgranicenjeZakazivanjaServiceTests
{
    private OgranicenjeZakazivanjaService _servis = null!;
    private DateTime _sada;

    [TestInitialize]
    public void Setup()
    {
        _servis = new OgranicenjeZakazivanjaService();
        _sada = DateTime.Now;
    }

    // CSV: očekivana poruka kad lista otkazivanja nije proslijeđena (null).
    public static IEnumerable<object[]> NullLista() =>
        CsvLoader.Ucitaj("OgranicenjeZakazivanjaService_nullLista.csv")
                 .Select(r => new object[] { r[0] });

    // CSV: pomaci u danima od "sada" (razdvojeni zarezom, negativno = u prošlosti)
    // za provjeru koja otkazivanja ulaze u zadnjih 30 dana.
    public static IEnumerable<object[]> PeriodOd30Dana() =>
        CsvLoader.Ucitaj("OgranicenjeZakazivanjaService_periodOd30Dana.csv")
                 .Select(r => new object[]
                 {
                     r[0].Split(',', StringSplitOptions.RemoveEmptyEntries)
                         .Select(p => double.Parse(p, CultureInfo.InvariantCulture))
                         .ToArray(),
                     bool.Parse(r[1])
                 });

    // XML: različit broj otkazivanja unutar zadnjih 30 dana (granica je 3).
    public static IEnumerable<object[]> BrojOtkazivanja() =>
        XmlLoader.Ucitaj("OgranicenjeZakazivanjaService_brojOtkazivanja.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     x.Elements("otkazivanje")
                      .Select(o => double.Parse((string)o.Attribute("pomakDana")!, CultureInfo.InvariantCulture))
                      .ToArray(),
                     bool.Parse((string)x.Element("mozeZakazati")!)
                 });

    [TestMethod]
    [DynamicData(nameof(NullLista), DynamicDataSourceType.Method)]
    [TestCategory("TDD"), TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public void MozeZakazatiNoviTermin_ListaJeNull_BacaException(string ocekivanaPoruka)
    {
        // Act
        var izuzetak = Assert.ThrowsException<Exception>(() => _servis.MozeZakazatiNoviTermin(null!, _sada));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(BrojOtkazivanja), DynamicDataSourceType.Method)]
    [TestCategory("TDD"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public void MozeZakazatiNoviTermin_RazlicitBrojOtkazivanja_TriIViseZabranjujeZakazivanje(double[] pomaciDana, bool mozeZakazati)
    {
        // Arrange
        var datumiOtkazivanja = pomaciDana.Select(p => _sada.AddDays(p)).ToList();

        // Act
        var rezultat = _servis.MozeZakazatiNoviTermin(datumiOtkazivanja, _sada);

        // Assert
        Assert.AreEqual(mozeZakazati, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(PeriodOd30Dana), DynamicDataSourceType.Method)]
    [TestCategory("TDD"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void MozeZakazatiNoviTermin_OtkazivanjaUnutarIVanPerioda_BrojeSamoZadnjih30Dana(double[] pomaciDana, bool mozeZakazati)
    {
        // Arrange
        var datumiOtkazivanja = pomaciDana.Select(p => _sada.AddDays(p)).ToList();

        // Act
        var rezultat = _servis.MozeZakazatiNoviTermin(datumiOtkazivanja, _sada);

        // Assert
        Assert.AreEqual(mozeZakazati, rezultat);
    }
}
