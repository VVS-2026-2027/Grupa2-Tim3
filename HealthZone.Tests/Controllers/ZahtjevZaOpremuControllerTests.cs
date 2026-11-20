using System.Security.Claims;
using System.Xml.Linq;
using HealthZone.Controllers;
using HealthZone.Models;
using HealthZone.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za ZahtjevZaOpremuControllerTests ─
public static class ZahtjevZaOpremuControllerPodaci
{
    // ── pomoćne metode ──

    // Vrijednosti koje kontroler stavlja u ViewData i TempData.
    public static SelectList ListaDoktora(Controller kontroler) => (SelectList)kontroler.ViewData["DoktorID"]!;

    public static object? PorukaGreske(Controller kontroler) => kontroler.TempData["Greška"];

    public static ZahtjevZaOpremu NapraviZahtjev(XElement x) => new()
    {
        ZahtjevId = (int)x.Element("id")!,
        Naziv = (string)x.Element("naziv")!,
        DoktorID = (string)x.Element("doktorId")!,
        Status = Enum.Parse<Status>((string)x.Element("status")!)
    };

    // Prazan id iz CSV-a znači da id nije proslijeđen (null).
    public static int? IdIliNull(string s) => string.IsNullOrEmpty(s) ? null : int.Parse(s);

    public static ZahtjevZaOpremu Nadji(List<ZahtjevZaOpremu> baza, int id) => baza.First(z => z.ZahtjevId == id);

