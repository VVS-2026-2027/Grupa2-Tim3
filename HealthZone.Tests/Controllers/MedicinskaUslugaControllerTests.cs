using System.Globalization;
using HealthZone.Controllers;
using HealthZone.Models;
using HealthZone.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HealthZone.Tests;

[TestClass]
public class MedicinskaUslugaControllerTests
{
    private Mock<IMedicinskaUslugaService> _servis = null!;
    private MedicinskaUslugaController _controller = null!;

    [TestInitialize]
    public void Setup()
    {
        _servis = new Mock<IMedicinskaUslugaService>();
        _controller = new MedicinskaUslugaController(_servis.Object);
    }

    // ───────────── Izvori podataka (CSV i XML) ─────────────

    public static IEnumerable<object[]> PostojecaUsluga() =>
        CsvLoader.Ucitaj("MedicinskaUslugaController_postojecaUsluga.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> NepostojecaUsluga() =>
        CsvLoader.Ucitaj("MedicinskaUslugaController_nepostojecaUsluga.csv")
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

    public static IEnumerable<object[]> Usluge() =>
        XmlLoader.Ucitaj("MedicinskaUslugaController_usluge.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     x.Elements("usluga")
                      .Select(u => new MedicinskaUsluga { UslugaId = (int)u.Element("id")!, Naziv = (string)u.Element("naziv")! })
                      .ToList()
                 });

    // ───────────── Index ─────────────

    [TestMethod]
    [DynamicData(nameof(Usluge), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
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
        _servis.Verify(s => s.GetAllAsync(), Times.Once);
    }

    // ───────────── Details ─────────────

    [TestMethod]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Details_IdNijeProslijedjen_VracaNotFound()
    {
        // Act
        var rezultat = await _controller.Details(null);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(NepostojecaUsluga), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Details_UslugaNePostoji_VracaNotFound(int id)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((MedicinskaUsluga?)null);

        // Act
        var rezultat = await _controller.Details(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        _servis.Verify(s => s.GetByIdAsync(id), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(PostojecaUsluga), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
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

    // ───────────── Create ─────────────

    [TestMethod]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void Create_Get_VracaView()
    {
        // Act
        var rezultat = _controller.Create();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(ValidnaUsluga), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public async Task Create_ValidanModel_DodajeUsluguIPreusmjeravaNaIndex(int id, string naziv, decimal cijena)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { UslugaId = id, Naziv = naziv, Cijena = cijena };

        // Act
        var rezultat = (RedirectToActionResult)await _controller.Create(usluga);

        // Assert
        Assert.AreEqual(nameof(MedicinskaUslugaController.Index), rezultat.ActionName);
        _servis.Verify(s => s.AddAsync(usluga), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NevalidanModel), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_NevalidanModel_VracaIstuFormuINeDodaje(string polje, string greska, string naziv)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { Naziv = naziv };
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        var rezultat = (ViewResult)await _controller.Create(usluga);

        // Assert
        Assert.AreSame(usluga, rezultat.Model);
        _servis.Verify(s => s.AddAsync(It.IsAny<MedicinskaUsluga>()), Times.Never);
    }

    // ───────────── Edit (GET) ─────────────

    [TestMethod]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_IdNijeProslijedjen_VracaNotFound()
    {
        // Act
        var rezultat = await _controller.Edit(null);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(NepostojecaUsluga), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_UslugaNePostoji_VracaNotFound(int id)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((MedicinskaUsluga?)null);

        // Act
        var rezultat = await _controller.Edit(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(PostojecaUsluga), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
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

    // ───────────── Edit (POST) ─────────────

    [TestMethod]
    [DynamicData(nameof(RazlicitId), DynamicDataSourceType.Method)]
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
    [DynamicData(nameof(ValidnaUsluga), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_ValidanModel_AzuriraISnimaPaPreusmjerava(int id, string naziv, decimal cijena)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { UslugaId = id, Naziv = naziv, Cijena = cijena };

        // Act
        var rezultat = (RedirectToActionResult)await _controller.Edit(id, usluga);

        // Assert
        Assert.AreEqual(nameof(MedicinskaUslugaController.Index), rezultat.ActionName);
        _servis.Verify(s => s.Update(usluga), Times.Once);
        _servis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NevalidanModel), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_NevalidanModel_VracaIstuFormuINeAzurira(string polje, string greska, string naziv)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { Naziv = naziv };
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        var rezultat = (ViewResult)await _controller.Edit(usluga.UslugaId, usluga);

        // Assert
        Assert.AreSame(usluga, rezultat.Model);
        _servis.Verify(s => s.Update(It.IsAny<MedicinskaUsluga>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(Konkurentnost), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
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
    [DynamicData(nameof(Konkurentnost), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
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

    // ───────────── Delete ─────────────

    [TestMethod]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Delete_IdNijeProslijedjen_VracaNotFound()
    {
        // Act
        var rezultat = await _controller.Delete(null);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(NepostojecaUsluga), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Delete_UslugaNePostoji_VracaNotFound(int id)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((MedicinskaUsluga?)null);

        // Act
        var rezultat = await _controller.Delete(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(PostojecaUsluga), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
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
    [DynamicData(nameof(PostojecaUsluga), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_UslugaPostoji_BriseISnima(int id, string naziv)
    {
        // Arrange
        var usluga = new MedicinskaUsluga { UslugaId = id, Naziv = naziv };
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(usluga);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(nameof(MedicinskaUslugaController.Index), rezultat.ActionName);
        _servis.Verify(s => s.Delete(usluga), Times.Once);
        _servis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NepostojecaUsluga), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_UslugaNePostoji_NistaNeBriseIPreusmjerava(int id)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((MedicinskaUsluga?)null);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(nameof(MedicinskaUslugaController.Index), rezultat.ActionName);
        _servis.Verify(s => s.Delete(It.IsAny<MedicinskaUsluga>()), Times.Never);
    }
}
