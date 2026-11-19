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

// ─── Izvori podataka i pomoćne metode za KorisnikNaListiControllerTests ─
public static class KorisnikNaListiControllerPodaci
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

    // Stavka iz XML "baze". Bez atributa doktorId lista stavke nije učitana (Lista je null).
    public static KorisnikNaListi NapraviStavku(XElement x) => new()
    {
        KorisnikNaListiID = (int)x.Attribute("id")!,
        ListaID = (int)x.Attribute("listaId")!,
        KorisnikID = (string)x.Attribute("korisnikId")!,
        Lista = x.Attribute("doktorId") == null
            ? null
            : new ListaCekanja { ListaId = (int)x.Attribute("listaId")!, DoktorID = (string)x.Attribute("doktorId")! }
    };

    public static KorisnikNaListi IzBaze(IEnumerable<XElement> stavke, int id) =>
        stavke.Select(NapraviStavku).Single(k => k.KorisnikNaListiID == id);

    // Lista iz XML-a. Bez atributa doktorId doktor liste nije učitan.
    public static ListaCekanja NapraviListu(XElement x) => new()
    {
        ListaId = (int)x.Attribute("id")!,
        DoktorID = (string?)x.Attribute("doktorId") ?? string.Empty,
        Doktor = x.Attribute("doktorId") == null
            ? null
            : new Korisnik { Ime = (string)x.Attribute("ime")!, Prezime = (string)x.Attribute("prezime")! }
    };

    // Korisnik iz XML-a; doktor ima specijalizaciju, pacijent nema.
    public static Korisnik NapraviKorisnika(XElement x) => new()
    {
        Id = (string)x.Attribute("id")!,
        Ime = (string)x.Attribute("ime")!,
        Prezime = (string)x.Attribute("prezime")!,
        Specijalizacija = x.Attribute("specijalizacija") == null
            ? null
            : Enum.Parse<Specijalizacija>((string)x.Attribute("specijalizacija")!)
    };

    public static List<Korisnik> NapraviPacijente(string[] ids) => ids.Select(id => new Korisnik { Id = id }).ToList();

    public static ListaCekanja NapraviListu(int listaId, string doktorId) => new() { ListaId = listaId, DoktorID = doktorId };

    public static int[] Ids(string lista) =>
        lista.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();

    public static string[] Lista(string lista) => lista.Split(',', StringSplitOptions.RemoveEmptyEntries);

    public static int? PrazanUNull(string vrijednost) => string.IsNullOrEmpty(vrijednost) ? null : int.Parse(vrijednost);

    // Čitanje onoga što akcija upiše u ViewData, TempData i ModelState.
    public static bool ListaFiksna(Controller c) => (bool)c.ViewData["ListaFiksna"]!;
    public static object? FiksnaLista(Controller c) => c.ViewData[nameof(KorisnikNaListi.ListaID)];
    public static SelectList ListeZaIzbor(Controller c) => (SelectList)c.ViewData[nameof(KorisnikNaListi.ListaID)]!;
    public static SelectList PacijentiZaIzbor(Controller c) => (SelectList)c.ViewData[nameof(KorisnikNaListi.KorisnikID)]!;
    public static object? PorukaGreske(Controller c) => c.TempData["Error"];
    public static string GreskaPolja(Controller c, string polje) => c.ModelState[polje]!.Errors[0].ErrorMessage;
    public static string[] Vrijednosti(SelectList lista) => lista.Select(i => i.Value).ToArray();

    public static int[] IdsIzModela(IActionResult rezultat) =>
        ((IEnumerable<KorisnikNaListi>)((ViewResult)rezultat).Model!).Select(k => k.KorisnikNaListiID).ToArray();

    // ── izvori podataka ──

    public static IEnumerable<object[]> StavkeDoktora() =>
        CsvLoader.Ucitaj("KorisnikNaListiController_stavkeDoktora.csv").Select(r => new object[] { r[0], r[1], Ids(r[2]) });

    private static IEnumerable<XElement> Administratori() =>
        XmlLoader.Ucitaj("KorisnikNaListiController_administrator.xml", "scenarij");

    public static IEnumerable<object[]> AdministratorIndex() =>
        Administratori().Select(x => new object[]
        {
            (string)x.Element("korisnikId")!,
            (string)x.Element("uloga")!,
            Ids((string)x.Element("ocekivaneStavke")!)
        });

    public static IEnumerable<object[]> AdministratorPrijava() =>
        Administratori().Select(x => new object[] { (string)x.Element("korisnikId")!, (string)x.Element("uloga")! });

    public static IEnumerable<object[]> AdministratorListe() =>
        Administratori().Select(x => new object[]
        {
            (string)x.Element("korisnikId")!,
            (string)x.Element("uloga")!,
            (int)x.Element("ocekivaniBrojListi")!
        });

    public static IEnumerable<object[]> AdministratorPacijenti() =>
        Administratori().Select(x => new object[]
        {
            (string)x.Element("korisnikId")!,
            (string)x.Element("uloga")!,
            Lista((string)x.Element("ocekivaniPacijenti")!)
        });

    public static IEnumerable<object[]> BezIdIliNepostojece() =>
        CsvLoader.Ucitaj("KorisnikNaListiController_bezIdIliNepostojeca.csv").Select(r => new object[] { PrazanUNull(r[0])! });

    public static IEnumerable<object[]> NepostojeciIds() =>
        CsvLoader.Ucitaj("KorisnikNaListiController_nepostojeciId.csv").Select(r => new object[] { int.Parse(r[0]) });

    private static IEnumerable<string[]> Pristup() => CsvLoader.Ucitaj("KorisnikNaListiController_pristup.csv");

    public static IEnumerable<object[]> PristupDozvoljen() =>
        Pristup().Where(r => bool.Parse(r[3])).Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> PristupZabranjen() =>
        Pristup().Where(r => !bool.Parse(r[3])).Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> Neprijavljeni() =>
        CsvLoader.Ucitaj("KorisnikNaListiController_neprijavljen.csv").Select(r => new object[] { r[0], r[1] });

    public static IEnumerable<object[]> DoktoriBezListe() =>
        CsvLoader.Ucitaj("KorisnikNaListiController_doktorBezListe.csv").Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> DoktoriBezListePrijava() =>
        CsvLoader.Ucitaj("KorisnikNaListiController_doktorBezListe.csv").Select(r => new object[] { r[0], r[1] });

    private static IEnumerable<string[]> DoktoriSaListomRedovi() => CsvLoader.Ucitaj("KorisnikNaListiController_doktorSaListom.csv");

    public static IEnumerable<object[]> DoktoriSaListom() =>
        DoktoriSaListomRedovi().Select(r => new object[] { r[0], r[1], int.Parse(r[2]) });

    public static IEnumerable<object[]> DoktoriSaPacijentima() =>
        DoktoriSaListomRedovi().Select(r => new object[] { r[0], r[1], int.Parse(r[2]), Lista(r[3]) });

    public static IEnumerable<object[]> Dodavanje() =>
        CsvLoader.Ucitaj("KorisnikNaListiController_dodavanje.csv")
                 .Select(r => new object[] { r[0], r[1], int.Parse(r[2]), r[3], int.Parse(r[4]) });

    public static IEnumerable<object[]> GreskaDodavanja() =>
        CsvLoader.Ucitaj("KorisnikNaListiController_greskaDodavanja.csv")
                 .Select(r => new object[] { r[0], r[1], int.Parse(r[2]), r[3], r[4] });

    public static IEnumerable<object[]> NevalidanModelDodavanja() =>
        CsvLoader.Ucitaj("KorisnikNaListiController_nevalidanModel.csv")
                 .Select(r => new object[] { r[0], r[1], int.Parse(r[2]), r[3], r[4] });

    public static IEnumerable<object[]> Postojece() =>
        CsvLoader.Ucitaj("KorisnikNaListiController_postojece.csv").Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]) });

    public static IEnumerable<object[]> PostojeciIds() =>
        CsvLoader.Ucitaj("KorisnikNaListiController_postojece.csv").Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> NevalidanModelIzmjene() =>
        CsvLoader.Ucitaj("KorisnikNaListiController_nevalidnaIzmjena.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> RazlicitId() =>
        CsvLoader.Ucitaj("KorisnikNaListiController_razlicitId.csv").Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]) });

    private static IEnumerable<string[]> Konkurentnost() => CsvLoader.Ucitaj("KorisnikNaListiController_konkurentnost.csv");

    public static IEnumerable<object[]> KonkurentnostObrisana() =>
        Konkurentnost().Where(r => !bool.Parse(r[1])).Select(r => new object[] { int.Parse(r[0]), r[2] });

    public static IEnumerable<object[]> KonkurentnostPostoji() =>
        Konkurentnost().Where(r => bool.Parse(r[1])).Select(r => new object[] { int.Parse(r[0]), r[2] });
}

