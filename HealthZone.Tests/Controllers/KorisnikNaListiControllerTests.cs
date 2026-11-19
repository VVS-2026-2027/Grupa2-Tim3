using System.Security.Claims;
using System.Xml.Linq;
using HealthZone.Models;
using HealthZone.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za KorisnikNaListiControllerTests ──
public static class KorisnikNaListiControllerPodaci
{
    private static IEnumerable<string[]> UcitajAkcije() =>
        CsvLoader.Ucitaj("KorisnikNaListiController_akcije.csv");

    public static IEnumerable<object[]> PostojeciIdAdminCsv() =>
        UcitajAkcije()
            .Where(r => r[0] == "1" && r[1] == "Administrator")
            .Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> NepostojeciIdCsv() =>
        UcitajAkcije()
            .Where(r => r[0] == "999")
            .Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> PostojeciIdDoktorCsv() =>
        UcitajAkcije()
            .Where(r => r[0] == "1" && r[1] == "Doktor")
            .Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> UlogeXml() =>
        XmlLoader.Ucitaj("KorisnikNaListiController_uloge.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("doktorId")!,
                     (string)x.Element("uloga")!,
                     (int)x.Element("ocekivanoStavki")!
                 });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class KorisnikNaListiControllerTests
{
    private static List<XElement> _podaciUloge = null!;

    private Mock<IKorisnikNaListiService> _korisnikNaListiService = null!;
    private Mock<IListaCekanjaService> _listaCekanjaService = null!;
    private Mock<UserManager<Korisnik>> _userManager = null!;
    private KorisnikNaListiController _controller = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _podaciUloge = XmlLoader.Ucitaj("KorisnikNaListiController_uloge.xml", "scenarij").ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        _korisnikNaListiService = new Mock<IKorisnikNaListiService>();
        _listaCekanjaService = new Mock<IListaCekanjaService>();

        var store = new Mock<IUserStore<Korisnik>>();
        _userManager = new Mock<UserManager<Korisnik>>(store.Object,
            null!, null!, null!, null!, null!, null!, null!, null!);

        _controller = new KorisnikNaListiController(
            _korisnikNaListiService.Object,
            _listaCekanjaService.Object,
            _userManager.Object);

        PostaviKorisnika("admin1", "Administrator");
    }

    private void PostaviKorisnika(string korisnikId, string uloga)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, korisnikId), new Claim(ClaimTypes.Role, uloga) }, "Test"))
        };
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        _controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    [TestCleanup]
    public void Cleanup()
    {
        _controller.Dispose();
        _korisnikNaListiService.Reset();
        _listaCekanjaService.Reset();
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.UlogeXml), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Index_RazliciteUloge_VracaView(string doktorId, string uloga, int ocekivanoStavki)
    {
        // Arrange
        PostaviKorisnika(doktorId, uloga);
        var stavke = new List<KorisnikNaListi>
        {
            new KorisnikNaListi { KorisnikNaListiID = 1, Lista = new ListaCekanja { DoktorID = "doc1" } },
            new KorisnikNaListi { KorisnikNaListiID = 2, Lista = new ListaCekanja { DoktorID = "drugi_doc" } }
        };
        _korisnikNaListiService.Setup(s => s.GetAllAsync()).ReturnsAsync(stavke);

        // Act
        var rezultat = await _controller.Index();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIdAdminCsv), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Details_IdNull_VracaNotFound(int id, string uloga, string status)
    {
        // Arrange & Act
        var rezultat = await _controller.Details(null);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.NepostojeciIdCsv), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Details_StavkaNePostoji_VracaNotFound(int id, string uloga, string status)
    {
        // Arrange
        _korisnikNaListiService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((KorisnikNaListi?)null);

        // Act
        var rezultat = await _controller.Details(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIdDoktorCsv), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Details_DoktorPristupaTuđojListi_VracaForbid(int id, string uloga, string status)
    {
        // Arrange
        PostaviKorisnika("doc1", "Doktor");
        var stavka = new KorisnikNaListi
        {
            KorisnikNaListiID = id,
            Lista = new ListaCekanja { DoktorID = "drugi_doktor" }
        };
        _korisnikNaListiService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(stavka);

        // Act
        var rezultat = await _controller.Details(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ForbidResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIdAdminCsv), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Details_AdminPristupa_VracaView(int id, string uloga, string status)
    {
        // Arrange
        var stavka = new KorisnikNaListi { KorisnikNaListiID = id };
        _korisnikNaListiService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(stavka);

        // Act
        var rezultat = await _controller.Details(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIdAdminCsv), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Create_GetKorisnikNull_VracaUnauthorized(int id, string uloga, string status)
    {
        // Arrange
        _userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((Korisnik?)null);

        // Act
        var rezultat = await _controller.Create();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(UnauthorizedResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIdDoktorCsv), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Create_GetDoktorNemaListu_RedirektujeNaListuCekanja(int id, string uloga, string status)
    {
        // Arrange
        PostaviKorisnika("doc1", "Doktor");
        _userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(new Korisnik { Id = "doc1" });
        _listaCekanjaService.Setup(s => s.GetListaZaDoktoraAsync("doc1")).ReturnsAsync((ListaCekanja?)null);

        // Act
        var rezultat = await _controller.Create();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIdDoktorCsv), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Create_GetDoktorImaListu_VracaView(int id, string uloga, string status)
    {
        // Arrange
        PostaviKorisnika("doc1", "Doktor");
        _userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(new Korisnik { Id = "doc1" });
        _listaCekanjaService.Setup(s => s.GetListaZaDoktoraAsync("doc1")).ReturnsAsync(new ListaCekanja { ListaId = id });
        _korisnikNaListiService.Setup(s => s.GetPacijentiSaTerminomAsync("doc1")).ReturnsAsync(new List<Korisnik>());

        // Act
        var rezultat = await _controller.Create();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIdAdminCsv), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Create_PostValidanModel_PozivaAddIRedirektuje(int id, string uloga, string status)
    {
        // Arrange
        _userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(new Korisnik { Id = "admin1" });
        var model = new KorisnikNaListi { KorisnikNaListiID = id, ListaID = 1, KorisnikID = "p1" };

        // Act
        var rezultat = await _controller.Create(model);

        // Assert
        _korisnikNaListiService.Verify(s => s.AddAsync(model), Times.Once);
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIdAdminCsv), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Create_PostNevalidanModel_VracaView(int id, string uloga, string status)
    {
        // Arrange
        _userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(new Korisnik { Id = "admin1" });
        _controller.ModelState.AddModelError("Error", "Greska");
        _korisnikNaListiService.Setup(s => s.GetListeCekanjaAsync()).ReturnsAsync(new List<ListaCekanja>());
        _korisnikNaListiService.Setup(s => s.GetKorisniciAsync()).ReturnsAsync(new List<Korisnik>());
        var model = new KorisnikNaListi { KorisnikNaListiID = id };

        // Act
        var rezultat = await _controller.Create(model);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIdAdminCsv), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Edit_GetIdNull_VracaNotFound(int id, string uloga, string status)
    {
        // Arrange & Act
        var rezultat = await _controller.Edit(null);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.NepostojeciIdCsv), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Edit_GetNepostojecaStavka_VracaNotFound(int id, string uloga, string status)
    {
        // Arrange
        _korisnikNaListiService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((KorisnikNaListi?)null);

        // Act
        var rezultat = await _controller.Edit(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIdAdminCsv), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Edit_GetPostojecaStavka_VracaView(int id, string uloga, string status)
    {
        // Arrange
        _korisnikNaListiService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(new KorisnikNaListi { KorisnikNaListiID = id });
        _korisnikNaListiService.Setup(s => s.GetListeCekanjaAsync()).ReturnsAsync(new List<ListaCekanja>());
        _korisnikNaListiService.Setup(s => s.GetKorisniciAsync()).ReturnsAsync(new List<Korisnik>());

        // Act
        var rezultat = await _controller.Edit(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIdAdminCsv), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Edit_PostIdMismatch_VracaNotFound(int id, string uloga, string status)
    {
        // Arrange
        var model = new KorisnikNaListi { KorisnikNaListiID = id + 1 };

        // Act
        var rezultat = await _controller.Edit(id, model);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIdAdminCsv), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Edit_PostValidanModel_AzuriraIRedirektuje(int id, string uloga, string status)
    {
        // Arrange
        var model = new KorisnikNaListi { KorisnikNaListiID = id, ListaID = 1, KorisnikID = "p1" };

        // Act
        var rezultat = await _controller.Edit(id, model);

        // Assert
        _korisnikNaListiService.Verify(s => s.Update(model), Times.Once);
        _korisnikNaListiService.Verify(s => s.SaveChangesAsync(), Times.Once);
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIdAdminCsv), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Delete_GetIdNull_VracaNotFound(int id, string uloga, string status)
    {
        // Arrange & Act
        var rezultat = await _controller.Delete(null);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.NepostojeciIdCsv), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Delete_GetNepostojecaStavka_VracaNotFound(int id, string uloga, string status)
    {
        // Arrange
        _korisnikNaListiService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((KorisnikNaListi?)null);

        // Act
        var rezultat = await _controller.Delete(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIdAdminCsv), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Delete_GetPostojecaStavka_VracaView(int id, string uloga, string status)
    {
        // Arrange
        _korisnikNaListiService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(new KorisnikNaListi { KorisnikNaListiID = id });

        // Act
        var rezultat = await _controller.Delete(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIdAdminCsv), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task DeleteConfirmed_PostojecaStavka_BriseIRedirektuje(int id, string uloga, string status)
    {
        // Arrange
        var stavka = new KorisnikNaListi { KorisnikNaListiID = id };
        _korisnikNaListiService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(stavka);

        // Act
        var rezultat = await _controller.DeleteConfirmed(id);

        // Assert
        _korisnikNaListiService.Verify(s => s.Delete(stavka), Times.Once);
        _korisnikNaListiService.Verify(s => s.SaveChangesAsync(), Times.Once);
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
    }
}
