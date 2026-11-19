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

// ─── Redovi iz CSV/XML-a za Edit testove (čitaju se po id-u) ────────────

public sealed class AdminEditRed
{
    public string Id { get; init; } = null!;
    public string UlogaPrijavljenog { get; init; } = null!;
    public Korisnik Forma { get; init; } = null!;
    public string? BrojKartona { get; init; }
    public string Uloga { get; init; } = null!;
    public string? NovaLozinka { get; init; }
    public string Token { get; init; } = null!;
    public string? SecurityStamp { get; init; }
    public string OcekivaniPrefiksKartona { get; init; } = null!;
}

public sealed class KorisnikEditRed
{
    public string Id { get; init; } = null!;
    public string UlogaPrijavljenog { get; init; } = null!;
    public string SecurityStamp { get; init; } = null!;
    public string StaroIme { get; init; } = null!;
    public string StaroPrezime { get; init; } = null!;
    public Korisnik Forma { get; init; } = null!;
    public string? NovaLozinka { get; init; }
    public string? Potvrda { get; init; }
    public string Token { get; init; } = null!;
}

public sealed class GreskaAzuriranjaRed
{
    public string Id { get; init; } = null!;
    public string UlogaPrijavljenog { get; init; } = null!;
    public string Uloga { get; init; } = null!;
    public string SecurityStamp { get; init; } = null!;
    public string[] Greske { get; init; } = null!;
    public string OcekivanaPoruka { get; init; } = null!;
    public string TrenutnaUloga { get; init; } = null!;
    public string OcekivanaUlogaUViewBagu { get; init; } = null!;
}

// ─── Izvori podataka i pomoćne metode za KorisnikControllerTests ────────
public static class KorisnikControllerPodaci
{
    // ── pomoćne metode ──

    public static string? Prazno(string? s) => string.IsNullOrEmpty(s) ? null : s;

    public static T? EnumIliNull<T>(string? s) where T : struct, Enum
        => string.IsNullOrEmpty(s) ? null : Enum.Parse<T>(s);

    public static IdentityResult Neuspjeh(string[] greske)
        => IdentityResult.Failed(greske.Select(g => new IdentityError { Description = g }).ToArray());

    public static string[] Greske(XElement x)
        => x.Element("greske")!.Elements("greska").Select(g => (string)g).ToArray();

    public static Mock<UserManager<Korisnik>> NapraviUserManager()
        => new(Mock.Of<IUserStore<Korisnik>>(), null!, null!, null!, null!, null!, null!, null!, null!);

    public static Mock<RoleManager<IdentityRole>> NapraviRoleManager()
        => new(Mock.Of<IRoleStore<IdentityRole>>(), null!, null!, null!, null!);

