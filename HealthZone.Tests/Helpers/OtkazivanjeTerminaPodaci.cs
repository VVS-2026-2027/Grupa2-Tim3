using System.Globalization;
using HealthZone.Models;

namespace HealthZone.Tests;

// Izvori podataka za [DynamicData] u OtkazivanjeTerminaServiceTests (TDD F2-A).
public static class OtkazivanjeTerminaPodaci
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
