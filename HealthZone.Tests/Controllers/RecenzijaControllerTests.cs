using System.Security.Claims;
using System.Xml.Linq;
using HealthZone.Controllers;
using HealthZone.Models;
using HealthZone.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za RecenzijaControllerTests ───────
public static class RecenzijaControllerPodaci
{
    // ── pomoćne metode ──

    // Vrijednosti koje kontroler stavlja u ViewData i TempData.
    public static SelectList ListaDoktora(Controller kontroler) => (SelectList)kontroler.ViewData["DoktorID"]!;

    public static object? PorukaUspjeha(Controller kontroler) => kontroler.TempData["Uspjeh"];

    public static Recenzija NapraviRecenziju(XElement x) => new()
    {
        RecenzijaId = (int)x.Element("id")!,
        PacijentID = (string)x.Element("pacijentId")!,
        DoktorID = (string)x.Element("doktorId")!
    };

    public static Korisnik NapraviDoktora(XElement x) => new()
    {
        Id = (string)x.Element("id")!,
        Ime = (string)x.Element("ime")!,
        Prezime = (string)x.Element("prezime")!
    };

    // UserManager je klasa, pa mu Moq treba poseban konstruktor.
    public static Mock<UserManager<Korisnik>> NapraviUserManager()
        => new(Mock.Of<IUserStore<Korisnik>>(), null!, null!, null!, null!, null!, null!, null!, null!);

