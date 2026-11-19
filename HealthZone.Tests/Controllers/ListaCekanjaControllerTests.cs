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
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za ListaCekanjaControllerTests ────
public static class ListaCekanjaControllerPodaci
{
    // ── pomoćne metode ──

    public static Mock<UserManager<Korisnik>> NapraviUserManager()
        => new(Mock.Of<IUserStore<Korisnik>>(), null!, null!, null!, null!, null!, null!, null!, null!);

    // Postavlja claimove prijavljenog korisnika (id i uloga), bez UserManager-a.
    public static void PostaviKorisnika(Controller kontroler, string korisnikId, string uloga)
    {
        kontroler.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, korisnikId), new Claim(ClaimTypes.Role, uloga) }, "Test"));
    }

    // Prijavljuje korisnika: claimovi + UserManager.GetUserAsync vraća tog korisnika.
    public static void Prijavi(Controller kontroler, Mock<UserManager<Korisnik>> userManager, string korisnikId, string uloga)
    {
        PostaviKorisnika(kontroler, korisnikId, uloga);
        userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(new Korisnik { Id = korisnikId });
    }

    public static Korisnik NapraviDoktora(XElement x) => new()
    {
        Id = (string)x.Attribute("id")!,
        Ime = (string)x.Attribute("ime")!,
        Prezime = (string)x.Attribute("prezime")!,
        Specijalizacija = Enum.Parse<Specijalizacija>((string)x.Attribute("specijalizacija")!)
    };

    public static KorisnikNaListi NapraviStavku(XElement x) => new()
    {
        KorisnikNaListiID = (int)x.Attribute("id")!,
        ListaID = (int)x.Attribute("listaId")!,
        KorisnikID = (string)x.Attribute("korisnikId")!,
        Datum = DateTime.Today.AddDays((int)x.Attribute("pomakDana")!),
        Korisnik = (bool?)x.Attribute("bezKorisnika") == true ? null : new Korisnik
        {
            Id = (string)x.Attribute("korisnikId")!,
            Prioritet = x.Attribute("prioritet") == null ? null : Enum.Parse<Prioritet>((string)x.Attribute("prioritet")!)
        }
    };

    public static List<ListaCekanja> NapraviListe(int[] ids) => ids.Select(id => new ListaCekanja { ListaId = id }).ToList();

    public static int[] Ids(string lista) =>
        lista.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();

    public static int? PrazanUNull(string vrijednost) => string.IsNullOrEmpty(vrijednost) ? null : int.Parse(vrijednost);

    // Čitanje onoga što akcija upiše u ViewData, TempData i ModelState.
    public static bool ImaListu(Controller c) => (bool)c.ViewData["ImaListu"]!;
    public static int? MojaListaId(Controller c) => (int?)c.ViewData["MojaListaId"];
    public static bool JeDoktor(Controller c) => (bool)c.ViewData["JeDoktor"]!;
    public static string? PorukaUspjeha(Controller c) => (string?)c.TempData["Success"];
    public static string PrvaGreska(Controller c) => c.ModelState[string.Empty]!.Errors[0].ErrorMessage;
    public static SelectList Doktori(Controller c) => (SelectList)c.ViewData[nameof(ListaCekanja.DoktorID)]!;
    public static object? PregledListaId(Controller c) => c.ViewData["ListaId"];
    public static string? PregledDoktorIme(Controller c) => (string?)c.ViewData["DoktorIme"];

    public static int[] IdsListi(IActionResult rezultat) =>
        ((IEnumerable<ListaCekanja>)((ViewResult)rezultat).Model!).Select(l => l.ListaId).ToArray();

    public static int[] IdsStavki(IActionResult rezultat) =>
        ((IEnumerable<KorisnikNaListi>)((ViewResult)rezultat).Model!).Select(k => k.KorisnikNaListiID).ToArray();

    // ── izvori podataka ──

    public static IEnumerable<object[]> DoktoriSaListom() =>
        CsvLoader.Ucitaj("ListaCekanjaController_doktorSaListom.csv").Select(r => new object[] { r[0], r[1], int.Parse(r[2]) });

    public static IEnumerable<object[]> DoktoriBezListe() =>
        CsvLoader.Ucitaj("ListaCekanjaController_doktorBezListe.csv").Select(r => new object[] { r[0], r[1] });

    public static IEnumerable<object[]> SveListe() =>
        XmlLoader.Ucitaj("ListaCekanjaController_sveListe.xml", "scenarij").Select(x => new object[]
        {
            (string)x.Element("korisnikId")!,
            (string)x.Element("uloga")!,
            Ids((string)x.Element("ids")!)
        });

    public static IEnumerable<object[]> BezIdIliNepostojece() =>
        CsvLoader.Ucitaj("ListaCekanjaController_bezIdIliNepostojeca.csv").Select(r => new object[] { PrazanUNull(r[0])! });

    public static IEnumerable<object[]> NepostojeciIds() =>
        CsvLoader.Ucitaj("ListaCekanjaController_nepostojeciId.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> Postojece() =>
        CsvLoader.Ucitaj("ListaCekanjaController_postojece.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> Uspjeh() =>
        CsvLoader.Ucitaj("ListaCekanjaController_uspjeh.csv").Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> GreskaKreiranja() =>
        CsvLoader.Ucitaj("ListaCekanjaController_greskaKreiranja.csv").Select(r => new object[] { r[0], r[1], r[2] });

    private static IEnumerable<string[]> Nevalidni() => CsvLoader.Ucitaj("ListaCekanjaController_nevalidanModel.csv");

    public static IEnumerable<object[]> NevalidanModelKreiranja() =>
        Nevalidni().Select(r => new object[] { r[1], r[2], r[3], r[4] });

    public static IEnumerable<object[]> NevalidanModelIzmjene() =>
        Nevalidni().Select(r => new object[] { int.Parse(r[0]), r[1], r[3], r[4] });

    public static IEnumerable<object[]> RazlicitId() =>
        CsvLoader.Ucitaj("ListaCekanjaController_razlicitId.csv").Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]) });

    private static IEnumerable<string[]> Konkurentnost() => CsvLoader.Ucitaj("ListaCekanjaController_konkurentnost.csv");

    public static IEnumerable<object[]> KonkurentnostObrisana() =>
        Konkurentnost().Where(r => !bool.Parse(r[1])).Select(r => new object[] { int.Parse(r[0]), r[2] });

    public static IEnumerable<object[]> KonkurentnostPostoji() =>
        Konkurentnost().Where(r => bool.Parse(r[1])).Select(r => new object[] { int.Parse(r[0]), r[2] });

    private static IEnumerable<string[]> Pregled() => CsvLoader.Ucitaj("ListaCekanjaController_pregled.csv");

    public static IEnumerable<object[]> PregledRedoslijed() =>
        Pregled().Select(r => new object[] { int.Parse(r[0]), Ids(r[3]) });

    public static IEnumerable<object[]> PregledSaDoktorom() =>
        Pregled().Select(r => new object[] { int.Parse(r[0]), r[1], r[2], r[4] });

    public static IEnumerable<object[]> PregledIds() =>
        Pregled().Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> PregledBezDoktora() =>
        CsvLoader.Ucitaj("ListaCekanjaController_pregledBezDoktora.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });
}

