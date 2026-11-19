using System.Diagnostics;
using System.Globalization;
using HealthZone.Controllers;
using HealthZone.Models;
using HealthZone.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za HomeControllerTests ────────────
public static class HomeControllerPodaci
{
    // ── pomoćne metode ──

    // Mockovi vraćaju doktore i sestre, a servis za recenzije za svakog doktora ukupnu ocjenu iz XML-a.
    public static void PripremiOsoblje(Mock<IKorisnikService> korisnikServis, Mock<IRecenzijaService> recenzijaServis,
                                       Dictionary<string, double> ocjeneDoktora, int brojSestara)
    {
        korisnikServis.Setup(s => s.GetDoktoriAsync())
                      .ReturnsAsync(ocjeneDoktora.Keys.Select(id => new Korisnik { Id = id }).ToList());
        korisnikServis.Setup(s => s.GetMedicinskeSestreAsync())
                      .ReturnsAsync(Enumerable.Range(0, brojSestara).Select(_ => new Korisnik()).ToList());
        recenzijaServis.Setup(s => s.GetProsjecneOcjeneAsync(It.IsAny<string>()))
                       .ReturnsAsync((string id) => (0d, 0d, 0d, ocjeneDoktora[id]));
    }

    // ── izvori podataka (CSV) ──

    public static IEnumerable<object[]> Prikaz() =>
        CsvLoader.Ucitaj("HomeController_prikaz.csv").Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> Greska() =>
        CsvLoader.Ucitaj("HomeController_greska.csv").Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> Aktivnost() =>
        CsvLoader.Ucitaj("HomeController_aktivnost.csv").Select(r => new object[] { r[0] });

    // ── izvori podataka (XML) ──

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
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class HomeControllerTests
{
    private static string _folderPodataka = null!;

    private Mock<IKorisnikService> _korisnikServis = null!;
    private Mock<IRecenzijaService> _recenzijaServis = null!;
    private HomeController _controller = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // Jednom za cijelu klasu: provjera da su CSV i XML fajlovi kopirani uz testove.
        _folderPodataka = Path.Combine(AppContext.BaseDirectory, "TestData");
        Assert.IsTrue(Directory.Exists(_folderPodataka), "Folder TestData nije kopiran u output.");
    }

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

    [TestCleanup]
    public void Cleanup()
    {
        _controller.Dispose();
        _korisnikServis.Reset();
        _recenzijaServis.Reset();
    }

    // ─── Index, Privacy, Error ────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(HomeControllerPodaci.Prikaz), typeof(HomeControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public void Index_Poziv_VracaView(string ocekivaniRezultat)
    {
        // Act
        var rezultat = _controller.Index();

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    [TestMethod]
    [DynamicData(nameof(HomeControllerPodaci.Prikaz), typeof(HomeControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void Privacy_Poziv_VracaView(string ocekivaniRezultat)
    {
        // Act
        var rezultat = _controller.Privacy();

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    [TestMethod]
    [DynamicData(nameof(HomeControllerPodaci.Greska), typeof(HomeControllerPodaci), DynamicDataSourceType.Method)]
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
    [DynamicData(nameof(HomeControllerPodaci.Aktivnost), typeof(HomeControllerPodaci), DynamicDataSourceType.Method)]
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

    // ─── MedicinskoOsoblje ────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(HomeControllerPodaci.MedicinskoOsoblje), typeof(HomeControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public async Task MedicinskoOsoblje_PostojeDoktori_UViewBagStavljaOcjenuSvakogDoktora(Dictionary<string, double> ocjeneDoktora, int brojSestara)
    {
        // Arrange
        HomeControllerPodaci.PripremiOsoblje(_korisnikServis, _recenzijaServis, ocjeneDoktora, brojSestara);

        // Act
        await _controller.MedicinskoOsoblje();

        // Assert
        CollectionAssert.AreEquivalent(ocjeneDoktora, (Dictionary<string, double>)_controller.ViewBag.Ocjene);
    }

    [TestMethod]
    [DynamicData(nameof(HomeControllerPodaci.MedicinskoOsoblje), typeof(HomeControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task MedicinskoOsoblje_PostojeDoktori_TraziOcjenuZaSvakogDoktora(Dictionary<string, double> ocjeneDoktora, int brojSestara)
    {
        // Arrange
        HomeControllerPodaci.PripremiOsoblje(_korisnikServis, _recenzijaServis, ocjeneDoktora, brojSestara);

        // Act
        await _controller.MedicinskoOsoblje();

        // Assert
        _recenzijaServis.Verify(s => s.GetProsjecneOcjeneAsync(It.IsAny<string>()), Times.Exactly(ocjeneDoktora.Count));
    }

    [TestMethod]
    [DynamicData(nameof(HomeControllerPodaci.MedicinskoOsoblje), typeof(HomeControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task MedicinskoOsoblje_PostojeDoktori_UViewBagStavljaDoktore(Dictionary<string, double> ocjeneDoktora, int brojSestara)
    {
        // Arrange
        HomeControllerPodaci.PripremiOsoblje(_korisnikServis, _recenzijaServis, ocjeneDoktora, brojSestara);

        // Act
        await _controller.MedicinskoOsoblje();

        // Assert
        Assert.AreEqual(ocjeneDoktora.Count, ((IEnumerable<Korisnik>)_controller.ViewBag.Doktori).Count());
    }

    [TestMethod]
    [DynamicData(nameof(HomeControllerPodaci.MedicinskoOsoblje), typeof(HomeControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task MedicinskoOsoblje_PostojeSestre_UViewBagStavljaSestre(Dictionary<string, double> ocjeneDoktora, int brojSestara)
    {
        // Arrange
        HomeControllerPodaci.PripremiOsoblje(_korisnikServis, _recenzijaServis, ocjeneDoktora, brojSestara);

        // Act
        await _controller.MedicinskoOsoblje();

        // Assert
        Assert.AreEqual(brojSestara, ((IEnumerable<Korisnik>)_controller.ViewBag.Sestre).Count());
    }

    [TestMethod]
    [DynamicData(nameof(HomeControllerPodaci.MedicinskoOsoblje), typeof(HomeControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task MedicinskoOsoblje_PostojeSestre_TraziSestreOdServisa(Dictionary<string, double> ocjeneDoktora, int brojSestara)
    {
        // Arrange
        HomeControllerPodaci.PripremiOsoblje(_korisnikServis, _recenzijaServis, ocjeneDoktora, brojSestara);

        // Act
        await _controller.MedicinskoOsoblje();

        // Assert
        _korisnikServis.Verify(s => s.GetMedicinskeSestreAsync(), Times.Once);
    }
}
