using System.Security.Claims;
using System.Xml.Linq;
using HealthZone.Controllers;
using HealthZone.Models;
using HealthZone.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za ListaCekanjaControllerTests ────
public static class ListaCekanjaControllerPodaci
{
    private static IEnumerable<string[]> UcitajAkcije() =>
        CsvLoader.Ucitaj("ListaCekanjaController_akcije.csv");

    public static IEnumerable<object[]> PostojeciIdCsv() =>
        UcitajAkcije()
            .Where(r => r[0] == "1")
            .Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> NepostojeciIdCsv() =>
        UcitajAkcije()
            .Where(r => r[0] == "999")
            .Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> PregledXml() =>
        XmlLoader.Ucitaj("ListaCekanjaController_pregled.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (int)x.Element("listaId")!,
                     (string)x.Element("doktorId")!,
                     (string)x.Element("doktorIme")!,
                     (string)x.Element("doktorPrezime")!,
                     (int)x.Element("ocekivanoPacijenata")!
                 });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class ListaCekanjaControllerTests
{
    private static List<XElement> _podaciPregled = null!;

    private Mock<IListaCekanjaService> _listaService = null!;
    private Mock<IKorisnikNaListiService> _korisnikNaListiService = null!;
    private Mock<UserManager<Korisnik>> _userManager = null!;
    private ListaCekanjaController _controller = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _podaciPregled = XmlLoader.Ucitaj("ListaCekanjaController_pregled.xml", "scenarij").ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        _listaService = new Mock<IListaCekanjaService>();
        _korisnikNaListiService = new Mock<IKorisnikNaListiService>();

        var store = new Mock<IUserStore<Korisnik>>();
        _userManager = new Mock<UserManager<Korisnik>>(store.Object,
            null!, null!, null!, null!, null!, null!, null!, null!);

        _controller = new ListaCekanjaController(
            _listaService.Object,
            _korisnikNaListiService.Object,
            _userManager.Object);

        PostaviKorisnika("doc1", "Doktor");
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
        _listaService.Reset();
        _korisnikNaListiService.Reset();
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Index_KorisnikDoktorImaListu_VracaView(int id, string tip)
    {
        // Arrange
        var doc = new Korisnik { Id = "doc1" };
        _userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(doc);
        _listaService.Setup(s => s.GetListaZaDoktoraAsync("doc1")).ReturnsAsync(new ListaCekanja { ListaId = id, DoktorID = "doc1" });

        // Act
        var rezultat = await _controller.Index();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Index_KorisnikAdmin_VracaSveListe(int id, string tip)
    {
        // Arrange
        PostaviKorisnika("admin1", "Administrator");
        _listaService.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<ListaCekanja> { new ListaCekanja { ListaId = id } });

        // Act
        var rezultat = await _controller.Index();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Details_IdNull_VracaNotFound(int id, string tip)
    {
        // Arrange & Act
        var rezultat = await _controller.Details(null);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.NepostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Details_ListaNePostoji_VracaNotFound(int id, string tip)
    {
        // Arrange
        _listaService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((ListaCekanja?)null);

        // Act
        var rezultat = await _controller.Details(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Details_ListaPostoji_VracaView(int id, string tip)
    {
        // Arrange
        _listaService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(new ListaCekanja { ListaId = id });

        // Act
        var rezultat = await _controller.Details(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Create_GetKorisnikNull_VracaUnauthorized(int id, string tip)
    {
        // Arrange
        _userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((Korisnik?)null);

        // Act
        var rezultat = await _controller.Create();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(UnauthorizedResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Create_GetDoktorVecImaListu_RedirektujeNaIndex(int id, string tip)
    {
        // Arrange
        _userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(new Korisnik { Id = "doc1" });
        _listaService.Setup(s => s.GetListaZaDoktoraAsync("doc1")).ReturnsAsync(new ListaCekanja { ListaId = id });

        // Act
        var rezultat = await _controller.Create();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Create_GetDoktorNemaListu_VracaView(int id, string tip)
    {
        // Arrange
        _userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(new Korisnik { Id = "doc1" });
        _listaService.Setup(s => s.GetListaZaDoktoraAsync("doc1")).ReturnsAsync((ListaCekanja?)null);

        // Act
        var rezultat = await _controller.Create();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Create_PostValidanModel_PozivaAddIRedirektuje(int id, string tip)
    {
        // Arrange
        _userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(new Korisnik { Id = "doc1" });
        _listaService.Setup(s => s.GetListaZaDoktoraAsync("doc1")).ReturnsAsync((ListaCekanja?)null);
        var model = new ListaCekanja { ListaId = id };

        // Act
        var rezultat = await _controller.Create(model);

        // Assert
        _listaService.Verify(s => s.AddAsync(model), Times.Once);
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Create_PostNevalidanModel_VracaView(int id, string tip)
    {
        // Arrange
        _userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(new Korisnik { Id = "doc1" });
        _listaService.Setup(s => s.GetListaZaDoktoraAsync("doc1")).ReturnsAsync((ListaCekanja?)null);
        _controller.ModelState.AddModelError("Error", "Greska");
        var model = new ListaCekanja { ListaId = id };

        // Act
        var rezultat = await _controller.Create(model);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Edit_GetIdNull_VracaNotFound(int id, string tip)
    {
        // Arrange & Act
        var rezultat = await _controller.Edit(null);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.NepostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Edit_GetNepostojećaLista_VracaNotFound(int id, string tip)
    {
        // Arrange
        _listaService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((ListaCekanja?)null);

        // Act
        var rezultat = await _controller.Edit(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Edit_GetPostojecaLista_VracaView(int id, string tip)
    {
        // Arrange
        _listaService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(new ListaCekanja { ListaId = id, DoktorID = "doc1" });
        _listaService.Setup(s => s.GetDoktoriAsync()).ReturnsAsync(new List<Korisnik>());

        // Act
        var rezultat = await _controller.Edit(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Edit_PostIdMismatch_VracaNotFound(int id, string tip)
    {
        // Arrange
        var model = new ListaCekanja { ListaId = id + 1 };

        // Act
        var rezultat = await _controller.Edit(id, model);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Edit_PostValidanModel_AzuriraIRedirektuje(int id, string tip)
    {
        // Arrange
        var model = new ListaCekanja { ListaId = id, DoktorID = "doc1" };

        // Act
        var rezultat = await _controller.Edit(id, model);

        // Assert
        _listaService.Verify(s => s.Update(model), Times.Once);
        _listaService.Verify(s => s.SaveChangesAsync(), Times.Once);
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Delete_GetIdNull_VracaNotFound(int id, string tip)
    {
        // Arrange & Act
        var rezultat = await _controller.Delete(null);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.NepostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Delete_GetNepostojecaLista_VracaNotFound(int id, string tip)
    {
        // Arrange
        _listaService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((ListaCekanja?)null);

        // Act
        var rezultat = await _controller.Delete(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Delete_GetPostojecaLista_VracaView(int id, string tip)
    {
        // Arrange
        _listaService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(new ListaCekanja { ListaId = id });

        // Act
        var rezultat = await _controller.Delete(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PostojeciIdCsv), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Mock"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task DeleteConfirmed_PostojecaLista_BriseIRedirektuje(int id, string tip)
    {
        // Arrange
        var model = new ListaCekanja { ListaId = id };
        _listaService.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(model);

        // Act
        var rezultat = await _controller.DeleteConfirmed(id);

        // Assert
        _listaService.Verify(s => s.Delete(model), Times.Once);
        _listaService.Verify(s => s.SaveChangesAsync(), Times.Once);
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PregledXml), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("Stub"), TestCategory("DataDriven")]
    [TestProperty("Autor", "Ramiz Plancic")]
    public async Task Pregled_PostojecaLista_VracaViewSaPacijentima(
        int listaId, string doktorId, string ime, string prezime, int ocekivanoPacijenata)
    {
        // Arrange
        var lista = new ListaCekanja
        {
            ListaId = listaId,
            Doktor = new Korisnik { Id = doktorId, Ime = ime, Prezime = prezime }
        };
        _listaService.Setup(s => s.GetByIdAsync(listaId)).ReturnsAsync(lista);

        var pacijenti = new List<KorisnikNaListi>
        {
            new KorisnikNaListi { ListaID = listaId, Datum = DateTime.Today, Korisnik = new Korisnik { Prioritet = Prioritet.Visok } },
            new KorisnikNaListi { ListaID = listaId, Datum = DateTime.Today.AddDays(1), Korisnik = new Korisnik { Prioritet = Prioritet.Nizak } }
        };
        _korisnikNaListiService.Setup(s => s.GetAllAsync()).ReturnsAsync(pacijenti);

        // Act
        var rezultat = await _controller.Pregled(listaId);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }
}
