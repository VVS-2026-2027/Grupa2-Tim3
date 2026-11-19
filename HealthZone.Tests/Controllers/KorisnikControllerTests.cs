using System.Security.Claims;
using System.Xml.Linq;
using HealthZone.Controllers;
using HealthZone.Data;
using HealthZone.Models;
using HealthZone.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HealthZone.Tests;

[TestClass]
public class KorisnikControllerTests
{
    private Mock<IKorisnikService> _korisnikServis = null!;
    private Mock<ITerminService> _terminServis = null!;
    private Mock<UserManager<Korisnik>> _userManager = null!;
    private Mock<RoleManager<IdentityRole>> _roleManager = null!;
    private ApplicationDbContext _context = null!;
    private KorisnikController _controller = null!;
    private string _imeBaze = null!;
    private List<string> _uloge = null!;

    [TestInitialize]
    public void Setup()
    {
        // Svaki test dobija svoju InMemory bazu, a uloge u njoj se pune iz XML-a.
        _imeBaze = Guid.NewGuid().ToString();
        _uloge = XmlLoader.Ucitaj("KorisnikController_uloge.xml", "uloga").Select(x => (string)x).ToList();
        using (var seed = NovaBaza())
        {
            seed.Roles.AddRange(_uloge.Select(u => new IdentityRole(u)));
            seed.SaveChanges();
        }
        _context = NovaBaza();

        _korisnikServis = new Mock<IKorisnikService>();
        _terminServis = new Mock<ITerminService>();
        _userManager = new Mock<UserManager<Korisnik>>(Mock.Of<IUserStore<Korisnik>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);
        _roleManager = new Mock<RoleManager<IdentityRole>>(Mock.Of<IRoleStore<IdentityRole>>(),
            null!, null!, null!, null!);
        _roleManager.Setup(r => r.Roles).Returns(_context.Roles);

        _controller = new KorisnikController(_korisnikServis.Object, _terminServis.Object,
            _roleManager.Object, _userManager.Object, _context)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [TestCleanup]
    public void Cleanup() => _context.Dispose();

    // ─── POMOĆNE METODE ───────────────────────────────────────────────────

    private ApplicationDbContext NovaBaza() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(_imeBaze).Options);

