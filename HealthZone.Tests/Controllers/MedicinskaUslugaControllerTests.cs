using System.Globalization;
using HealthZone.Controllers;
using HealthZone.Models;
using HealthZone.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za MedicinskaUslugaControllerTests ─
public static class MedicinskaUslugaControllerPodaci
{
    // ── pomoćne metode ──

    // Prazan id iz CSV-a znači da id nije proslijeđen (null).
    public static int? IdIliNull(string s) => string.IsNullOrEmpty(s) ? null : int.Parse(s);

    // ── izvori podataka (CSV) ──

    public static IEnumerable<object[]> Prikaz() =>
        CsvLoader.Ucitaj("MedicinskaUslugaController_prikaz.csv").Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> PostojecaUsluga() =>
        CsvLoader.Ucitaj("MedicinskaUslugaController_postojecaUsluga.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1] });

    // Svi slučajevi kad usluga nije pronađena: id nije proslijeđen (null) ili ne postoji.
    public static IEnumerable<object?[]> NepostojecaUsluga() =>
        CsvLoader.Ucitaj("MedicinskaUslugaController_nepostojecaUsluga.csv")
                 .Select(r => new object?[] { IdIliNull(r[0]) });

    // Samo redovi s id-jem (servis se tada stvarno pita za uslugu).
    public static IEnumerable<object[]> NepostojecaUslugaSaId() =>
        CsvLoader.Ucitaj("MedicinskaUslugaController_nepostojecaUsluga.csv")
                 .Where(r => !string.IsNullOrEmpty(r[0]))
                 .Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> ValidnaUsluga() =>
        CsvLoader.Ucitaj("MedicinskaUslugaController_validnaUsluga.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1], decimal.Parse(r[2], CultureInfo.InvariantCulture) });

    // Polje i poruka greške kojom se ModelState proglašava nevalidnim.
    public static IEnumerable<object[]> NevalidanModel() =>
        CsvLoader.Ucitaj("MedicinskaUslugaController_nevalidanModel.csv")
                 .Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> RazlicitId() =>
        CsvLoader.Ucitaj("MedicinskaUslugaController_razlicitId.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]) });

    public static IEnumerable<object[]> Konkurentnost() =>
        CsvLoader.Ucitaj("MedicinskaUslugaController_konkurentnost.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1] });

    // ── izvori podataka (XML) ──

    public static IEnumerable<object[]> Usluge() =>
        XmlLoader.Ucitaj("MedicinskaUslugaController_usluge.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     x.Elements("usluga")
                      .Select(u => new MedicinskaUsluga { UslugaId = (int)u.Element("id")!, Naziv = (string)u.Element("naziv")! })
                      .ToList()
                 });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class MedicinskaUslugaControllerTests
{
    private static string _folderPodataka = null!;

    private Mock<IMedicinskaUslugaService> _servis = null!;
    private MedicinskaUslugaController _controller = null!;

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
        _servis = new Mock<IMedicinskaUslugaService>();
        _controller = new MedicinskaUslugaController(_servis.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _controller.Dispose();
        _servis.Reset();
    }

    // ─── Index ────────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.Usluge), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task Index_PostojeUsluge_VracaViewSaSvimUslugama(List<MedicinskaUsluga> usluge)
    {
        // Arrange
        _servis.Setup(s => s.GetAllAsync()).ReturnsAsync(usluge);

        // Act
        var rezultat = (ViewResult)await _controller.Index();

        // Assert
        Assert.AreSame(usluge, rezultat.Model);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.Usluge), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Index_PostojeUsluge_TraziUslugeOdServisa(List<MedicinskaUsluga> usluge)
    {
        // Arrange
        _servis.Setup(s => s.GetAllAsync()).ReturnsAsync(usluge);

        // Act
        await _controller.Index();

        // Assert
        _servis.Verify(s => s.GetAllAsync(), Times.Once);
    }

    // ─── Details ──────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.NepostojecaUsluga), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Details_IdNijeProslijedjenIliUslugaNePostoji_VracaNotFound(int? id)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((MedicinskaUsluga?)null);

        // Act
        var rezultat = await _controller.Details(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.NepostojecaUslugaSaId), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Details_UslugaNePostoji_TraziUsluguOdServisa(int id)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((MedicinskaUsluga?)null);

        // Act
        await _controller.Details(id);

        // Assert
        _servis.Verify(s => s.GetByIdAsync(id), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.PostojecaUsluga), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Details_UslugaPostoji_VracaViewSUslugom(int id, string naziv)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(new MedicinskaUsluga { UslugaId = id, Naziv = naziv });

        // Act
        var rezultat = (ViewResult)await _controller.Details(id);

        // Assert
        Assert.AreEqual(naziv, ((MedicinskaUsluga)rezultat.Model!).Naziv);
    }

    // ─── Create ───────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.Prikaz), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void Create_Get_VracaPraznuFormu(string ocekivaniRezultat)
    {
        // Act
        var rezultat = _controller.Create();

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.ValidnaUsluga), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_ValidanModel_PreusmjeravaNaIndex(int id, string naziv, decimal cijena)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { UslugaId = id, Naziv = naziv, Cijena = cijena };

        // Act
        var rezultat = (RedirectToActionResult)await _controller.Create(usluga);

        // Assert
        Assert.AreEqual(nameof(MedicinskaUslugaController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.ValidnaUsluga), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public async Task Create_ValidanModel_DodajeUslugu(int id, string naziv, decimal cijena)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { UslugaId = id, Naziv = naziv, Cijena = cijena };

        // Act
        await _controller.Create(usluga);

        // Assert
        _servis.Verify(s => s.AddAsync(usluga), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.NevalidanModel), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_NevalidanModel_VracaIstuFormu(string polje, string greska, string naziv)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { Naziv = naziv };
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        var rezultat = (ViewResult)await _controller.Create(usluga);

        // Assert
        Assert.AreSame(usluga, rezultat.Model);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.NevalidanModel), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_NevalidanModel_NeDodajeUslugu(string polje, string greska, string naziv)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { Naziv = naziv };
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        await _controller.Create(usluga);

        // Assert
        _servis.Verify(s => s.AddAsync(It.IsAny<MedicinskaUsluga>()), Times.Never);
    }

    // ─── Edit (GET) ───────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.NepostojecaUsluga), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_IdNijeProslijedjenIliUslugaNePostoji_VracaNotFound(int? id)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((MedicinskaUsluga?)null);

        // Act
        var rezultat = await _controller.Edit(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.PostojecaUsluga), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_UslugaPostoji_VracaFormuSUslugom(int id, string naziv)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(new MedicinskaUsluga { UslugaId = id, Naziv = naziv });

        // Act
        var rezultat = (ViewResult)await _controller.Edit(id);

        // Assert
        Assert.AreEqual(naziv, ((MedicinskaUsluga)rezultat.Model!).Naziv);
    }

    // ─── Edit (POST) ──────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.RazlicitId), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_IdIzRuteRazlicitOdIdUsluge_VracaNotFound(int idIzRute, int idUsluge)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { UslugaId = idUsluge };

        // Act
        var rezultat = await _controller.Edit(idIzRute, usluga);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.ValidnaUsluga), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_ValidanModel_PreusmjeravaNaIndex(int id, string naziv, decimal cijena)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { UslugaId = id, Naziv = naziv, Cijena = cijena };

