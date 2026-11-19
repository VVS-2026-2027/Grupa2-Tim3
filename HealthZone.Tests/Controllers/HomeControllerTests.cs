using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using HealthZone.Controllers;
using HealthZone.Models;
using HealthZone.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace HealthZone.Tests;

[TestClass]
public class HomeControllerTests
{
    private Mock<IKorisnikService> _korisnikServis = null!;
    private Mock<IRecenzijaService> _recenzijaServis = null!;
    private HomeController _controller = null!;

    [TestInitialize]
    public void Setup()
    {
        _korisnikServis = new Mock<IKorisnikService>();
        _recenzijaServis = new Mock<IRecenzijaService>();
        _controller = new HomeController(_korisnikServis.Object, _recenzijaServis.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    // ───────────── Izvori podataka (CSV i XML) ─────────────

    public static IEnumerable<object[]> Greska() =>
        CsvLoader.Ucitaj("HomeController_greska.csv")
                 .Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> Aktivnost() =>
        CsvLoader.Ucitaj("HomeController_aktivnost.csv")
                 .Select(r => new object[] { r[0] });

    // Doktori s ukupnom ocjenom koju servis za recenzije treba vratiti, i broj medicinskih sestara.
    public static IEnumerable<object[]> MedicinskoOsoblje() =>
        XmlLoader.Ucitaj("HomeController_medicinskoOsoblje.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     x.Elements("doktor").ToDictionary(
                         d => (string)d.Element("id")!,
                         d => double.Parse((string)d.Element("ukupno")!, CultureInfo.InvariantCulture)),
                     (int)x.Element("brojSestara")!
                 });

    // Postavlja mockove: servis za korisnike vraća doktore i sestre,
    // a servis za recenzije za svakog doktora vraća ukupnu ocjenu iz XML-a.
    private void PripremiOsoblje(Dictionary<string, double> ocjeneDoktora, int brojSestara)
    {
        _korisnikServis.Setup(s => s.GetDoktoriAsync())
                       .ReturnsAsync(ocjeneDoktora.Keys.Select(id => new Korisnik { Id = id }).ToList());
        _korisnikServis.Setup(s => s.GetMedicinskeSestreAsync())
                       .ReturnsAsync(Enumerable.Range(0, brojSestara).Select(_ => new Korisnik()).ToList());
        _recenzijaServis.Setup(s => s.GetProsjecneOcjeneAsync(It.IsAny<string>()))
                        .ReturnsAsync((string id) => (0d, 0d, 0d, ocjeneDoktora[id]));
    }

    // ───────────── Index, Privacy, Error ─────────────

    [TestMethod]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public void Index_Poziv_VracaView()
    {
        // Act
        var rezultat = _controller.Index();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void Privacy_Poziv_VracaView()
    {
        // Act
        var rezultat = _controller.Privacy();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(Greska), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void Error_NemaAktivnosti_ModelSadrziIdentifikatorZahtjeva(string traceIdentifier)
    {
        // Arrange
        _controller.HttpContext.TraceIdentifier = traceIdentifier;

        // Act
        var rezultat = (ViewResult)_controller.Error();

        // Assert
        Assert.AreEqual(traceIdentifier, ((ErrorViewModel)rezultat.Model!).RequestId);
    }

    [TestMethod]
    [DynamicData(nameof(Aktivnost), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void Error_PostojiAktivnost_ModelSadrziIdAktivnosti(string nazivAktivnosti)
    {
        // Arrange
        using var aktivnost = new Activity(nazivAktivnosti).Start();

        // Act
        var rezultat = (ViewResult)_controller.Error();

        // Assert
        Assert.AreEqual(aktivnost.Id, ((ErrorViewModel)rezultat.Model!).RequestId);
    }

    // ───────────── MedicinskoOsoblje ─────────────

    [TestMethod]
    [DynamicData(nameof(MedicinskoOsoblje), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public async Task MedicinskoOsoblje_PostojeDoktori_UViewBagStavljaOcjenuSvakogDoktora(Dictionary<string, double> ocjeneDoktora, int brojSestara)
    {
        // Arrange
        PripremiOsoblje(ocjeneDoktora, brojSestara);

        // Act
        await _controller.MedicinskoOsoblje();

        // Assert
        CollectionAssert.AreEquivalent(ocjeneDoktora, (Dictionary<string, double>)_controller.ViewBag.Ocjene);
        _recenzijaServis.Verify(s => s.GetProsjecneOcjeneAsync(It.IsAny<string>()), Times.Exactly(ocjeneDoktora.Count));
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskoOsoblje), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task MedicinskoOsoblje_PostojeSestre_UViewBagStavljaSestre(Dictionary<string, double> ocjeneDoktora, int brojSestara)
    {
        // Arrange
        PripremiOsoblje(ocjeneDoktora, brojSestara);

        // Act
        await _controller.MedicinskoOsoblje();

        // Assert
        Assert.AreEqual(brojSestara, ((IEnumerable<Korisnik>)_controller.ViewBag.Sestre).Count());
        _korisnikServis.Verify(s => s.GetMedicinskeSestreAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskoOsoblje), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task MedicinskoOsoblje_PostojeDoktori_VracaViewSDoktorimaUViewBagu(Dictionary<string, double> ocjeneDoktora, int brojSestara)
    {
        // Arrange
        PripremiOsoblje(ocjeneDoktora, brojSestara);

        // Act
        var rezultat = await _controller.MedicinskoOsoblje();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
        Assert.AreEqual(ocjeneDoktora.Count, ((IEnumerable<Korisnik>)_controller.ViewBag.Doktori).Count());
    }
}
