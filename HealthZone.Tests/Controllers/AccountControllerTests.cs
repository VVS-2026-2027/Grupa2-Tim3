using HealthZone.Controllers;
using HealthZone.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
// I Mvc i Identity imaju klasu SignInResult, pa se ova uvijek piše ovim imenom.
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za AccountControllerTests ─────────
public static class AccountControllerPodaci
{
    // ── pomoćne metode ──

    // UserManager i SignInManager su klase, pa im Moq treba posebne konstruktore.
    public static Mock<UserManager<Korisnik>> NapraviUserManager()
        => new(Mock.Of<IUserStore<Korisnik>>(), null!, null!, null!, null!, null!, null!, null!, null!);

    public static Mock<SignInManager<Korisnik>> NapraviSignInManager(Mock<UserManager<Korisnik>> userManager)
        => new(userManager.Object, Mock.Of<IHttpContextAccessor>(), Mock.Of<IUserClaimsPrincipalFactory<Korisnik>>(),
               null!, null!, null!, null!);

    // Ispravni podaci za registraciju (iz CSV-a), za testove kojima su potrebni, a ne testiraju ih.
    public static string[] ValidniPodaci() =>
        CsvLoader.Ucitaj("AccountController_validniPodaci.csv").Single();

    public static void PrijavaUspijeva(Mock<SignInManager<Korisnik>> signInManager, string email, string lozinka) =>
        signInManager.Setup(s => s.PasswordSignInAsync(email, lozinka, false, false))
                     .ReturnsAsync(IdentitySignInResult.Success);