// ─── Testovi ────────────────────────────────────────────────────────────
// IListaCekanjaService, IKorisnikNaListiService i UserManager<Korisnik> su zamijenjeni Moq objektima.
// Prijavljeni korisnik se postavlja kroz claimove (id i uloga) na HttpContext-u.
[TestClass]
public class ListaCekanjaControllerTests
{
    private static List<Korisnik> _doktori = null!;        // samo za čitanje
    private static List<XElement> _stavke = null!;         // samo za čitanje

    private Mock<IListaCekanjaService> _listaServis = null!;
    private Mock<IKorisnikNaListiService> _stavkeServis = null!;
    private Mock<UserManager<Korisnik>> _userManager = null!;
    private ListaCekanjaController _controller = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _doktori = XmlLoader.Ucitaj("ListaCekanjaController_doktori.xml", "doktor")
                            .Select(ListaCekanjaControllerPodaci.NapraviDoktora).ToList();
        _stavke = XmlLoader.Ucitaj("ListaCekanjaController_pacijenti.xml", "stavka").ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        _listaServis = new Mock<IListaCekanjaService>();
        _stavkeServis = new Mock<IKorisnikNaListiService>();
        _userManager = ListaCekanjaControllerPodaci.NapraviUserManager();
        _listaServis.Setup(s => s.GetDoktoriAsync()).ReturnsAsync(_doktori);
        _stavkeServis.Setup(s => s.GetAllAsync()).ReturnsAsync(_stavke.Select(ListaCekanjaControllerPodaci.NapraviStavku).ToList());

