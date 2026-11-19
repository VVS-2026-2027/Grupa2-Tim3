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

[TestClass]
public class RecenzijaControllerTests
{
    private Mock<IRecenzijaService> _recenzijaServis = null!;
    private Mock<IKorisnikService> _korisnikServis = null!;
    private Mock<UserManager<Korisnik>> _userManager = null!;
    private RecenzijaController _controller = null!;
    private List<Recenzija> _recenzije = null!;
    private List<Korisnik> _doktori = null!;

    [TestInitialize]
    public void Setup()
    {
        // "Baza" recenzija i doktora se čita iz XML-a za svaki test ispočetka.
        _recenzije = XmlLoader.Ucitaj("RecenzijaController_recenzije.xml", "recenzija").Select(NapraviRecenziju).ToList();
        _doktori = XmlLoader.Ucitaj("RecenzijaController_doktori.xml", "doktor").Select(NapraviDoktora).ToList();

        _recenzijaServis = new Mock<IRecenzijaService>();
        _recenzijaServis.Setup(s => s.GetAllAsync()).ReturnsAsync(_recenzije);
        _recenzijaServis.Setup(s => s.GetByIdAsync(It.IsAny<int>()))
                        .ReturnsAsync((int id) => _recenzije.FirstOrDefault(r => r.RecenzijaId == id));
        _recenzijaServis.Setup(s => s.GetRecenzijeDoktoraAsync(It.IsAny<string>()))
                        .ReturnsAsync((string doktorId) => _recenzije.Where(r => r.DoktorID == doktorId).ToList());

        _korisnikServis = new Mock<IKorisnikService>();
        _korisnikServis.Setup(s => s.GetDoktoriAsync()).ReturnsAsync(_doktori);
        _korisnikServis.Setup(s => s.GetDoktorByIdAsync(It.IsAny<string>()))
                       .ReturnsAsync((string id) => _doktori.FirstOrDefault(d => d.Id == id));

        // UserManager je klasa, pa mu treba poseban konstruktor (isto kao u KorisnikServiceTests).
        _userManager = new Mock<UserManager<Korisnik>>(Mock.Of<IUserStore<Korisnik>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);

        _controller = new RecenzijaController(_recenzijaServis.Object, _korisnikServis.Object, _userManager.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };
    }

    // ───────────── Pomoćne metode ─────────────

    private static Recenzija NapraviRecenziju(XElement x) => new Recenzija
    {
        RecenzijaId = (int)x.Element("id")!,
        PacijentID = (string)x.Element("pacijentId")!,
        DoktorID = (string)x.Element("doktorId")!
    };

    private static Korisnik NapraviDoktora(XElement x) => new Korisnik
    {
        Id = (string)x.Element("id")!,
        Ime = (string)x.Element("ime")!,
        Prezime = (string)x.Element("prezime")!
    };

