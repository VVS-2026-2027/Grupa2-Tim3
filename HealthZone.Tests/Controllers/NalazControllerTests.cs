using System.Security.Claims;
using System.Xml.Linq;
using HealthZone.Controllers;
using HealthZone.Models;
using HealthZone.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za NalazControllerTests ───────────
public static class NalazControllerPodaci
{
    // ── pomoćne metode ──

    // Pristup vrijednostima koje kontroler vraća (ViewData, ModelState, JSON).
    public static Korisnik Pacijent(Controller kontroler) => (Korisnik)kontroler.ViewData["Pacijent"]!;

    public static string GreskaPolja(Controller kontroler, string polje)
        => kontroler.ModelState[polje]!.Errors.First().ErrorMessage;

    public static object? PacijentIzJsona(JsonResult rezultat)
        => rezultat.Value!.GetType().GetProperty("pacijentId")!.GetValue(rezultat.Value);

    public static T? EnumIliNull<T>(string? s) where T : struct, Enum
        => string.IsNullOrEmpty(s) ? null : Enum.Parse<T>(s);

    public static int? IdIliNull(string? s) => string.IsNullOrEmpty(s) ? null : int.Parse(s);

    public static string[] Lista(string? s) => (s ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);

    public static Korisnik NapraviKorisnika(XElement x) => new()
    {
        Id = (string)x.Attribute("id")!,
        Ime = (string)x.Attribute("ime")!,
        Prezime = (string)x.Attribute("prezime")!,
        Specijalizacija = EnumIliNull<Specijalizacija>((string)x.Attribute("specijalizacija")!)
    };

    public static Termin NapraviTermin(XElement x, List<Korisnik> korisnici)
    {
        var usluga = (string)x.Attribute("usluga")!;
        var pacijentId = (string)x.Attribute("pacijent")!;
        return new Termin
        {
            TerminId = (int)x.Attribute("id")!,
            DoktorID = (string)x.Attribute("doktor")!,
            PacijentID = pacijentId,
            Pacijent = korisnici.FirstOrDefault(k => k.Id == pacijentId),
            Status = Enum.Parse<Status>((string)x.Attribute("status")!),
            Datum = DateTime.Today.AddDays((int)x.Attribute("pomakDana")!),
            Vrijeme = TimeOnly.Parse((string)x.Attribute("vrijeme")!),
            Usluga = string.IsNullOrEmpty(usluga) ? null : new MedicinskaUsluga { Naziv = usluga }
        };
    }

    public static Nalaz NapraviNalaz(XElement x, List<Termin> termini)
    {
        var termin = termini.FirstOrDefault(t => t.TerminId.ToString() == (string)x.Attribute("termin")!);
        return new Nalaz
        {
            NalazId = (int)x.Attribute("id")!,
            PacijentID = (string)x.Attribute("pacijent")!,
            TerminID = termin?.TerminId ?? default,
            Termin = termin
        };
    }

