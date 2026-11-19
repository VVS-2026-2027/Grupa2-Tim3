using System.Xml.Linq;
using HealthZone.Models;
using HealthZone.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za NotifikacijaControllerTests ─────
public static class NotifikacijaControllerPodaci
{
    public static IEnumerable<object[]> NepostojeciIdCsv() =>
        CsvLoader.Ucitaj("NotifikacijaController_nepostojeciId.csv")
                 .Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> AkcijeXml() =>
        XmlLoader.Ucitaj("NotifikacijaController_akcije.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (int)x.Element("id")!,
                     (string)x.Element("poruka")!,
                     (string)x.Element("ocekivaniTip")!
                 });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class NotifikacijaControllerTests
{
    private static List<XElement> _podaciAkcije = null!;

    private Mock<INotifikacijaService> _servis = null!;
    private NotifikacijaController _controller = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _podaciAkcije = XmlLoader.Ucitaj("NotifikacijaController_akcije.xml", "scenarij").ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        _servis = new Mock<INotifikacijaService>();
        _controller = new NotifikacijaController(_servis.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _controller.Dispose();
        _servis.Reset();
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.AkcijeXml), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Index_PozivAkcije_VracaViewSaNotifikacijama(int id, string poruka, string ocekivaniTip)
    {
        // Arrange
        var lista = new List<Notifikacija> { new Notifikacija { NotifikacijaId = id, Poruka = poruka } };
        _servis.Setup(s => s.GetAllAsync()).ReturnsAsync(lista);

        // Act
        var rezultat = await _controller.Index();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.NepostojeciIdCsv), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Details_NotifikacijaNePostoji_VracaNotFound(int id)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((Notifikacija?)null);

        // Act
        var rezultat = await _controller.Details(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        _servis.Verify(s => s.GetByIdAsync(id), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.AkcijeXml), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Details_IdJeNull_VracaNotFound(int id, string poruka, string ocekivaniTip)
    {
        // Arrange & Act
        var rezultat = await _controller.Details(null);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.AkcijeXml), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Details_NotifikacijaPostoji_VracaView(int id, string poruka, string ocekivaniTip)
    {
        // Arrange
        var notif = new Notifikacija { NotifikacijaId = id, Poruka = poruka };
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(notif);

        // Act
        var rezultat = await _controller.Details(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.AkcijeXml), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public void Create_GetAkcija_VracaView(int id, string poruka, string ocekivaniTip)
    {
        // Arrange & Act
        var rezultat = _controller.Create();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.AkcijeXml), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Create_ValidanModel_PozivaAddIRedirektuje(int id, string poruka, string ocekivaniTip)
    {
        // Arrange
        var notif = new Notifikacija { NotifikacijaId = id, Poruka = poruka, TerminID = 1 };

        // Act
        var rezultat = await _controller.Create(notif);

        // Assert
        _servis.Verify(s => s.AddAsync(notif), Times.Once);
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.AkcijeXml), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Create_NevalidanModel_VracaView(int id, string poruka, string ocekivaniTip)
    {
        // Arrange
        _controller.ModelState.AddModelError("Poruka", "Obavezno polje");
        var notif = new Notifikacija { NotifikacijaId = id };

        // Act
        var rezultat = await _controller.Create(notif);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.AkcijeXml), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Edit_GetIdNull_VracaNotFound(int id, string poruka, string ocekivaniTip)
    {
        // Arrange & Act
        var rezultat = await _controller.Edit(null);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.NepostojeciIdCsv), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Edit_GetNepostojecaNotifikacija_VracaNotFound(int id)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((Notifikacija?)null);

        // Act
        var rezultat = await _controller.Edit(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.AkcijeXml), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Edit_GetPostojecaNotifikacija_VracaView(int id, string poruka, string ocekivaniTip)
    {
        // Arrange
        var notif = new Notifikacija { NotifikacijaId = id, Poruka = poruka };
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(notif);

        // Act
        var rezultat = await _controller.Edit(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.AkcijeXml), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Edit_PostIdMismatch_VracaNotFound(int id, string poruka, string ocekivaniTip)
    {
        // Arrange
        var notif = new Notifikacija { NotifikacijaId = id + 1 };

        // Act
        var rezultat = await _controller.Edit(id, notif);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.AkcijeXml), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Edit_PostValidanModel_AzuriraIRedirektuje(int id, string poruka, string ocekivaniTip)
    {
        // Arrange
        var notif = new Notifikacija { NotifikacijaId = id, Poruka = poruka };

        // Act
        var rezultat = await _controller.Edit(id, notif);

        // Assert
        _servis.Verify(s => s.Update(notif), Times.Once);
        _servis.Verify(s => s.SaveChangesAsync(), Times.Once);
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.AkcijeXml), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Edit_PostNevalidanModel_VracaView(int id, string poruka, string ocekivaniTip)
    {
        // Arrange
        _controller.ModelState.AddModelError("Poruka", "Greska");
        var notif = new Notifikacija { NotifikacijaId = id };

        // Act
        var rezultat = await _controller.Edit(id, notif);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.AkcijeXml), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Delete_GetIdNull_VracaNotFound(int id, string poruka, string ocekivaniTip)
    {
        // Arrange & Act
        var rezultat = await _controller.Delete(null);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.NepostojeciIdCsv), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Delete_GetNepostojecaNotifikacija_VracaNotFound(int id)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((Notifikacija?)null);

        // Act
        var rezultat = await _controller.Delete(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.AkcijeXml), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Delete_GetPostojecaNotifikacija_VracaView(int id, string poruka, string ocekivaniTip)
    {
        // Arrange
        var notif = new Notifikacija { NotifikacijaId = id, Poruka = poruka };
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(notif);

        // Act
        var rezultat = await _controller.Delete(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.AkcijeXml), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task DeleteConfirmed_PostojecaNotifikacija_BriseIRedirektuje(int id, string poruka, string ocekivaniTip)
    {
        // Arrange
        var notif = new Notifikacija { NotifikacijaId = id, Poruka = poruka };
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(notif);

        // Act
        var rezultat = await _controller.DeleteConfirmed(id);

        // Assert
        _servis.Verify(s => s.Delete(notif), Times.Once);
        _servis.Verify(s => s.SaveChangesAsync(), Times.Once);
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
    }
}
