using HealthZone.Controllers;
using HealthZone.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
// I Mvc i Identity imaju klasu SignInResult, pa se ova uvijek piše ovim imenom.
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace HealthZone.Tests;

[TestClass]
public class AccountControllerTests
{
    private Mock<UserManager<Korisnik>> _userManager = null!;
    private Mock<SignInManager<Korisnik>> _signInManager = null!;
    private AccountController _controller = null!;

    [TestInitialize]
    public void Setup()
    {
        // UserManager i SignInManager su klase, pa im trebaju posebni konstruktori za Moq.
        _userManager = new Mock<UserManager<Korisnik>>(Mock.Of<IUserStore<Korisnik>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);
        _signInManager = new Mock<SignInManager<Korisnik>>(_userManager.Object,
            Mock.Of<IHttpContextAccessor>(), Mock.Of<IUserClaimsPrincipalFactory<Korisnik>>(),
            null!, null!, null!, null!);

        _controller = new AccountController(_signInManager.Object, _userManager.Object);
    }

    // ───────────── Pomoćne metode ─────────────

    // Ispravni podaci za registraciju (iz CSV-a), za testove kojima su potrebni, a ne testiraju ih.
    private static string[] ValidniPodaci() =>
        CsvLoader.Ucitaj("AccountController_validniPodaci.csv").Single();

    private void PrijavaUspijeva(string email, string lozinka) =>
        _signInManager.Setup(s => s.PasswordSignInAsync(email, lozinka, false, false))
                      .ReturnsAsync(IdentitySignInResult.Success);