        _controller = new ListaCekanjaController(_listaServis.Object, _stavkeServis.Object, _userManager.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        _controller.Dispose();
        _listaServis.Reset();
        _stavkeServis.Reset();
        _userManager.Reset();
    }

    // ─── INDEX ────────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.DoktoriSaListom), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Index_DoktorImaListu_PrikazujeSamoSvojuListu(string doktorId, string uloga, int listaId)
    {
        // Arrange
        ListaCekanjaControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);
        _listaServis.Setup(s => s.GetListaZaDoktoraAsync(doktorId))
                    .ReturnsAsync(new ListaCekanja { ListaId = listaId, DoktorID = doktorId });

        // Act
        var rezultat = await _controller.Index();

        // Assert
        CollectionAssert.AreEqual(new[] { listaId }, ListaCekanjaControllerPodaci.IdsListi(rezultat));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.DoktoriSaListom), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Index_DoktorImaListu_PamtiIdNjegoveListe(string doktorId, string uloga, int listaId)
    {
        // Arrange
        ListaCekanjaControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);
        _listaServis.Setup(s => s.GetListaZaDoktoraAsync(doktorId))
                    .ReturnsAsync(new ListaCekanja { ListaId = listaId, DoktorID = doktorId });

        // Act
        await _controller.Index();

        // Assert
        Assert.AreEqual(listaId, ListaCekanjaControllerPodaci.MojaListaId(_controller));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.DoktoriBezListe), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Index_DoktorNemaListu_VracaPraznuListu(string doktorId, string uloga)
    {
        // Arrange
        ListaCekanjaControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);

        // Act
        var rezultat = await _controller.Index();

        // Assert
        Assert.IsFalse(ListaCekanjaControllerPodaci.IdsListi(rezultat).Any());
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.DoktoriBezListe), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Index_DoktorNemaListu_OznacavaDaNemaListu(string doktorId, string uloga)
    {
        // Arrange
        ListaCekanjaControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);

        // Act
        await _controller.Index();

        // Assert
        Assert.IsFalse(ListaCekanjaControllerPodaci.ImaListu(_controller));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.DoktoriBezListe), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Index_DoktorNijePronadjenUBazi_NeTraziListu(string doktorId, string uloga)
    {
        // Arrange (samo claimovi; UserManager ne pronalazi korisnika)
        ListaCekanjaControllerPodaci.PostaviKorisnika(_controller, doktorId, uloga);

        // Act
        await _controller.Index();

        // Assert
        _listaServis.Verify(s => s.GetListaZaDoktoraAsync(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.SveListe), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Index_Administrator_PrikazujeSveListe(string korisnikId, string uloga, int[] ids)
    {
        // Arrange
        ListaCekanjaControllerPodaci.Prijavi(_controller, _userManager, korisnikId, uloga);
        _listaServis.Setup(s => s.GetAllAsync()).ReturnsAsync(ListaCekanjaControllerPodaci.NapraviListe(ids));

        // Act
        var rezultat = await _controller.Index();

        // Assert
        CollectionAssert.AreEquivalent(ids, ListaCekanjaControllerPodaci.IdsListi(rezultat));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.SveListe), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Index_Administrator_OznacavaDaNijeDoktor(string korisnikId, string uloga, int[] ids)
    {
        // Arrange
        ListaCekanjaControllerPodaci.Prijavi(_controller, _userManager, korisnikId, uloga);
        _listaServis.Setup(s => s.GetAllAsync()).ReturnsAsync(ListaCekanjaControllerPodaci.NapraviListe(ids));

        // Act
        await _controller.Index();

        // Assert
        Assert.IsFalse(ListaCekanjaControllerPodaci.JeDoktor(_controller));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.SveListe), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Index_Administrator_CitaSveListeIzServisa(string korisnikId, string uloga, int[] ids)
    {
        // Arrange
        ListaCekanjaControllerPodaci.Prijavi(_controller, _userManager, korisnikId, uloga);
        _listaServis.Setup(s => s.GetAllAsync()).ReturnsAsync(ListaCekanjaControllerPodaci.NapraviListe(ids));

        // Act
        await _controller.Index();

        // Assert
        _listaServis.Verify(s => s.GetAllAsync(), Times.Once);
    }

    // ─── DETAILS ──────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.BezIdIliNepostojece), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Details_NemaIdIliListaNePostoji_VracaNotFound(int? id)
    {
        // Arrange (servis za nepostojeći id vraća null)

        // Act
        var rezultat = await _controller.Details(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.Postojece), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Details_ListaPostoji_VracaViewSaListom(int id, string doktorId)
    {
        // Arrange
        var lista = new ListaCekanja { ListaId = id, DoktorID = doktorId };
        _listaServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(lista);

        // Act
        var rezultat = await _controller.Details(id);

        // Assert
        Assert.AreSame(lista, ((ViewResult)rezultat).Model);
    }

    // ─── CREATE (GET) ─────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.DoktoriBezListe), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Get_KorisnikNijePronadjen_VracaUnauthorized(string doktorId, string uloga)
    {
        // Arrange (samo claimovi; UserManager ne pronalazi korisnika)
        ListaCekanjaControllerPodaci.PostaviKorisnika(_controller, doktorId, uloga);

        // Act
        var rezultat = await _controller.Create();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(UnauthorizedResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.DoktoriSaListom), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Get_DoktorVecImaListu_PreusmjeravaNaIndex(string doktorId, string uloga, int listaId)
    {
        // Arrange
        ListaCekanjaControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);
        _listaServis.Setup(s => s.GetListaZaDoktoraAsync(doktorId))
                    .ReturnsAsync(new ListaCekanja { ListaId = listaId, DoktorID = doktorId });

        // Act
        var rezultat = await _controller.Create();

        // Assert
        Assert.AreEqual(nameof(ListaCekanjaController.Index), ((RedirectToActionResult)rezultat).ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.DoktoriBezListe), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Get_DoktorNemaListu_VracaFormu(string doktorId, string uloga)
    {
        // Arrange
        ListaCekanjaControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);

        // Act
        var rezultat = await _controller.Create();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    // ─── CREATE (POST) ────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.DoktoriBezListe), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_KorisnikNijePronadjen_VracaUnauthorized(string doktorId, string uloga)
    {
        // Arrange (samo claimovi; UserManager ne pronalazi korisnika)
        ListaCekanjaControllerPodaci.PostaviKorisnika(_controller, doktorId, uloga);

        // Act
        var rezultat = await _controller.Create(new ListaCekanja());

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(UnauthorizedResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.DoktoriSaListom), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_DoktorVecImaListu_NeDodajeNovuListu(string doktorId, string uloga, int listaId)
    {
        // Arrange
        ListaCekanjaControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);
        _listaServis.Setup(s => s.GetListaZaDoktoraAsync(doktorId))
                    .ReturnsAsync(new ListaCekanja { ListaId = listaId, DoktorID = doktorId });

        // Act
        await _controller.Create(new ListaCekanja());

        // Assert
        _listaServis.Verify(s => s.AddAsync(It.IsAny<ListaCekanja>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.DoktoriSaListom), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_DoktorVecImaListu_PreusmjeravaNaIndex(string doktorId, string uloga, int listaId)
    {
        // Arrange
        ListaCekanjaControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);
        _listaServis.Setup(s => s.GetListaZaDoktoraAsync(doktorId))
                    .ReturnsAsync(new ListaCekanja { ListaId = listaId, DoktorID = doktorId });

        // Act
        var rezultat = await _controller.Create(new ListaCekanja());

        // Assert
        Assert.AreEqual(nameof(ListaCekanjaController.Index), ((RedirectToActionResult)rezultat).ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.DoktoriBezListe), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Create_Post_ValidnaLista_DodajeListuZaPrijavljenogDoktora(string doktorId, string uloga)
    {
        // Arrange
        ListaCekanjaControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);

        // Act
        await _controller.Create(new ListaCekanja());

        // Assert
        _listaServis.Verify(s => s.AddAsync(It.Is<ListaCekanja>(l => l.DoktorID == doktorId)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.Uspjeh), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_ValidnaLista_PostavljaPorukuUspjeha(string doktorId, string uloga, string ocekivanaPoruka)
    {
        // Arrange
        ListaCekanjaControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);

        // Act
        await _controller.Create(new ListaCekanja());

        // Assert
        Assert.AreEqual(ocekivanaPoruka, ListaCekanjaControllerPodaci.PorukaUspjeha(_controller));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.DoktoriBezListe), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_ValidnaLista_PreusmjeravaNaIndex(string doktorId, string uloga)
    {
        // Arrange
        ListaCekanjaControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);

        // Act
        var rezultat = await _controller.Create(new ListaCekanja());

        // Assert
        Assert.AreEqual(nameof(ListaCekanjaController.Index), ((RedirectToActionResult)rezultat).ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.GreskaKreiranja), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_ServisBaciIzuzetak_PorukaIdeUModelState(string doktorId, string uloga, string poruka)
    {
        // Arrange
        ListaCekanjaControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);
        _listaServis.Setup(s => s.AddAsync(It.IsAny<ListaCekanja>())).ThrowsAsync(new Exception(poruka));

        // Act
        await _controller.Create(new ListaCekanja());

        // Assert
        Assert.AreEqual(poruka, ListaCekanjaControllerPodaci.PrvaGreska(_controller));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.GreskaKreiranja), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_ServisBaciIzuzetak_VracaFormuSaIstomListom(string doktorId, string uloga, string poruka)
    {
        // Arrange
        ListaCekanjaControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);
        _listaServis.Setup(s => s.AddAsync(It.IsAny<ListaCekanja>())).ThrowsAsync(new Exception(poruka));
        var lista = new ListaCekanja();

        // Act
        var rezultat = await _controller.Create(lista);

        // Assert
        Assert.AreSame(lista, ((ViewResult)rezultat).Model);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.NevalidanModelKreiranja), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_NevalidanModel_NeDodajeListu(string doktorId, string uloga, string polje, string greska)
    {
        // Arrange
        ListaCekanjaControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        await _controller.Create(new ListaCekanja());

        // Assert
        _listaServis.Verify(s => s.AddAsync(It.IsAny<ListaCekanja>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.NevalidanModelKreiranja), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_NevalidanModel_VracaFormuSaIstomListom(string doktorId, string uloga, string polje, string greska)
    {
        // Arrange
        ListaCekanjaControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);
        _controller.ModelState.AddModelError(polje, greska);
        var lista = new ListaCekanja();

        // Act
        var rezultat = await _controller.Create(lista);

        // Assert
        Assert.AreSame(lista, ((ViewResult)rezultat).Model);
    }

    // ─── EDIT (GET) ───────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.BezIdIliNepostojece), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Get_NemaIdIliListaNePostoji_VracaNotFound(int? id)
    {
        // Arrange (servis za nepostojeći id vraća null)

        // Act
        var rezultat = await _controller.Edit(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.Postojece), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Get_ListaPostoji_VracaFormuSaListom(int id, string doktorId)
    {
        // Arrange
        var lista = new ListaCekanja { ListaId = id, DoktorID = doktorId };
        _listaServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(lista);

        // Act
        var rezultat = await _controller.Edit(id);

        // Assert
        Assert.AreSame(lista, ((ViewResult)rezultat).Model);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.Postojece), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Get_ListaPostoji_NudiSveDoktore(int id, string doktorId)
    {
        // Arrange
        _listaServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(new ListaCekanja { ListaId = id, DoktorID = doktorId });

        // Act
        await _controller.Edit(id);

        // Assert
        Assert.AreEqual(_doktori.Count, ListaCekanjaControllerPodaci.Doktori(_controller).Count());
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.Postojece), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Get_ListaPostoji_OznacavaDoktoraListe(int id, string doktorId)
    {
        // Arrange
        _listaServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(new ListaCekanja { ListaId = id, DoktorID = doktorId });

        // Act
        await _controller.Edit(id);

        // Assert
        Assert.AreEqual(doktorId, ListaCekanjaControllerPodaci.Doktori(_controller).SelectedValue);
    }

    // ─── EDIT (POST) ──────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.RazlicitId), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_IdRuteRazlicitOdListe_VracaNotFound(int idRute, int idListe)
    {
        // Arrange
        var lista = new ListaCekanja { ListaId = idListe };

        // Act
        var rezultat = await _controller.Edit(idRute, lista);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.Postojece), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Edit_Post_ValidnaLista_AzuriraListu(int id, string doktorId)
    {
        // Arrange
        var lista = new ListaCekanja { ListaId = id, DoktorID = doktorId };

        // Act
        await _controller.Edit(id, lista);

        // Assert
        _listaServis.Verify(s => s.Update(lista), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.Postojece), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_ValidnaLista_SnimaPromjene(int id, string doktorId)
    {
        // Arrange
        var lista = new ListaCekanja { ListaId = id, DoktorID = doktorId };

        // Act
        await _controller.Edit(id, lista);

        // Assert
        _listaServis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.Postojece), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_ValidnaLista_PreusmjeravaNaIndex(int id, string doktorId)
    {
        // Arrange
        var lista = new ListaCekanja { ListaId = id, DoktorID = doktorId };

        // Act
        var rezultat = await _controller.Edit(id, lista);

        // Assert
        Assert.AreEqual(nameof(ListaCekanjaController.Index), ((RedirectToActionResult)rezultat).ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.KonkurentnostObrisana), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_ListaObrisanaUMedjuvremenu_VracaNotFound(int id, string poruka)
    {
        // Arrange
        _listaServis.Setup(s => s.SaveChangesAsync()).ThrowsAsync(new DbUpdateConcurrencyException(poruka));
        _listaServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((ListaCekanja?)null);

        // Act
        var rezultat = await _controller.Edit(id, new ListaCekanja { ListaId = id });

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.KonkurentnostPostoji), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_KonfliktAListaPostoji_PonovoBacaIzuzetak(int id, string poruka)
    {
        // Arrange
        _listaServis.Setup(s => s.SaveChangesAsync()).ThrowsAsync(new DbUpdateConcurrencyException(poruka));
        _listaServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(new ListaCekanja { ListaId = id });

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(
            () => _controller.Edit(id, new ListaCekanja { ListaId = id }));

        // Assert
        Assert.AreEqual(poruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.NevalidanModelIzmjene), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_NevalidanModel_NeSnimaPromjene(int id, string doktorId, string polje, string greska)
    {
        // Arrange
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        await _controller.Edit(id, new ListaCekanja { ListaId = id, DoktorID = doktorId });

        // Assert
        _listaServis.Verify(s => s.SaveChangesAsync(), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.NevalidanModelIzmjene), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_NevalidanModel_VracaFormuSaIstomListom(int id, string doktorId, string polje, string greska)
    {
        // Arrange
        _controller.ModelState.AddModelError(polje, greska);
        var lista = new ListaCekanja { ListaId = id, DoktorID = doktorId };

        // Act
        var rezultat = await _controller.Edit(id, lista);

        // Assert
        Assert.AreSame(lista, ((ViewResult)rezultat).Model);
    }

    // ─── DELETE ───────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.BezIdIliNepostojece), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Delete_Get_NemaIdIliListaNePostoji_VracaNotFound(int? id)
    {
        // Arrange (servis za nepostojeći id vraća null)

        // Act
        var rezultat = await _controller.Delete(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.Postojece), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Delete_Get_ListaPostoji_VracaPotvrduSaListom(int id, string doktorId)
    {
        // Arrange
        var lista = new ListaCekanja { ListaId = id, DoktorID = doktorId };
        _listaServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(lista);

        // Act
        var rezultat = await _controller.Delete(id);

        // Assert
        Assert.AreSame(lista, ((ViewResult)rezultat).Model);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.Postojece), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task DeleteConfirmed_ListaPostoji_BriseListu(int id, string doktorId)
    {
        // Arrange
        var lista = new ListaCekanja { ListaId = id, DoktorID = doktorId };
        _listaServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(lista);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _listaServis.Verify(s => s.Delete(lista), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.Postojece), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_ListaPostoji_SnimaPromjene(int id, string doktorId)
    {
        // Arrange
        _listaServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(new ListaCekanja { ListaId = id, DoktorID = doktorId });

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _listaServis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.NepostojeciIds), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_ListaNePostoji_NistaNeBrise(int id)
    {
        // Arrange
        _listaServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((ListaCekanja?)null);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _listaServis.Verify(s => s.Delete(It.IsAny<ListaCekanja>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.NepostojeciIds), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_BiloKojiId_PreusmjeravaNaIndex(int id)
    {
        // Arrange
        _listaServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((ListaCekanja?)null);

        // Act
        var rezultat = await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(nameof(ListaCekanjaController.Index), ((RedirectToActionResult)rezultat).ActionName);
    }

    // ─── PREGLED LISTE ────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.BezIdIliNepostojece), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Pregled_NemaIdIliListaNePostoji_VracaNotFound(int? id)
    {
        // Arrange (servis za nepostojeći id vraća null)

        // Act
        var rezultat = await _controller.Pregled(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PregledRedoslijed), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Pregled_ListaPostoji_SortiraPacijentePoPrioritetuPaDatumu(int listaId, int[] ocekivaniRedoslijed)
    {
        // Arrange
        _listaServis.Setup(s => s.GetByIdAsync(listaId)).ReturnsAsync(new ListaCekanja { ListaId = listaId });

        // Act
        var rezultat = await _controller.Pregled(listaId);

        // Assert
        CollectionAssert.AreEqual(ocekivaniRedoslijed, ListaCekanjaControllerPodaci.IdsStavki(rezultat));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PregledIds), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Pregled_ListaPostoji_PamtiIdListe(int listaId)
    {
        // Arrange
        _listaServis.Setup(s => s.GetByIdAsync(listaId)).ReturnsAsync(new ListaCekanja { ListaId = listaId });

        // Act
        await _controller.Pregled(listaId);

        // Assert
        Assert.AreEqual(listaId, ListaCekanjaControllerPodaci.PregledListaId(_controller));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PregledSaDoktorom), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Pregled_ListaImaDoktora_PrikazujeImeIPrezimeDoktora(int listaId, string ime, string prezime,
        string ocekivanoIme)
    {
        // Arrange
        _listaServis.Setup(s => s.GetByIdAsync(listaId)).ReturnsAsync(new ListaCekanja
        {
            ListaId = listaId,
            Doktor = new Korisnik { Ime = ime, Prezime = prezime }
        });

        // Act
        await _controller.Pregled(listaId);

        // Assert
        Assert.AreEqual(ocekivanoIme, ListaCekanjaControllerPodaci.PregledDoktorIme(_controller));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PregledBezDoktora), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Pregled_ListaBezDoktora_PrikazujeCrticu(int listaId, string ocekivanoIme)
    {
        // Arrange
        _listaServis.Setup(s => s.GetByIdAsync(listaId)).ReturnsAsync(new ListaCekanja { ListaId = listaId });

        // Act
        await _controller.Pregled(listaId);

        // Assert
        Assert.AreEqual(ocekivanoIme, ListaCekanjaControllerPodaci.PregledDoktorIme(_controller));
    }

    [TestMethod]
    [DynamicData(nameof(ListaCekanjaControllerPodaci.PregledIds), typeof(ListaCekanjaControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Pregled_ListaPostoji_CitaPacijenteIzServisaListe(int listaId)
    {
        // Arrange
        _listaServis.Setup(s => s.GetByIdAsync(listaId)).ReturnsAsync(new ListaCekanja { ListaId = listaId });

        // Act
        await _controller.Pregled(listaId);

        // Assert
        _stavkeServis.Verify(s => s.GetAllAsync(), Times.Once);
    }
}