    // Identity vraća greške kad CreateAsync ne uspije.
    public static void RegistracijaVracaGreske(Mock<UserManager<Korisnik>> userManager, params IdentityError[] greske) =>
        userManager.Setup(u => u.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()))
                   .ReturnsAsync(IdentityResult.Failed(greske));

    public static void RegistracijaUspijeva(Mock<UserManager<Korisnik>> userManager, string lozinka) =>
        userManager.Setup(u => u.CreateAsync(It.IsAny<Korisnik>(), lozinka)).ReturnsAsync(IdentityResult.Success);

    // Ispravni podaci za registraciju s imenovanim poljima, da test ne čita kolone po indeksu.
    public sealed record ValidniKorisnik(string Ime, string Prezime, string Email, string Telefon, string Lozinka);

    public static ValidniKorisnik ValidnaRegistracija()
    {
        var p = ValidniPodaci();
        return new ValidniKorisnik(p[0], p[1], p[2], p[3], p[4]);
    }

    // Vrijednosti koje kontroler stavlja u ViewData.
    public static object? Greska(ViewResult rezultat) => rezultat.ViewData["Greška"];

    public static object? ZapamceniReturnUrl(ViewResult rezultat) => rezultat.ViewData["ReturnUrl"];

    // Poziva Register s ispravnim podacima iz CSV-a (lozinka i potvrda su iste).
    public static Task<IActionResult> RegistrujValidnogKorisnika(AccountController controller)
    {
        var p = ValidniPodaci();
        return controller.Register(p[0], p[1], p[2], p[3], p[4], p[4]);
    }

    // ── izvori podataka (CSV) ──

    public static IEnumerable<object[]> Prikaz() =>
        CsvLoader.Ucitaj("AccountController_prikaz.csv").Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> ReturnUrl() =>
        CsvLoader.Ucitaj("AccountController_returnUrl.csv").Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> PraznaPrijava() =>
        CsvLoader.Ucitaj("AccountController_praznaPrijava.csv").Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> PogresnaPrijava() =>
        CsvLoader.Ucitaj("AccountController_pogresnaPrijava.csv").Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> LokalniReturnUrl() =>
        CsvLoader.Ucitaj("AccountController_lokalniReturnUrl.csv").Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> BezReturnUrl() =>
        CsvLoader.Ucitaj("AccountController_bezReturnUrl.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], bool.Parse(r[3]), r[4], r[5] });

    public static IEnumerable<object[]> Odjava() =>
        CsvLoader.Ucitaj("AccountController_odjava.csv").Select(r => new object[] { r[0], r[1] });

    public static IEnumerable<object[]> PraznaPolja() =>
        CsvLoader.Ucitaj("AccountController_praznaPolja.csv").Select(r => new object[] { r[0], r[1], r[2], r[3], r[4] });

    public static IEnumerable<object[]> RazliciteLozinke() =>
        CsvLoader.Ucitaj("AccountController_razliciteLozinke.csv").Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> Registracija() =>
        CsvLoader.Ucitaj("AccountController_registracija.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], r[3], r[4], r[5], r[6] });

    public static IEnumerable<object[]> GreskeRegistracije() =>
        CsvLoader.Ucitaj("AccountController_greskeRegistracije.csv").Select(r => new object[] { r[0], r[1], r[2] });

    // ── izvori podataka (XML) ──

    // Prazan telefon se snima kao null (element ocekivaniTelefon ne postoji).
    public static IEnumerable<object[]> Telefon() =>
        XmlLoader.Ucitaj("AccountController_telefon.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("email")!, (string)x.Element("lozinka")!,
                     (string)x.Element("telefon")!, (string?)x.Element("ocekivaniTelefon")!
                 });

    public static IEnumerable<object[]> ViseGresaka() =>
        XmlLoader.Ucitaj("AccountController_viseGresaka.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     x.Elements("greska")
                      .Select(g => new IdentityError { Code = (string)g.Element("kod")!, Description = (string)g.Element("opis")! })
                      .ToArray(),
                     (string)x.Element("ocekivanaPoruka")!
                 });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class AccountControllerTests
{
    private static string _folderPodataka = null!;

    private Mock<UserManager<Korisnik>> _userManager = null!;
    private Mock<SignInManager<Korisnik>> _signInManager = null!;
    private AccountController _controller = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // Jednom za cijelu klasu: provjera da su CSV i XML fajlovi kopirani uz testove.
        _folderPodataka = Path.Combine(AppContext.BaseDirectory, "TestData");
        Assert.IsTrue(Directory.Exists(_folderPodataka), "Folder TestData nije kopiran u output.");
    }

    [TestInitialize]
    public void Setup()
    {
        _userManager = AccountControllerPodaci.NapraviUserManager();
        _signInManager = AccountControllerPodaci.NapraviSignInManager(_userManager);
        _controller = new AccountController(_signInManager.Object, _userManager.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _controller.Dispose();
        _userManager.Reset();
        _signInManager.Reset();
    }

    // ─── Login (GET), AccessDenied, Register (GET) ────────────────────────

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.ReturnUrl), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void Login_Get_PamtiReturnUrlUViewData(string returnUrl)
    {
        // Act
        var rezultat = (ViewResult)_controller.Login(returnUrl);

        // Assert
        Assert.AreEqual(returnUrl, AccountControllerPodaci.ZapamceniReturnUrl(rezultat));
    }

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.Prikaz), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void AccessDenied_Poziv_VracaView(string ocekivaniRezultat)
    {
        // Act
        var rezultat = _controller.AccessDenied();

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.Prikaz), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void Register_Get_VracaView(string ocekivaniRezultat)
    {
        // Act
        var rezultat = _controller.Register();

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    // ─── Login (POST) ─────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.PraznaPrijava), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task Login_PrazanEmailIliLozinka_PrikazujeGresku(string email, string lozinka, string ocekivanaPoruka)
    {
        // Act
        var rezultat = (ViewResult)await _controller.Login(email, lozinka);

        // Assert
        Assert.AreEqual(ocekivanaPoruka, AccountControllerPodaci.Greska(rezultat));
    }

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.PraznaPrijava), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Login_PrazanEmailIliLozinka_NePokusavaPrijavu(string email, string lozinka, string ocekivanaPoruka)
    {
        // Act
        await _controller.Login(email, lozinka);

        // Assert
        _signInManager.Verify(s => s.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.PogresnaPrijava), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task Login_PogresniPodaci_PrikazujeGresku(string email, string lozinka, string ocekivanaPoruka)
    {
        // Arrange
        _signInManager.Setup(s => s.PasswordSignInAsync(email, lozinka, false, false))
                      .ReturnsAsync(IdentitySignInResult.Failed);

        // Act
        var rezultat = (ViewResult)await _controller.Login(email, lozinka);

        // Assert
        Assert.AreEqual(ocekivanaPoruka, AccountControllerPodaci.Greska(rezultat));
    }

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.PogresnaPrijava), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Login_PogresniPodaci_PokusavaPrijavuSUnesenimPodacima(string email, string lozinka, string ocekivanaPoruka)
    {
        // Arrange
        _signInManager.Setup(s => s.PasswordSignInAsync(email, lozinka, false, false))
                      .ReturnsAsync(IdentitySignInResult.Failed);

        // Act
        await _controller.Login(email, lozinka);

        // Assert
        _signInManager.Verify(s => s.PasswordSignInAsync(email, lozinka, false, false), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.LokalniReturnUrl), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public async Task Login_UspjesnaPrijavaSLokalnimReturnUrl_VracaNaTajUrl(string email, string lozinka, string returnUrl)
    {
        // Arrange
        AccountControllerPodaci.PrijavaUspijeva(_signInManager, email, lozinka);
        _controller.Url = Mock.Of<IUrlHelper>(u => u.IsLocalUrl(returnUrl) == true);

        // Act
        var rezultat = (RedirectResult)await _controller.Login(email, lozinka, false, returnUrl);

        // Assert
        Assert.AreEqual(returnUrl, rezultat.Url);
    }

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.BezReturnUrl), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Login_UspjesnaPrijavaBezLokalnogReturnUrl_IdeNaPocetnu(
        string email, string lozinka, string returnUrl, bool jeLokalni, string ocekivanaAkcija, string ocekivaniKontroler)
    {
        // Arrange
        AccountControllerPodaci.PrijavaUspijeva(_signInManager, email, lozinka);
        _controller.Url = Mock.Of<IUrlHelper>(u => u.IsLocalUrl(It.IsAny<string>()) == jeLokalni);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.Login(email, lozinka, false, returnUrl);

        // Assert
        Assert.AreEqual(ocekivanaAkcija, rezultat.ActionName);
        Assert.AreEqual(ocekivaniKontroler, rezultat.ControllerName);
    }

    // ─── Logout ───────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.Odjava), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Logout_PrijavljenKorisnik_VracaNaLogin(string ocekivanaAkcija, string ocekivaniKontroler)
    {
        // Act
        var rezultat = (RedirectToActionResult)await _controller.Logout();

        // Assert
        Assert.AreEqual(ocekivanaAkcija, rezultat.ActionName);
        Assert.AreEqual(ocekivaniKontroler, rezultat.ControllerName);
    }

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.Odjava), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Logout_PrijavljenKorisnik_OdjavljujeKorisnika(string ocekivanaAkcija, string ocekivaniKontroler)
    {
        // Act
        await _controller.Logout();

        // Assert
        _signInManager.Verify(s => s.SignOutAsync(), Times.Once);
    }

    // ─── Register (POST): neispravan unos ─────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.PraznaPolja), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_PraznoObaveznoPolje_PrikazujeGresku(string ime, string prezime, string email, string lozinka, string ocekivanaPoruka)
    {
        // Act
        var rezultat = (ViewResult)await _controller.Register(ime, prezime, email, string.Empty, lozinka, lozinka);

        // Assert
        Assert.AreEqual(ocekivanaPoruka, AccountControllerPodaci.Greska(rezultat));
    }

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.PraznaPolja), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_PraznoObaveznoPolje_NeKreiraKorisnika(string ime, string prezime, string email, string lozinka, string ocekivanaPoruka)
    {
        // Act
        await _controller.Register(ime, prezime, email, string.Empty, lozinka, lozinka);

        // Assert
        _userManager.Verify(u => u.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.RazliciteLozinke), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_LozinkeSeNePodudaraju_PrikazujeGresku(string lozinka, string potvrdiLozinku, string ocekivanaPoruka)
    {
        // Arrange
        var podaci = AccountControllerPodaci.ValidnaRegistracija();

        // Act
        var rezultat = (ViewResult)await _controller.Register(podaci.Ime, podaci.Prezime, podaci.Email, podaci.Telefon,
            lozinka, potvrdiLozinku);

        // Assert
        Assert.AreEqual(ocekivanaPoruka, AccountControllerPodaci.Greska(rezultat));
    }

    // ─── Register (POST): uspješna registracija ───────────────────────────

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.Registracija), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_IspravniPodaci_KreiraKorisnikaSEmailomKaoKorisnickimImenom(
        string ime, string prezime, string email, string lozinka, string ocekivanaUloga, string ocekivanaAkcija, string ocekivaniKontroler)
    {
        // Arrange
        AccountControllerPodaci.RegistracijaUspijeva(_userManager, lozinka);

        // Act
        await _controller.Register(ime, prezime, email, string.Empty, lozinka, lozinka);

        // Assert
        _userManager.Verify(u => u.CreateAsync(It.Is<Korisnik>(k => k.Email == email && k.UserName == email && k.Ime == ime), lozinka), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.Registracija), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_IspravniPodaci_DodajeUloguPacijent(
        string ime, string prezime, string email, string lozinka, string ocekivanaUloga, string ocekivanaAkcija, string ocekivaniKontroler)
    {
        // Arrange
        AccountControllerPodaci.RegistracijaUspijeva(_userManager, lozinka);

        // Act
        await _controller.Register(ime, prezime, email, string.Empty, lozinka, lozinka);

        // Assert
        _userManager.Verify(u => u.AddToRoleAsync(It.Is<Korisnik>(k => k.Email == email), ocekivanaUloga), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.Registracija), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_IspravniPodaci_PrijavljujeNovogKorisnika(
        string ime, string prezime, string email, string lozinka, string ocekivanaUloga, string ocekivanaAkcija, string ocekivaniKontroler)
    {
        // Arrange
        AccountControllerPodaci.RegistracijaUspijeva(_userManager, lozinka);

        // Act
        await _controller.Register(ime, prezime, email, string.Empty, lozinka, lozinka);

        // Assert
        _signInManager.Verify(s => s.SignInAsync(It.Is<Korisnik>(k => k.Email == email), false, null), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.Registracija), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_IspravniPodaci_PreusmjeravaNaPocetnu(
        string ime, string prezime, string email, string lozinka, string ocekivanaUloga, string ocekivanaAkcija, string ocekivaniKontroler)
    {
        // Arrange
        AccountControllerPodaci.RegistracijaUspijeva(_userManager, lozinka);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.Register(ime, prezime, email, string.Empty, lozinka, lozinka);

        // Assert
        Assert.AreEqual(ocekivanaAkcija, rezultat.ActionName);
        Assert.AreEqual(ocekivaniKontroler, rezultat.ControllerName);
    }

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.Telefon), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_PrazanIliUnesenTelefon_SnimaTelefonIliNull(string email, string lozinka, string telefon, string? ocekivaniTelefon)
    {
        // Arrange
        var podaci = AccountControllerPodaci.ValidnaRegistracija();
        AccountControllerPodaci.RegistracijaUspijeva(_userManager, lozinka);

        // Act
        await _controller.Register(podaci.Ime, podaci.Prezime, email, telefon, lozinka, lozinka);

        // Assert
        _userManager.Verify(u => u.CreateAsync(It.Is<Korisnik>(k => k.PhoneNumber == ocekivaniTelefon), lozinka), Times.Once);
    }

    // ─── Register (POST): greške iz Identity-ja ───────────────────────────

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.GreskeRegistracije), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_IdentityVratiGresku_PrikazujePrevedenuPoruku(string kodGreske, string opisGreske, string ocekivanaPoruka)
    {
        // Arrange
        AccountControllerPodaci.RegistracijaVracaGreske(_userManager, new IdentityError { Code = kodGreske, Description = opisGreske });

        // Act
        var rezultat = (ViewResult)await AccountControllerPodaci.RegistrujValidnogKorisnika(_controller);

        // Assert
        Assert.AreEqual(ocekivanaPoruka, AccountControllerPodaci.Greska(rezultat));
    }

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.GreskeRegistracije), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_IdentityVratiGresku_NeDodajeUlogu(string kodGreske, string opisGreske, string ocekivanaPoruka)
    {
        // Arrange
        AccountControllerPodaci.RegistracijaVracaGreske(_userManager, new IdentityError { Code = kodGreske, Description = opisGreske });

        // Act
        await AccountControllerPodaci.RegistrujValidnogKorisnika(_controller);

        // Assert
        _userManager.Verify(u => u.AddToRoleAsync(It.IsAny<Korisnik>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.GreskeRegistracije), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_IdentityVratiGresku_NePrijavljujeKorisnika(string kodGreske, string opisGreske, string ocekivanaPoruka)
    {
        // Arrange
        AccountControllerPodaci.RegistracijaVracaGreske(_userManager, new IdentityError { Code = kodGreske, Description = opisGreske });

        // Act
        await AccountControllerPodaci.RegistrujValidnogKorisnika(_controller);

        // Assert
        _signInManager.Verify(s => s.SignInAsync(It.IsAny<Korisnik>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(AccountControllerPodaci.ViseGresaka), typeof(AccountControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_ViseGresaka_SpajaPorukeURazmakom(IdentityError[] greske, string ocekivanaPoruka)
    {
        // Arrange
        AccountControllerPodaci.RegistracijaVracaGreske(_userManager, greske);

        // Act
        var rezultat = (ViewResult)await AccountControllerPodaci.RegistrujValidnogKorisnika(_controller);

        // Assert
        Assert.AreEqual(ocekivanaPoruka, AccountControllerPodaci.Greska(rezultat));
    }
}
