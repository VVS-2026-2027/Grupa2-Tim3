using System.ComponentModel.DataAnnotations;
using HealthZone.Models;

namespace HealthZone.Tests;

[TestClass]
public class RecenzijaTests
{
    // Vraća poruke svih validacijskih grešaka modela ([Required], [Range]), redom kojim su svojstva definisana.
    private static List<string> Greske(object model)
    {
        var rezultati = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), rezultati, true);
        return rezultati.Select(r => r.ErrorMessage!).ToList();
    }

    // Podaci iz CSV-a: ocjene i poruke koje validacija treba vratiti (razdvojene znakom |, prazno = validno).
    public static IEnumerable<object[]> Validacija() =>
        CsvLoader.Ucitaj("Recenzija_validacija.csv")
                 .Select(r => new object[]
                 {
                     r[0], r[1], int.Parse(r[2]), int.Parse(r[3]), int.Parse(r[4]),
                     r[5].Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
                 });

    [TestMethod]
    [DynamicData(nameof(Validacija), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public void Validacija_RazliciteOcjene_VracaOcekivaneGreske(
        string pacijentId, string doktorId, int ljubaznost, int profesionalnost, int usluga, List<string> ocekivaneGreske)
    {
        // Arrange
        var recenzija = new Recenzija
        {
            PacijentID = pacijentId,
            DoktorID = doktorId,
            OcjenaLjubaznosti = ljubaznost,
            OcjenaProfesionalnosti = profesionalnost,
            OcjenaUsluge = usluga
        };

        // Act
        var greske = Greske(recenzija);

        // Assert
        CollectionAssert.AreEqual(ocekivaneGreske, greske);
    }
}