    // Postavlja prijavljenog doktora (akcije čitaju njegov Id iz User claima).
    public static void PrijaviDoktora(Controller controller, string doktorId)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, doktorId) }, "Test"))
            }
        };
    }

    // ── izvori podataka (CSV) ──

    public static IEnumerable<object[]> SviZahtjevi() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuController_sviZahtjevi.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> VlastitiZahtjev() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuController_vlastitiZahtjev.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> TudjiZahtjev() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuController_tudjiZahtjev.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> ObradjenZahtjev() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuController_obradjenZahtjev.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    // Svi slučajevi kad zahtjev nije pronađen: id nije proslijeđen (null) ili ne postoji.
    public static IEnumerable<object?[]> NepostojeciZahtjev() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuController_nepostojeciZahtjev.csv").Select(r => new object?[] { IdIliNull(r[0]) });

    // Samo redovi s id-jem (za akcije koje primaju obavezan id).
    public static IEnumerable<object[]> NepostojeciZahtjevSaId() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuController_nepostojeciZahtjev.csv")
                 .Where(r => !string.IsNullOrEmpty(r[0]))
                 .Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> NoviZahtjev() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuController_noviZahtjev.csv").Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> GreskaServisa() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuController_greskaServisa.csv").Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> NevalidanModel() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuController_nevalidanModel.csv").Select(r => new object[] { r[0], r[1] });

    public static IEnumerable<object[]> RazlicitId() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuController_razlicitId.csv").Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]) });

    public static IEnumerable<object[]> Konkurentnost() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuController_konkurentnost.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> GreskaObrade() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuController_greskaObrade.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> Obrada() =>
        CsvLoader.Ucitaj("ZahtjevZaOpremuController_obrada.csv").Select(r => new object[] { int.Parse(r[0]) });

    // ── izvori podataka (XML) ──

    public static IEnumerable<object[]> Doktori() =>
        XmlLoader.Ucitaj("ZahtjevZaOpremuController_doktori.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     x.Elements("doktor")
                      .Select(d => new Korisnik { Id = (string)d.Element("id")!, Ime = (string)d.Element("ime")! })
                      .ToList()
                 });

    public static IEnumerable<object[]> IgnorisanaPolja() =>
        XmlLoader.Ucitaj("ZahtjevZaOpremuController_ignorisanaPolja.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("polje")!, (string)x.Element("greska")!, (string)x.Element("prijavljeniDoktorId")!
                 });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class ZahtjevZaOpremuControllerTests
{
    private static List<XElement> _bazaZahtjeva = null!;   // samo za čitanje

    private Mock<IZahtjevZaOpremuService> _zahtjevServis = null!;
    private Mock<IKorisnikService> _korisnikServis = null!;
    private ZahtjevZaOpremuController _controller = null!;
    private List<ZahtjevZaOpremu> _baza = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // XML "baza" se čita jednom; objekti se iz nje prave ispočetka za svaki test.
        _bazaZahtjeva = XmlLoader.Ucitaj("ZahtjevZaOpremuController_zahtjevi.xml", "zahtjev").ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        _baza = _bazaZahtjeva.Select(ZahtjevZaOpremuControllerPodaci.NapraviZahtjev).ToList();

        _zahtjevServis = new Mock<IZahtjevZaOpremuService>();
        _zahtjevServis.Setup(s => s.GetAllAsync()).ReturnsAsync(_baza);
        _zahtjevServis.Setup(s => s.GetByIdAsync(It.IsAny<int>()))
                      .ReturnsAsync((int id) => _baza.FirstOrDefault(z => z.ZahtjevId == id));

        _korisnikServis = new Mock<IKorisnikService>();
        _korisnikServis.Setup(s => s.GetDoktoriAsync()).ReturnsAsync(new List<Korisnik>());

        _controller = new ZahtjevZaOpremuController(_zahtjevServis.Object, _korisnikServis.Object)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        _controller.Dispose();
        _zahtjevServis.Reset();
        _korisnikServis.Reset();
    }

    // ─── Index ────────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.SviZahtjevi), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task Index_PostojeZahtjevi_VracaViewSaSvimZahtjevima(int brojZahtjeva)
    {
        // Act
        var rezultat = (ViewResult)await _controller.Index();

        // Assert
        Assert.AreEqual(brojZahtjeva, ((IEnumerable<ZahtjevZaOpremu>)rezultat.Model!).Count());
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.SviZahtjevi), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Index_PostojeZahtjevi_TraziZahtjeveOdServisa(int brojZahtjeva)
    {
        // Act
        await _controller.Index();

        // Assert
        _zahtjevServis.Verify(s => s.GetAllAsync(), Times.Once);
    }

    // ─── Details i Dokumentacija ──────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.NepostojeciZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Details_IdNijeProslijedjenIliZahtjevNePostoji_VracaNotFound(int? id)
    {
        // Act
        var rezultat = await _controller.Details(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.NepostojeciZahtjevSaId), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Details_ZahtjevNePostoji_TraziZahtjevOdServisa(int id)
    {
        // Act
        await _controller.Details(id);

        // Assert
        _zahtjevServis.Verify(s => s.GetByIdAsync(id), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.VlastitiZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Details_ZahtjevPostoji_VracaViewSaZahtjevom(int id, string doktorId, string naziv)
    {
        // Act
        var rezultat = (ViewResult)await _controller.Details(id);

        // Assert
        Assert.AreEqual(naziv, ((ZahtjevZaOpremu)rezultat.Model!).Naziv);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.NepostojeciZahtjevSaId), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Dokumentacija_ZahtjevNePostoji_VracaNotFound(int id)
    {
        // Act
        var rezultat = await _controller.Dokumentacija(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.VlastitiZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Dokumentacija_ZahtjevPostoji_VracaViewSaZahtjevom(int id, string doktorId, string naziv)
    {
        // Act
        var rezultat = (ViewResult)await _controller.Dokumentacija(id);

        // Assert
        Assert.AreEqual(naziv, ((ZahtjevZaOpremu)rezultat.Model!).Naziv);
    }

    // ─── Create ───────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.Doktori), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_Get_PuniListuDoktora(List<Korisnik> doktori)
    {
        // Arrange
        _korisnikServis.Setup(s => s.GetDoktoriAsync()).ReturnsAsync(doktori);

        // Act
        await _controller.Create();

        // Assert
        Assert.AreEqual(doktori.Count, ZahtjevZaOpremuControllerPodaci.ListaDoktora(_controller).Count());
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.Doktori), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_Get_TraziDoktoreOdServisa(List<Korisnik> doktori)
    {
        // Arrange
        _korisnikServis.Setup(s => s.GetDoktoriAsync()).ReturnsAsync(doktori);

        // Act
        await _controller.Create();

        // Assert
        _korisnikServis.Verify(s => s.GetDoktoriAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.NoviZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public async Task Create_ValidanZahtjev_DodajeZahtjevPrijavljenogDoktora(string prijavljeniDoktorId, string naziv, string opis)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, prijavljeniDoktorId);
        var zahtjev = new ZahtjevZaOpremu { Naziv = naziv, Opis = opis };

        // Act
        await _controller.Create(zahtjev);

        // Assert
        _zahtjevServis.Verify(s => s.AddAsync(It.Is<ZahtjevZaOpremu>(z => z.DoktorID == prijavljeniDoktorId)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.NoviZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_ValidanZahtjev_PreusmjeravaNaIndex(string prijavljeniDoktorId, string naziv, string opis)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, prijavljeniDoktorId);
        var zahtjev = new ZahtjevZaOpremu { Naziv = naziv, Opis = opis };

        // Act
        var rezultat = (RedirectToActionResult)await _controller.Create(zahtjev);

        // Assert
        Assert.AreEqual(nameof(ZahtjevZaOpremuController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.GreskaServisa), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_ServisBaciIzuzetak_PorukaIdeUModelState(string prijavljeniDoktorId, string naziv, string opis, string poruka)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, prijavljeniDoktorId);
        var zahtjev = new ZahtjevZaOpremu { Naziv = naziv, Opis = opis };
        _zahtjevServis.Setup(s => s.AddAsync(zahtjev)).ThrowsAsync(new Exception(poruka));

        // Act
        await _controller.Create(zahtjev);

        // Assert
        Assert.AreEqual(poruka, _controller.ModelState[string.Empty]!.Errors.Single().ErrorMessage);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.NevalidanModel), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_NevalidanModel_VracaIstuFormu(string polje, string greska)
    {
        // Arrange
        var zahtjev = new ZahtjevZaOpremu();
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        var rezultat = (ViewResult)await _controller.Create(zahtjev);

        // Assert
        Assert.AreSame(zahtjev, rezultat.Model);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.NevalidanModel), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_NevalidanModel_NeDodajeZahtjev(string polje, string greska)
    {
        // Arrange
        var zahtjev = new ZahtjevZaOpremu();
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        await _controller.Create(zahtjev);

        // Assert
        _zahtjevServis.Verify(s => s.AddAsync(It.IsAny<ZahtjevZaOpremu>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.IgnorisanaPolja), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_GreskaNaPoljuKojeNijeUFormi_IpakDodajeZahtjev(string polje, string greska, string prijavljeniDoktorId)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, prijavljeniDoktorId);
        var zahtjev = new ZahtjevZaOpremu();
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        await _controller.Create(zahtjev);

        // Assert
        _zahtjevServis.Verify(s => s.AddAsync(zahtjev), Times.Once);
    }

    // ─── Edit (GET) ───────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.NepostojeciZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_IdNijeProslijedjenIliZahtjevNePostoji_VracaNotFound(int? id)
    {
        // Act
        var rezultat = await _controller.Edit(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.ObradjenZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_ZahtjevVecObradjen_PreusmjeravaNaIndex(int id, string prijavljeniDoktorId)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, prijavljeniDoktorId);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.Edit(id);

        // Assert
        Assert.AreEqual(nameof(ZahtjevZaOpremuController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.TudjiZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_TudjiZahtjev_PreusmjeravaNaIndex(int id, string prijavljeniDoktorId)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, prijavljeniDoktorId);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.Edit(id);

        // Assert
        Assert.AreEqual(nameof(ZahtjevZaOpremuController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.VlastitiZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_VlastitiZahtjevNaCekanju_VracaFormuSaZahtjevom(int id, string doktorId, string naziv)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, doktorId);

        // Act
        var rezultat = (ViewResult)await _controller.Edit(id);

        // Assert
        Assert.AreEqual(naziv, ((ZahtjevZaOpremu)rezultat.Model!).Naziv);
    }

    // ─── Edit (POST) ──────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.RazlicitId), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_IdIzRuteRazlicitOdIdZahtjeva_VracaNotFound(int idIzRute, int idZahtjeva)
    {
        // Arrange
        var zahtjev = new ZahtjevZaOpremu { ZahtjevId = idZahtjeva };

        // Act
        var rezultat = await _controller.Edit(idIzRute, zahtjev);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.NevalidanModel), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_NevalidanModel_VracaIstuFormu(string polje, string greska)
    {
        // Arrange
        var zahtjev = new ZahtjevZaOpremu();
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        var rezultat = (ViewResult)await _controller.Edit(zahtjev.ZahtjevId, zahtjev);

        // Assert
        Assert.AreSame(zahtjev, rezultat.Model);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.NevalidanModel), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_NevalidanModel_NeAzuriraZahtjev(string polje, string greska)
    {
        // Arrange
        var zahtjev = new ZahtjevZaOpremu();
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        await _controller.Edit(zahtjev.ZahtjevId, zahtjev);

        // Assert
        _zahtjevServis.Verify(s => s.Update(It.IsAny<ZahtjevZaOpremu>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.TudjiZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_TudjiZahtjevPost_PreusmjeravaNaIndex(int id, string prijavljeniDoktorId)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, prijavljeniDoktorId);
        var zahtjev = ZahtjevZaOpremuControllerPodaci.Nadji(_baza, id);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.Edit(id, zahtjev);

        // Assert
        Assert.AreEqual(nameof(ZahtjevZaOpremuController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.TudjiZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_TudjiZahtjevPost_NeAzuriraZahtjev(int id, string prijavljeniDoktorId)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, prijavljeniDoktorId);
        var zahtjev = ZahtjevZaOpremuControllerPodaci.Nadji(_baza, id);

        // Act
        await _controller.Edit(id, zahtjev);

        // Assert
        _zahtjevServis.Verify(s => s.Update(It.IsAny<ZahtjevZaOpremu>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.VlastitiZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_VlastitiZahtjevPost_PreusmjeravaNaIndex(int id, string doktorId, string naziv)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, doktorId);
        var zahtjev = ZahtjevZaOpremuControllerPodaci.Nadji(_baza, id);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.Edit(id, zahtjev);

        // Assert
        Assert.AreEqual(nameof(ZahtjevZaOpremuController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.VlastitiZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_VlastitiZahtjevPost_AzuriraZahtjev(int id, string doktorId, string naziv)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, doktorId);
        var zahtjev = ZahtjevZaOpremuControllerPodaci.Nadji(_baza, id);

        // Act
        await _controller.Edit(id, zahtjev);

        // Assert
        _zahtjevServis.Verify(s => s.Update(It.Is<ZahtjevZaOpremu>(z => z.Naziv == naziv)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.VlastitiZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_VlastitiZahtjevPost_SnimaPromjene(int id, string doktorId, string naziv)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, doktorId);
        var zahtjev = ZahtjevZaOpremuControllerPodaci.Nadji(_baza, id);

        // Act
        await _controller.Edit(id, zahtjev);

        // Assert
        _zahtjevServis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.Konkurentnost), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_ZahtjevObrisanUMedjuvremenu_VracaNotFound(int id, string doktorId, string poruka)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, doktorId);
        var zahtjev = ZahtjevZaOpremuControllerPodaci.Nadji(_baza, id);
        _zahtjevServis.Setup(s => s.SaveChangesAsync()).ThrowsAsync(new DbUpdateConcurrencyException(poruka));
        _zahtjevServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((ZahtjevZaOpremu?)null);

        // Act
        var rezultat = await _controller.Edit(id, zahtjev);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.Konkurentnost), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Edit_KonfliktAZahtjevPostoji_PonovoBacaIzuzetak(int id, string doktorId, string poruka)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, doktorId);
        var zahtjev = ZahtjevZaOpremuControllerPodaci.Nadji(_baza, id);
        _zahtjevServis.Setup(s => s.SaveChangesAsync()).ThrowsAsync(new DbUpdateConcurrencyException(poruka));

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(() => _controller.Edit(id, zahtjev));

        // Assert
        Assert.AreEqual(poruka, izuzetak.Message);
    }

    // ─── Delete ───────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.NepostojeciZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Delete_IdNijeProslijedjenIliZahtjevNePostoji_VracaNotFound(int? id)
    {
        // Act
        var rezultat = await _controller.Delete(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.TudjiZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Delete_TudjiZahtjev_PreusmjeravaNaIndex(int id, string prijavljeniDoktorId)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, prijavljeniDoktorId);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.Delete(id);

        // Assert
        Assert.AreEqual(nameof(ZahtjevZaOpremuController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.VlastitiZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Delete_VlastitiZahtjev_VracaPotvrduSaZahtjevom(int id, string doktorId, string naziv)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, doktorId);

        // Act
        var rezultat = (ViewResult)await _controller.Delete(id);

        // Assert
        Assert.AreEqual(naziv, ((ZahtjevZaOpremu)rezultat.Model!).Naziv);
    }

    // ─── DeleteConfirmed ──────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.NepostojeciZahtjevSaId), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_ZahtjevNePostoji_PreusmjeravaNaIndex(int id)
    {
        // Act
        var rezultat = (RedirectToActionResult)await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(nameof(ZahtjevZaOpremuController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.NepostojeciZahtjevSaId), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_ZahtjevNePostoji_NistaNeBrise(int id)
    {
        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _zahtjevServis.Verify(s => s.Delete(It.IsAny<ZahtjevZaOpremu>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.TudjiZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_TudjiZahtjev_PreusmjeravaNaIndex(int id, string prijavljeniDoktorId)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, prijavljeniDoktorId);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(nameof(ZahtjevZaOpremuController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.TudjiZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_TudjiZahtjev_NeBriseZahtjev(int id, string prijavljeniDoktorId)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, prijavljeniDoktorId);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _zahtjevServis.Verify(s => s.Delete(It.IsAny<ZahtjevZaOpremu>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.VlastitiZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_VlastitiZahtjev_PreusmjeravaNaIndex(int id, string doktorId, string naziv)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, doktorId);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(nameof(ZahtjevZaOpremuController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.VlastitiZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_VlastitiZahtjev_BriseZahtjev(int id, string doktorId, string naziv)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, doktorId);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _zahtjevServis.Verify(s => s.Delete(It.Is<ZahtjevZaOpremu>(z => z.Naziv == naziv)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.VlastitiZahtjev), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_VlastitiZahtjev_SnimaPromjene(int id, string doktorId, string naziv)
    {
        // Arrange
        ZahtjevZaOpremuControllerPodaci.PrijaviDoktora(_controller, doktorId);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _zahtjevServis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    // ─── Odobri i Odbij ───────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.Obrada), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Odobri_ZahtjevNaCekanju_PreusmjeravaNaIndex(int id)
    {
        // Act
        var rezultat = (RedirectToActionResult)await _controller.Odobri(id);

        // Assert
        Assert.AreEqual(nameof(ZahtjevZaOpremuController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.Obrada), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Odobri_ZahtjevNaCekanju_PozivaServisZaOdobravanje(int id)
    {
        // Act
        await _controller.Odobri(id);

        // Assert
        _zahtjevServis.Verify(s => s.OdobriZahtjevAsync(id), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.GreskaObrade), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Odobri_ServisBaciIzuzetak_PorukaIdeUTempData(int id, string poruka)
    {
        // Arrange
        _zahtjevServis.Setup(s => s.OdobriZahtjevAsync(id)).ThrowsAsync(new Exception(poruka));

        // Act
        await _controller.Odobri(id);

        // Assert
        Assert.AreEqual(poruka, ZahtjevZaOpremuControllerPodaci.PorukaGreske(_controller));
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.Obrada), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Odbij_ZahtjevNaCekanju_PreusmjeravaNaIndex(int id)
    {
        // Act
        var rezultat = (RedirectToActionResult)await _controller.Odbij(id);

        // Assert
        Assert.AreEqual(nameof(ZahtjevZaOpremuController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.Obrada), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Odbij_ZahtjevNaCekanju_PozivaServisZaOdbijanje(int id)
    {
        // Act
        await _controller.Odbij(id);

        // Assert
        _zahtjevServis.Verify(s => s.OdbijZahtjevAsync(id), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ZahtjevZaOpremuControllerPodaci.GreskaObrade), typeof(ZahtjevZaOpremuControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Odbij_ServisBaciIzuzetak_PorukaIdeUTempData(int id, string poruka)
    {
        // Arrange
        _zahtjevServis.Setup(s => s.OdbijZahtjevAsync(id)).ThrowsAsync(new Exception(poruka));

        // Act
        await _controller.Odbij(id);

        // Assert
        Assert.AreEqual(poruka, ZahtjevZaOpremuControllerPodaci.PorukaGreske(_controller));
    }
}
