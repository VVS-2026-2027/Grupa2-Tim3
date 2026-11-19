using HealthZone.Models;
using HealthZone.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za NotifikacijaControllerTests ────
public static class NotifikacijaControllerPodaci
{
    // ── pomoćne metode ──

    public static Notifikacija NapraviNotifikaciju(int id, int terminId, string poruka) =>
        new() { NotifikacijaId = id, TerminID = terminId, Poruka = poruka };

    public static List<Notifikacija> NapraviNotifikacije(int[] ids) =>
        ids.Select(id => new Notifikacija { NotifikacijaId = id }).ToList();

    // Prazno CSV polje znači da id nije proslijeđen.
    public static int? PrazanUNull(string vrijednost) => string.IsNullOrEmpty(vrijednost) ? null : int.Parse(vrijednost);

    public static int[] Ids(string lista) =>
        lista.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();

    public static int[] IdsIzModela(IActionResult rezultat) =>
        ((IEnumerable<Notifikacija>)((ViewResult)rezultat).Model!).Select(n => n.NotifikacijaId).ToArray();

    // ── izvori podataka ──

    public static IEnumerable<object[]> Index() =>
        XmlLoader.Ucitaj("NotifikacijaController_index.xml", "scenarij")
                 .Select(x => new object[] { Ids((string)x.Element("ids")!) });

    public static IEnumerable<object[]> BezIdIliNepostojece() =>
        CsvLoader.Ucitaj("NotifikacijaController_bezIdIliNepostojece.csv").Select(r => new object[] { PrazanUNull(r[0])! });