// ─── Testovi ────────────────────────────────────────────────────────────
// IKorisnikNaListiService, IListaCekanjaService i UserManager<Korisnik> su zamijenjeni Moq objektima.
// XML "baza" (stavke, liste i korisnici) se čita jednom, a servis je vraća u svakom testu.
[TestClass]
public class KorisnikNaListiControllerTests
{
    private static List<XElement> _stavke = null!;      // samo za čitanje
    private static List<XElement> _liste = null!;       // samo za čitanje
    private static List<XElement> _korisnici = null!;   // samo za čitanje

    private Mock<IKorisnikNaListiService> _stavkeServis = null!;
    private Mock<IListaCekanjaService> _listaServis = null!;
    private Mock<UserManager<Korisnik>> _userManager = null!;
    private KorisnikNaListiController _controller = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _stavke = XmlLoader.Ucitaj("KorisnikNaListiController_baza.xml", "stavka").ToList();
        _liste = XmlLoader.Ucitaj("KorisnikNaListiController_baza.xml", "lista").ToList();
        _korisnici = XmlLoader.Ucitaj("KorisnikNaListiController_baza.xml", "korisnik").ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        _stavkeServis = new Mock<IKorisnikNaListiService>();
        _listaServis = new Mock<IListaCekanjaService>();
        _userManager = KorisnikNaListiControllerPodaci.NapraviUserManager();
        _stavkeServis.Setup(s => s.GetAllAsync()).ReturnsAsync(_stavke.Select(KorisnikNaListiControllerPodaci.NapraviStavku).ToList());
        _stavkeServis.Setup(s => s.GetListeCekanjaAsync()).ReturnsAsync(_liste.Select(KorisnikNaListiControllerPodaci.NapraviListu).ToList());
        _stavkeServis.Setup(s => s.GetKorisniciAsync()).ReturnsAsync(_korisnici.Select(KorisnikNaListiControllerPodaci.NapraviKorisnika).ToList());