    // Identity vraća greške kad CreateAsync ne uspije.
    private void RegistracijaVracaGreske(params IdentityError[] greske) =>
        _userManager.Setup(u => u.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()))
                    .ReturnsAsync(IdentityResult.Failed(greske));

    // Poziva Register s ispravnim podacima iz CSV-a (lozinka i potvrda su iste).
    private Task<IActionResult> RegistrujValidnogKorisnika()
    {
        var p = ValidniPodaci();
        return _controller.Register(p[0], p[1], p[2], p[3], p[4], p[4]);
    }

    // ───────────── Izvori podataka (CSV i XML) ─────────────

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

    // ───────────── Login (GET), AccessDenied, Register (GET) ─────────────

    [TestMethod]
    [DynamicData(nameof(ReturnUrl), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void Login_Get_PamtiReturnUrlUViewData(string returnUrl)
    {
        // Act
        var rezultat = (ViewResult)_controller.Login(returnUrl);

        // Assert
        Assert.AreEqual(returnUrl, rezultat.ViewData["ReturnUrl"]);
    }

    [TestMethod]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void AccessDenied_Poziv_VracaView()
    {
        // Act
        var rezultat = _controller.AccessDenied();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    [TestMethod]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public void Register_Get_VracaView()
    {
        // Act
        var rezultat = _controller.Register();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
    }

    // ───────────── Login (POST) ─────────────

    [TestMethod]
    [DynamicData(nameof(PraznaPrijava), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task Login_PrazanEmailIliLozinka_PrikazujeGreskuINePrijavljuje(string email, string lozinka, string ocekivanaPoruka)
    {
        // Act
        var rezultat = (ViewResult)await _controller.Login(email, lozinka);

        // Assert
        Assert.AreEqual(ocekivanaPoruka, rezultat.ViewData["Greška"]);
        _signInManager.Verify(s => s.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(PogresnaPrijava), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
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
        Assert.AreEqual(ocekivanaPoruka, rezultat.ViewData["Greška"]);
        _signInManager.Verify(s => s.PasswordSignInAsync(email, lozinka, false, false), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(LokalniReturnUrl), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public async Task Login_UspjesnaPrijavaSLokalnimReturnUrl_VracaNaTajUrl(string email, string lozinka, string returnUrl)
    {
        // Arrange
        PrijavaUspijeva(email, lozinka);
        _controller.Url = Mock.Of<IUrlHelper>(u => u.IsLocalUrl(returnUrl) == true);

        // Act
        var rezultat = (RedirectResult)await _controller.Login(email, lozinka, false, returnUrl);

        // Assert
        Assert.AreEqual(returnUrl, rezultat.Url);
    }

    [TestMethod]
    [DynamicData(nameof(BezReturnUrl), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Login_UspjesnaPrijavaBezLokalnogReturnUrl_IdeNaPocetnu(
        string email, string lozinka, string returnUrl, bool jeLokalni, string ocekivanaAkcija, string ocekivaniKontroler)
    {
        // Arrange
        PrijavaUspijeva(email, lozinka);
        _controller.Url = Mock.Of<IUrlHelper>(u => u.IsLocalUrl(It.IsAny<string>()) == jeLokalni);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.Login(email, lozinka, false, returnUrl);

        // Assert
        Assert.AreEqual(ocekivanaAkcija, rezultat.ActionName);
        Assert.AreEqual(ocekivaniKontroler, rezultat.ControllerName);
    }

    // ───────────── Logout ─────────────

    [TestMethod]
    [DynamicData(nameof(Odjava), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Logout_PrijavljenKorisnik_OdjavljujeIVracaNaLogin(string ocekivanaAkcija, string ocekivaniKontroler)
    {
        // Act
        var rezultat = (RedirectToActionResult)await _controller.Logout();

        // Assert
        Assert.AreEqual(ocekivanaAkcija, rezultat.ActionName);
        Assert.AreEqual(ocekivaniKontroler, rezultat.ControllerName);
        _signInManager.Verify(s => s.SignOutAsync(), Times.Once);
    }

    // ───────────── Register (POST) ─────────────

    [TestMethod]
    [DynamicData(nameof(PraznaPolja), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_PraznoObaveznoPolje_PrikazujeGreskuINeKreira(string ime, string prezime, string email, string lozinka, string ocekivanaPoruka)
    {
        // Act
        var rezultat = (ViewResult)await _controller.Register(ime, prezime, email, string.Empty, lozinka, lozinka);

        // Assert
        Assert.AreEqual(ocekivanaPoruka, rezultat.ViewData["Greška"]);
        _userManager.Verify(u => u.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(RazliciteLozinke), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_LozinkeSeNePodudaraju_PrikazujeGresku(string lozinka, string potvrdiLozinku, string ocekivanaPoruka)
    {
        // Arrange
        var p = ValidniPodaci();

        // Act
        var rezultat = (ViewResult)await _controller.Register(p[0], p[1], p[2], p[3], lozinka, potvrdiLozinku);

        // Assert
        Assert.AreEqual(ocekivanaPoruka, rezultat.ViewData["Greška"]);
    }

    [TestMethod]
    [DynamicData(nameof(Registracija), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_IspravniPodaci_KreiraPacijentaIPrijavljuje(
        string ime, string prezime, string email, string lozinka, string ocekivanaUloga, string ocekivanaAkcija, string ocekivaniKontroler)
    {
        // Arrange
        _userManager.Setup(u => u.CreateAsync(It.IsAny<Korisnik>(), lozinka)).ReturnsAsync(IdentityResult.Success);

        // Act
        await _controller.Register(ime, prezime, email, string.Empty, lozinka, lozinka);

        // Assert
        _userManager.Verify(u => u.CreateAsync(It.Is<Korisnik>(k => k.Email == email && k.UserName == email && k.Ime == ime), lozinka), Times.Once);
        _userManager.Verify(u => u.AddToRoleAsync(It.Is<Korisnik>(k => k.Email == email), ocekivanaUloga), Times.Once);
        _signInManager.Verify(s => s.SignInAsync(It.Is<Korisnik>(k => k.Email == email), false, null), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(Registracija), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_IspravniPodaci_PreusmjeravaNaPocetnu(
        string ime, string prezime, string email, string lozinka, string ocekivanaUloga, string ocekivanaAkcija, string ocekivaniKontroler)
    {
        // Arrange
        _userManager.Setup(u => u.CreateAsync(It.IsAny<Korisnik>(), lozinka)).ReturnsAsync(IdentityResult.Success);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.Register(ime, prezime, email, string.Empty, lozinka, lozinka);

        // Assert
        Assert.AreEqual(ocekivanaAkcija, rezultat.ActionName);
        Assert.AreEqual(ocekivaniKontroler, rezultat.ControllerName);
    }

    [TestMethod]
    [DynamicData(nameof(Telefon), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_PrazanIliUnesenTelefon_SnimaTelefonIliNull(string email, string lozinka, string telefon, string? ocekivaniTelefon)
    {
        // Arrange
        var p = ValidniPodaci();
        _userManager.Setup(u => u.CreateAsync(It.IsAny<Korisnik>(), lozinka)).ReturnsAsync(IdentityResult.Success);

        // Act
        await _controller.Register(p[0], p[1], email, telefon, lozinka, lozinka);

        // Assert
        _userManager.Verify(u => u.CreateAsync(It.Is<Korisnik>(k => k.PhoneNumber == ocekivaniTelefon), lozinka), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(GreskeRegistracije), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_IdentityVratiGresku_PrikazujePrevedenuPoruku(string kodGreske, string opisGreske, string ocekivanaPoruka)
    {
        // Arrange
        RegistracijaVracaGreske(new IdentityError { Code = kodGreske, Description = opisGreske });

        // Act
        var rezultat = (ViewResult)await RegistrujValidnogKorisnika();

        // Assert
        Assert.AreEqual(ocekivanaPoruka, rezultat.ViewData["Greška"]);
    }

    [TestMethod]
    [DynamicData(nameof(GreskeRegistracije), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_IdentityVratiGresku_NeDodajeUloguINePrijavljuje(string kodGreske, string opisGreske, string ocekivanaPoruka)
    {
        // Arrange
        RegistracijaVracaGreske(new IdentityError { Code = kodGreske, Description = opisGreske });

        // Act
        await RegistrujValidnogKorisnika();

        // Assert
        _userManager.Verify(u => u.AddToRoleAsync(It.IsAny<Korisnik>(), It.IsAny<string>()), Times.Never);
        _signInManager.Verify(s => s.SignInAsync(It.IsAny<Korisnik>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(ViseGresaka), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Register_ViseGresaka_SpajaPorukeURazmakom(IdentityError[] greske, string ocekivanaPoruka)
    {
        // Arrange
        RegistracijaVracaGreske(greske);

        // Act
        var rezultat = (ViewResult)await RegistrujValidnogKorisnika();

        // Assert
        Assert.AreEqual(ocekivanaPoruka, rezultat.ViewData["Greška"]);
    }
}