    public static IEnumerable<object[]> NepostojeciIds() =>
        CsvLoader.Ucitaj("NotifikacijaController_nepostojeciId.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> Postojece() =>
        CsvLoader.Ucitaj("NotifikacijaController_postojece.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), r[2] });

    public static IEnumerable<object[]> Prikaz() =>
        CsvLoader.Ucitaj("NotifikacijaController_prikaz.csv").Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> Nove() =>
        XmlLoader.Ucitaj("NotifikacijaController_nove.xml", "notifikacija").Select(x => new object[]
        {
            (int)x.Element("terminId")!,
            (string)x.Element("poruka")!
        });

    public static IEnumerable<object[]> NevalidanModel() =>
        CsvLoader.Ucitaj("NotifikacijaController_nevalidanModel.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), r[2], r[3] });

    public static IEnumerable<object[]> RazlicitId() =>
        CsvLoader.Ucitaj("NotifikacijaController_razlicitId.csv")
                 .Select(r => new object[] { PrazanUNull(r[0])!, int.Parse(r[1]) });

    private static IEnumerable<string[]> Konkurentnost() => CsvLoader.Ucitaj("NotifikacijaController_konkurentnost.csv");

    public static IEnumerable<object[]> KonkurentnostObrisana() =>
        Konkurentnost().Where(r => !bool.Parse(r[1])).Select(r => new object[] { int.Parse(r[0]), r[2] });

    public static IEnumerable<object[]> KonkurentnostPostoji() =>
        Konkurentnost().Where(r => bool.Parse(r[1])).Select(r => new object[] { int.Parse(r[0]), r[2] });
}

// ─── Testovi ────────────────────────────────────────────────────────────
// INotifikacijaService je zamijenjen Moq objektom, pa kontroler ne treba bazu.
[TestClass]
public class NotifikacijaControllerTests
{
    private static Type _tipNotFound = null!;   // samo za čitanje

    private Mock<INotifikacijaService> _servis = null!;
    private NotifikacijaController _controller = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _tipNotFound = typeof(NotFoundResult);
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

    // ─── INDEX ────────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.Index), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Index_PostojeNotifikacije_VracaViewSaSvimNotifikacijama(int[] ids)
    {
        // Arrange
        _servis.Setup(s => s.GetAllAsync()).ReturnsAsync(NotifikacijaControllerPodaci.NapraviNotifikacije(ids));

        // Act
        var rezultat = await _controller.Index();

        // Assert
        CollectionAssert.AreEquivalent(ids, NotifikacijaControllerPodaci.IdsIzModela(rezultat));
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.Index), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Index_Poziv_CitaNotifikacijeIzServisa(int[] ids)
    {
        // Arrange
        _servis.Setup(s => s.GetAllAsync()).ReturnsAsync(NotifikacijaControllerPodaci.NapraviNotifikacije(ids));

        // Act
        await _controller.Index();

        // Assert
        _servis.Verify(s => s.GetAllAsync(), Times.Once);
    }

    // ─── DETAILS ──────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.BezIdIliNepostojece), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Details_NemaIdIliNotifikacijaNePostoji_VracaNotFound(int? id)
    {
        // Arrange (servis za nepostojeći id vraća null)

        // Act
        var rezultat = await _controller.Details(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, _tipNotFound);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.Postojece), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Details_NotifikacijaPostoji_VracaViewSaNotifikacijom(int id, int terminId, string poruka)
    {
        // Arrange
        var notifikacija = NotifikacijaControllerPodaci.NapraviNotifikaciju(id, terminId, poruka);
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(notifikacija);

        // Act
        var rezultat = await _controller.Details(id);

        // Assert
        Assert.AreSame(notifikacija, ((ViewResult)rezultat).Model);
    }

    // ─── CREATE ───────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.Prikaz), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Create_Get_VracaPraznuFormu(string ocekivaniRezultat)
    {
        // Arrange (forma ne treba nikakve podatke)

        // Act
        var rezultat = _controller.Create();

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.Nove), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Create_Post_ValidanModel_DodajeNotifikacijuKrozServis(int terminId, string poruka)
    {
        // Arrange
        var notifikacija = new Notifikacija { TerminID = terminId, Poruka = poruka };

        // Act
        await _controller.Create(notifikacija);

        // Assert
        _servis.Verify(s => s.AddAsync(notifikacija), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.Nove), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_ValidanModel_PreusmjeravaNaIndex(int terminId, string poruka)
    {
        // Arrange
        var notifikacija = new Notifikacija { TerminID = terminId, Poruka = poruka };

        // Act
        var rezultat = await _controller.Create(notifikacija);

        // Assert
        Assert.AreEqual(nameof(NotifikacijaController.Index), ((RedirectToActionResult)rezultat).ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.NevalidanModel), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_NevalidanModel_VracaFormuSaIstomNotifikacijom(int id, int terminId, string polje, string greska)
    {
        // Arrange
        var notifikacija = new Notifikacija { NotifikacijaId = id, TerminID = terminId };
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        var rezultat = await _controller.Create(notifikacija);

        // Assert
        Assert.AreSame(notifikacija, ((ViewResult)rezultat).Model);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.NevalidanModel), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_NevalidanModel_NeDodajeNotifikaciju(int id, int terminId, string polje, string greska)
    {
        // Arrange
        var notifikacija = new Notifikacija { NotifikacijaId = id, TerminID = terminId };
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        await _controller.Create(notifikacija);

        // Assert
        _servis.Verify(s => s.AddAsync(It.IsAny<Notifikacija>()), Times.Never);
    }

    // ─── EDIT (GET) ───────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.BezIdIliNepostojece), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Get_NemaIdIliNotifikacijaNePostoji_VracaNotFound(int? id)
    {
        // Arrange (servis za nepostojeći id vraća null)

        // Act
        var rezultat = await _controller.Edit(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, _tipNotFound);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.Postojece), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Get_NotifikacijaPostoji_VracaFormuSaNotifikacijom(int id, int terminId, string poruka)
    {
        // Arrange
        var notifikacija = NotifikacijaControllerPodaci.NapraviNotifikaciju(id, terminId, poruka);
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(notifikacija);

        // Act
        var rezultat = await _controller.Edit(id);

        // Assert
        Assert.AreSame(notifikacija, ((ViewResult)rezultat).Model);
    }

    // ─── EDIT (POST) ──────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.RazlicitId), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_IdRuteRazlicitOdNotifikacije_VracaNotFound(int? idRute, int idNotifikacije)
    {
        // Arrange
        var notifikacija = new Notifikacija { NotifikacijaId = idNotifikacije };

        // Act
        var rezultat = await _controller.Edit(idRute, notifikacija);

        // Assert
        Assert.IsInstanceOfType(rezultat, _tipNotFound);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.Postojece), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Edit_Post_ValidanModel_AzuriraNotifikaciju(int id, int terminId, string poruka)
    {
        // Arrange
        var notifikacija = NotifikacijaControllerPodaci.NapraviNotifikaciju(id, terminId, poruka);

        // Act
        await _controller.Edit(id, notifikacija);

        // Assert
        _servis.Verify(s => s.Update(notifikacija), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.Postojece), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_ValidanModel_SnimaPromjene(int id, int terminId, string poruka)
    {
        // Arrange
        var notifikacija = NotifikacijaControllerPodaci.NapraviNotifikaciju(id, terminId, poruka);

        // Act
        await _controller.Edit(id, notifikacija);

        // Assert
        _servis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.Postojece), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_ValidanModel_PreusmjeravaNaIndex(int id, int terminId, string poruka)
    {
        // Arrange
        var notifikacija = NotifikacijaControllerPodaci.NapraviNotifikaciju(id, terminId, poruka);

        // Act
        var rezultat = await _controller.Edit(id, notifikacija);

        // Assert
        Assert.AreEqual(nameof(NotifikacijaController.Index), ((RedirectToActionResult)rezultat).ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.KonkurentnostObrisana), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_NotifikacijaObrisanaUMedjuvremenu_VracaNotFound(int id, string poruka)
    {
        // Arrange
        _servis.Setup(s => s.SaveChangesAsync()).ThrowsAsync(new DbUpdateConcurrencyException(poruka));
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((Notifikacija?)null);

        // Act
        var rezultat = await _controller.Edit(id, new Notifikacija { NotifikacijaId = id });

        // Assert
        Assert.IsInstanceOfType(rezultat, _tipNotFound);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.KonkurentnostPostoji), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_KonfliktANotifikacijaPostoji_PonovoBacaIzuzetak(int id, string poruka)
    {
        // Arrange
        _servis.Setup(s => s.SaveChangesAsync()).ThrowsAsync(new DbUpdateConcurrencyException(poruka));
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(new Notifikacija { NotifikacijaId = id });

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(
            () => _controller.Edit(id, new Notifikacija { NotifikacijaId = id }));

        // Assert
        Assert.AreEqual(poruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.NevalidanModel), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_NevalidanModel_VracaFormuSaIstomNotifikacijom(int id, int terminId, string polje, string greska)
    {
        // Arrange
        var notifikacija = new Notifikacija { NotifikacijaId = id, TerminID = terminId };
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        var rezultat = await _controller.Edit(id, notifikacija);

        // Assert
        Assert.AreSame(notifikacija, ((ViewResult)rezultat).Model);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.NevalidanModel), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_NevalidanModel_NeSnimaPromjene(int id, int terminId, string polje, string greska)
    {
        // Arrange
        var notifikacija = new Notifikacija { NotifikacijaId = id, TerminID = terminId };
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        await _controller.Edit(id, notifikacija);

        // Assert
        _servis.Verify(s => s.SaveChangesAsync(), Times.Never);
    }

    // ─── DELETE ───────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.BezIdIliNepostojece), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Delete_Get_NemaIdIliNotifikacijaNePostoji_VracaNotFound(int? id)
    {
        // Arrange (servis za nepostojeći id vraća null)

        // Act
        var rezultat = await _controller.Delete(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, _tipNotFound);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.Postojece), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Delete_Get_NotifikacijaPostoji_VracaPotvrduSaNotifikacijom(int id, int terminId, string poruka)
    {
        // Arrange
        var notifikacija = NotifikacijaControllerPodaci.NapraviNotifikaciju(id, terminId, poruka);
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(notifikacija);

        // Act
        var rezultat = await _controller.Delete(id);

        // Assert
        Assert.AreSame(notifikacija, ((ViewResult)rezultat).Model);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.Postojece), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task DeleteConfirmed_NotifikacijaPostoji_BriseNotifikaciju(int id, int terminId, string poruka)
    {
        // Arrange
        var notifikacija = NotifikacijaControllerPodaci.NapraviNotifikaciju(id, terminId, poruka);
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(notifikacija);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _servis.Verify(s => s.Delete(notifikacija), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.Postojece), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_NotifikacijaPostoji_SnimaPromjene(int id, int terminId, string poruka)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(id))
               .ReturnsAsync(NotifikacijaControllerPodaci.NapraviNotifikaciju(id, terminId, poruka));

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _servis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.NepostojeciIds), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_NotifikacijaNePostoji_NistaNeBrise(int id)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((Notifikacija?)null);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _servis.Verify(s => s.Delete(It.IsAny<Notifikacija>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(NotifikacijaControllerPodaci.NepostojeciIds), typeof(NotifikacijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_BiloKojiId_PreusmjeravaNaIndex(int id)
    {
        // Arrange
        _servis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((Notifikacija?)null);

        // Act
        var rezultat = await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(nameof(NotifikacijaController.Index), ((RedirectToActionResult)rezultat).ActionName);
    }
}