        _controller = new KorisnikNaListiController(_stavkeServis.Object, _listaServis.Object, _userManager.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        _controller.Dispose();
        _stavkeServis.Reset();
        _listaServis.Reset();
        _userManager.Reset();
    }

    // ─── INDEX ────────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.StavkeDoktora), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Index_Doktor_PrikazujeSamoStavkeSvojihLista(string doktorId, string uloga, int[] ocekivaneStavke)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.PostaviKorisnika(_controller, doktorId, uloga);

        // Act
        var rezultat = await _controller.Index();

        // Assert
        CollectionAssert.AreEquivalent(ocekivaneStavke, KorisnikNaListiControllerPodaci.IdsIzModela(rezultat));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.AdministratorIndex), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Index_Administrator_PrikazujeSveStavke(string korisnikId, string uloga, int[] ocekivaneStavke)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.PostaviKorisnika(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.Index();

        // Assert
        CollectionAssert.AreEquivalent(ocekivaneStavke, KorisnikNaListiControllerPodaci.IdsIzModela(rezultat));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.AdministratorPrijava), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Index_Poziv_CitaStavkeIzServisa(string korisnikId, string uloga)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.PostaviKorisnika(_controller, korisnikId, uloga);

        // Act
        await _controller.Index();

        // Assert
        _stavkeServis.Verify(s => s.GetAllAsync(), Times.Once);
    }

    // ─── DETAILS ──────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.BezIdIliNepostojece), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Details_NemaIdIliStavkaNePostoji_VracaNotFound(int? id)
    {
        // Arrange (servis za nepostojeći id vraća null)

        // Act
        var rezultat = await _controller.Details(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PristupDozvoljen), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Details_VlastitaListaIliAdministrator_VracaViewSaStavkom(int id, string korisnikId, string uloga)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.PostaviKorisnika(_controller, korisnikId, uloga);
        var stavka = KorisnikNaListiControllerPodaci.IzBaze(_stavke, id);
        _stavkeServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(stavka);

        // Act
        var rezultat = await _controller.Details(id);

        // Assert
        Assert.AreSame(stavka, ((ViewResult)rezultat).Model);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PristupZabranjen), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Details_TudjaLista_VracaForbid(int id, string korisnikId, string uloga)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.PostaviKorisnika(_controller, korisnikId, uloga);
        _stavkeServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(KorisnikNaListiControllerPodaci.IzBaze(_stavke, id));

        // Act
        var rezultat = await _controller.Details(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ForbidResult));
    }

    // ─── CREATE (GET) ─────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.Neprijavljeni), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Get_KorisnikNijePronadjen_VracaUnauthorized(string korisnikId, string uloga)
    {
        // Arrange (samo claimovi; UserManager ne pronalazi korisnika)
        KorisnikNaListiControllerPodaci.PostaviKorisnika(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.Create();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(UnauthorizedResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.DoktoriBezListe), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Get_DoktorNemaListu_PreusmjeravaNaListeCekanja(string doktorId, string uloga,
        string ocekivanaAkcija, string ocekivaniKontroler)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.Create();

        // Assert
        Assert.AreEqual(ocekivanaAkcija, rezultat.ActionName);
        Assert.AreEqual(ocekivaniKontroler, rezultat.ControllerName);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.DoktoriBezListePrijava), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Get_DoktorNemaListu_PostavljaPorukuGreske(string doktorId, string uloga)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);

        // Act
        await _controller.Create();

        // Assert
        Assert.IsNotNull(KorisnikNaListiControllerPodaci.PorukaGreske(_controller));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.DoktoriSaListom), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Create_Get_DoktorImaListu_FiksiraNjegovuListu(string doktorId, string uloga, int listaId)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);
        _listaServis.Setup(s => s.GetListaZaDoktoraAsync(doktorId))
                    .ReturnsAsync(KorisnikNaListiControllerPodaci.NapraviListu(listaId, doktorId));

        // Act
        await _controller.Create();

        // Assert
        Assert.AreEqual(listaId, KorisnikNaListiControllerPodaci.FiksnaLista(_controller));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.DoktoriSaPacijentima), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Get_DoktorImaListu_NudiSamoPacijenteSaTerminom(string doktorId, string uloga, int listaId,
        string[] pacijenti)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);
        _listaServis.Setup(s => s.GetListaZaDoktoraAsync(doktorId))
                    .ReturnsAsync(KorisnikNaListiControllerPodaci.NapraviListu(listaId, doktorId));
        _stavkeServis.Setup(s => s.GetPacijentiSaTerminomAsync(doktorId))
                     .ReturnsAsync(KorisnikNaListiControllerPodaci.NapraviPacijente(pacijenti));

        // Act
        await _controller.Create();

        // Assert
        CollectionAssert.AreEquivalent(pacijenti,
            KorisnikNaListiControllerPodaci.Vrijednosti(KorisnikNaListiControllerPodaci.PacijentiZaIzbor(_controller)));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.DoktoriSaListom), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Get_DoktorImaListu_TraziPacijenteSaTerminomKodDoktora(string doktorId, string uloga, int listaId)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);
        _listaServis.Setup(s => s.GetListaZaDoktoraAsync(doktorId))
                    .ReturnsAsync(KorisnikNaListiControllerPodaci.NapraviListu(listaId, doktorId));

        // Act
        await _controller.Create();

        // Assert
        _stavkeServis.Verify(s => s.GetPacijentiSaTerminomAsync(doktorId), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.AdministratorPrijava), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Get_Administrator_ListaNijeFiksna(string korisnikId, string uloga)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.Prijavi(_controller, _userManager, korisnikId, uloga);

        // Act
        await _controller.Create();

        // Assert
        Assert.IsFalse(KorisnikNaListiControllerPodaci.ListaFiksna(_controller));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.AdministratorListe), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Get_Administrator_NudiSveListe(string korisnikId, string uloga, int ocekivaniBrojListi)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.Prijavi(_controller, _userManager, korisnikId, uloga);

        // Act
        await _controller.Create();

        // Assert
        Assert.AreEqual(ocekivaniBrojListi, KorisnikNaListiControllerPodaci.ListeZaIzbor(_controller).Count());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.AdministratorPacijenti), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Get_Administrator_NudiSamoKorisnikeBezSpecijalizacije(string korisnikId, string uloga,
        string[] ocekivaniPacijenti)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.Prijavi(_controller, _userManager, korisnikId, uloga);

        // Act
        await _controller.Create();

        // Assert
        CollectionAssert.AreEquivalent(ocekivaniPacijenti,
            KorisnikNaListiControllerPodaci.Vrijednosti(KorisnikNaListiControllerPodaci.PacijentiZaIzbor(_controller)));
    }

    // ─── CREATE (POST) ────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.Neprijavljeni), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_KorisnikNijePronadjen_VracaUnauthorized(string korisnikId, string uloga)
    {
        // Arrange (samo claimovi; UserManager ne pronalazi korisnika)
        KorisnikNaListiControllerPodaci.PostaviKorisnika(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.Create(new KorisnikNaListi());

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(UnauthorizedResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.DoktoriBezListe), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_DoktorNemaListu_PreusmjeravaNaListeCekanja(string doktorId, string uloga,
        string ocekivanaAkcija, string ocekivaniKontroler)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.Prijavi(_controller, _userManager, doktorId, uloga);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.Create(new KorisnikNaListi());

        // Assert
        Assert.AreEqual(ocekivanaAkcija, rezultat.ActionName);
        Assert.AreEqual(ocekivaniKontroler, rezultat.ControllerName);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.Dodavanje), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Create_Post_ValidnaStavka_DodajePacijentaNaOcekivanuListu(string korisnikId, string uloga,
        int listaIzForme, string pacijentId, int ocekivanaLista)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.Prijavi(_controller, _userManager, korisnikId, uloga);
        _listaServis.Setup(s => s.GetListaZaDoktoraAsync(korisnikId))
                    .ReturnsAsync(KorisnikNaListiControllerPodaci.NapraviListu(ocekivanaLista, korisnikId));
        var stavka = new KorisnikNaListi { ListaID = listaIzForme, KorisnikID = pacijentId };

        // Act
        await _controller.Create(stavka);

        // Assert
        _stavkeServis.Verify(s => s.AddAsync(It.Is<KorisnikNaListi>(k =>
            k.ListaID == ocekivanaLista && k.KorisnikID == pacijentId)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.Dodavanje), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_ValidnaStavka_PreusmjeravaNaIndex(string korisnikId, string uloga,
        int listaIzForme, string pacijentId, int ocekivanaLista)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.Prijavi(_controller, _userManager, korisnikId, uloga);
        _listaServis.Setup(s => s.GetListaZaDoktoraAsync(korisnikId))
                    .ReturnsAsync(KorisnikNaListiControllerPodaci.NapraviListu(ocekivanaLista, korisnikId));

        // Act
        var rezultat = await _controller.Create(new KorisnikNaListi { ListaID = listaIzForme, KorisnikID = pacijentId });

        // Assert
        Assert.AreEqual(nameof(KorisnikNaListiController.Index), ((RedirectToActionResult)rezultat).ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.GreskaDodavanja), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_ServisBaciIzuzetak_PorukaIdeUzPoljePacijent(string korisnikId, string uloga,
        int listaId, string pacijentId, string poruka)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.Prijavi(_controller, _userManager, korisnikId, uloga);
        _listaServis.Setup(s => s.GetListaZaDoktoraAsync(korisnikId))
                    .ReturnsAsync(KorisnikNaListiControllerPodaci.NapraviListu(listaId, korisnikId));
        _stavkeServis.Setup(s => s.AddAsync(It.IsAny<KorisnikNaListi>())).ThrowsAsync(new Exception(poruka));

        // Act
        await _controller.Create(new KorisnikNaListi { ListaID = listaId, KorisnikID = pacijentId });

        // Assert
        Assert.AreEqual(poruka,
            KorisnikNaListiControllerPodaci.GreskaPolja(_controller, nameof(KorisnikNaListi.KorisnikID)));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.GreskaDodavanja), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_ServisBaciIzuzetak_VracaFormuSaIstomStavkom(string korisnikId, string uloga,
        int listaId, string pacijentId, string poruka)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.Prijavi(_controller, _userManager, korisnikId, uloga);
        _listaServis.Setup(s => s.GetListaZaDoktoraAsync(korisnikId))
                    .ReturnsAsync(KorisnikNaListiControllerPodaci.NapraviListu(listaId, korisnikId));
        _stavkeServis.Setup(s => s.AddAsync(It.IsAny<KorisnikNaListi>())).ThrowsAsync(new Exception(poruka));
        var stavka = new KorisnikNaListi { ListaID = listaId, KorisnikID = pacijentId };

        // Act
        var rezultat = await _controller.Create(stavka);

        // Assert
        Assert.AreSame(stavka, ((ViewResult)rezultat).Model);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.NevalidanModelDodavanja), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_NevalidanModel_NeDodajeStavku(string korisnikId, string uloga, int listaId,
        string polje, string greska)
    {
        // Arrange
        KorisnikNaListiControllerPodaci.Prijavi(_controller, _userManager, korisnikId, uloga);
        _listaServis.Setup(s => s.GetListaZaDoktoraAsync(korisnikId))
                    .ReturnsAsync(KorisnikNaListiControllerPodaci.NapraviListu(listaId, korisnikId));
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        await _controller.Create(new KorisnikNaListi { ListaID = listaId });

        // Assert
        _stavkeServis.Verify(s => s.AddAsync(It.IsAny<KorisnikNaListi>()), Times.Never);
    }

    // ─── EDIT (GET) ───────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.BezIdIliNepostojece), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Get_NemaIdIliStavkaNePostoji_VracaNotFound(int? id)
    {
        // Arrange (servis za nepostojeći id vraća null)

        // Act
        var rezultat = await _controller.Edit(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIds), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Get_StavkaPostoji_VracaFormuSaStavkom(int id)
    {
        // Arrange
        var stavka = KorisnikNaListiControllerPodaci.IzBaze(_stavke, id);
        _stavkeServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(stavka);

        // Act
        var rezultat = await _controller.Edit(id);

        // Assert
        Assert.AreSame(stavka, ((ViewResult)rezultat).Model);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.Postojece), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Get_StavkaPostoji_OznacavaListuStavke(int id, int ocekivanaLista)
    {
        // Arrange
        _stavkeServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(KorisnikNaListiControllerPodaci.IzBaze(_stavke, id));

        // Act
        await _controller.Edit(id);

        // Assert
        Assert.AreEqual(ocekivanaLista, KorisnikNaListiControllerPodaci.ListeZaIzbor(_controller).SelectedValue);
    }

    // ─── EDIT (POST) ──────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.RazlicitId), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_IdRuteRazlicitOdStavke_VracaNotFound(int idRute, int idStavke)
    {
        // Arrange
        var stavka = new KorisnikNaListi { KorisnikNaListiID = idStavke };

        // Act
        var rezultat = await _controller.Edit(idRute, stavka);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIds), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Edit_Post_ValidnaStavka_AzuriraStavku(int id)
    {
        // Arrange
        var stavka = KorisnikNaListiControllerPodaci.IzBaze(_stavke, id);

        // Act
        await _controller.Edit(id, stavka);

        // Assert
        _stavkeServis.Verify(s => s.Update(stavka), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIds), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_ValidnaStavka_SnimaPromjene(int id)
    {
        // Arrange
        var stavka = KorisnikNaListiControllerPodaci.IzBaze(_stavke, id);

        // Act
        await _controller.Edit(id, stavka);

        // Assert
        _stavkeServis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIds), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_ValidnaStavka_PreusmjeravaNaIndex(int id)
    {
        // Arrange
        var stavka = KorisnikNaListiControllerPodaci.IzBaze(_stavke, id);

        // Act
        var rezultat = await _controller.Edit(id, stavka);

        // Assert
        Assert.AreEqual(nameof(KorisnikNaListiController.Index), ((RedirectToActionResult)rezultat).ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.KonkurentnostObrisana), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_StavkaObrisanaUMedjuvremenu_VracaNotFound(int id, string poruka)
    {
        // Arrange
        _stavkeServis.Setup(s => s.SaveChangesAsync()).ThrowsAsync(new DbUpdateConcurrencyException(poruka));
        _stavkeServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((KorisnikNaListi?)null);

        // Act
        var rezultat = await _controller.Edit(id, new KorisnikNaListi { KorisnikNaListiID = id });

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.KonkurentnostPostoji), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_KonfliktAStavkaPostoji_PonovoBacaIzuzetak(int id, string poruka)
    {
        // Arrange
        _stavkeServis.Setup(s => s.SaveChangesAsync()).ThrowsAsync(new DbUpdateConcurrencyException(poruka));
        _stavkeServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(KorisnikNaListiControllerPodaci.IzBaze(_stavke, id));

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(
            () => _controller.Edit(id, new KorisnikNaListi { KorisnikNaListiID = id }));

        // Assert
        Assert.AreEqual(poruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.NevalidanModelIzmjene), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_NevalidanModel_NeSnimaPromjene(int id, string polje, string greska)
    {
        // Arrange
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        await _controller.Edit(id, KorisnikNaListiControllerPodaci.IzBaze(_stavke, id));

        // Assert
        _stavkeServis.Verify(s => s.SaveChangesAsync(), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.NevalidanModelIzmjene), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_NevalidanModel_VracaFormuSaIstomStavkom(int id, string polje, string greska)
    {
        // Arrange
        _controller.ModelState.AddModelError(polje, greska);
        var stavka = KorisnikNaListiControllerPodaci.IzBaze(_stavke, id);

        // Act
        var rezultat = await _controller.Edit(id, stavka);

        // Assert
        Assert.AreSame(stavka, ((ViewResult)rezultat).Model);
    }

    // ─── DELETE ───────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.BezIdIliNepostojece), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Delete_Get_NemaIdIliStavkaNePostoji_VracaNotFound(int? id)
    {
        // Arrange (servis za nepostojeći id vraća null)

        // Act
        var rezultat = await _controller.Delete(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIds), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Delete_Get_StavkaPostoji_VracaPotvrduSaStavkom(int id)
    {
        // Arrange
        var stavka = KorisnikNaListiControllerPodaci.IzBaze(_stavke, id);
        _stavkeServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(stavka);

        // Act
        var rezultat = await _controller.Delete(id);

        // Assert
        Assert.AreSame(stavka, ((ViewResult)rezultat).Model);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIds), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task DeleteConfirmed_StavkaPostoji_BriseStavku(int id)
    {
        // Arrange
        var stavka = KorisnikNaListiControllerPodaci.IzBaze(_stavke, id);
        _stavkeServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(stavka);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _stavkeServis.Verify(s => s.Delete(stavka), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.PostojeciIds), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_StavkaPostoji_SnimaPromjene(int id)
    {
        // Arrange
        _stavkeServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(KorisnikNaListiControllerPodaci.IzBaze(_stavke, id));

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _stavkeServis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.NepostojeciIds), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_StavkaNePostoji_NistaNeBrise(int id)
    {
        // Arrange
        _stavkeServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((KorisnikNaListi?)null);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _stavkeServis.Verify(s => s.Delete(It.IsAny<KorisnikNaListi>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikNaListiControllerPodaci.NepostojeciIds), typeof(KorisnikNaListiControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_BiloKojiId_PreusmjeravaNaIndex(int id)
    {
        // Arrange
        _stavkeServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((KorisnikNaListi?)null);

        // Act
        var rezultat = await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(nameof(KorisnikNaListiController.Index), ((RedirectToActionResult)rezultat).ActionName);
    }
}
