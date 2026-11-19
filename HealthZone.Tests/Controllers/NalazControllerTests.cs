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

[TestClass]
public class NalazControllerTests
{
    private Mock<INalazService> _nalazServis = null!;
    private Mock<IKorisnikService> _korisnikServis = null!;
    private Mock<ITerminService> _terminServis = null!;
    private NalazController _controller = null!;
    private List<Korisnik> _korisnici = null!;
    private List<Termin> _termini = null!;
    private List<Nalaz> _nalazi = null!;

    [TestInitialize]
    public void Setup()
    {
        // "Baza" korisnika, termina i nalaza se čita iz XML-a ispočetka za svaki test.
        var baza = XmlLoader.Ucitaj("NalazController_baza.xml", "baza").Single();
        _korisnici = baza.Descendants("korisnik").Select(NapraviKorisnika).ToList();
        _termini = baza.Descendants("termin").Select(NapraviTermin).ToList();
        _nalazi = baza.Descendants("nalaz").Select(NapraviNalaz).ToList();

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

    // ─── POMOĆNE METODE ───────────────────────────────────────────────────

    private static Korisnik NapraviKorisnika(XElement x) => new()
    {
        Id = (string)x.Attribute("id")!,
        Ime = (string)x.Attribute("ime")!,
        Prezime = (string)x.Attribute("prezime")!,
        Specijalizacija = EnumIliNull<Specijalizacija>((string)x.Attribute("specijalizacija")!)
    };

    private Termin NapraviTermin(XElement x)
    {
        var usluga = (string)x.Attribute("usluga")!;
        return new Termin
        {
            TerminId = (int)x.Attribute("id")!,
            DoktorID = (string)x.Attribute("doktor")!,
            PacijentID = (string)x.Attribute("pacijent")!,
            Pacijent = _korisnici.FirstOrDefault(k => k.Id == (string)x.Attribute("pacijent")!),
            Status = Enum.Parse<Status>((string)x.Attribute("status")!),
            Datum = DateTime.Today.AddDays((int)x.Attribute("pomakDana")!),
            Vrijeme = TimeOnly.Parse((string)x.Attribute("vrijeme")!),
            Usluga = string.IsNullOrEmpty(usluga) ? null : new MedicinskaUsluga { Naziv = usluga }
        };
    }

    private Nalaz NapraviNalaz(XElement x)
    {
        var termin = _termini.FirstOrDefault(t => t.TerminId.ToString() == (string)x.Attribute("termin")!);
        return new Nalaz
        {
            NalazId = (int)x.Attribute("id")!,
            PacijentID = (string)x.Attribute("pacijent")!,
            TerminID = termin?.TerminId ?? default,
            Termin = termin
        };
    }

    private static T? EnumIliNull<T>(string s) where T : struct, Enum
        => string.IsNullOrEmpty(s) ? null : Enum.Parse<T>(s);

    private static int? IdIliNull(string s) => string.IsNullOrEmpty(s) ? null : int.Parse(s);

    private static string[] Lista(string s) => s.Split(',', StringSplitOptions.RemoveEmptyEntries);

    private void Prijavi(string korisnikId, string uloga)
    {
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, korisnikId), new Claim(ClaimTypes.Role, uloga) }, "Test"));
    }

    private string[] VrijednostiListe(string kljuc)
        => ((SelectList)_controller.ViewData[kljuc]!).Select(i => i.Value).ToArray();

    // ─── IZVORI PODATAKA (CSV) ────────────────────────────────────────────

    public static IEnumerable<object[]> PristupDetaljima() =>
        CsvLoader.Ucitaj("NalazController_details.csv").Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> PristupEditu() =>
        CsvLoader.Ucitaj("NalazController_editGet.csv").Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> PristupBrisanju() =>
        CsvLoader.Ucitaj("NalazController_deleteGet.csv").Select(r => new object[] { r[0], r[1] });

    public static IEnumerable<object[]> PristupPrintu() =>
        CsvLoader.Ucitaj("NalazController_print.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> PadajuceListe() =>
        CsvLoader.Ucitaj("NalazController_padajuceListe.csv").Select(r => new object[]
        {
            r[0], r[1], Lista(r[2]), Lista(r[3])
        });

    public static IEnumerable<object[]> NevalidanNalaz() =>
        CsvLoader.Ucitaj("NalazController_nevalidanModel.csv").Select(r => new object[]
        {
            r[0], r[1], int.Parse(r[2]), r[3], r[4], r[5]
        });

    public static IEnumerable<object[]> NoviNalazi() =>
        CsvLoader.Ucitaj("NalazController_noviNalaz.csv").Select(r => new object[]
        {
            r[0], r[1], r[2], int.Parse(r[3]), r[4]
        });

    public static IEnumerable<object[]> GreskeServisa() =>
        CsvLoader.Ucitaj("NalazController_greskaServisa.csv").Select(r => new object[]
        {
            int.Parse(r[0]), r[1], r[2], r[3], r[4]
        });

    public static IEnumerable<object[]> RazlicitId() =>
        CsvLoader.Ucitaj("NalazController_razlicitId.csv").Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]) });

    public static IEnumerable<object[]> NevalidanEdit() =>
        CsvLoader.Ucitaj("NalazController_nevalidanEdit.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> IzmjeneNalaza() =>
        CsvLoader.Ucitaj("NalazController_izmjena.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2], r[3] });

    private static IEnumerable<string[]> Konkurentnost() => CsvLoader.Ucitaj("NalazController_konkurentnost.csv");

    public static IEnumerable<object[]> KonkurentnostObrisan() =>
        Konkurentnost().Where(r => !bool.Parse(r[1])).Select(r => new object[] { int.Parse(r[0]), r[2] });

    public static IEnumerable<object[]> KonkurentnostPostoji() =>
        Konkurentnost().Where(r => bool.Parse(r[1])).Select(r => new object[] { int.Parse(r[0]), r[2] });

    public static IEnumerable<object[]> Brisanje() =>
        CsvLoader.Ucitaj("NalazController_brisanje.csv").Select(r => new object[]
        {
            int.Parse(r[0]), r[1], r[2], r[3], bool.Parse(r[4])
        });

    public static IEnumerable<object[]> NepostojeciPacijenti() =>
        CsvLoader.Ucitaj("NalazController_nepostojeciPacijent.csv").Select(r => new object[] { r[0], r[1] });

    public static IEnumerable<object[]> KartoniPacijenata() =>
        CsvLoader.Ucitaj("NalazController_karton.csv").Select(r => new object[] { r[0], int.Parse(r[1]) });

    public static IEnumerable<object[]> PacijentiTermina() =>
        CsvLoader.Ucitaj("NalazController_pacijentTermina.csv").Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> NepostojeciTermini() =>
        CsvLoader.Ucitaj("NalazController_nepostojeciTermin.csv").Select(r => new object[] { int.Parse(r[0]) });

    // ─── IZVORI PODATAKA (XML) ────────────────────────────────────────────

    public static IEnumerable<object[]> IndexPoUlozi() =>
        XmlLoader.Ucitaj("NalazController_index.xml", "scenarij").Select(x => new object[]
        {
            (string)x.Element("korisnikId")!,
            (string)x.Element("uloga")!,
            Lista((string)x.Element("ocekivaniRedoslijed")!)
        });

    // ─── INDEX ────────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(IndexPoUlozi), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Index_PoUlozi_VracaDozvoljeneNalazeOdNajnovijeg(string korisnikId, string uloga,
        string[] ocekivaniRedoslijed)
    {
        // Arrange
        Prijavi(korisnikId, uloga);

        // Act
        var rezultat = await _controller.Index() as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        var nalazi = ((IEnumerable<Nalaz>)rezultat.Model!).Select(n => n.NalazId.ToString()).ToArray();
        CollectionAssert.AreEqual(ocekivaniRedoslijed, nalazi);
    }

    // ─── DETAILS / EDIT (GET) / DELETE (GET) / PRINT ──────────────────────

    [TestMethod]
    [DynamicData(nameof(PristupDetaljima), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Details_RazliciteUlogeIId_VracaOcekivaniRezultat(string id, string korisnikId, string uloga,
        string ocekivaniRezultat)
    {
        // Arrange
        Prijavi(korisnikId, uloga);

        // Act
        var rezultat = await _controller.Details(IdIliNull(id));

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    [TestMethod]
    [DynamicData(nameof(PristupEditu), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Get_RazlicitiId_VracaOcekivaniRezultat(string id, string korisnikId, string uloga,
        string ocekivaniRezultat)
    {
        // Arrange
        Prijavi(korisnikId, uloga);

        // Act
        var rezultat = await _controller.Edit(IdIliNull(id));

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
        if (rezultat is ViewResult view)
        {
            var nalaz = (Nalaz)view.Model!;
            Assert.AreEqual(nalaz.TerminID, ((SelectList)_controller.ViewData["TerminID"]!).SelectedValue);
            Assert.AreEqual(nalaz.PacijentID, ((SelectList)_controller.ViewData["PacijentID"]!).SelectedValue);
        }
    }

    [TestMethod]
    [DynamicData(nameof(PristupBrisanju), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Delete_Get_RazlicitiId_VracaOcekivaniRezultat(string id, string ocekivaniRezultat)
    {
        // Act
        var rezultat = await _controller.Delete(IdIliNull(id));

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    [TestMethod]
    [DynamicData(nameof(PristupPrintu), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Print_RazlicitiId_VracaOcekivaniRezultat(int id, string ocekivaniRezultat)
    {
        // Act
        var rezultat = await _controller.Print(id);

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    // ─── CREATE ───────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(PadajuceListe), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Create_Get_PoUlozi_NudiSamoAktivneTermineIPacijente(string korisnikId, string uloga,
        string[] ocekivaniTermini, string[] ocekivaniPacijenti)
    {
        // Arrange
        Prijavi(korisnikId, uloga);

        // Act
        var rezultat = await _controller.Create() as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        CollectionAssert.AreEqual(ocekivaniTermini, VrijednostiListe("TerminID"));
        CollectionAssert.AreEqual(ocekivaniPacijenti, VrijednostiListe("PacijentID"));
    }

    [TestMethod]
    [DynamicData(nameof(NevalidanNalaz), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_NevalidanModel_VracaViewSaOdabranimVrijednostima(string kljucGreske,
        string porukaGreske, int terminId, string pacijentId, string korisnikId, string uloga)
    {
        // Arrange
        Prijavi(korisnikId, uloga);
        var nalaz = new Nalaz { TerminID = terminId, PacijentID = pacijentId };
        _controller.ModelState.AddModelError(kljucGreske, porukaGreske);

        // Act
        var rezultat = await _controller.Create(nalaz) as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreSame(nalaz, rezultat.Model);
        Assert.AreEqual(terminId, ((SelectList)_controller.ViewData["TerminID"]!).SelectedValue);
        Assert.AreEqual(pacijentId, ((SelectList)_controller.ViewData["PacijentID"]!).SelectedValue);
        _nalazServis.Verify(s => s.AddAsync(It.IsAny<Nalaz>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(NoviNalazi), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Create_Post_ValidanNalaz_SnimaIPreusmjerava(string opis, string dijagnoza, string terapija,
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
        _nalazServis.Verify(s => s.AddAsync(nalaz), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(GreskeServisa), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Create_Post_ServisOdbijeNalaz_PrikazujeGreskuKodTermina(int terminId, string pacijentId,
        string korisnikId, string uloga, string poruka)
    {
        // Arrange
        Prijavi(korisnikId, uloga);
        var nalaz = new Nalaz { TerminID = terminId, PacijentID = pacijentId };
        _nalazServis.Setup(s => s.AddAsync(nalaz)).ThrowsAsync(new Exception(poruka));

        // Act
        var rezultat = await _controller.Create(nalaz) as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(poruka, _controller.ModelState[nameof(Nalaz.TerminID)]!.Errors[0].ErrorMessage);
    }

    // ─── EDIT (POST) ──────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(RazlicitId), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task Edit_Post_IdRuteRazlicitOdNalaza_VracaNotFound(int idRute, int idNalaza)
    {
        // Act
        var rezultat = await _controller.Edit(idRute, new Nalaz { NalazId = idNalaza });

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        _nalazServis.Verify(s => s.Update(It.IsAny<Nalaz>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(NevalidanEdit), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
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
        _nalazServis.Verify(s => s.Update(It.IsAny<Nalaz>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(IzmjeneNalaza), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task Edit_Post_ValidanNalaz_AzuriraISnima(int id, string opis, string dijagnoza, string terapija)
    {
        // Arrange
        var nalaz = new Nalaz { NalazId = id, Opis = opis, Dijagnoza = dijagnoza, Terapija = terapija };

        // Act
        var rezultat = await _controller.Edit(id, nalaz) as RedirectToActionResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(nameof(NalazController.Index), rezultat.ActionName);
        _nalazServis.Verify(s => s.Update(nalaz), Times.Once);
        _nalazServis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(KonkurentnostObrisan), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
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
    [DynamicData(nameof(KonkurentnostPostoji), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
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
    [DynamicData(nameof(Brisanje), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task DeleteConfirmed_PoUloziIVlasnistvu_BriseSamoDozvoljeno(int id, string korisnikId,
        string uloga, string ocekivaniRezultat, bool brise)
    {
        // Arrange
        Prijavi(korisnikId, uloga);

        // Act
        var rezultat = await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
        _nalazServis.Verify(s => s.Delete(It.Is<Nalaz>(n => n.NalazId == id)), brise ? Times.Once() : Times.Never());
        _nalazServis.Verify(s => s.SaveChangesAsync(), brise ? Times.Once() : Times.Never());
    }

    // ─── MEDICINSKI KARTON ────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(NepostojeciPacijenti), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task MedicinskiKarton_PrazanIliNepostojeciPacijent_VracaNotFound(string id, string opis)
    {
        // Act
        var rezultat = await _controller.MedicinskiKarton(id);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult), opis);
        _nalazServis.Verify(s => s.GetNalaziPacijentaAsync(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(KartoniPacijenata), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task MedicinskiKarton_PacijentPostoji_VracaSveNjegoveNalaze(string id, int ocekivaniBroj)
    {
        // Act
        var rezultat = await _controller.MedicinskiKarton(id) as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        Assert.AreEqual(ocekivaniBroj, ((IEnumerable<Nalaz>)rezultat.Model!).Count());
        Assert.AreEqual(id, ((Korisnik)_controller.ViewData["Pacijent"]!).Id);
    }

    // ─── AJAX: PACIJENT ZA TERMIN ─────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(PacijentiTermina), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetPacijentZaTermin_TerminPostoji_VracaJsonSaPacijentom(int terminId, string ocekivaniPacijent)
    {
        // Act
        var rezultat = await _controller.GetPacijentZaTermin(terminId) as JsonResult;

        // Assert
        Assert.IsNotNull(rezultat);
        var vrijednost = rezultat.Value!.GetType().GetProperty("pacijentId")!.GetValue(rezultat.Value);
        Assert.AreEqual(ocekivaniPacijent, vrijednost);
    }

    [TestMethod]
    [DynamicData(nameof(NepostojeciTermini), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetPacijentZaTermin_TerminNePostoji_VracaNotFound(int terminId)
    {
        // Act
        var rezultat = await _controller.GetPacijentZaTermin(terminId);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }
}