        // Act
        var rezultat = (RedirectToActionResult)await _controller.Edit(id, usluga);

        // Assert
        Assert.AreEqual(nameof(MedicinskaUslugaController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.ValidnaUsluga), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_ValidanModel_AzuriraUslugu(int id, string naziv, decimal cijena)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { UslugaId = id, Naziv = naziv, Cijena = cijena };

        // Act
        await _controller.Edit(id, usluga);

        // Assert
        _servis.Verify(s => s.Update(usluga), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.ValidnaUsluga), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_ValidanModel_SnimaPromjene(int id, string naziv, decimal cijena)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { UslugaId = id, Naziv = naziv, Cijena = cijena };

        // Act
        await _controller.Edit(id, usluga);

        // Assert
        _servis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.NevalidanModel), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_NevalidanModel_VracaIstuFormu(string polje, string greska, string naziv)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { Naziv = naziv };
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        var rezultat = (ViewResult)await _controller.Edit(usluga.UslugaId, usluga);

        // Assert
        Assert.AreSame(usluga, rezultat.Model);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.NevalidanModel), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_NevalidanModel_NeAzuriraUslugu(string polje, string greska, string naziv)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { Naziv = naziv };
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        await _controller.Edit(usluga.UslugaId, usluga);

        // Assert
        _servis.Verify(s => s.Update(It.IsAny<MedicinskaUsluga>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.Konkurentnost), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_UslugaObrisanaUMedjuvremenu_VracaNotFound(int id, string poruka)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { UslugaId = id };
        _servis.Setup(s => s.SaveChangesAsync()).ThrowsAsync(new DbUpdateConcurrencyException(poruka));
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((MedicinskaUsluga?)null);

        // Act
        var rezultat = await _controller.Edit(id, usluga);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.Konkurentnost), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_KonfliktAUslugaPostoji_PonovoBacaIzuzetak(int id, string poruka)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { UslugaId = id };
        _servis.Setup(s => s.SaveChangesAsync()).ThrowsAsync(new DbUpdateConcurrencyException(poruka));
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(usluga);

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(() => _controller.Edit(id, usluga));

        // Assert
        Assert.AreEqual(poruka, izuzetak.Message);
    }

    // ─── Delete ───────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.NepostojecaUsluga), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Delete_IdNijeProslijedjenIliUslugaNePostoji_VracaNotFound(int? id)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((MedicinskaUsluga?)null);

        // Act
        var rezultat = await _controller.Delete(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.PostojecaUsluga), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Delete_UslugaPostoji_VracaPotvrduSUslugom(int id, string naziv)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(new MedicinskaUsluga { UslugaId = id, Naziv = naziv });

        // Act
        var rezultat = (ViewResult)await _controller.Delete(id);

        // Assert
        Assert.AreEqual(naziv, ((MedicinskaUsluga)rezultat.Model!).Naziv);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.PostojecaUsluga), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_UslugaPostoji_PreusmjeravaNaIndex(int id, string naziv)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(new MedicinskaUsluga { UslugaId = id, Naziv = naziv });

        // Act
        var rezultat = (RedirectToActionResult)await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(nameof(MedicinskaUslugaController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.PostojecaUsluga), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_UslugaPostoji_BriseUslugu(int id, string naziv)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { UslugaId = id, Naziv = naziv };
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(usluga);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _servis.Verify(s => s.Delete(usluga), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.PostojecaUsluga), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_UslugaPostoji_SnimaPromjene(int id, string naziv)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(new MedicinskaUsluga { UslugaId = id, Naziv = naziv });

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _servis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.NepostojecaUslugaSaId), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_UslugaNePostoji_PreusmjeravaNaIndex(int id)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((MedicinskaUsluga?)null);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(nameof(MedicinskaUslugaController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(MedicinskaUslugaControllerPodaci.NepostojecaUslugaSaId), typeof(MedicinskaUslugaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_UslugaNePostoji_NistaNeBrise(int id)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((MedicinskaUsluga?)null);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _servis.Verify(s => s.Delete(It.IsAny<MedicinskaUsluga>()), Times.Never);
    }
}