    public static ApplicationDbContext NovaBaza(string imeBaze) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(imeBaze).Options);

    public static void NapuniUloge(string imeBaze, IEnumerable<string> uloge)
    {
        using var baza = NovaBaza(imeBaze);
        baza.Roles.AddRange(uloge.Select(u => new IdentityRole(u)));
        baza.SaveChanges();
    }

    // Prijavljuje korisnika s ulogom: User dobija claimove, a UserManager.GetUserAsync vraća tog korisnika.
    public static void Prijavi(Controller kontroler, Mock<UserManager<Korisnik>> userManager, string korisnikId,
        string uloga)
    {
        kontroler.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, korisnikId), new Claim(ClaimTypes.Role, uloga) }, "Test"));
        userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                   .ReturnsAsync(new Korisnik { Id = korisnikId });
    }

    public static string PrvaGreska(Controller kontroler)
        => kontroler.ModelState[string.Empty]!.Errors[0].ErrorMessage;

    // Svi UserManager pozivi koje uspješan Edit (POST) koristi.
    public static void PodesiUspjesanEdit(Mock<UserManager<Korisnik>> userManager, Korisnik postojeci, string token)
    {
        userManager.Setup(u => u.FindByIdAsync(postojeci.Id)).ReturnsAsync(postojeci);
        userManager.Setup(u => u.UpdateSecurityStampAsync(postojeci)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(u => u.UpdateAsync(postojeci)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(u => u.GeneratePasswordResetTokenAsync(postojeci)).ReturnsAsync(token);
        userManager.Setup(u => u.ResetPasswordAsync(postojeci, It.IsAny<string>(), It.IsAny<string>()))
                   .ReturnsAsync(IdentityResult.Success);
    }

    public static XElement Scenarij(string fajl, string naziv)
        => XmlLoader.Ucitaj(fajl, "scenarij").Single(s => (string)s.Attribute("naziv")! == naziv);

    // Puni InMemory bazu termina, nalaza, notifikacija, lista čekanja i recenzija iz XML scenarija.
    public static void NapuniBazu(string imeBaze, XElement s)
    {
        using var baza = NovaBaza(imeBaze);
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

    // Napuni bazu iz scenarija brisanja i podesi UserManager da korisnik postoji i da se uspješno briše.
    public static (string Id, Korisnik Korisnik, XElement Ocekivano) PripremiBrisanje(string imeBaze,
        Mock<UserManager<Korisnik>> userManager, string naziv)
    {
        var scenarij = Scenarij("KorisnikController_brisanje.xml", naziv);
        NapuniBazu(imeBaze, scenarij);
        var id = (string)scenarij.Element("korisnikId")!;
        var korisnik = new Korisnik { Id = id };
        userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(korisnik);
        userManager.Setup(u => u.DeleteAsync(korisnik)).ReturnsAsync(IdentityResult.Success);
        return (id, korisnik, scenarij.Element("ocekivano")!);
    }

    // ── redovi po id-u ──

    private static IEnumerable<string[]> AdminEditCsv() => CsvLoader.Ucitaj("KorisnikController_adminEdit.csv");

    public static AdminEditRed AdminEdit(string id)
    {
        var r = AdminEditCsv().Single(red => red[0] == id);
        return new AdminEditRed
        {
            Id = r[0],
            UlogaPrijavljenog = r[1],
            Forma = new Korisnik
            {
                Id = r[0],
                Ime = r[2],
                Prezime = r[3],
                Email = r[4],
                PhoneNumber = Prazno(r[5]),
                Specijalizacija = EnumIliNull<Specijalizacija>(r[6]),
                Prioritet = EnumIliNull<Prioritet>(r[7]),
                BrojKartona = Prazno(r[8])
            },
            BrojKartona = Prazno(r[8]),
            Uloga = r[9],
            NovaLozinka = Prazno(r[10]),
            Token = r[11],
            SecurityStamp = Prazno(r[12]),
            OcekivaniPrefiksKartona = r[13]
        };
    }

    private static IEnumerable<string[]> KorisnikEditCsv() => CsvLoader.Ucitaj("KorisnikController_korisnikEdit.csv");

    public static KorisnikEditRed KorisnikEdit(string id)
    {
        var r = KorisnikEditCsv().Single(red => red[0] == id);
        return new KorisnikEditRed
        {
            Id = r[0],
            UlogaPrijavljenog = r[1],
            SecurityStamp = r[2],
            StaroIme = r[3],
            StaroPrezime = r[4],
            Forma = new Korisnik { Id = r[0], Ime = r[5], Prezime = r[6], Email = r[7], PhoneNumber = r[8] },
            NovaLozinka = Prazno(r[9]),
            Potvrda = Prazno(r[10]),
            Token = r[11]
        };
    }

    private static IEnumerable<XElement> GreskeAzuriranjaXml()
        => XmlLoader.Ucitaj("KorisnikController_greskaAzuriranja.xml", "scenarij");

    public static GreskaAzuriranjaRed GreskaAzuriranja(string id)
    {
        var x = GreskeAzuriranjaXml().Single(s => (string)s.Element("id")! == id);
        return new GreskaAzuriranjaRed
        {
            Id = (string)x.Element("id")!,
            UlogaPrijavljenog = (string)x.Element("ulogaPrijavljenog")!,
            Uloga = (string)x.Element("uloga")!,
            SecurityStamp = (string)x.Element("securityStamp")!,
            Greske = Greske(x),
            OcekivanaPoruka = (string)x.Element("ocekivanaPoruka")!,
            TrenutnaUloga = (string)x.Element("trenutnaUloga")!,
            OcekivanaUlogaUViewBagu = (string)x.Element("ocekivanaUlogaUViewBagu")!
        };
    }

    // ── izvori podataka (CSV) ──

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
        CsvLoader.Ucitaj("KorisnikController_greskaKreiranja.csv").Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> TudjiProfil() =>
        CsvLoader.Ucitaj("KorisnikController_tudjiProfil.csv").Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> DozvoljenEdit() =>
        CsvLoader.Ucitaj("KorisnikController_dozvoljenEdit.csv").Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> RazlicitId() =>
        CsvLoader.Ucitaj("KorisnikController_razlicitId.csv").Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> EditNepostojeci() =>
        CsvLoader.Ucitaj("KorisnikController_editNepostojeci.csv").Select(r => new object[] { r[0], r[1], r[2] });

    // adminEdit.csv: [8]=brojKartona, [9]=uloga, [10]=novaLozinka, [12]=securityStamp, [13]=prefiks kartona
    public static IEnumerable<object[]> AdminEditSvi() =>
        AdminEditCsv().Select(r => new object[] { r[0] });
    public static IEnumerable<object[]> AdminEditPacijentBezKartona() =>
        AdminEditCsv().Where(r => r[8] == string.Empty && r[13] != string.Empty).Select(r => new object[] { r[0] });
    public static IEnumerable<object[]> AdminEditSaKartonom() =>
        AdminEditCsv().Where(r => r[8] != string.Empty).Select(r => new object[] { r[0] });
    public static IEnumerable<object[]> AdminEditOsobljeBezKartona() =>
        AdminEditCsv().Where(r => r[8] == string.Empty && r[13] == string.Empty).Select(r => new object[] { r[0] });
    public static IEnumerable<object[]> AdminEditSaUlogom() =>
        AdminEditCsv().Where(r => r[9] != string.Empty).Select(r => new object[] { r[0] });
    public static IEnumerable<object[]> AdminEditBezUloge() =>
        AdminEditCsv().Where(r => r[9] == string.Empty).Select(r => new object[] { r[0] });
    public static IEnumerable<object[]> AdminEditSaLozinkom() =>
        AdminEditCsv().Where(r => r[10] != string.Empty).Select(r => new object[] { r[0] });
    public static IEnumerable<object[]> AdminEditBezLozinke() =>
        AdminEditCsv().Where(r => r[10] == string.Empty).Select(r => new object[] { r[0] });
    public static IEnumerable<object[]> AdminEditBezStampa() =>
        AdminEditCsv().Where(r => r[12] == string.Empty).Select(r => new object[] { r[0] });
    public static IEnumerable<object[]> AdminEditSaStampom() =>
        AdminEditCsv().Where(r => r[12] != string.Empty).Select(r => new object[] { r[0] });

    // korisnikEdit.csv: [9]=novaLozinka
    public static IEnumerable<object[]> KorisnikEditSvi() =>
        KorisnikEditCsv().Select(r => new object[] { r[0] });
    public static IEnumerable<object[]> KorisnikEditSaLozinkom() =>
        KorisnikEditCsv().Where(r => r[9] != string.Empty).Select(r => new object[] { r[0] });
    public static IEnumerable<object[]> KorisnikEditBezLozinke() =>
        KorisnikEditCsv().Where(r => r[9] == string.Empty).Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> RazliciteLozinke() =>
        CsvLoader.Ucitaj("KorisnikController_lozinkeRazlicite.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7] });

    public static IEnumerable<object[]> BrisanjeNepostojeci() =>
        CsvLoader.Ucitaj("KorisnikController_brisanjeNepostojeci.csv").Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> UnutrasnjaGreska() =>
        CsvLoader.Ucitaj("KorisnikController_unutrasnjaGreska.csv").Select(r => new object[] { r[0], r[1], r[2], r[3] });

    // ── izvori podataka (XML) ──

    public static IEnumerable<object[]> IndexScenariji() =>
        XmlLoader.Ucitaj("KorisnikController_index.xml", "scenarij").Select(x => new object[]
        {
            x.Elements("korisnik").Select(k => (string)k.Attribute("id")!).ToArray()
        });

    // Svaki korisnik iz index.xml kao poseban red: id, uloga koju servis vraća, očekivani tekst u mapi.
    public static IEnumerable<object[]> IndexKorisnici() =>
        XmlLoader.Ucitaj("KorisnikController_index.xml", "korisnik").Select(k => new object[]
        {
            (string)k.Attribute("id")!,
            (string)k.Attribute("uloga")!,
            (string)k.Attribute("ocekivano")!
        });

    public static IEnumerable<object[]> SortiraneUloge() =>
        XmlLoader.Ucitaj("KorisnikController_uloge.xml", "sortirano")
                 .Select(x => new object[] { ((string)x).Split(',') });

    public static IEnumerable<object[]> GreskeAzuriranjaSvi() =>
        GreskeAzuriranjaXml().Select(x => new object[] { (string)x.Element("id")! });
    public static IEnumerable<object[]> GreskeAzuriranjaKorisnikPostoji() =>
        GreskeAzuriranjaXml().Where(x => (bool)x.Element("svjeziPostoji")!)
                             .Select(x => new object[] { (string)x.Element("id")! });
    public static IEnumerable<object[]> GreskeAzuriranjaKorisnikObrisan() =>
        GreskeAzuriranjaXml().Where(x => !(bool)x.Element("svjeziPostoji")!)
                             .Select(x => new object[] { (string)x.Element("id")! });

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
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class KorisnikControllerTests
{
    private static List<string> _uloge = null!;   // samo za čitanje

    private Mock<IKorisnikService> _korisnikServis = null!;
    private Mock<ITerminService> _terminServis = null!;   // Dummy: kontroler ga prima, ali ga ne koristi
    private Mock<UserManager<Korisnik>> _userManager = null!;
    private Mock<RoleManager<IdentityRole>> _roleManager = null!;
    private ApplicationDbContext _context = null!;
    private KorisnikController _controller = null!;
    private string _imeBaze = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // Uloge se čitaju jednom; svaki test ih upisuje u svoju InMemory bazu.
        _uloge = XmlLoader.Ucitaj("KorisnikController_uloge.xml", "uloga").Select(x => (string)x).ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        _imeBaze = Guid.NewGuid().ToString();
        KorisnikControllerPodaci.NapuniUloge(_imeBaze, _uloge);
        _context = KorisnikControllerPodaci.NovaBaza(_imeBaze);

        _korisnikServis = new Mock<IKorisnikService>();
        _terminServis = new Mock<ITerminService>();
        _userManager = KorisnikControllerPodaci.NapraviUserManager();
        _roleManager = KorisnikControllerPodaci.NapraviRoleManager();
        _roleManager.Setup(r => r.Roles).Returns(_context.Roles);

        _controller = new KorisnikController(_korisnikServis.Object, _terminServis.Object,
            _roleManager.Object, _userManager.Object, _context)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        _controller.Dispose();
        _context.Dispose();
    }

    // ─── INDEX ────────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.IndexScenariji), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Index_PostojeKorisnici_VracaSveKorisnike(string[] ids)
    {
        // Arrange
        var korisnici = ids.Select(id => new Korisnik { Id = id }).ToList();
        _korisnikServis.Setup(s => s.GetAllAsync()).ReturnsAsync(korisnici);
        _korisnikServis.Setup(s => s.GetUlogaAsync(It.IsAny<Korisnik>())).ReturnsAsync(string.Empty);

        // Act
        var rezultat = await _controller.Index() as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreSame(korisnici, rezultat.Model);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.IndexKorisnici), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Index_ZaSvakogKorisnika_MapaSadrziOcekivanuUlogu(string id, string uloga, string ocekivano)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id };
        _korisnikServis.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Korisnik> { korisnik });
        _korisnikServis.Setup(s => s.GetUlogaAsync(korisnik)).ReturnsAsync(KorisnikControllerPodaci.Prazno(uloga)!);

        // Act
        await _controller.Index();

        // Assert
        var mapa = (Dictionary<string, string>)_controller.ViewData["UlogeMap"]!;
        Assert.AreEqual(ocekivano, mapa[id]);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.SortiraneUloge), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Index_UlogeUBazi_SortiraAbecedno(string[] sortirano)
    {
        // Arrange
        _korisnikServis.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Korisnik>());

        // Act
        await _controller.Index();

        // Assert
        CollectionAssert.AreEqual(sortirano, (System.Collections.ICollection)_controller.ViewData["SveUloge"]!);
    }

    // ─── DETAILS ──────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.NepostojeciId), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Details_IdPrazanIliNepostojeci_VracaNotFound(string id, string opis)
    {
        // Arrange
        _korisnikServis.Setup(s => s.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((Korisnik?)null);

        // Act
        var rezultat = await _controller.Details(KorisnikControllerPodaci.Prazno(id));

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult), opis);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.PostojeciKorisnici), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Details_KorisnikPostoji_VracaViewSKorisnikom(string id, string ime, string prezime, string uloga)
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
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.PostojeciKorisnici), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Details_KorisnikPostoji_PrikazujeUlogu(string id, string ime, string prezime, string uloga)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id, Ime = ime, Prezime = prezime };
        _korisnikServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(korisnik);
        _korisnikServis.Setup(s => s.GetUlogaAsync(korisnik)).ReturnsAsync(uloga);

        // Act
        await _controller.Details(id);

        // Assert
        Assert.AreEqual(uloga, _controller.ViewData["Uloga"]);
    }

    // ─── CREATE ───────────────────────────────────────────────────────────

    [TestMethod]
    [TestCategory("Stub"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Get_NudiSvePrioritete()
    {
        // Act
        await _controller.Create();

        // Assert
        Assert.AreEqual(Enum.GetValues<Prioritet>().Length, ((SelectList)_controller.ViewData["Prioriteti"]!).Count());
    }

    [TestMethod]
    [TestCategory("Stub"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Get_NudiSveSpecijalizacije()
    {
        // Act
        await _controller.Create();

        // Assert
        Assert.AreEqual(Enum.GetValues<Specijalizacija>().Length,
            ((SelectList)_controller.ViewData["Specijalizacije"]!).Count());
    }

    [TestMethod]
    [TestCategory("Stub"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Get_NudiSveUlogeIzBaze()
    {
        // Act
        await _controller.Create();

        // Assert
        Assert.AreEqual(_uloge.Count, ((SelectList)_controller.ViewData["Uloge"]!).Count());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.NevalidanModel), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_NevalidanModel_VracaIstiView(string ime, string prezime, string email,
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
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.NevalidanModel), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_NevalidanModel_NeSnimaKorisnika(string ime, string prezime, string email,
        string lozinka, string uloga, string kljucGreske, string porukaGreske)
    {
        // Arrange
        var korisnik = new Korisnik { Ime = ime, Prezime = prezime, Email = email };
        _controller.ModelState.AddModelError(kljucGreske, porukaGreske);

        // Act
        await _controller.Create(korisnik, lozinka, uloga);

        // Assert
        _korisnikServis.Verify(s => s.AddAsync(It.IsAny<Korisnik>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.NoviKorisnici), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Create_Post_ValidanKorisnik_PreusmjeravaNaIndex(string ime, string prezime,
        string email, string lozinka, string uloga, string ocekivaniPrioritet)
    {
        // Arrange
        var korisnik = new Korisnik { Ime = ime, Prezime = prezime, Email = email };

        // Act
        var rezultat = await _controller.Create(korisnik, lozinka, uloga) as RedirectToActionResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(nameof(KorisnikController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.NoviKorisnici), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_ValidanKorisnik_PostavljaNizakPrioritet(string ime, string prezime,
        string email, string lozinka, string uloga, string ocekivaniPrioritet)
    {
        // Arrange
        var korisnik = new Korisnik { Ime = ime, Prezime = prezime, Email = email };

        // Act
        await _controller.Create(korisnik, lozinka, uloga);

        // Assert
        Assert.AreEqual(Enum.Parse<Prioritet>(ocekivaniPrioritet), korisnik.Prioritet);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.NoviKorisnici), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Create_Post_ValidanKorisnik_SnimaKrozServis(string ime, string prezime,
        string email, string lozinka, string uloga, string ocekivaniPrioritet)
    {
        // Arrange
        var korisnik = new Korisnik { Ime = ime, Prezime = prezime, Email = email };

        // Act
        await _controller.Create(korisnik, lozinka, uloga);

        // Assert
        _korisnikServis.Verify(s => s.AddAsync(korisnik, lozinka, uloga), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.GreskeKreiranja), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_ServisBaciGresku_VracaViewSaPorukom(string email, string lozinka, string uloga,
        string poruka)
    {
        // Arrange
        var korisnik = new Korisnik { Email = email };
        _korisnikServis.Setup(s => s.AddAsync(korisnik, lozinka, uloga)).ThrowsAsync(new Exception(poruka));

        // Act
        await _controller.Create(korisnik, lozinka, uloga);

        // Assert
        Assert.AreEqual(poruka, KorisnikControllerPodaci.PrvaGreska(_controller));
    }

    // ─── EDIT (GET) ───────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.NepostojeciId), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Get_IdPrazanIliNepostojeci_VracaNotFound(string id, string opis)
    {
        // Arrange
        _korisnikServis.Setup(s => s.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((Korisnik?)null);

        // Act
        var rezultat = await _controller.Edit(KorisnikControllerPodaci.Prazno(id));

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult), opis);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.TudjiProfil), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Edit_Get_TudjiProfilBezAdmina_VracaForbid(string korisnikId, string prijavljeniId,
        string ulogaPrijavljenog, string uloga)
    {
        // Arrange
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, prijavljeniId, ulogaPrijavljenog);
        _korisnikServis.Setup(s => s.GetByIdAsync(korisnikId)).ReturnsAsync(new Korisnik { Id = korisnikId });

        // Act
        var rezultat = await _controller.Edit(korisnikId);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ForbidResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.DozvoljenEdit), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Get_VlastitiProfilIliAdmin_VracaViewSKorisnikom(string korisnikId,
        string prijavljeniId, string ulogaPrijavljenog, string ulogaKorisnika)
    {
        // Arrange
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, prijavljeniId, ulogaPrijavljenog);
        var korisnik = new Korisnik { Id = korisnikId };
        _korisnikServis.Setup(s => s.GetByIdAsync(korisnikId)).ReturnsAsync(korisnik);
        _korisnikServis.Setup(s => s.GetUlogaAsync(korisnik)).ReturnsAsync(ulogaKorisnika);

        // Act
        var rezultat = await _controller.Edit(korisnikId) as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreSame(korisnik, rezultat.Model);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.DozvoljenEdit), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Get_VlastitiProfilIliAdmin_PrikazujeTrenutnuUlogu(string korisnikId,
        string prijavljeniId, string ulogaPrijavljenog, string ulogaKorisnika)
    {
        // Arrange
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, prijavljeniId, ulogaPrijavljenog);
        var korisnik = new Korisnik { Id = korisnikId };
        _korisnikServis.Setup(s => s.GetByIdAsync(korisnikId)).ReturnsAsync(korisnik);
        _korisnikServis.Setup(s => s.GetUlogaAsync(korisnik)).ReturnsAsync(ulogaKorisnika);

        // Act
        await _controller.Edit(korisnikId);

        // Assert
        Assert.AreEqual(ulogaKorisnika, _controller.ViewData["TrenutnaUloga"]);
    }

    // ─── EDIT (POST): ulaz i autorizacija ─────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.RazlicitId), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_IdRuteRazlicitOdModela_VracaNotFound(string idRute, string idModela, string uloga)
    {
        // Act
        var rezultat = await _controller.Edit(idRute, new Korisnik { Id = idModela }, uloga, null, null);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.TudjiProfil), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Edit_Post_TudjiProfilBezAdmina_VracaForbid(string korisnikId, string prijavljeniId,
        string ulogaPrijavljenog, string uloga)
    {
        // Arrange
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, prijavljeniId, ulogaPrijavljenog);

        // Act
        var rezultat = await _controller.Edit(korisnikId, new Korisnik { Id = korisnikId }, uloga, null, null);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ForbidResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.EditNepostojeci), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_KorisnikNePostoji_VracaNotFound(string id, string ulogaPrijavljenog, string uloga)
    {
        // Arrange
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, id, ulogaPrijavljenog);
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync((Korisnik?)null);

        // Act
        var rezultat = await _controller.Edit(id, new Korisnik { Id = id }, uloga, null, null);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    // ─── EDIT (POST): administrator ───────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.AdminEditSvi), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Edit_PostAdmin_ValidniPodaci_PreusmjeravaNaDetalje(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.AdminEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik { Id = red.Id, SecurityStamp = red.SecurityStamp };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        var rezultat = await _controller.Edit(red.Id, red.Forma, red.Uloga, red.NovaLozinka, red.NovaLozinka)
            as RedirectToActionResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(nameof(KorisnikController.Details), rezultat.ActionName);
        Assert.AreEqual(red.Id, rezultat.RouteValues!["id"]);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.AdminEditSvi), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_PostAdmin_ValidniPodaci_KopiraLicnePodatke(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.AdminEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik { Id = red.Id, SecurityStamp = red.SecurityStamp };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        await _controller.Edit(red.Id, red.Forma, red.Uloga, red.NovaLozinka, red.NovaLozinka);

        // Assert
        Assert.AreEqual(red.Forma.Ime, postojeci.Ime);
        Assert.AreEqual(red.Forma.Prezime, postojeci.Prezime);
        Assert.AreEqual(red.Forma.PhoneNumber, postojeci.PhoneNumber);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.AdminEditSvi), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_PostAdmin_ValidniPodaci_EmailPostajeKorisnickoIme(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.AdminEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik { Id = red.Id, SecurityStamp = red.SecurityStamp };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        await _controller.Edit(red.Id, red.Forma, red.Uloga, red.NovaLozinka, red.NovaLozinka);

        // Assert
        Assert.AreEqual(red.Forma.Email, postojeci.Email);
        Assert.AreEqual(red.Forma.Email, postojeci.UserName);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.AdminEditSvi), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_PostAdmin_ValidniPodaci_KopiraSpecijalizacijuIPrioritet(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.AdminEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik { Id = red.Id, SecurityStamp = red.SecurityStamp };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        await _controller.Edit(red.Id, red.Forma, red.Uloga, red.NovaLozinka, red.NovaLozinka);

        // Assert
        Assert.AreEqual(red.Forma.Specijalizacija, postojeci.Specijalizacija);
        Assert.AreEqual(red.Forma.Prioritet, postojeci.Prioritet);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.AdminEditPacijentBezKartona), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_PostAdmin_PacijentBezKartona_GeneriseKarton(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.AdminEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik { Id = red.Id, SecurityStamp = red.SecurityStamp };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        await _controller.Edit(red.Id, red.Forma, red.Uloga, red.NovaLozinka, red.NovaLozinka);

        // Assert
        StringAssert.StartsWith(postojeci.BrojKartona, red.OcekivaniPrefiksKartona);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.AdminEditSaKartonom), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_PostAdmin_PostojeciKarton_KartonOstajeIsti(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.AdminEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik { Id = red.Id, SecurityStamp = red.SecurityStamp };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        await _controller.Edit(red.Id, red.Forma, red.Uloga, red.NovaLozinka, red.NovaLozinka);

        // Assert
        Assert.AreEqual(red.BrojKartona, postojeci.BrojKartona);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.AdminEditOsobljeBezKartona), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_PostAdmin_OsobljeBezKartona_KartonOstajePrazan(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.AdminEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik { Id = red.Id, SecurityStamp = red.SecurityStamp };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        await _controller.Edit(red.Id, red.Forma, red.Uloga, red.NovaLozinka, red.NovaLozinka);

        // Assert
        Assert.IsNull(postojeci.BrojKartona);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.AdminEditSaUlogom), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Edit_PostAdmin_ZadanaUloga_MijenjaUlogu(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.AdminEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik { Id = red.Id, SecurityStamp = red.SecurityStamp };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        await _controller.Edit(red.Id, red.Forma, red.Uloga, red.NovaLozinka, red.NovaLozinka);

        // Assert
        _korisnikServis.Verify(s => s.PromijeniUloguAsync(red.Id, red.Uloga), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.AdminEditBezUloge), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_PostAdmin_PraznaUloga_NeMijenjaUlogu(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.AdminEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik { Id = red.Id, SecurityStamp = red.SecurityStamp };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        await _controller.Edit(red.Id, red.Forma, red.Uloga, red.NovaLozinka, red.NovaLozinka);

        // Assert
        _korisnikServis.Verify(s => s.PromijeniUloguAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.AdminEditSaLozinkom), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_PostAdmin_NovaLozinka_ResetujeLozinku(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.AdminEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik { Id = red.Id, SecurityStamp = red.SecurityStamp };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        await _controller.Edit(red.Id, red.Forma, red.Uloga, red.NovaLozinka, red.NovaLozinka);

        // Assert
        _userManager.Verify(u => u.ResetPasswordAsync(postojeci, red.Token, red.NovaLozinka!), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.AdminEditBezLozinke), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_PostAdmin_BezNoveLozinke_NeResetujeLozinku(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.AdminEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik { Id = red.Id, SecurityStamp = red.SecurityStamp };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        await _controller.Edit(red.Id, red.Forma, red.Uloga, red.NovaLozinka, red.NovaLozinka);

        // Assert
        _userManager.Verify(u => u.ResetPasswordAsync(It.IsAny<Korisnik>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.AdminEditBezStampa), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_PostAdmin_PrazanSecurityStamp_OsvjezavaStamp(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.AdminEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik { Id = red.Id, SecurityStamp = red.SecurityStamp };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        await _controller.Edit(red.Id, red.Forma, red.Uloga, red.NovaLozinka, red.NovaLozinka);

        // Assert
        _userManager.Verify(u => u.UpdateSecurityStampAsync(postojeci), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.AdminEditSaStampom), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_PostAdmin_PostojeciSecurityStamp_NeOsvjezavaStamp(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.AdminEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik { Id = red.Id, SecurityStamp = red.SecurityStamp };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        await _controller.Edit(red.Id, red.Forma, red.Uloga, red.NovaLozinka, red.NovaLozinka);

        // Assert
        _userManager.Verify(u => u.UpdateSecurityStampAsync(It.IsAny<Korisnik>()), Times.Never);
    }

    // ─── EDIT (POST): obični korisnik ─────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.KorisnikEditSvi), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_PostKorisnik_ValidniPodaci_PreusmjeravaNaDetalje(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.KorisnikEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik
        {
            Id = red.Id, Ime = red.StaroIme, Prezime = red.StaroPrezime, SecurityStamp = red.SecurityStamp
        };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        var rezultat = await _controller.Edit(red.Id, red.Forma, red.UlogaPrijavljenog, red.NovaLozinka, red.Potvrda)
            as RedirectToActionResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(nameof(KorisnikController.Details), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.KorisnikEditSvi), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Edit_PostKorisnik_ValidniPodaci_MijenjaKontakt(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.KorisnikEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik
        {
            Id = red.Id, Ime = red.StaroIme, Prezime = red.StaroPrezime, SecurityStamp = red.SecurityStamp
        };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        await _controller.Edit(red.Id, red.Forma, red.UlogaPrijavljenog, red.NovaLozinka, red.Potvrda);

        // Assert
        Assert.AreEqual(red.Forma.Email, postojeci.Email);
        Assert.AreEqual(red.Forma.Email, postojeci.UserName);
        Assert.AreEqual(red.Forma.PhoneNumber, postojeci.PhoneNumber);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.KorisnikEditSvi), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_PostKorisnik_ValidniPodaci_NeMijenjaImeIPrezime(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.KorisnikEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik
        {
            Id = red.Id, Ime = red.StaroIme, Prezime = red.StaroPrezime, SecurityStamp = red.SecurityStamp
        };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        await _controller.Edit(red.Id, red.Forma, red.UlogaPrijavljenog, red.NovaLozinka, red.Potvrda);

        // Assert
        Assert.AreEqual(red.StaroIme, postojeci.Ime);
        Assert.AreEqual(red.StaroPrezime, postojeci.Prezime);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.KorisnikEditSvi), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_PostKorisnik_ValidniPodaci_NeMijenjaUlogu(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.KorisnikEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik
        {
            Id = red.Id, Ime = red.StaroIme, Prezime = red.StaroPrezime, SecurityStamp = red.SecurityStamp
        };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        await _controller.Edit(red.Id, red.Forma, red.UlogaPrijavljenog, red.NovaLozinka, red.Potvrda);

        // Assert
        _korisnikServis.Verify(s => s.PromijeniUloguAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.KorisnikEditSaLozinkom), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_PostKorisnik_NovaLozinka_ResetujeLozinku(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.KorisnikEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik
        {
            Id = red.Id, Ime = red.StaroIme, Prezime = red.StaroPrezime, SecurityStamp = red.SecurityStamp
        };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        await _controller.Edit(red.Id, red.Forma, red.UlogaPrijavljenog, red.NovaLozinka, red.Potvrda);

        // Assert
        _userManager.Verify(u => u.ResetPasswordAsync(postojeci, red.Token, red.NovaLozinka!), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.KorisnikEditBezLozinke), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_PostKorisnik_BezNoveLozinke_NeResetujeLozinku(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.KorisnikEdit(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik
        {
            Id = red.Id, Ime = red.StaroIme, Prezime = red.StaroPrezime, SecurityStamp = red.SecurityStamp
        };
        KorisnikControllerPodaci.PodesiUspjesanEdit(_userManager, postojeci, red.Token);

        // Act
        await _controller.Edit(red.Id, red.Forma, red.UlogaPrijavljenog, red.NovaLozinka, red.Potvrda);

        // Assert
        _userManager.Verify(u => u.ResetPasswordAsync(It.IsAny<Korisnik>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.RazliciteLozinke), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_PostKorisnik_LozinkeSeNePodudaraju_PrikazujeGresku(string id, string ulogaPrijavljenog,
        string securityStamp, string email, string novaLozinka, string potvrda, string ocekivanaPoruka,
        string trenutnaUloga)
    {
        // Arrange
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, id, ulogaPrijavljenog);
        var postojeci = new Korisnik { Id = id, SecurityStamp = securityStamp };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(postojeci);
        _userManager.Setup(u => u.UpdateAsync(postojeci)).ReturnsAsync(IdentityResult.Success);
        _korisnikServis.Setup(s => s.GetUlogaAsync(postojeci)).ReturnsAsync(trenutnaUloga);

        // Act
        await _controller.Edit(id, new Korisnik { Id = id, Email = email }, trenutnaUloga, novaLozinka, potvrda);

        // Assert
        Assert.AreEqual(ocekivanaPoruka, KorisnikControllerPodaci.PrvaGreska(_controller));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.RazliciteLozinke), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_PostKorisnik_LozinkeSeNePodudaraju_NeResetujeLozinku(string id, string ulogaPrijavljenog,
        string securityStamp, string email, string novaLozinka, string potvrda, string ocekivanaPoruka,
        string trenutnaUloga)
    {
        // Arrange
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, id, ulogaPrijavljenog);
        var postojeci = new Korisnik { Id = id, SecurityStamp = securityStamp };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(postojeci);
        _userManager.Setup(u => u.UpdateAsync(postojeci)).ReturnsAsync(IdentityResult.Success);
        _korisnikServis.Setup(s => s.GetUlogaAsync(postojeci)).ReturnsAsync(trenutnaUloga);

        // Act
        await _controller.Edit(id, new Korisnik { Id = id, Email = email }, trenutnaUloga, novaLozinka, potvrda);

        // Assert
        _userManager.Verify(u => u.ResetPasswordAsync(It.IsAny<Korisnik>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    // ─── EDIT (POST): greške Identity-ja ──────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.GreskeAzuriranjaSvi), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_UpdateNeuspjesan_PrikazujeGresku(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.GreskaAzuriranja(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik { Id = red.Id, SecurityStamp = red.SecurityStamp };
        _userManager.Setup(u => u.FindByIdAsync(red.Id)).ReturnsAsync(postojeci);
        _userManager.Setup(u => u.UpdateAsync(postojeci)).ReturnsAsync(KorisnikControllerPodaci.Neuspjeh(red.Greske));
        _korisnikServis.Setup(s => s.GetUlogaAsync(postojeci)).ReturnsAsync(red.TrenutnaUloga);

        // Act
        await _controller.Edit(red.Id, new Korisnik { Id = red.Id }, red.Uloga, null, null);

        // Assert
        Assert.AreEqual(red.OcekivanaPoruka, KorisnikControllerPodaci.PrvaGreska(_controller));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.GreskeAzuriranjaSvi), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_UpdateNeuspjesan_NeMijenjaUlogu(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.GreskaAzuriranja(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik { Id = red.Id, SecurityStamp = red.SecurityStamp };
        _userManager.Setup(u => u.FindByIdAsync(red.Id)).ReturnsAsync(postojeci);
        _userManager.Setup(u => u.UpdateAsync(postojeci)).ReturnsAsync(KorisnikControllerPodaci.Neuspjeh(red.Greske));
        _korisnikServis.Setup(s => s.GetUlogaAsync(postojeci)).ReturnsAsync(red.TrenutnaUloga);

        // Act
        await _controller.Edit(red.Id, new Korisnik { Id = red.Id }, red.Uloga, null, null);

        // Assert
        _korisnikServis.Verify(s => s.PromijeniUloguAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.GreskeAzuriranjaKorisnikPostoji), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_UpdateNeuspjesanKorisnikPostoji_PrikazujeUloguIzBaze(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.GreskaAzuriranja(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik { Id = red.Id, SecurityStamp = red.SecurityStamp };
        _userManager.Setup(u => u.FindByIdAsync(red.Id)).ReturnsAsync(postojeci);
        _userManager.Setup(u => u.UpdateAsync(postojeci)).ReturnsAsync(KorisnikControllerPodaci.Neuspjeh(red.Greske));
        _korisnikServis.Setup(s => s.GetUlogaAsync(postojeci)).ReturnsAsync(red.TrenutnaUloga);

        // Act
        await _controller.Edit(red.Id, new Korisnik { Id = red.Id }, red.Uloga, null, null);

        // Assert
        Assert.AreEqual(red.OcekivanaUlogaUViewBagu, _controller.ViewData["TrenutnaUloga"]);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.GreskeAzuriranjaKorisnikObrisan), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_UpdateNeuspjesanKorisnikObrisan_PrikazujePoslanuUlogu(string id)
    {
        // Arrange
        var red = KorisnikControllerPodaci.GreskaAzuriranja(id);
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, red.Id, red.UlogaPrijavljenog);
        var postojeci = new Korisnik { Id = red.Id, SecurityStamp = red.SecurityStamp };
        _userManager.SetupSequence(u => u.FindByIdAsync(red.Id))
                    .ReturnsAsync(postojeci)
                    .ReturnsAsync((Korisnik?)null);
        _userManager.Setup(u => u.UpdateAsync(postojeci)).ReturnsAsync(KorisnikControllerPodaci.Neuspjeh(red.Greske));

        // Act
        await _controller.Edit(red.Id, new Korisnik { Id = red.Id }, red.Uloga, null, null);

        // Assert
        Assert.AreEqual(red.OcekivanaUlogaUViewBagu, _controller.ViewData["TrenutnaUloga"]);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.GreskeLozinke), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_ResetLozinkeNeuspjesan_PrikazujeGresku(string id, string ulogaPrijavljenog,
        string uloga, string securityStamp, string novaLozinka, string token, string[] greske,
        string ocekivanaPoruka, string trenutnaUloga)
    {
        // Arrange
        KorisnikControllerPodaci.Prijavi(_controller, _userManager, id, ulogaPrijavljenog);
        var postojeci = new Korisnik { Id = id, SecurityStamp = securityStamp };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(postojeci);
        _userManager.Setup(u => u.UpdateAsync(postojeci)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.GeneratePasswordResetTokenAsync(postojeci)).ReturnsAsync(token);
        _userManager.Setup(u => u.ResetPasswordAsync(postojeci, token, novaLozinka))
                    .ReturnsAsync(KorisnikControllerPodaci.Neuspjeh(greske));
        _korisnikServis.Setup(s => s.GetUlogaAsync(postojeci)).ReturnsAsync(trenutnaUloga);

        // Act
        await _controller.Edit(id, new Korisnik { Id = id }, uloga, novaLozinka, novaLozinka);

        // Assert
        Assert.AreEqual(ocekivanaPoruka, KorisnikControllerPodaci.PrvaGreska(_controller));
    }

    // ─── DELETE (GET) ─────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.NepostojeciId), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Delete_Get_IdPrazanIliNepostojeci_VracaNotFound(string id, string opis)
    {
        // Arrange
        _korisnikServis.Setup(s => s.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((Korisnik?)null);

        // Act
        var rezultat = await _controller.Delete(KorisnikControllerPodaci.Prazno(id));

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult), opis);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.PostojeciKorisnici), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Delete_Get_KorisnikPostoji_VracaPotvrduSKorisnikom(string id, string ime, string prezime,
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
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.PostojeciKorisnici), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Delete_Get_KorisnikPostoji_PrikazujeUlogu(string id, string ime, string prezime, string uloga)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id, Ime = ime, Prezime = prezime };
        _korisnikServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(korisnik);
        _korisnikServis.Setup(s => s.GetUlogaAsync(korisnik)).ReturnsAsync(uloga);

        // Act
        await _controller.Delete(id);

        // Assert
        Assert.AreEqual(uloga, _controller.ViewData["Uloga"]);
    }

    // ─── DELETE (POST) ────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.BrisanjeNepostojeci), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_KorisnikNePostoji_VracaNotFound(string id)
    {
        // Arrange
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync((Korisnik?)null);

        // Act
        var rezultat = await _controller.DeleteConfirmed(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.ScenarijiBrisanja), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_KorisnikPostoji_PreusmjeravaNaIndex(string naziv)
    {
        // Arrange
        var (id, _, _) = KorisnikControllerPodaci.PripremiBrisanje(_imeBaze, _userManager, naziv);

        // Act
        var rezultat = await _controller.DeleteConfirmed(id) as RedirectToActionResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(nameof(KorisnikController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.ScenarijiBrisanja), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    [Timeout(5000)]
    public async Task DeleteConfirmed_KorisnikPostoji_BriseNjegoveTermine(string naziv)
    {
        // Arrange
        var (id, _, ocekivano) = KorisnikControllerPodaci.PripremiBrisanje(_imeBaze, _userManager, naziv);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        using var baza = KorisnikControllerPodaci.NovaBaza(_imeBaze);
        Assert.AreEqual((int)ocekivano.Attribute("termini")!, baza.Termin.Count());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.ScenarijiBrisanja), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Timeout(5000)]
    public async Task DeleteConfirmed_KorisnikPostoji_BriseNalazeNjegovihTermina(string naziv)
    {
        // Arrange
        var (id, _, ocekivano) = KorisnikControllerPodaci.PripremiBrisanje(_imeBaze, _userManager, naziv);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        using var baza = KorisnikControllerPodaci.NovaBaza(_imeBaze);
        Assert.AreEqual((int)ocekivano.Attribute("nalazi")!, baza.Nalaz.Count());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.ScenarijiBrisanja), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Timeout(5000)]
    public async Task DeleteConfirmed_KorisnikPostoji_BriseNotifikacijeNjegovihTermina(string naziv)
    {
        // Arrange
        var (id, _, ocekivano) = KorisnikControllerPodaci.PripremiBrisanje(_imeBaze, _userManager, naziv);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        using var baza = KorisnikControllerPodaci.NovaBaza(_imeBaze);
        Assert.AreEqual((int)ocekivano.Attribute("notifikacije")!, baza.Notifikacija.Count());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.ScenarijiBrisanja), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Timeout(5000)]
    public async Task DeleteConfirmed_KorisnikPostoji_BriseNjegoveListeCekanja(string naziv)
    {
        // Arrange
        var (id, _, ocekivano) = KorisnikControllerPodaci.PripremiBrisanje(_imeBaze, _userManager, naziv);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        using var baza = KorisnikControllerPodaci.NovaBaza(_imeBaze);
        Assert.AreEqual((int)ocekivano.Attribute("listeCekanja")!, baza.ListaCekanja.Count());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.ScenarijiBrisanja), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Timeout(5000)]
    public async Task DeleteConfirmed_KorisnikPostoji_BriseZapiseNaListamaCekanja(string naziv)
    {
        // Arrange
        var (id, _, ocekivano) = KorisnikControllerPodaci.PripremiBrisanje(_imeBaze, _userManager, naziv);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        using var baza = KorisnikControllerPodaci.NovaBaza(_imeBaze);
        Assert.AreEqual((int)ocekivano.Attribute("naListi")!, baza.KorisnikNaListi.Count());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.ScenarijiBrisanja), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Timeout(5000)]
    public async Task DeleteConfirmed_KorisnikPostoji_BriseNjegoveRecenzije(string naziv)
    {
        // Arrange
        var (id, _, ocekivano) = KorisnikControllerPodaci.PripremiBrisanje(_imeBaze, _userManager, naziv);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        using var baza = KorisnikControllerPodaci.NovaBaza(_imeBaze);
        Assert.AreEqual((int)ocekivano.Attribute("recenzije")!, baza.Recenzija.Count());
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.ScenarijiBrisanja), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task DeleteConfirmed_KorisnikPostoji_BriseKorisnikaKrozUserManager(string naziv)
    {
        // Arrange
        var (id, korisnik, _) = KorisnikControllerPodaci.PripremiBrisanje(_imeBaze, _userManager, naziv);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _userManager.Verify(u => u.DeleteAsync(korisnik), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.GreskeBrisanja), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_BrisanjeNeuspjesno_PrikazujeGresku(string id, string uloga,
        string[] greske, string ocekivanaPoruka)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(korisnik);
        _userManager.Setup(u => u.DeleteAsync(korisnik)).ReturnsAsync(KorisnikControllerPodaci.Neuspjeh(greske));
        _korisnikServis.Setup(s => s.GetUlogaAsync(korisnik)).ReturnsAsync(uloga);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(ocekivanaPoruka, _controller.ViewData["GreskaBrisanja"]);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.GreskeBrisanja), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_BrisanjeNeuspjesno_ZadrzavaPrikazUloge(string id, string uloga,
        string[] greske, string ocekivanaPoruka)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(korisnik);
        _userManager.Setup(u => u.DeleteAsync(korisnik)).ReturnsAsync(KorisnikControllerPodaci.Neuspjeh(greske));
        _korisnikServis.Setup(s => s.GetUlogaAsync(korisnik)).ReturnsAsync(uloga);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(uloga, _controller.ViewData["Uloga"]);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.UnutrasnjaGreska), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
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
        await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(unutrasnjaPoruka, _controller.ViewData["GreskaBrisanja"]);
    }

    // ─── POPRAVI STAMPOVE ─────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.Stampovi), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PopraviStampove_NekiKorisniciBezStampa_VracaPorukuSBrojem(string[] ids, string[] stampovi,
        int ocekivaniBroj, string ocekivanaPoruka)
    {
        // Arrange
        var korisnici = ids.Select((id, i) => new Korisnik
        {
            Id = id, SecurityStamp = KorisnikControllerPodaci.Prazno(stampovi[i])
        }).ToList();
        _korisnikServis.Setup(s => s.GetAllAsync()).ReturnsAsync(korisnici);
        _userManager.Setup(u => u.UpdateSecurityStampAsync(It.IsAny<Korisnik>())).ReturnsAsync(IdentityResult.Success);

        // Act
        var rezultat = await _controller.PopraviStampove() as ContentResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivanaPoruka, rezultat.Content);
    }

    [TestMethod]
    [DynamicData(nameof(KorisnikControllerPodaci.Stampovi), typeof(KorisnikControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PopraviStampove_NekiKorisniciBezStampa_AzuriraSamoNjih(string[] ids, string[] stampovi,
        int ocekivaniBroj, string ocekivanaPoruka)
    {
        // Arrange
        var korisnici = ids.Select((id, i) => new Korisnik
        {
            Id = id, SecurityStamp = KorisnikControllerPodaci.Prazno(stampovi[i])
        }).ToList();
        _korisnikServis.Setup(s => s.GetAllAsync()).ReturnsAsync(korisnici);
        _userManager.Setup(u => u.UpdateSecurityStampAsync(It.IsAny<Korisnik>())).ReturnsAsync(IdentityResult.Success);

        // Act
        await _controller.PopraviStampove();

        // Assert
        _userManager.Verify(u => u.UpdateSecurityStampAsync(It.IsAny<Korisnik>()), Times.Exactly(ocekivaniBroj));
    }
}