    // Prijavljuje korisnika s ulogom: User dobija claimove, a UserManager.GetUserAsync vraća tog korisnika.
    private void Prijavi(string korisnikId, string uloga)
    {
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, korisnikId), new Claim(ClaimTypes.Role, uloga) }, "Test"));
        _userManager.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync(new Korisnik { Id = korisnikId });
    }

    // ───────────── Izvori podataka (CSV) ─────────────

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

    // Naziv uloge pacijenta (koristi se u više testova), također iz CSV-a.
    private static string UlogaPacijenta() =>
        CsvLoader.Ucitaj("RecenzijaController_ulogaPacijenta.csv").Single()[0];

    // ───────────── Index ─────────────

    [TestMethod]
    [DynamicData(nameof(SveRecenzije), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Priority(1)]
    public async Task Index_PostojeRecenzije_VracaViewSaSvimRecenzijama(int brojRecenzija)
    {
        // Act
        var rezultat = (ViewResult)await _controller.Index();

        // Assert
        Assert.AreEqual(brojRecenzija, ((IEnumerable<Recenzija>)rezultat.Model!).Count());
        _recenzijaServis.Verify(s => s.GetAllAsync(), Times.Once);
    }

    // ───────────── RecenzijeDoktora ─────────────

    [TestMethod]
    [DynamicData(nameof(NepostojeciDoktor), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task RecenzijeDoktora_DoktorNePostoji_VracaNotFound(string doktorId)
    {
        // Act
        var rezultat = await _controller.RecenzijeDoktora(doktorId);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        _recenzijaServis.Verify(s => s.GetRecenzijeDoktoraAsync(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijeDoktora), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task RecenzijeDoktora_DoktorPostoji_VracaNjegoveRecenzijeIOcjene(string doktorId, int brojRecenzija)
    {
        // Act
        var rezultat = (ViewResult)await _controller.RecenzijeDoktora(doktorId);

        // Assert
        Assert.AreEqual(brojRecenzija, ((IEnumerable<Recenzija>)rezultat.Model!).Count());
        _recenzijaServis.Verify(s => s.GetProsjecneOcjeneAsync(doktorId), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(RecenzijeDoktora), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task RecenzijeDoktora_DoktorPostoji_UViewBagStavljaDoktora(string doktorId, int brojRecenzija)
    {
        // Act
        await _controller.RecenzijeDoktora(doktorId);

        // Assert
        Assert.AreEqual(doktorId, ((Korisnik)_controller.ViewBag.Doktor).Id);
    }

    [TestMethod]
    [DynamicData(nameof(Uloga), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task RecenzijeDoktora_RazliciteUloge_SamoPacijentMozeOstavitiRecenziju(string doktorId, string uloga, bool mozeOstaviti)
    {
        // Arrange
        Prijavi(doktorId, uloga);

        // Act
        await _controller.RecenzijeDoktora(doktorId);

        // Assert
        Assert.AreEqual(mozeOstaviti, (bool)_controller.ViewBag.MozeOstaviti);
    }

    // ───────────── MojeRecenzije ─────────────

    [TestMethod]
    [DynamicData(nameof(RecenzijeDoktora), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task MojeRecenzije_PrijavljenDoktor_VracaSamoNjegoveRecenzije(string doktorId, int brojRecenzija)
    {
        // Arrange
        Prijavi(doktorId, string.Empty);

        // Act
        var rezultat = (ViewResult)await _controller.MojeRecenzije();

        // Assert
        Assert.AreEqual(brojRecenzija, ((IEnumerable<Recenzija>)rezultat.Model!).Count());
        _recenzijaServis.Verify(s => s.GetProsjecneOcjeneAsync(doktorId), Times.Once);
    }

    // ───────────── Create ─────────────

    [TestMethod]
    [DynamicData(nameof(OdabraniDoktor), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_Get_PuniListuDoktoraSaOdabranim(string doktorId, int brojDoktora)
    {
        // Act
        await _controller.Create(doktorId);

        // Assert
        var lista = (SelectList)_controller.ViewData["DoktorID"]!;
        Assert.AreEqual(brojDoktora, lista.Count());
        Assert.AreEqual(doktorId, lista.SelectedValue);
    }

    [TestMethod]
    [DynamicData(nameof(NovaRecenzija), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    [Timeout(2000)]
    public async Task Create_ValidnaRecenzija_DodajeRecenzijuPrijavljenogPacijenta(string pacijentId, string doktorId, int ljubaznost, int profesionalnost, int usluga)
    {
        // Arrange
        Prijavi(pacijentId, UlogaPacijenta());
        var recenzija = new Recenzija
        {
            DoktorID = doktorId,
            OcjenaLjubaznosti = ljubaznost,
            OcjenaProfesionalnosti = profesionalnost,
            OcjenaUsluge = usluga
        };

        // Act
        var rezultat = (RedirectToActionResult)await _controller.Create(recenzija);

        // Assert
        Assert.AreEqual(nameof(RecenzijaController.Index), rezultat.ActionName);
        _recenzijaServis.Verify(s => s.AddAsync(It.Is<Recenzija>(r => r.PacijentID == pacijentId && r.DoktorID == doktorId)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NovaRecenzija), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_ValidnaRecenzija_PostavljaPorukuOUspjehu(string pacijentId, string doktorId, int ljubaznost, int profesionalnost, int usluga)
    {
        // Arrange
        Prijavi(pacijentId, UlogaPacijenta());
        var recenzija = new Recenzija
        {
            DoktorID = doktorId,
            OcjenaLjubaznosti = ljubaznost,
            OcjenaProfesionalnosti = profesionalnost,
            OcjenaUsluge = usluga
        };

        // Act
        await _controller.Create(recenzija);

        // Assert
        Assert.IsNotNull(_controller.TempData["Uspjeh"]);
    }

    [TestMethod]
    [DynamicData(nameof(GreskaServisa), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_ServisBaciIzuzetak_PorukaIdeUModelState(string pacijentId, string doktorId, string poruka)
    {
        // Arrange
        Prijavi(pacijentId, UlogaPacijenta());
        var recenzija = new Recenzija { DoktorID = doktorId };
        _recenzijaServis.Setup(s => s.AddAsync(recenzija)).ThrowsAsync(new Exception(poruka));

        // Act
        var rezultat = (ViewResult)await _controller.Create(recenzija);

        // Assert
        Assert.AreEqual(poruka, rezultat.ViewData.ModelState[string.Empty]!.Errors.Single().ErrorMessage);
    }

    [TestMethod]
    [DynamicData(nameof(NevalidanModel), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_NevalidanModel_VracaFormuINeDodaje(string pacijentId, string polje, string greska)
    {
        // Arrange
        Prijavi(pacijentId, UlogaPacijenta());
        var recenzija = new Recenzija();
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        var rezultat = (ViewResult)await _controller.Create(recenzija);

        // Assert
        Assert.AreSame(recenzija, rezultat.Model);
        _recenzijaServis.Verify(s => s.AddAsync(It.IsAny<Recenzija>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(IgnorisanoPolje), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task Create_GreskaNaPacijentId_IpakDodajeRecenziju(string pacijentId, string polje, string greska)
    {
        // Arrange
        Prijavi(pacijentId, UlogaPacijenta());
        var recenzija = new Recenzija();
        _controller.ModelState.AddModelError(polje, greska);

        // Act
        await _controller.Create(recenzija);

        // Assert
        _recenzijaServis.Verify(s => s.AddAsync(recenzija), Times.Once);
    }

    // ───────────── DeleteConfirmed ─────────────

    [TestMethod]
    [DynamicData(nameof(NepostojecaRecenzija), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_RecenzijaNePostoji_NistaNeBriseIPreusmjerava(int id, string korisnikId, string uloga)
    {
        // Arrange
        Prijavi(korisnikId, uloga);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(nameof(RecenzijaController.Index), rezultat.ActionName);
        _recenzijaServis.Verify(s => s.Delete(It.IsAny<Recenzija>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(TudjaRecenzija), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_PacijentBriseTudjuRecenziju_VracaForbid(int id, string pacijentId)
    {
        // Arrange
        Prijavi(pacijentId, UlogaPacijenta());

        // Act
        var rezultat = await _controller.DeleteConfirmed(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ForbidResult));
        _recenzijaServis.Verify(s => s.Delete(It.IsAny<Recenzija>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(VlastitaRecenzija), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_PacijentBriseSvojuRecenziju_BriseISnima(int id, string pacijentId)
    {
        // Arrange
        Prijavi(pacijentId, UlogaPacijenta());

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _recenzijaServis.Verify(s => s.Delete(It.Is<Recenzija>(r => r.RecenzijaId == id)), Times.Once);
        _recenzijaServis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(AdminBrisanje), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Fatih Crljenkovic")]
    public async Task DeleteConfirmed_AdministratorBriseBiloKojuRecenziju_BriseISnima(int id, string adminId, string uloga)
    {
        // Arrange
        Prijavi(adminId, uloga);

        // Act
        var rezultat = (RedirectToActionResult)await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(nameof(RecenzijaController.Index), rezultat.ActionName);
        _recenzijaServis.Verify(s => s.Delete(It.Is<Recenzija>(r => r.RecenzijaId == id)), Times.Once);
    }
}