    // Prijavljuje korisnika s ulogom: User dobija claimove, a UserManager.GetUserAsync vraća tog korisnika.
    public static void Prijavi(Controller controller, Mock<UserManager<Korisnik>> userManager, string korisnikId, string uloga)
    {
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, korisnikId), new Claim(ClaimTypes.Role, uloga) }, "Test"));
        userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                   .ReturnsAsync(new Korisnik { Id = korisnikId });
    }

    // Naziv uloge pacijenta (koristi se u više testova), iz CSV-a.
    public static string UlogaPacijenta() =>
        CsvLoader.Ucitaj("RecenzijaController_ulogaPacijenta.csv").Single()[0];

    public static Recenzija NapraviNovuRecenziju(string doktorId, int ljubaznost, int profesionalnost, int usluga) => new()
    {
        DoktorID = doktorId,
        OcjenaLjubaznosti = ljubaznost,
        OcjenaProfesionalnosti = profesionalnost,
        OcjenaUsluge = usluga
    };

    // ── izvori podataka (CSV) ──

    public static IEnumerable<object[]> SveRecenzije() =>
        CsvLoader.Ucitaj("RecenzijaController_sveRecenzije.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> RecenzijeDoktora() =>
        CsvLoader.Ucitaj("RecenzijaController_recenzijeDoktora.csv").Select(r => new object[] { r[0], int.Parse(r[1]) });

    public static IEnumerable<object[]> NepostojeciDoktor() =>
        CsvLoader.Ucitaj("RecenzijaController_nepostojeciDoktor.csv").Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> Uloga() =>
        CsvLoader.Ucitaj("RecenzijaController_uloga.csv").Select(r => new object[] { r[0], r[1], bool.Parse(r[2]) });

    public static IEnumerable<object[]> OdabraniDoktor() =>
        CsvLoader.Ucitaj("RecenzijaController_odabraniDoktor.csv").Select(r => new object[] { r[0], int.Parse(r[1]) });

    public static IEnumerable<object[]> NovaRecenzija() =>
        CsvLoader.Ucitaj("RecenzijaController_novaRecenzija.csv")
                 .Select(r => new object[] { r[0], r[1], int.Parse(r[2]), int.Parse(r[3]), int.Parse(r[4]) });

    public static IEnumerable<object[]> GreskaServisa() =>
        CsvLoader.Ucitaj("RecenzijaController_greskaServisa.csv").Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> NevalidanModel() =>
        CsvLoader.Ucitaj("RecenzijaController_nevalidanModel.csv").Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> IgnorisanoPolje() =>
        CsvLoader.Ucitaj("RecenzijaController_ignorisanoPolje.csv").Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> VlastitaRecenzija() =>
        CsvLoader.Ucitaj("RecenzijaController_vlastitaRecenzija.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> TudjaRecenzija() =>
        CsvLoader.Ucitaj("RecenzijaController_tudjaRecenzija.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> AdminBrisanje() =>
        CsvLoader.Ucitaj("RecenzijaController_adminBrisanje.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> NepostojecaRecenzija() =>
        CsvLoader.Ucitaj("RecenzijaController_nepostojecaRecenzija.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class RecenzijaControllerTests
{
    // XML "baza" (samo za čitanje): recenzije i doktori.
    private static List<XElement> _bazaRecenzija = null!;
    private static List<XElement> _bazaDoktora = null!;

    private Mock<IRecenzijaService> _recenzijaServis = null!;
    private Mock<IKorisnikService> _korisnikServis = null!;
    private Mock<UserManager<Korisnik>> _userManager = null!;
    private RecenzijaController _controller = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _bazaRecenzija = XmlLoader.Ucitaj("RecenzijaController_recenzije.xml", "recenzija").ToList();
        _bazaDoktora = XmlLoader.Ucitaj("RecenzijaController_doktori.xml", "doktor").ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        var recenzije = _bazaRecenzija.Select(RecenzijaControllerPodaci.NapraviRecenziju).ToList();
        var doktori = _bazaDoktora.Select(RecenzijaControllerPodaci.NapraviDoktora).ToList();

        _recenzijaServis = new Mock<IRecenzijaService>();
        _recenzijaServis.Setup(s => s.GetAllAsync()).ReturnsAsync(recenzije);
        _recenzijaServis.Setup(s => s.GetByIdAsync(It.IsAny<int>()))
                        .ReturnsAsync((int id) => recenzije.FirstOrDefault(r => r.RecenzijaId == id));
        _recenzijaServis.Setup(s => s.GetRecenzijeDoktoraAsync(It.IsAny<string>()))
                        .ReturnsAsync((string doktorId) => recenzije.Where(r => r.DoktorID == doktorId).ToList());

        _korisnikServis = new Mock<IKorisnikService>();
        _korisnikServis.Setup(s => s.GetDoktoriAsync()).ReturnsAsync(doktori);
        _korisnikServis.Setup(s => s.GetDoktorByIdAsync(It.IsAny<string>()))
                       .ReturnsAsync((string id) => doktori.FirstOrDefault(d => d.Id == id));

        _userManager = RecenzijaControllerPodaci.NapraviUserManager();

        _controller = new RecenzijaController(_recenzijaServis.Object, _korisnikServis.Object, _userManager.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        _controller.Dispose();
        _recenzijaServis.Reset();
        _korisnikServis.Reset();
        _userManager.Reset();
    }

    // ─── Index ────────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.SveRecenzije), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task Index_PostojeRecenzije_VracaViewSaSvimRecenzijama(int brojRecenzija)
    {
        // Act
        var rezultat = (ViewResult)await _controller.Index();

        // Assert
        Assert.AreEqual(brojRecenzija, ((IEnumerable<Recenzija>)rezultat.Model!).Count());
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.SveRecenzije), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Index_PostojeRecenzije_TraziRecenzijeOdServisa(int brojRecenzija)
    {
        // Act
        await _controller.Index();

        // Assert
        _recenzijaServis.Verify(s => s.GetAllAsync(), Times.Once);
    }

    // ─── RecenzijeDoktora ─────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.NepostojeciDoktor), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task RecenzijeDoktora_DoktorNePostoji_VracaNotFound(string doktorId)
    {
        // Act
        var rezultat = await _controller.RecenzijeDoktora(doktorId);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.NepostojeciDoktor), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task RecenzijeDoktora_DoktorNePostoji_NeTraziRecenzije(string doktorId)
    {
        // Act
        await _controller.RecenzijeDoktora(doktorId);

        // Assert
        _recenzijaServis.Verify(s => s.GetRecenzijeDoktoraAsync(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.RecenzijeDoktora), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task RecenzijeDoktora_DoktorPostoji_VracaNjegoveRecenzije(string doktorId, int brojRecenzija)
    {
        // Act
        var rezultat = (ViewResult)await _controller.RecenzijeDoktora(doktorId);

        // Assert
        Assert.AreEqual(brojRecenzija, ((IEnumerable<Recenzija>)rezultat.Model!).Count());
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.RecenzijeDoktora), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task RecenzijeDoktora_DoktorPostoji_TraziProsjecneOcjene(string doktorId, int brojRecenzija)
    {
        // Act
        await _controller.RecenzijeDoktora(doktorId);

        // Assert
        _recenzijaServis.Verify(s => s.GetProsjecneOcjeneAsync(doktorId), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.RecenzijeDoktora), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task RecenzijeDoktora_DoktorPostoji_UViewBagStavljaDoktora(string doktorId, int brojRecenzija)
    {
        // Act
        await _controller.RecenzijeDoktora(doktorId);

        // Assert
        Assert.AreEqual(doktorId, ((Korisnik)_controller.ViewBag.Doktor).Id);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.Uloga), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task RecenzijeDoktora_RazliciteUloge_SamoPacijentMozeOstavitiRecenziju(string doktorId, string uloga, bool mozeOstaviti)
    {
        // Arrange
        RecenzijaControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);

        // Act
        await _controller.RecenzijeDoktora(doktorId);

        // Assert
        Assert.AreEqual(mozeOstaviti, (bool)_controller.ViewBag.MozeOstaviti);
    }

    // ─── MojeRecenzije ────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.RecenzijeDoktora), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task MojeRecenzije_PrijavljenDoktor_VracaSamoNjegoveRecenzije(string doktorId, int brojRecenzija)
    {
        // Arrange
        RecenzijaControllerPodaci.Prijavi(_controller, _userManager, doktorId, string.Empty);

        // Act
        var rezultat = (ViewResult)await _controller.MojeRecenzije();

        // Assert
        Assert.AreEqual(brojRecenzija, ((IEnumerable<Recenzija>)rezultat.Model!).Count());
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.RecenzijeDoktora), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task MojeRecenzije_PrijavljenDoktor_TraziNjegoveProsjecneOcjene(string doktorId, int brojRecenzija)
    {
        // Arrange
        RecenzijaControllerPodaci.Prijavi(_controller, _userManager, doktorId, string.Empty);

        // Act
        await _controller.MojeRecenzije();

        // Assert
        _recenzijaServis.Verify(s => s.GetProsjecneOcjeneAsync(doktorId), Times.Once);
    }

    // ─── Create (GET) ─────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.OdabraniDoktor), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_Get_PuniListuSvimDoktorima(string doktorId, int brojDoktora)
    {
        // Act
        await _controller.Create(doktorId);

        // Assert
        Assert.AreEqual(brojDoktora, RecenzijaControllerPodaci.ListaDoktora(_controller).Count());
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.OdabraniDoktor), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_Get_OznacavaOdabranogDoktora(string doktorId, int brojDoktora)
    {
        // Act
        await _controller.Create(doktorId);

        // Assert
        Assert.AreEqual(doktorId, RecenzijaControllerPodaci.ListaDoktora(_controller).SelectedValue);
    }

    // ─── Create (POST) ────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.NovaRecenzija), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public async Task Create_ValidnaRecenzija_DodajeRecenzijuPrijavljenogPacijenta(string pacijentId, string doktorId, int ljubaznost, int profesionalnost, int usluga)
    {
        // Arrange
        RecenzijaControllerPodaci.Prijavi(_controller, _userManager, pacijentId, RecenzijaControllerPodaci.UlogaPacijenta());
        var recenzija = RecenzijaControllerPodaci.NapraviNovuRecenziju(doktorId, ljubaznost, profesionalnost, usluga);

        // Act
        await _controller.Create(recenzija);

        // Assert
        _recenzijaServis.Verify(s => s.AddAsync(It.Is<Recenzija>(r => r.PacijentID == pacijentId && r.DoktorID == doktorId)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.NovaRecenzija), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_ValidnaRecenzija_PreusmjeravaNaIndex(string pacijentId, string doktorId, int ljubaznost, int profesionalnost, int usluga)
    {
        // Arrange
        RecenzijaControllerPodaci.Prijavi(_controller, _userManager, pacijentId, RecenzijaControllerPodaci.UlogaPacijenta());
        var recenzija = RecenzijaControllerPodaci.NapraviNovuRecenziju(doktorId, ljubaznost, profesionalnost, usluga);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.Create(recenzija);

        // Assert
        Assert.AreEqual(nameof(RecenzijaController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.NovaRecenzija), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_ValidnaRecenzija_PostavljaPorukuOUspjehu(string pacijentId, string doktorId, int ljubaznost, int profesionalnost, int usluga)
    {
        // Arrange
        RecenzijaControllerPodaci.Prijavi(_controller, _userManager, pacijentId, RecenzijaControllerPodaci.UlogaPacijenta());
        var recenzija = RecenzijaControllerPodaci.NapraviNovuRecenziju(doktorId, ljubaznost, profesionalnost, usluga);

        // Act
        await _controller.Create(recenzija);

        // Assert
        Assert.IsNotNull(RecenzijaControllerPodaci.PorukaUspjeha(_controller));
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.GreskaServisa), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_ServisBaciIzuzetak_PorukaIdeUModelState(string pacijentId, string doktorId, string poruka)
    {
        // Arrange
        RecenzijaControllerPodaci.Prijavi(_controller, _userManager, pacijentId, RecenzijaControllerPodaci.UlogaPacijenta());
        var recenzija = new Recenzija { DoktorID = doktorId };
        _recenzijaServis.Setup(s => s.AddAsync(recenzija)).ThrowsAsync(new Exception(poruka));

        // Act
        var rezultat = (ViewResult)await _controller.Create(recenzija);

        // Assert
        Assert.AreEqual(poruka, rezultat.ViewData.ModelState[string.Empty]!.Errors.Single().ErrorMessage);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.NevalidanModel), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_NevalidanModel_VracaIstuFormu(string pacijentId, string polje, string greska)
    {
        // Arrange
        RecenzijaControllerPodaci.Prijavi(_controller, _userManager, pacijentId, RecenzijaControllerPodaci.UlogaPacijenta());
        var recenzija = new Recenzija();
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        var rezultat = (ViewResult)await _controller.Create(recenzija);

        // Assert
        Assert.AreSame(recenzija, rezultat.Model);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.NevalidanModel), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_NevalidanModel_NeDodajeRecenziju(string pacijentId, string polje, string greska)
    {
        // Arrange
        RecenzijaControllerPodaci.Prijavi(_controller, _userManager, pacijentId, RecenzijaControllerPodaci.UlogaPacijenta());
        var recenzija = new Recenzija();
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        await _controller.Create(recenzija);

        // Assert
        _recenzijaServis.Verify(s => s.AddAsync(It.IsAny<Recenzija>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.IgnorisanoPolje), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_GreskaNaPacijentId_IpakDodajeRecenziju(string pacijentId, string polje, string greska)
    {
        // Arrange
        RecenzijaControllerPodaci.Prijavi(_controller, _userManager, pacijentId, RecenzijaControllerPodaci.UlogaPacijenta());
        var recenzija = new Recenzija();
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        await _controller.Create(recenzija);

        // Assert
        _recenzijaServis.Verify(s => s.AddAsync(recenzija), Times.Once);
    }

    // ─── DeleteConfirmed ──────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.NepostojecaRecenzija), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_RecenzijaNePostoji_PreusmjeravaNaIndex(int id, string korisnikId, string uloga)
    {
        // Arrange
        RecenzijaControllerPodaci.Prijavi(_controller, _userManager, korisnikId, uloga);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(nameof(RecenzijaController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.NepostojecaRecenzija), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_RecenzijaNePostoji_NistaNeBrise(int id, string korisnikId, string uloga)
    {
        // Arrange
        RecenzijaControllerPodaci.Prijavi(_controller, _userManager, korisnikId, uloga);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _recenzijaServis.Verify(s => s.Delete(It.IsAny<Recenzija>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.TudjaRecenzija), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_PacijentBriseTudjuRecenziju_VracaForbid(int id, string pacijentId)
    {
        // Arrange
        RecenzijaControllerPodaci.Prijavi(_controller, _userManager, pacijentId, RecenzijaControllerPodaci.UlogaPacijenta());

        // Act
        var rezultat = await _controller.DeleteConfirmed(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ForbidResult));
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.TudjaRecenzija), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_PacijentBriseTudjuRecenziju_NeBriseRecenziju(int id, string pacijentId)
    {
        // Arrange
        RecenzijaControllerPodaci.Prijavi(_controller, _userManager, pacijentId, RecenzijaControllerPodaci.UlogaPacijenta());

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _recenzijaServis.Verify(s => s.Delete(It.IsAny<Recenzija>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.VlastitaRecenzija), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_PacijentBriseSvojuRecenziju_BriseRecenziju(int id, string pacijentId)
    {
        // Arrange
        RecenzijaControllerPodaci.Prijavi(_controller, _userManager, pacijentId, RecenzijaControllerPodaci.UlogaPacijenta());

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _recenzijaServis.Verify(s => s.Delete(It.Is<Recenzija>(r => r.RecenzijaId == id)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.VlastitaRecenzija), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_PacijentBriseSvojuRecenziju_SnimaPromjene(int id, string pacijentId)
    {
        // Arrange
        RecenzijaControllerPodaci.Prijavi(_controller, _userManager, pacijentId, RecenzijaControllerPodaci.UlogaPacijenta());

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _recenzijaServis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.AdminBrisanje), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_AdministratorBriseBiloKojuRecenziju_PreusmjeravaNaIndex(int id, string adminId, string uloga)
    {
        // Arrange
        RecenzijaControllerPodaci.Prijavi(_controller, _userManager, adminId, uloga);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(nameof(RecenzijaController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijaControllerPodaci.AdminBrisanje), typeof(RecenzijaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_AdministratorBriseBiloKojuRecenziju_BriseRecenziju(int id, string adminId, string uloga)
    {
        // Arrange
        RecenzijaControllerPodaci.Prijavi(_controller, _userManager, adminId, uloga);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _recenzijaServis.Verify(s => s.Delete(It.Is<Recenzija>(r => r.RecenzijaId == id)), Times.Once);
    }
}