    public static void Prijavi(Controller kontroler, string korisnikId, string uloga)
    {
        kontroler.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, korisnikId), new Claim(ClaimTypes.Role, uloga) }, "Test"));
    }

    public static string[] VrijednostiListe(Controller kontroler, string kljuc)
        => ((SelectList)kontroler.ViewData[kljuc]!).Select(i => i.Value).ToArray();

    public static object? OdabranaVrijednost(Controller kontroler, string kljuc)
        => ((SelectList)kontroler.ViewData[kljuc]!).SelectedValue;

    // ── izvori podataka (CSV) ──

    public static IEnumerable<object[]> PristupDetaljima() =>
        CsvLoader.Ucitaj("NalazController_details.csv").Select(r => new object[] { r[0], r[1], r[2], r[3] });

    // editGet.csv: [3] = očekivani rezultat
    private static IEnumerable<string[]> EditGet() => CsvLoader.Ucitaj("NalazController_editGet.csv");
    public static IEnumerable<object[]> EditGetNePostoji() =>
        EditGet().Where(r => r[3] == nameof(NotFoundResult)).Select(r => new object[] { r[0], r[1], r[2] });
    public static IEnumerable<object[]> EditGetPostoji() =>
        EditGet().Where(r => r[3] == nameof(ViewResult)).Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> PristupBrisanju() =>
        CsvLoader.Ucitaj("NalazController_deleteGet.csv").Select(r => new object[] { r[0], r[1] });

    public static IEnumerable<object[]> PristupPrintu() =>
        CsvLoader.Ucitaj("NalazController_print.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> PadajuceListe() =>
        CsvLoader.Ucitaj("NalazController_padajuceListe.csv")
                 .Select(r => new object[] { r[0], r[1], Lista(r[2]), Lista(r[3]) });

    public static IEnumerable<object[]> NevalidanNalaz() =>
        CsvLoader.Ucitaj("NalazController_nevalidanModel.csv")
                 .Select(r => new object[] { r[0], r[1], int.Parse(r[2]), r[3], r[4], r[5] });

    public static IEnumerable<object[]> NoviNalazi() =>
        CsvLoader.Ucitaj("NalazController_noviNalaz.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], int.Parse(r[3]), r[4] });

    public static IEnumerable<object[]> GreskeServisa() =>
        CsvLoader.Ucitaj("NalazController_greskaServisa.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1], r[2], r[3], r[4] });

    public static IEnumerable<object[]> RazlicitId() =>
        CsvLoader.Ucitaj("NalazController_razlicitId.csv").Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]) });

    public static IEnumerable<object[]> NevalidanEdit() =>
        CsvLoader.Ucitaj("NalazController_nevalidanEdit.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> IzmjeneNalaza() =>
        CsvLoader.Ucitaj("NalazController_izmjena.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1], r[2], r[3] });

    private static IEnumerable<string[]> Konkurentnost() => CsvLoader.Ucitaj("NalazController_konkurentnost.csv");
    public static IEnumerable<object[]> KonkurentnostObrisan() =>
        Konkurentnost().Where(r => !bool.Parse(r[1])).Select(r => new object[] { int.Parse(r[0]), r[2] });
    public static IEnumerable<object[]> KonkurentnostPostoji() =>
        Konkurentnost().Where(r => bool.Parse(r[1])).Select(r => new object[] { int.Parse(r[0]), r[2] });

    // brisanje.csv: [3] = očekivani rezultat, [4] = da li se nalaz briše
    private static IEnumerable<string[]> Brisanje() => CsvLoader.Ucitaj("NalazController_brisanje.csv");
    public static IEnumerable<object[]> BrisanjeDozvoljeno() =>
        Brisanje().Where(r => bool.Parse(r[4])).Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });
    public static IEnumerable<object[]> BrisanjeTudjegNalaza() =>
        Brisanje().Where(r => r[3] == nameof(ForbidResult)).Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });
    public static IEnumerable<object[]> BrisanjeNepostojecegNalaza() =>
        Brisanje().Where(r => r[3] == nameof(RedirectToActionResult) && !bool.Parse(r[4]))
                  .Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });
    public static IEnumerable<object[]> BrisanjeBezEfekta() =>
        Brisanje().Where(r => !bool.Parse(r[4])).Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> NepostojeciPacijenti() =>
        CsvLoader.Ucitaj("NalazController_nepostojeciPacijent.csv").Select(r => new object[] { r[0], r[1] });

    public static IEnumerable<object[]> KartoniPacijenata() =>
        CsvLoader.Ucitaj("NalazController_karton.csv").Select(r => new object[] { r[0], int.Parse(r[1]) });

    public static IEnumerable<object[]> PacijentiTermina() =>
        CsvLoader.Ucitaj("NalazController_pacijentTermina.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> NepostojeciTermini() =>
        CsvLoader.Ucitaj("NalazController_nepostojeciTermin.csv").Select(r => new object[] { int.Parse(r[0]) });

    // ── izvori podataka (XML) ──

    public static IEnumerable<object[]> IndexPoUlozi() =>
        XmlLoader.Ucitaj("NalazController_index.xml", "scenarij").Select(x => new object[]
        {
            (string)x.Element("korisnikId")!,
            (string)x.Element("uloga")!,
            Lista((string?)x.Element("ocekivaniRedoslijed"))
        });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class NalazControllerTests
{
    private static XElement _baza = null!;   // samo za čitanje

    private Mock<INalazService> _nalazServis = null!;
    private Mock<IKorisnikService> _korisnikServis = null!;
    private Mock<ITerminService> _terminServis = null!;
    private NalazController _controller = null!;
    private List<Korisnik> _korisnici = null!;
    private List<Termin> _termini = null!;
    private List<Nalaz> _nalazi = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // XML "baza" se čita jednom; objekti se iz nje prave ispočetka za svaki test.
        _baza = XmlLoader.Ucitaj("NalazController_baza.xml", "baza").Single();
    }

    [TestInitialize]
    public void Setup()
    {
        _korisnici = _baza.Descendants("korisnik").Select(NalazControllerPodaci.NapraviKorisnika).ToList();
        _termini = _baza.Descendants("termin").Select(x => NalazControllerPodaci.NapraviTermin(x, _korisnici)).ToList();
        _nalazi = _baza.Descendants("nalaz").Select(x => NalazControllerPodaci.NapraviNalaz(x, _termini)).ToList();

        _nalazServis = new Mock<INalazService>();
        _nalazServis.Setup(s => s.GetAllAsync()).ReturnsAsync(_nalazi);
        _nalazServis.Setup(s => s.GetByIdAsync(It.IsAny<int>()))
                    .ReturnsAsync((int id) => _nalazi.FirstOrDefault(n => n.NalazId == id));
        _nalazServis.Setup(s => s.GetNalaziPacijentaAsync(It.IsAny<string>()))
                    .ReturnsAsync((string id) => _nalazi.Where(n => n.PacijentID == id).ToList());

        _korisnikServis = new Mock<IKorisnikService>();
        _korisnikServis.Setup(s => s.GetAllAsync()).ReturnsAsync(_korisnici);
        _korisnikServis.Setup(s => s.GetByIdAsync(It.IsAny<string>()))
                       .ReturnsAsync((string id) => _korisnici.FirstOrDefault(k => k.Id == id));

        _terminServis = new Mock<ITerminService>();
        _terminServis.Setup(s => s.GetAllAsync()).ReturnsAsync(_termini);
        _terminServis.Setup(s => s.GetByIdAsync(It.IsAny<int>()))
                     .ReturnsAsync((int id) => _termini.FirstOrDefault(t => t.TerminId == id));

        _controller = new NalazController(_nalazServis.Object, _korisnikServis.Object, _terminServis.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        _controller.Dispose();
        _nalazServis.Reset();
        _korisnikServis.Reset();
        _terminServis.Reset();
    }

    // ─── INDEX ────────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.IndexPoUlozi), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Index_PoUlozi_VracaDozvoljeneNalazeOdNajnovijeg(string korisnikId, string uloga,
        string[] ocekivaniRedoslijed)
    {
        // Arrange
        NalazControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.Index() as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        CollectionAssert.AreEqual(ocekivaniRedoslijed,
            ((IEnumerable<Nalaz>)rezultat.Model!).Select(n => n.NalazId.ToString()).ToArray());
    }

    // ─── DETAILS / EDIT (GET) / DELETE (GET) / PRINT ──────────────────────

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.PristupDetaljima), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Details_RazliciteUlogeIId_VracaOcekivaniRezultat(string id, string korisnikId, string uloga,
        string ocekivaniRezultat)
    {
        // Arrange
        NalazControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.Details(NalazControllerPodaci.IdIliNull(id));

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.EditGetNePostoji), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Get_IdPrazanIliNepostojeci_VracaNotFound(string id, string korisnikId, string uloga)
    {
        // Arrange
        NalazControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.Edit(NalazControllerPodaci.IdIliNull(id));

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.EditGetPostoji), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Get_NalazPostoji_OznacavaTerminNalaza(int id, string korisnikId, string uloga)
    {
        // Arrange
        NalazControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.Edit(id) as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(((Nalaz)rezultat.Model!).TerminID,
            NalazControllerPodaci.OdabranaVrijednost(_controller, nameof(Nalaz.TerminID)));
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.EditGetPostoji), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Get_NalazPostoji_OznacavaPacijentaNalaza(int id, string korisnikId, string uloga)
    {
        // Arrange
        NalazControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.Edit(id) as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(((Nalaz)rezultat.Model!).PacijentID,
            NalazControllerPodaci.OdabranaVrijednost(_controller, nameof(Nalaz.PacijentID)));
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.PristupBrisanju), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Delete_Get_RazlicitiId_VracaOcekivaniRezultat(string id, string ocekivaniRezultat)
    {
        // Act
        var rezultat = await _controller.Delete(NalazControllerPodaci.IdIliNull(id));

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.PristupPrintu), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Print_RazlicitiId_VracaOcekivaniRezultat(int id, string ocekivaniRezultat)
    {
        // Act
        var rezultat = await _controller.Print(id);

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    // ─── CREATE (GET) ─────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.PadajuceListe), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Create_Get_PoUlozi_NudiSamoDozvoljeneAktivneTermine(string korisnikId, string uloga,
        string[] ocekivaniTermini, string[] ocekivaniPacijenti)
    {
        // Arrange
        NalazControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.Create();

        // Assert
        CollectionAssert.AreEqual(ocekivaniTermini,
            NalazControllerPodaci.VrijednostiListe(_controller, nameof(Nalaz.TerminID)));
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.PadajuceListe), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Get_PoUlozi_NudiSamoDozvoljenePacijente(string korisnikId, string uloga,
        string[] ocekivaniTermini, string[] ocekivaniPacijenti)
    {
        // Arrange
        NalazControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.Create();

        // Assert
        CollectionAssert.AreEqual(ocekivaniPacijenti,
            NalazControllerPodaci.VrijednostiListe(_controller, nameof(Nalaz.PacijentID)));
    }

    // ─── CREATE (POST) ────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.NevalidanNalaz), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_NevalidanModel_VracaIstiView(string kljucGreske, string porukaGreske,
        int terminId, string pacijentId, string korisnikId, string uloga)
    {
        // Arrange
        NalazControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        var nalaz = new Nalaz { TerminID = terminId, PacijentID = pacijentId };
        _controller.ModelState.AddModelError(kljucGreske, porukaGreske);

        // Act
        var rezultat = await _controller.Create(nalaz) as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreSame(nalaz, rezultat.Model);
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.NevalidanNalaz), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_NevalidanModel_ZadrzavaOdabraniTermin(string kljucGreske, string porukaGreske,
        int terminId, string pacijentId, string korisnikId, string uloga)
    {
        // Arrange
        NalazControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        _controller.ModelState.AddModelError(kljucGreske, porukaGreske);

        // Act
        await _controller.Create(new Nalaz { TerminID = terminId, PacijentID = pacijentId });

        // Assert
        Assert.AreEqual(terminId, NalazControllerPodaci.OdabranaVrijednost(_controller, nameof(Nalaz.TerminID)));
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.NevalidanNalaz), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_NevalidanModel_ZadrzavaOdabranogPacijenta(string kljucGreske, string porukaGreske,
        int terminId, string pacijentId, string korisnikId, string uloga)
    {
        // Arrange
        NalazControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        _controller.ModelState.AddModelError(kljucGreske, porukaGreske);

        // Act
        await _controller.Create(new Nalaz { TerminID = terminId, PacijentID = pacijentId });

        // Assert
        Assert.AreEqual(pacijentId, NalazControllerPodaci.OdabranaVrijednost(_controller, nameof(Nalaz.PacijentID)));
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.NevalidanNalaz), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_NevalidanModel_NeSnimaNalaz(string kljucGreske, string porukaGreske,
        int terminId, string pacijentId, string korisnikId, string uloga)
    {
        // Arrange
        NalazControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        _controller.ModelState.AddModelError(kljucGreske, porukaGreske);

        // Act
        await _controller.Create(new Nalaz { TerminID = terminId, PacijentID = pacijentId });

        // Assert
        _nalazServis.Verify(s => s.AddAsync(It.IsAny<Nalaz>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.NoviNalazi), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_ValidanNalaz_PreusmjeravaNaIndex(string opis, string dijagnoza, string terapija,
        int terminId, string pacijentId)
    {
        // Arrange
        var nalaz = new Nalaz
        {
            Opis = opis, Dijagnoza = dijagnoza, Terapija = terapija, TerminID = terminId, PacijentID = pacijentId
        };

        // Act
        var rezultat = await _controller.Create(nalaz) as RedirectToActionResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(nameof(NalazController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.NoviNalazi), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Create_Post_ValidanNalaz_SnimaKrozServis(string opis, string dijagnoza, string terapija,
        int terminId, string pacijentId)
    {
        // Arrange
        var nalaz = new Nalaz
        {
            Opis = opis, Dijagnoza = dijagnoza, Terapija = terapija, TerminID = terminId, PacijentID = pacijentId
        };

        // Act
        await _controller.Create(nalaz);

        // Assert
        _nalazServis.Verify(s => s.AddAsync(nalaz), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.GreskeServisa), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_ServisOdbijeNalaz_PrikazujeGreskuKodTermina(int terminId, string pacijentId,
        string korisnikId, string uloga, string poruka)
    {
        // Arrange
        NalazControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        var nalaz = new Nalaz { TerminID = terminId, PacijentID = pacijentId };
        _nalazServis.Setup(s => s.AddAsync(nalaz)).ThrowsAsync(new Exception(poruka));

        // Act
        await _controller.Create(nalaz);

        // Assert
        Assert.AreEqual(poruka, NalazControllerPodaci.GreskaPolja(_controller, nameof(Nalaz.TerminID)));
    }

    // ─── EDIT (POST) ──────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.RazlicitId), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_IdRuteRazlicitOdNalaza_VracaNotFound(int idRute, int idNalaza)
    {
        // Act
        var rezultat = await _controller.Edit(idRute, new Nalaz { NalazId = idNalaza });

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.NevalidanEdit), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_NevalidanModel_VracaIstiView(int id, string kljucGreske, string porukaGreske)
    {
        // Arrange
        var nalaz = new Nalaz { NalazId = id };
        _controller.ModelState.AddModelError(kljucGreske, porukaGreske);

        // Act
        var rezultat = await _controller.Edit(id, nalaz) as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreSame(nalaz, rezultat.Model);
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.NevalidanEdit), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_NevalidanModel_NeAzuriraNalaz(int id, string kljucGreske, string porukaGreske)
    {
        // Arrange
        _controller.ModelState.AddModelError(kljucGreske, porukaGreske);

        // Act
        await _controller.Edit(id, new Nalaz { NalazId = id });

        // Assert
        _nalazServis.Verify(s => s.Update(It.IsAny<Nalaz>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.IzmjeneNalaza), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_ValidanNalaz_PreusmjeravaNaIndex(int id, string opis, string dijagnoza, string terapija)
    {
        // Arrange
        var nalaz = new Nalaz { NalazId = id, Opis = opis, Dijagnoza = dijagnoza, Terapija = terapija };

        // Act
        var rezultat = await _controller.Edit(id, nalaz) as RedirectToActionResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(nameof(NalazController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.IzmjeneNalaza), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Edit_Post_ValidanNalaz_AzuriraNalaz(int id, string opis, string dijagnoza, string terapija)
    {
        // Arrange
        var nalaz = new Nalaz { NalazId = id, Opis = opis, Dijagnoza = dijagnoza, Terapija = terapija };

        // Act
        await _controller.Edit(id, nalaz);

        // Assert
        _nalazServis.Verify(s => s.Update(nalaz), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.IzmjeneNalaza), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_ValidanNalaz_SnimaPromjene(int id, string opis, string dijagnoza, string terapija)
    {
        // Arrange
        var nalaz = new Nalaz { NalazId = id, Opis = opis, Dijagnoza = dijagnoza, Terapija = terapija };

        // Act
        await _controller.Edit(id, nalaz);

        // Assert
        _nalazServis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.KonkurentnostObrisan), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_NalazObrisanUMeduvremenu_VracaNotFound(int id, string poruka)
    {
        // Arrange
        _nalazServis.Setup(s => s.SaveChangesAsync()).ThrowsAsync(new DbUpdateConcurrencyException(poruka));
        _nalazServis.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((Nalaz?)null);

        // Act
        var rezultat = await _controller.Edit(id, new Nalaz { NalazId = id });

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.KonkurentnostPostoji), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_KonfliktANalazPostoji_PonovoBacaIzuzetak(int id, string poruka)
    {
        // Arrange
        _nalazServis.Setup(s => s.SaveChangesAsync()).ThrowsAsync(new DbUpdateConcurrencyException(poruka));

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(
            () => _controller.Edit(id, new Nalaz { NalazId = id }));

        // Assert
        Assert.AreEqual(poruka, izuzetak.Message);
    }

    // ─── DELETE (POST) ────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.BrisanjeDozvoljeno), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_DozvoljenoBrisanje_PreusmjeravaNaIndex(int id, string korisnikId, string uloga)
    {
        // Arrange
        NalazControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.DeleteConfirmed(id) as RedirectToActionResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(nameof(NalazController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.BrisanjeDozvoljeno), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task DeleteConfirmed_DozvoljenoBrisanje_BriseNalaz(int id, string korisnikId, string uloga)
    {
        // Arrange
        NalazControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _nalazServis.Verify(s => s.Delete(It.Is<Nalaz>(n => n.NalazId == id)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.BrisanjeDozvoljeno), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_DozvoljenoBrisanje_SnimaPromjene(int id, string korisnikId, string uloga)
    {
        // Arrange
        NalazControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _nalazServis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.BrisanjeTudjegNalaza), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task DeleteConfirmed_DoktorBriseTudjiNalaz_VracaForbid(int id, string korisnikId, string uloga)
    {
        // Arrange
        NalazControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.DeleteConfirmed(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(ForbidResult));
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.BrisanjeNepostojecegNalaza), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_NalazNePostoji_PreusmjeravaNaIndex(int id, string korisnikId, string uloga)
    {
        // Arrange
        NalazControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.DeleteConfirmed(id) as RedirectToActionResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(nameof(NalazController.Index), rezultat.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.BrisanjeBezEfekta), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteConfirmed_TudjiIliNepostojeciNalaz_NeBriseNista(int id, string korisnikId, string uloga)
    {
        // Arrange
        NalazControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _nalazServis.Verify(s => s.Delete(It.IsAny<Nalaz>()), Times.Never);
    }

    // ─── MEDICINSKI KARTON ────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.NepostojeciPacijenti), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task MedicinskiKarton_PrazanIliNepostojeciPacijent_VracaNotFound(string id, string opis)
    {
        // Act
        var rezultat = await _controller.MedicinskiKarton(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult), opis);
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.KartoniPacijenata), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task MedicinskiKarton_PacijentPostoji_VracaSveNjegoveNalaze(string id, int ocekivaniBroj)
    {
        // Act
        var rezultat = await _controller.MedicinskiKarton(id) as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivaniBroj, ((IEnumerable<Nalaz>)rezultat.Model!).Count());
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.KartoniPacijenata), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task MedicinskiKarton_PacijentPostoji_PrikazujePacijenta(string id, int ocekivaniBroj)
    {
        // Act
        await _controller.MedicinskiKarton(id);

        // Assert
        Assert.AreEqual(id, NalazControllerPodaci.Pacijent(_controller).Id);
    }

    // ─── AJAX: PACIJENT ZA TERMIN ─────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.PacijentiTermina), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetPacijentZaTermin_TerminPostoji_VracaJsonSaPacijentom(int terminId, string ocekivaniPacijent)
    {
        // Act
        var rezultat = await _controller.GetPacijentZaTermin(terminId) as JsonResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivaniPacijent, NalazControllerPodaci.PacijentIzJsona(rezultat));
    }

    [TestMethod]
    [DynamicData(nameof(NalazControllerPodaci.NepostojeciTermini), typeof(NalazControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetPacijentZaTermin_TerminNePostoji_VracaNotFound(int terminId)
    {
        // Act
        var rezultat = await _controller.GetPacijentZaTermin(terminId);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }
}
