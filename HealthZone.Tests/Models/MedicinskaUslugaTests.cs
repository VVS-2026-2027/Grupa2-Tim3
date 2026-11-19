using System.ComponentModel.DataAnnotations;
using HealthZone.Models;

namespace HealthZone.Tests;

[TestClass]
public class MedicinskaUslugaTests
{
    // Vraća poruke svih validacijskih grešaka modela ([Required], [Range]), redom kojim su svojstva definisana.
    private static List<string> Greske(object model)
    {
        var rezultati = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), rezultati, true);
        return rezultati.Select(r => r.ErrorMessage!).ToList();
    }

    // Podaci iz XML-a: element koji nedostaje znači da polje nije uneseno (null).
    public static IEnumerable<object[]> Validacija() =>
        XmlLoader.Ucitaj("MedicinskaUsluga_validacija.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string?)x.Element("naziv")!, (string?)x.Element("opis")!, (decimal)x.Element("cijena")!,
                     ((string)x.Element("ocekivaneGreske")!).Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
                 });

    [TestMethod]
    [DynamicData(nameof(Validacija), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public void Validacija_RazlicitiPodaci_VracaOcekivaneGreske(string? naziv, string? opis, decimal cijena, List<string> ocekivaneGreske)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { Naziv = naziv!, Opis = opis!, Cijena = cijena };

        // Act
        var greske = Greske(usluga);

        // Assert
        CollectionAssert.AreEqual(ocekivaneGreske, greske);
    }
}