    // Prijavljuje korisnika s ulogom: User dobija claimove, a UserManager.GetUserAsync vraća tog korisnika.
    private void Prijavi(string korisnikId, string uloga)
    {
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, korisnikId), new Claim(ClaimTypes.Role, uloga) }, "Test"));
        _userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(new Korisnik { Id = korisnikId });
    }

    private static string? Prazno(string s) => string.IsNullOrEmpty(s) ? null : s;

    private static T? EnumIliNull<T>(string s) where T : struct, Enum
        => string.IsNullOrEmpty(s) ? null : Enum.Parse<T>(s);

    private static IdentityResult Neuspjeh(string[] greske)
        => IdentityResult.Failed(greske.Select(g => new IdentityError { Description = g }).ToArray());

    private static string[] Greske(XElement x)
        => x.Element("greske")!.Elements("greska").Select(g => (string)g).ToArray();

    private string PrvaGreska() => _controller.ModelState[string.Empty]!.Errors[0].ErrorMessage;

    private static XElement Scenarij(string fajl, string naziv)
        => XmlLoader.Ucitaj(fajl, "scenarij").Single(s => (string)s.Attribute("naziv")! == naziv);

    // Puni InMemory bazu termina, nalaza, notifikacija, lista čekanja i recenzija iz XML scenarija.
    private void NapuniBazu(XElement s)
    {
        using var baza = NovaBaza();
        baza.Termin.AddRange(s.Descendants("termin").Select(t => new Termin
        {
            TerminId = (int)t.Attribute("id")!,
            DoktorID = (string)t.Attribute("doktor")!,
            PacijentID = (string)t.Attribute("pacijent")!
        }));
        baza.Nalaz.AddRange(s.Descendants("nalaz").Select(n => new Nalaz
        {
            NalazId = (int)n.Attribute("id")!,
            TerminID = (int)n.Attribute("termin")!,
            PacijentID = (string)n.Attribute("pacijent")!,
            Opis = (string)n.Attribute("opis")!,
            Dijagnoza = (string)n.Attribute("dijagnoza")!,
            Terapija = (string)n.Attribute("terapija")!
        }));
        baza.Notifikacija.AddRange(s.Descendants("notifikacija").Select(n => new Notifikacija
        {
            NotifikacijaId = (int)n.Attribute("id")!,
            TerminID = (int)n.Attribute("termin")!,
            Poruka = (string)n.Attribute("poruka")!
        }));
        baza.ListaCekanja.AddRange(s.Descendants("lista").Select(l => new ListaCekanja
        {
            ListaId = (int)l.Attribute("id")!,
            DoktorID = (string)l.Attribute("doktor")!
        }));
        baza.KorisnikNaListi.AddRange(s.Descendants("zapis").Select(z => new KorisnikNaListi
        {
            KorisnikNaListiID = (int)z.Attribute("id")!,
            ListaID = (int)z.Attribute("lista")!,
            KorisnikID = (string)z.Attribute("korisnik")!
        }));
        baza.Recenzija.AddRange(s.Descendants("recenzija").Select(r => new Recenzija
        {
            RecenzijaId = (int)r.Attribute("id")!,
            DoktorID = (string)r.Attribute("doktor")!,
            PacijentID = (string)r.Attribute("pacijent")!
        }));
        baza.SaveChanges();
    }

    // ─── IZVORI PODATAKA (CSV) ────────────────────────────────────────────

    public static IEnumerable<object[]> NepostojeciId() =>
        CsvLoader.Ucitaj("KorisnikController_nepostojeciId.csv").Select(r => new object[] { r[0], r[1] });

    public static IEnumerable<object[]> PostojeciKorisnici() =>
        CsvLoader.Ucitaj("KorisnikController_postojeci.csv").Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> NevalidanModel() =>
        CsvLoader.Ucitaj("KorisnikController_nevalidanModel.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], r[3], r[4], r[5], r[6] });

    public static IEnumerable<object[]> NoviKorisnici() =>
        CsvLoader.Ucitaj("KorisnikController_noviKorisnik.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], r[3], r[4], r[5] });

    public static IEnumerable<object[]> GreskeKreiranja() =>
        CsvLoader.Ucitaj("KorisnikController_greskaKreiranja.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> TudjiProfil() =>
        CsvLoader.Ucitaj("KorisnikController_tudjiProfil.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> DozvoljenEdit() =>
        CsvLoader.Ucitaj("KorisnikController_dozvoljenEdit.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> RazlicitId() =>
        CsvLoader.Ucitaj("KorisnikController_razlicitId.csv").Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> EditNepostojeci() =>
        CsvLoader.Ucitaj("KorisnikController_editNepostojeci.csv").Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> AdminEdit() =>
        CsvLoader.Ucitaj("KorisnikController_adminEdit.csv").Select(r => new object[]
        {
            r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7], r[8], r[9], r[10], r[11], r[12], r[13],
            bool.Parse(r[14]), bool.Parse(r[15]), bool.Parse(r[16])
        });

    public static IEnumerable<object[]> KorisnikEdit() =>
        CsvLoader.Ucitaj("KorisnikController_korisnikEdit.csv").Select(r => new object[]
        {
            r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7], r[8], r[9], r[10], r[11], bool.Parse(r[12])
        });

    public static IEnumerable<object[]> RazliciteLozinke() =>
        CsvLoader.Ucitaj("KorisnikController_lozinkeRazlicite.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7] });

    public static IEnumerable<object[]> BrisanjeNepostojeci() =>
        CsvLoader.Ucitaj("KorisnikController_brisanjeNepostojeci.csv").Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> UnutrasnjaGreska() =>
        CsvLoader.Ucitaj("KorisnikController_unutrasnjaGreska.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], r[3] });

    // ─── IZVORI PODATAKA (XML) ────────────────────────────────────────────

    public static IEnumerable<object[]> IndexKorisnici() =>
        XmlLoader.Ucitaj("KorisnikController_index.xml", "scenarij").Select(x => new object[]
        {
            x.Elements("korisnik").Select(k => (string)k.Attribute("id")!).ToArray(),
            x.Elements("korisnik").Select(k => (string)k.Attribute("uloga")!).ToArray(),
            x.Elements("korisnik").Select(k => (string)k.Attribute("ocekivano")!).ToArray()
        });

    public static IEnumerable<object[]> GreskeAzuriranja() =>
        XmlLoader.Ucitaj("KorisnikController_greskaAzuriranja.xml", "scenarij").Select(x => new object[]
        {
            (string)x.Element("id")!,
            (string)x.Element("ulogaPrijavljenog")!,
            (string)x.Element("uloga")!,
            (string)x.Element("securityStamp")!,
            Greske(x),
            (string)x.Element("ocekivanaPoruka")!,
            (bool)x.Element("svjeziPostoji")!,
            (string)x.Element("trenutnaUloga")!,
            (string)x.Element("ocekivanaUlogaUViewBagu")!
        });

    public static IEnumerable<object[]> GreskeLozinke() =>
        XmlLoader.Ucitaj("KorisnikController_greskaLozinke.xml", "scenarij").Select(x => new object[]
        {
            (string)x.Element("id")!,
            (string)x.Element("ulogaPrijavljenog")!,
            (string)x.Element("uloga")!,
            (string)x.Element("securityStamp")!,
            (string)x.Element("novaLozinka")!,
            (string)x.Element("token")!,
            Greske(x),
            (string)x.Element("ocekivanaPoruka")!,
            (string)x.Element("trenutnaUloga")!
        });

    public static IEnumerable<object[]> ScenarijiBrisanja() =>
        XmlLoader.Ucitaj("KorisnikController_brisanje.xml", "scenarij")
                 .Select(x => new object[] { (string)x.Attribute("naziv")! });

    public static IEnumerable<object[]> GreskeBrisanja() =>
        XmlLoader.Ucitaj("KorisnikController_greskaBrisanja.xml", "scenarij").Select(x => new object[]
        {
            (string)x.Element("id")!,
            (string)x.Element("uloga")!,
            Greske(x),
            (string)x.Element("ocekivanaPoruka")!
        });

    public static IEnumerable<object[]> Stampovi() =>
        XmlLoader.Ucitaj("KorisnikController_stampovi.xml", "scenarij").Select(x => new object[]
        {
            x.Elements("korisnik").Select(k => (string)k.Attribute("id")!).ToArray(),
            x.Elements("korisnik").Select(k => (string)k.Attribute("stamp")!).ToArray(),
            (int)x.Element("ocekivaniBroj")!,
            (string)x.Element("ocekivanaPoruka")!
        });

    // ─── INDEX ────────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(IndexKorisnici), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("InMemory")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Index_PostojeKorisnici_VracaViewSaMapomUlogaISortiranimUlogama(string[] ids, string[] uloge,
        string[] ocekivaneUloge)
    {
        // Arrange
        var korisnici = ids.Select(id => new Korisnik { Id = id }).ToList();
        _korisnikServis.Setup(s => s.GetAllAsync()).ReturnsAsync(korisnici);
        for (int i = 0; i < korisnici.Count; i++)
        {
            var korisnik = korisnici[i];
            var uloga = Prazno(uloge[i]);
            _korisnikServis.Setup(s => s.GetUlogaAsync(korisnik)).ReturnsAsync(uloga!);
        }
        var sortirano = ((string)XmlLoader.Ucitaj("KorisnikController_uloge.xml", "sortirano").Single())
            .Split(',');

        // Act
        var rezultat = await _controller.Index() as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreSame(korisnici, rezultat.Model);
        var mapa = (Dictionary<string, string>)_controller.ViewData["UlogeMap"]!;
        for (int i = 0; i < ids.Length; i++)
            Assert.AreEqual(ocekivaneUloge[i], mapa[ids[i]]);
        CollectionAssert.AreEqual(sortirano, (System.Collections.ICollection)_controller.ViewData["SveUloge"]!);
    }

    // ─── DETAILS ──────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NepostojeciId), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Details_IdPrazanIliNepostojeci_VracaNotFound(string id, string opis)
    {
        // Arrange
        _korisnikServis.Setup(s => s.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((Korisnik?)null);

        // Act
        var rezultat = await _controller.Details(Prazno(id));

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult), opis);
    }

    [TestMethod]
    [DynamicData(nameof(PostojeciKorisnici), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Details_KorisnikPostoji_VracaViewSaUlogom(string id, string ime, string prezime, string uloga)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id, Ime = ime, Prezime = prezime };
        _korisnikServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(korisnik);
        _korisnikServis.Setup(s => s.GetUlogaAsync(korisnik)).ReturnsAsync(uloga);

        // Act
        var rezultat = await _controller.Details(id) as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreSame(korisnik, rezultat.Model);
        Assert.AreEqual(uloga, _controller.ViewData["Uloga"]);
    }

    // ─── CREATE ───────────────────────────────────────────────────────────

    [TestMethod]
    [TestCategory("InMemory")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Get_PopunjavaPadajuceListe()
    {
        // Act
        var rezultat = await _controller.Create() as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(Enum.GetValues<Prioritet>().Length, ((SelectList)_controller.ViewData["Prioriteti"]!).Count());
        Assert.AreEqual(Enum.GetValues<Specijalizacija>().Length,
            ((SelectList)_controller.ViewData["Specijalizacije"]!).Count());
        Assert.AreEqual(_uloge.Count, ((SelectList)_controller.ViewData["Uloge"]!).Count());
    }

    [TestMethod]
    [DynamicData(nameof(NevalidanModel), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_NevalidanModel_VracaIstiViewBezSnimanja(string ime, string prezime, string email,
        string lozinka, string uloga, string kljucGreske, string porukaGreske)
    {
        // Arrange
        var korisnik = new Korisnik { Ime = ime, Prezime = prezime, Email = email };
        _controller.ModelState.AddModelError(kljucGreske, porukaGreske);

        // Act
        var rezultat = await _controller.Create(korisnik, lozinka, uloga) as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreSame(korisnik, rezultat.Model);
        Assert.IsNotNull(_controller.ViewData["Uloge"]);
        _korisnikServis.Verify(s => s.AddAsync(It.IsAny<Korisnik>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(NoviKorisnici), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Create_Post_ValidanKorisnik_PostavljaPrioritetIPreusmjerava(string ime, string prezime,
        string email, string lozinka, string uloga, string ocekivaniPrioritet)
    {
        // Arrange
        var korisnik = new Korisnik { Ime = ime, Prezime = prezime, Email = email };

        // Act
        var rezultat = await _controller.Create(korisnik, lozinka, uloga) as RedirectToActionResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(nameof(KorisnikController.Index), rezultat.ActionName);
        Assert.AreEqual(Enum.Parse<Prioritet>(ocekivaniPrioritet), korisnik.Prioritet);
        _korisnikServis.Verify(s => s.AddAsync(korisnik, lozinka, uloga), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(GreskeKreiranja), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_ServisBaciGresku_VracaViewSaPorukom(string email, string lozinka, string uloga,
        string poruka)
    {
        // Arrange
        var korisnik = new Korisnik { Email = email };
        _korisnikServis.Setup(s => s.AddAsync(korisnik, lozinka, uloga)).ThrowsAsync(new Exception(poruka));

        // Act
        var rezultat = await _controller.Create(korisnik, lozinka, uloga) as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(poruka, PrvaGreska());
    }

    // ─── EDIT (GET) ───────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NepostojeciId), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Get_IdPrazanIliNepostojeci_VracaNotFound(string id, string opis)
    {
        // Arrange
        _korisnikServis.Setup(s => s.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((Korisnik?)null);

        // Act
        var rezultat = await _controller.Edit(Prazno(id));

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult), opis);
    }

    [TestMethod]
    [DynamicData(nameof(TudjiProfil), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Edit_Get_TudjiProfilBezAdmina_VracaForbid(string korisnikId, string prijavljeniId,
        string ulogaPrijavljenog, string uloga)
    {
        // Arrange
        Prijavi(prijavljeniId, ulogaPrijavljenog);
        _korisnikServis.Setup(s => s.GetByIdAsync(korisnikId)).ReturnsAsync(new Korisnik { Id = korisnikId });

        // Act
        var rezultat = await _controller.Edit(korisnikId);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ForbidResult));
    }

    [TestMethod]
    [DynamicData(nameof(DozvoljenEdit), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Autorizacija"), TestCategory("InMemory")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Get_VlastitiProfilIliAdmin_VracaViewSaTrenutnomUlogom(string korisnikId,
        string prijavljeniId, string ulogaPrijavljenog, string ulogaKorisnika)
    {
        // Arrange
        Prijavi(prijavljeniId, ulogaPrijavljenog);
        var korisnik = new Korisnik { Id = korisnikId };
        _korisnikServis.Setup(s => s.GetByIdAsync(korisnikId)).ReturnsAsync(korisnik);
        _korisnikServis.Setup(s => s.GetUlogaAsync(korisnik)).ReturnsAsync(ulogaKorisnika);

        // Act
        var rezultat = await _controller.Edit(korisnikId) as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreSame(korisnik, rezultat.Model);
        Assert.AreEqual(ulogaKorisnika, _controller.ViewData["TrenutnaUloga"]);
        Assert.IsNotNull(_controller.ViewData["Uloge"]);
    }

    // ─── EDIT (POST) ──────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(RazlicitId), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_IdRuteRazlicitOdModela_VracaNotFound(string idRute, string idModela, string uloga)
    {
        // Act
        var rezultat = await _controller.Edit(idRute, new Korisnik { Id = idModela }, uloga, null, null);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        _userManager.Verify(u => u.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(TudjiProfil), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Edit_Post_TudjiProfilBezAdmina_VracaForbid(string korisnikId, string prijavljeniId,
        string ulogaPrijavljenog, string uloga)
    {
        // Arrange
        Prijavi(prijavljeniId, ulogaPrijavljenog);

        // Act
        var rezultat = await _controller.Edit(korisnikId, new Korisnik { Id = korisnikId }, uloga, null, null);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ForbidResult));
        _userManager.Verify(u => u.UpdateAsync(It.IsAny<Korisnik>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(EditNepostojeci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_KorisnikNePostoji_VracaNotFound(string id, string ulogaPrijavljenog, string uloga)
    {
        // Arrange
        Prijavi(id, ulogaPrijavljenog);
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync((Korisnik?)null);

        // Act
        var rezultat = await _controller.Edit(id, new Korisnik { Id = id }, uloga, null, null);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(AdminEdit), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Edit_Post_AdminValidniPodaci_AzuriraSvaPoljaIPreusmjerava(string id, string ulogaPrijavljenog,
        string ime,
        string prezime, string email, string telefon, string specijalizacija, string prioritet, string brojKartona,
        string uloga, string novaLozinka, string token, string securityStamp, string ocekivaniPrefiksKartona,
        bool mijenjaUlogu, bool resetujeLozinku, bool osvjezavaStamp)
    {
        // Arrange
        Prijavi(id, ulogaPrijavljenog);
        var postojeci = new Korisnik { Id = id, SecurityStamp = Prazno(securityStamp) };
        var forma = new Korisnik
        {
            Id = id,
            Ime = ime,
            Prezime = prezime,
            Email = email,
            PhoneNumber = Prazno(telefon),
            Specijalizacija = EnumIliNull<Specijalizacija>(specijalizacija),
            Prioritet = EnumIliNull<Prioritet>(prioritet),
            BrojKartona = Prazno(brojKartona)
        };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(postojeci);
        _userManager.Setup(u => u.UpdateSecurityStampAsync(postojeci)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.UpdateAsync(postojeci)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.GeneratePasswordResetTokenAsync(postojeci)).ReturnsAsync(token);
        _userManager.Setup(u => u.ResetPasswordAsync(postojeci, token, novaLozinka))
                    .ReturnsAsync(IdentityResult.Success);

        // Act
        var rezultat = await _controller.Edit(id, forma, uloga, Prazno(novaLozinka), Prazno(novaLozinka))
            as RedirectToActionResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(nameof(KorisnikController.Details), rezultat.ActionName);
        Assert.AreEqual(id, rezultat.RouteValues!["id"]);
        Assert.AreEqual(forma.Ime, postojeci.Ime);
        Assert.AreEqual(forma.Prezime, postojeci.Prezime);
        Assert.AreEqual(forma.Email, postojeci.Email);
        Assert.AreEqual(forma.Email, postojeci.UserName);
        Assert.AreEqual(forma.PhoneNumber, postojeci.PhoneNumber);
        Assert.AreEqual(forma.Specijalizacija, postojeci.Specijalizacija);
        Assert.AreEqual(forma.Prioritet, postojeci.Prioritet);
        if (string.IsNullOrEmpty(ocekivaniPrefiksKartona))
            Assert.IsNull(postojeci.BrojKartona);
        else
            StringAssert.StartsWith(postojeci.BrojKartona, ocekivaniPrefiksKartona);
        _korisnikServis.Verify(s => s.PromijeniUloguAsync(id, uloga), mijenjaUlogu ? Times.Once() : Times.Never());
        _userManager.Verify(u => u.ResetPasswordAsync(postojeci, token, novaLozinka),
            resetujeLozinku ? Times.Once() : Times.Never());
        _userManager.Verify(u => u.UpdateSecurityStampAsync(postojeci),
            osvjezavaStamp ? Times.Once() : Times.Never());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikEdit), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_ObicanKorisnik_MijenjaSamoKontaktILozinku(string id, string ulogaPrijavljenog,
        string securityStamp, string staroIme, string staroPrezime, string ime, string prezime, string email,
        string telefon, string novaLozinka, string potvrda, string token, bool resetujeLozinku)
    {
        // Arrange
        Prijavi(id, ulogaPrijavljenog);
        var postojeci = new Korisnik { Id = id, Ime = staroIme, Prezime = staroPrezime, SecurityStamp = securityStamp };
        var forma = new Korisnik { Id = id, Ime = ime, Prezime = prezime, Email = email, PhoneNumber = telefon };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(postojeci);
        _userManager.Setup(u => u.UpdateAsync(postojeci)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.GeneratePasswordResetTokenAsync(postojeci)).ReturnsAsync(token);
        _userManager.Setup(u => u.ResetPasswordAsync(postojeci, token, novaLozinka))
                    .ReturnsAsync(IdentityResult.Success);

        // Act
        var rezultat = await _controller.Edit(id, forma, ulogaPrijavljenog, Prazno(novaLozinka), Prazno(potvrda))
            as RedirectToActionResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(staroIme, postojeci.Ime);
        Assert.AreEqual(staroPrezime, postojeci.Prezime);
        Assert.AreEqual(email, postojeci.Email);
        Assert.AreEqual(email, postojeci.UserName);
        Assert.AreEqual(telefon, postojeci.PhoneNumber);
        _korisnikServis.Verify(s => s.PromijeniUloguAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _userManager.Verify(u => u.ResetPasswordAsync(postojeci, token, novaLozinka),
            resetujeLozinku ? Times.Once() : Times.Never());
    }

    [TestMethod]
    [DynamicData(nameof(RazliciteLozinke), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_LozinkeSeNePodudaraju_VracaViewSaGreskom(string id, string ulogaPrijavljenog,
        string securityStamp, string email, string novaLozinka, string potvrda, string ocekivanaPoruka,
        string trenutnaUloga)
    {
        // Arrange
        Prijavi(id, ulogaPrijavljenog);
        var postojeci = new Korisnik { Id = id, SecurityStamp = securityStamp };
        var forma = new Korisnik { Id = id, Email = email };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(postojeci);
        _userManager.Setup(u => u.UpdateAsync(postojeci)).ReturnsAsync(IdentityResult.Success);
        _korisnikServis.Setup(s => s.GetUlogaAsync(postojeci)).ReturnsAsync(trenutnaUloga);

        // Act
        var rezultat = await _controller.Edit(id, forma, trenutnaUloga, novaLozinka, potvrda) as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreSame(forma, rezultat.Model);
        Assert.AreEqual(ocekivanaPoruka, PrvaGreska());
        Assert.AreEqual(trenutnaUloga, _controller.ViewData["TrenutnaUloga"]);
        _userManager.Verify(u => u.ResetPasswordAsync(It.IsAny<Korisnik>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(GreskeAzuriranja), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_UpdateNeuspjesan_VracaViewSaGreskomIUlogom(string id, string ulogaPrijavljenog,
        string uloga, string securityStamp, string[] greske, string ocekivanaPoruka, bool svjeziPostoji,
        string trenutnaUloga, string ocekivanaUlogaUViewBagu)
    {
        // Arrange
        Prijavi(id, ulogaPrijavljenog);
        var postojeci = new Korisnik { Id = id, SecurityStamp = securityStamp };
        _userManager.SetupSequence(u => u.FindByIdAsync(id))
                    .ReturnsAsync(postojeci)
                    .ReturnsAsync(svjeziPostoji ? postojeci : null);
        _userManager.Setup(u => u.UpdateAsync(postojeci)).ReturnsAsync(Neuspjeh(greske));
        _korisnikServis.Setup(s => s.GetUlogaAsync(postojeci)).ReturnsAsync(trenutnaUloga);

        // Act
        var rezultat = await _controller.Edit(id, new Korisnik { Id = id }, uloga, null, null) as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivanaPoruka, PrvaGreska());
        Assert.AreEqual(ocekivanaUlogaUViewBagu, _controller.ViewData["TrenutnaUloga"]);
        _korisnikServis.Verify(s => s.PromijeniUloguAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(GreskeLozinke), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_ResetLozinkeNeuspjesan_VracaViewSaGreskom(string id, string ulogaPrijavljenog,
        string uloga, string securityStamp, string novaLozinka, string token, string[] greske,
        string ocekivanaPoruka, string trenutnaUloga)
    {
        // Arrange
        Prijavi(id, ulogaPrijavljenog);
        var postojeci = new Korisnik { Id = id, SecurityStamp = securityStamp };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(postojeci);
        _userManager.Setup(u => u.UpdateAsync(postojeci)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.GeneratePasswordResetTokenAsync(postojeci)).ReturnsAsync(token);
        _userManager.Setup(u => u.ResetPasswordAsync(postojeci, token, novaLozinka)).ReturnsAsync(Neuspjeh(greske));
        _korisnikServis.Setup(s => s.GetUlogaAsync(postojeci)).ReturnsAsync(trenutnaUloga);

        // Act
        var rezultat = await _controller.Edit(id, new Korisnik { Id = id }, uloga, novaLozinka, novaLozinka)
            as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivanaPoruka, PrvaGreska());
        Assert.AreEqual(trenutnaUloga, _controller.ViewData["TrenutnaUloga"]);
    }

    // ─── DELETE (GET) ─────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NepostojeciId), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Delete_Get_IdPrazanIliNepostojeci_VracaNotFound(string id, string opis)
    {
        // Arrange
        _korisnikServis.Setup(s => s.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((Korisnik?)null);

        // Act
        var rezultat = await _controller.Delete(Prazno(id));

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult), opis);
    }

    [TestMethod]
    [DynamicData(nameof(PostojeciKorisnici), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Delete_Get_KorisnikPostoji_VracaPotvrduSaUlogom(string id, string ime, string prezime,
        string uloga)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id, Ime = ime, Prezime = prezime };
        _korisnikServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(korisnik);
        _korisnikServis.Setup(s => s.GetUlogaAsync(korisnik)).ReturnsAsync(uloga);

        // Act
        var rezultat = await _controller.Delete(id) as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreSame(korisnik, rezultat.Model);
        Assert.AreEqual(uloga, _controller.ViewData["Uloga"]);
    }

    // ─── DELETE (POST) ────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(BrisanjeNepostojeci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_KorisnikNePostoji_VracaNotFound(string id)
    {
        // Arrange
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync((Korisnik?)null);

        // Act
        var rezultat = await _controller.DeleteConfirmed(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        _userManager.Verify(u => u.DeleteAsync(It.IsAny<Korisnik>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(ScenarijiBrisanja), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("InMemory"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    [Timeout(5000)]
    public async Task DeleteConfirmed_KorisnikPostoji_BriseVezanePodatkeIKorisnika(string naziv)
    {
        // Arrange
        var scenarij = Scenarij("KorisnikController_brisanje.xml", naziv);
        NapuniBazu(scenarij);
        var id = (string)scenarij.Element("korisnikId")!;
        var korisnik = new Korisnik { Id = id };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(korisnik);
        _userManager.Setup(u => u.DeleteAsync(korisnik)).ReturnsAsync(IdentityResult.Success);
        var ocekivano = scenarij.Element("ocekivano")!;

        // Act
        var rezultat = await _controller.DeleteConfirmed(id) as RedirectToActionResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(nameof(KorisnikController.Index), rezultat.ActionName);
        using var provjera = NovaBaza();
        Assert.AreEqual((int)ocekivano.Attribute("termini")!, provjera.Termin.Count());
        Assert.AreEqual((int)ocekivano.Attribute("nalazi")!, provjera.Nalaz.Count());
        Assert.AreEqual((int)ocekivano.Attribute("notifikacije")!, provjera.Notifikacija.Count());
        Assert.AreEqual((int)ocekivano.Attribute("listeCekanja")!, provjera.ListaCekanja.Count());
        Assert.AreEqual((int)ocekivano.Attribute("naListi")!, provjera.KorisnikNaListi.Count());
        Assert.AreEqual((int)ocekivano.Attribute("recenzije")!, provjera.Recenzija.Count());
        Assert.IsFalse(provjera.Termin.Any(t => t.DoktorID == id || t.PacijentID == id));
        Assert.IsFalse(provjera.Recenzija.Any(r => r.DoktorID == id || r.PacijentID == id));
        _userManager.Verify(u => u.DeleteAsync(korisnik), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(GreskeBrisanja), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_BrisanjeNeuspjesno_VracaViewSaGreskom(string id, string uloga,
        string[] greske, string ocekivanaPoruka)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(korisnik);
        _userManager.Setup(u => u.DeleteAsync(korisnik)).ReturnsAsync(Neuspjeh(greske));
        _korisnikServis.Setup(s => s.GetUlogaAsync(korisnik)).ReturnsAsync(uloga);

        // Act
        var rezultat = await _controller.DeleteConfirmed(id) as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreSame(korisnik, rezultat.Model);
        Assert.AreEqual(ocekivanaPoruka, _controller.ViewData["GreskaBrisanja"]);
        Assert.AreEqual(uloga, _controller.ViewData["Uloga"]);
    }

    [TestMethod]
    [DynamicData(nameof(UnutrasnjaGreska), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_IzuzetakSaUnutrasnjim_PrikazujeUnutrasnjuPoruku(string id, string uloga,
        string vanjskaPoruka, string unutrasnjaPoruka)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(korisnik);
        _userManager.Setup(u => u.DeleteAsync(korisnik))
                    .ThrowsAsync(new Exception(vanjskaPoruka, new Exception(unutrasnjaPoruka)));
        _korisnikServis.Setup(s => s.GetUlogaAsync(korisnik)).ReturnsAsync(uloga);

        // Act
        var rezultat = await _controller.DeleteConfirmed(id) as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(unutrasnjaPoruka, _controller.ViewData["GreskaBrisanja"]);
    }

    // ─── POPRAVI STAMPOVE ─────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(Stampovi), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PopraviStampove_NekiKorisniciBezStampa_AzuriraSamoNjih(string[] ids, string[] stampovi,
        int ocekivaniBroj, string ocekivanaPoruka)
    {
        // Arrange
        var korisnici = ids.Select((id, i) => new Korisnik { Id = id, SecurityStamp = Prazno(stampovi[i]) }).ToList();
        _korisnikServis.Setup(s => s.GetAllAsync()).ReturnsAsync(korisnici);
        _userManager.Setup(u => u.UpdateSecurityStampAsync(It.IsAny<Korisnik>())).ReturnsAsync(IdentityResult.Success);

        // Act
        var rezultat = await _controller.PopraviStampove() as ContentResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivanaPoruka, rezultat.Content);
        _userManager.Verify(u => u.UpdateSecurityStampAsync(It.IsAny<Korisnik>()), Times.Exactly(ocekivaniBroj));
    }
}
