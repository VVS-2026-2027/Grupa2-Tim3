using System.ComponentModel.DataAnnotations;
using HealthZone.Models;

namespace HealthZone.Tests;

[TestClass]
public class ZahtjevZaOpremuTests
{
    // Vraća poruke svih validacijskih grešaka modela ([Required] i sl.), redom kojim su svojstva definisana.
    private static List<string> Greske(object model)
    {
        var rezultati = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), rezultati, true);
        return rezultati.Select(r => r.ErrorMessage!).ToList();
    }

    // Podaci iz XML-a: element koji nedostaje znači da polje nije uneseno (null).
    public static IEnumerable<object[]> Validacija() =>
        XmlLoader.Ucitaj("ZahtjevZaOpremu_validacija.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string?)x.Element("naziv")!, (string?)x.Element("opis")!, (string?)x.Element("doktorId")!,
                     ((string)x.Element("ocekivaneGreske")!).Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
                 });

    public static IEnumerable<object[]> Doktor() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremu_doktor.csv")
                 .Select(r => new object[] { r[0], r[1], Enum.Parse<VrstaZahtjeva>(r[2]) });

    [TestMethod]
    [DynamicData(nameof(Validacija), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public void Validacija_RazlicitiPodaci_VracaOcekivaneGreske(string? naziv, string? opis, string? doktorId, List<string> ocekivaneGreske)
    {
        // Arrange
        var zahtjev = new ZahtjevZaOpremu { Naziv = naziv!, Opis = opis!, DoktorID = doktorId! };

        // Act
        var greske = Greske(zahtjev);

        // Assert
        CollectionAssert.AreEqual(ocekivaneGreske, greske);
    }

    [TestMethod]
    [DynamicData(nameof(Doktor), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void Doktor_PostavljenDoktorIVrsta_CuvaPostavljeneVrijednosti(string doktorId, string ime, VrstaZahtjeva vrsta)
    {
        // Arrange
        var doktor = new Korisnik { Id = doktorId, Ime = ime };

        // Act
        var zahtjev = new ZahtjevZaOpremu { DoktorID = doktorId, Doktor = doktor, VrstaZahtjeva = vrsta };

        // Assert
        Assert.AreSame(doktor, zahtjev.Doktor);
        Assert.AreEqual(vrsta, zahtjev.VrstaZahtjeva);
    }
}
