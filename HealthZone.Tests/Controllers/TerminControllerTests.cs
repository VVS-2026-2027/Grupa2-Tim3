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

// ─── Izvori podataka i pomoćne metode za TerminControllerTests ──────────
public static class TerminControllerPodaci
{
    // ── pomoćne metode ──

    public static T? EnumIliNull<T>(string? s) where T : struct, Enum
        => string.IsNullOrEmpty(s) ? null : Enum.Parse<T>(s);

    public static int? IdIliNull(string? s) => string.IsNullOrEmpty(s) ? null : int.Parse(s);

    public static string[] Lista(string? s) => (s ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);

    public static Korisnik NapraviKorisnika(XElement x) => new()
    {
        Id = (string)x.Attribute("id")!,
        Ime = (string)x.Attribute("ime")!,
        Prezime = (string)x.Attribute("prezime")!,
        Prioritet = EnumIliNull<Prioritet>((string)x.Attribute("prioritet")!)
    };

    public static MedicinskaUsluga NapraviUslugu(XElement x) => new()
    {
        UslugaId = (int)x.Attribute("id")!,
        Naziv = (string)x.Attribute("naziv")!
    };

    public static Termin NapraviTermin(XElement x) => new()
    {
        TerminId = (int)x.Attribute("id")!,
        DoktorID = (string)x.Attribute("doktor")!,
        PacijentID = (string)x.Attribute("pacijent")!,
        UslugaID = (int)x.Attribute("usluga")!,
        Datum = DateTime.Today.AddDays((int)x.Attribute("pomakDana")!),
        Vrijeme = TimeOnly.Parse((string)x.Attribute("vrijeme")!),
        Status = Enum.Parse<Status>((string)x.Attribute("status")!)
    };

    // Termin kakav stiže iz forme (Create/Edit).
    public static Termin NoviTermin(int id, string doktorId, string pacijentId, int uslugaId, int pomakDana, int sat) => new()
    {
        TerminId = id,
        DoktorID = doktorId,
        PacijentID = pacijentId,
        UslugaID = uslugaId,
        Datum = DateTime.Today.AddDays(pomakDana),
        Vrijeme = new TimeOnly(sat, 0)
    };

    public static List<Korisnik> SaUlogom(XElement baza, string uloga) =>
        baza.Descendants("korisnik").Where(k => (string)k.Attribute("uloga")! == uloga).Select(NapraviKorisnika).ToList();

    public static Termin Pronadji(IEnumerable<Termin> termini, int id) => termini.Single(t => t.TerminId == id);

    public static Korisnik NadjiKorisnika(IEnumerable<Korisnik> korisnici, string id) => korisnici.Single(k => k.Id == id);

    public static Korisnik? PrijavljeniKorisnik(IEnumerable<Korisnik> korisnici, ClaimsPrincipal principal) =>
        korisnici.FirstOrDefault(k => k.Id == principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);

    public static void Prijavi(Controller kontroler, string korisnikId, string uloga)
    {
        kontroler.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, korisnikId), new Claim(ClaimTypes.Role, uloga) }, "Test"));
    }

    // Vrijednost svojstva anonimnog objekta iz JsonResult-a (npr. "zauzetiSlotovi").
    public static object? JsonVrijednost(IActionResult rezultat, string svojstvo)
    {
        var vrijednost = ((JsonResult)rezultat).Value!;
        return vrijednost.GetType().GetProperty(svojstvo)!.GetValue(vrijednost);
    }

    public static string[] JsonLista(IActionResult rezultat, string svojstvo) =>
        ((IEnumerable<string>)JsonVrijednost(rezultat, svojstvo)!).ToArray();

    public static string DatumTekst(int pomakDana) => DateTime.Today.AddDays(pomakDana).ToString("yyyy-MM-dd");

    public static string[] DatumiTekst(string pomaci) => Lista(pomaci).Select(p => DatumTekst(int.Parse(p))).ToArray();

    public static string KljucBrojaTermina(string pacijentId) => $"BrojTermina_{pacijentId}";

    public static string[] IdKorisnika(object? model) => ((IEnumerable<Korisnik>)model!).Select(k => k.Id).ToArray();

    public static string[] IdTermina(object? model) => ((IEnumerable<Termin>)model!).Select(t => t.TerminId.ToString()).ToArray();

    // ── izvori podataka (CSV) ──

    public static IEnumerable<object[]> IndexPoUlozi() =>
        CsvLoader.Ucitaj("TerminController_index.csv").Select(r => new object[] { r[0], r[1], Lista(r[2]) });

    private static IEnumerable<string[]> Pristup() => CsvLoader.Ucitaj("TerminController_pristup.csv");

    // Details, Edit (GET) i Delete (GET): očekivani rezultat je u koloni rezultatPrikaza.
    public static IEnumerable<object[]> PristupPrikazu() =>
        Pristup().Select(r => new object[] { r[0], r[1], r[2], r[3] });

    // Otkazi, Edit (POST) i DeleteConfirmed primaju int id, pa red bez id-ja ("-") ne ulazi.
    public static IEnumerable<object[]> PristupAkciji() =>
        Pristup().Where(r => r[4] != "-").Select(r => new object[] { int.Parse(r[0]), r[1], r[2], r[4] });

    public static IEnumerable<object[]> Detalji() =>
        CsvLoader.Ucitaj("TerminController_detalji.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2], r[3], r[4], r[5] });

    public static IEnumerable<object[]> PadajuceListe() =>
        CsvLoader.Ucitaj("TerminController_padajuceListe.csv")
                 .Select(r => new object[] { r[0], r[1], bool.Parse(r[2]), bool.Parse(r[3]) });

    public static IEnumerable<object[]> UlogovanDoktor() =>
        CsvLoader.Ucitaj("TerminController_ulogovanDoktor.csv").Select(r => new object[] { r[0], r[1], r[2] });

    private static IEnumerable<string[]> Novi() => CsvLoader.Ucitaj("TerminController_novi.csv");

    private static object[] NoviRed(string[] r) =>
        new object[] { r[0], r[1], r[2], r[3], int.Parse(r[4]), int.Parse(r[5]), int.Parse(r[6]) };

    public static IEnumerable<object[]> NoviTermini() => Novi().Select(NoviRed);

    public static IEnumerable<object[]> NoviTerminiStatus() =>
        Novi().Select(r => NoviRed(r).Append(Enum.Parse<Status>(r[7])).ToArray());

    public static IEnumerable<object[]> NoviTerminiPacijenta() => Novi().Where(r => r[1] == "Pacijent").Select(NoviRed);

    public static IEnumerable<object[]> NoviTerminiDoktora() => Novi().Where(r => r[1] == "Doktor").Select(NoviRed);

    public static IEnumerable<object[]> BezListe() =>
        CsvLoader.Ucitaj("TerminController_bezListe.csv").Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> Otkazivanje() =>
        CsvLoader.Ucitaj("TerminController_otkazivanje.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> Mail() =>
        CsvLoader.Ucitaj("TerminController_mail.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2] });

    public static IEnumerable<object[]> RazlicitId() =>
        CsvLoader.Ucitaj("TerminController_razlicitId.csv").Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]) });

    public static IEnumerable<object[]> Izmjene() =>
        CsvLoader.Ucitaj("TerminController_izmjena.csv").Select(r => new object[]
        {
            int.Parse(r[0]), r[1], r[2], r[3], r[4], int.Parse(r[5]), int.Parse(r[6]), int.Parse(r[7]),
            Enum.Parse<Status>(r[8]), Enum.Parse<Status>(r[9])
        });

    public static IEnumerable<object[]> Konkurentnost() =>
        CsvLoader.Ucitaj("TerminController_konkurentnost.csv").Select(r => new object[] { int.Parse(r[0]), r[1], r[2], r[3] });

    public static IEnumerable<object[]> ZauzetiSlotovi() =>
        CsvLoader.Ucitaj("TerminController_slotovi.csv").Select(r => new object[] { DatumTekst(int.Parse(r[0])), r[1], Lista(r[2]) });

    public static IEnumerable<object[]> NeispravanDatum() =>
        CsvLoader.Ucitaj("TerminController_neispravanDatum.csv").Select(r => new object[] { r[0], r[1] });

    public static IEnumerable<object[]> ZauzetiDani() =>
        CsvLoader.Ucitaj("TerminController_zauzetiDani.csv").Select(r => new object[] { r[0], DatumiTekst(r[1]) });

    public static IEnumerable<object[]> MjesecZauzet() =>
        CsvLoader.Ucitaj("TerminController_mjesecZauzet.csv").Select(r => new object[]
        {
            r[0], DateTime.Today.AddMonths(int.Parse(r[1])).Year, DateTime.Today.AddMonths(int.Parse(r[1])).Month, bool.Parse(r[2])
        });

    public static IEnumerable<object[]> PrioritetPristup() =>
        CsvLoader.Ucitaj("TerminController_prioritetPristup.csv").Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> PrioritetPrikaz() =>
        CsvLoader.Ucitaj("TerminController_prioritetPrikaz.csv").Select(r => new object[] { r[0], r[1], r[2], r[3], r[4] });

    public static IEnumerable<object[]> PrioritetIzmjena() =>
        CsvLoader.Ucitaj("TerminController_prioritetIzmjena.csv").Select(r => new object[] { r[0], r[1], r[2], r[3], r[4] });

    public static IEnumerable<object[]> PrioritetNovi() =>
        CsvLoader.Ucitaj("TerminController_prioritetNovi.csv").Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> PrioritetUklonjen() =>
        CsvLoader.Ucitaj("TerminController_prioritetUklonjen.csv").Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> PrioritetNeispravan() =>
        CsvLoader.Ucitaj("TerminController_prioritetNeispravan.csv").Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> MojiPacijenti() =>
        CsvLoader.Ucitaj("TerminController_mojiPacijenti.csv").Select(r => new object[] { r[0], r[1], Lista(r[2]) });

    public static IEnumerable<object[]> BrojTermina() =>
        CsvLoader.Ucitaj("TerminController_brojTermina.csv").Select(r => new object[] { r[0], r[1], r[2], int.Parse(r[3]) });

    public static IEnumerable<object[]> Neprijavljen() =>
        CsvLoader.Ucitaj("TerminController_neprijavljen.csv").Select(r => new object[] { r[0], r[1] });

    // ── izvori podataka (XML) ──

    public static IEnumerable<object[]> Greske() =>
        XmlLoader.Ucitaj("TerminController_greske.xml", "greska").Select(x => new object[]
        {
            (int)x.Element("terminId")!, (string)x.Element("korisnikId")!, (string)x.Element("uloga")!,
            (string)x.Element("doktorId")!, (int)x.Element("uslugaId")!, (int)x.Element("pomakDana")!,
            (int)x.Element("sat")!, (string)x.Element("poruka")!
        });

    public static IEnumerable<object[]> ListaCekanja() =>
        XmlLoader.Ucitaj("TerminController_listaCekanja.xml", "scenarij").Select(x => new object[]
        {
            (string)x.Element("korisnikId")!, (string)x.Element("uloga")!, (string)x.Element("doktorId")!,
            (int)x.Element("listaId")!
        });

    public static IEnumerable<object[]> AdminStatus() =>
        XmlLoader.Ucitaj("TerminController_adminStatus.xml", "scenarij").Select(x => new object[]
        {
            (string)x.Element("korisnikId")!, (string)x.Element("uloga")!, (string)x.Element("doktorId")!,
            (string)x.Element("pacijentId")!, (int)x.Element("uslugaId")!, (int)x.Element("pomakDana")!, (int)x.Element("sat")!,
            Enum.Parse<Status>((string)x.Element("status")!)
        });
}

// ─── Testovi ────────────────────────────────────────────────────────────
// Svi servisi kontrolera su zamijenjeni Moq objektima. "Stub" testovi provjeravaju rezultat akcije,
// a "Mock" testovi provjeravaju da je kontroler pozvao servis (Verify).
[TestClass]
public class TerminControllerTests
{
    private static XElement _baza = null!;   // samo za čitanje

    private Mock<ITerminService> _terminServis = null!;
    private Mock<IKorisnikService> _korisnikServis = null!;
    private Mock<IMedicinskaUslugaService> _uslugaServis = null!;
    private Mock<UserManager<Korisnik>> _userManager = null!;
    private Mock<IKorisnikNaListiService> _korisnikNaListiServis = null!;
    private Mock<IListaCekanjaService> _listaCekanjaServis = null!;
    private TerminController _controller = null!;
    private List<Korisnik> _korisnici = null!;
    private List<Termin> _termini = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // XML "baza" se čita jednom; objekti se iz nje prave ispočetka za svaki test.
        _baza = XmlLoader.Ucitaj("TerminController_baza.xml", "baza").Single();
    }

    [TestInitialize]
    public void Setup()
    {
        _korisnici = _baza.Descendants("korisnik").Select(TerminControllerPodaci.NapraviKorisnika).ToList();
        _termini = _baza.Descendants("termin").Select(TerminControllerPodaci.NapraviTermin).ToList();
        var usluge = _baza.Descendants("usluga").Select(TerminControllerPodaci.NapraviUslugu).ToList();

        _terminServis = new Mock<ITerminService>();
        _terminServis.Setup(s => s.GetAllAsync()).ReturnsAsync(_termini);
        _terminServis.Setup(s => s.GetByIdAsync(It.IsAny<int>()))
                     .ReturnsAsync((int id) => _termini.FirstOrDefault(t => t.TerminId == id));

        _korisnikServis = new Mock<IKorisnikService>();
        _korisnikServis.Setup(s => s.GetByIdAsync(It.IsAny<string>()))
                       .ReturnsAsync((string id) => _korisnici.FirstOrDefault(k => k.Id == id));
        _korisnikServis.Setup(s => s.GetDoktoriAsync()).ReturnsAsync(TerminControllerPodaci.SaUlogom(_baza, "Doktor"));
        _korisnikServis.Setup(s => s.GetPacijentiAsync()).ReturnsAsync(TerminControllerPodaci.SaUlogom(_baza, "Pacijent"));

        _uslugaServis = new Mock<IMedicinskaUslugaService>();
        _uslugaServis.Setup(s => s.GetAllAsync()).ReturnsAsync(usluge);
        _uslugaServis.Setup(s => s.GetByIdAsync(It.IsAny<int>()))
                     .ReturnsAsync((int id) => usluge.FirstOrDefault(u => u.UslugaId == id));

        _userManager = new Mock<UserManager<Korisnik>>(Mock.Of<IUserStore<Korisnik>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);
        _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                    .ReturnsAsync((ClaimsPrincipal p) => TerminControllerPodaci.PrijavljeniKorisnik(_korisnici, p));

        _korisnikNaListiServis = new Mock<IKorisnikNaListiService>();
        _listaCekanjaServis = new Mock<IListaCekanjaService>();

        _controller = new TerminController(_terminServis.Object, _korisnikServis.Object, _uslugaServis.Object,
            _userManager.Object, _korisnikNaListiServis.Object, _listaCekanjaServis.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        _controller.Dispose();
        _terminServis.Reset();
        _korisnikServis.Reset();
        _uslugaServis.Reset();
        _userManager.Reset();
        _korisnikNaListiServis.Reset();
        _listaCekanjaServis.Reset();
    }

    // ─── INDEX ────────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.IndexPoUlozi), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task Index_PoUlozi_VracaSamoDozvoljeneTermine(string korisnikId, string uloga, string[] ocekivaniTermini)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.Index() as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        CollectionAssert.AreEqual(ocekivaniTermini, TerminControllerPodaci.IdTermina(rezultat.Model));
    }

    // ─── DETAILS / EDIT (GET) / DELETE (GET) ──────────────────────────────

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.PristupPrikazu), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task Details_RazliciteUlogeIId_VracaOcekivaniRezultat(string id, string korisnikId, string uloga,
        string ocekivaniRezultat)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.Details(TerminControllerPodaci.IdIliNull(id));

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Detalji), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Details_TerminPostoji_PrikazujeImeDoktoraIliNepoznat(int id, string korisnikId, string uloga,
        string doktorIme, string pacijentIme, string uslugaNaziv)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.Details(id);

        // Assert
        Assert.AreEqual(doktorIme, _controller.ViewData["DoktorIme"]);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Detalji), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Details_TerminPostoji_PrikazujeImePacijentaIliNepoznat(int id, string korisnikId, string uloga,
        string doktorIme, string pacijentIme, string uslugaNaziv)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.Details(id);

        // Assert
        Assert.AreEqual(pacijentIme, _controller.ViewData["PacijentIme"]);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Detalji), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Details_TerminPostoji_PrikazujeNazivUslugeIliNepoznat(int id, string korisnikId, string uloga,
        string doktorIme, string pacijentIme, string uslugaNaziv)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.Details(id);

        // Assert
        Assert.AreEqual(uslugaNaziv, _controller.ViewData["UslugaNaziv"]);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.PristupPrikazu), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Edit_Get_RazliciteUlogeIId_VracaOcekivaniRezultat(string id, string korisnikId, string uloga,
        string ocekivaniRezultat)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.Edit(TerminControllerPodaci.IdIliNull(id));

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.PristupPrikazu), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Delete_Get_RazliciteUlogeIId_VracaOcekivaniRezultat(string id, string korisnikId, string uloga,
        string ocekivaniRezultat)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.Delete(TerminControllerPodaci.IdIliNull(id));

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    // ─── CREATE (GET) ─────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.PadajuceListe), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Create_Get_PoUlozi_PopunjavaListuDoktoraSamoKadTreba(string korisnikId, string uloga, bool imaDoktore,
        bool imaPacijente)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.Create();

        // Assert
        Assert.AreEqual(imaDoktore, _controller.ViewData.ContainsKey(nameof(Termin.DoktorID)));
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.PadajuceListe), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Create_Get_PoUlozi_PopunjavaListuPacijenataSamoKadTreba(string korisnikId, string uloga, bool imaDoktore,
        bool imaPacijente)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.Create();

        // Assert
        Assert.AreEqual(imaPacijente, _controller.ViewData.ContainsKey(nameof(Termin.PacijentID)));
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.UlogovanDoktor), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Create_Get_Doktor_PamtiIdPrijavljenogDoktora(string korisnikId, string uloga, string ocekivaniId)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.Create();

        // Assert
        Assert.AreEqual(ocekivaniId, _controller.ViewData["UlogovanDoktorId"]);
    }

    // ─── CREATE (POST) ────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.NoviTerminiStatus), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task Create_Post_PoUlozi_PostavljaStatusNaCekanju(string korisnikId, string uloga, string doktorId,
        string pacijentId, int uslugaId, int pomakDana, int sat, Status ocekivaniStatus)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        var termin = TerminControllerPodaci.NoviTermin(default, doktorId, pacijentId, uslugaId, pomakDana, sat);

        // Act
        await _controller.Create(termin);

        // Assert
        Assert.AreEqual(ocekivaniStatus, termin.Status);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.NoviTerminiPacijenta), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Create_Post_Pacijent_PostavljaPrijavljenogPacijenta(string korisnikId, string uloga, string doktorId,
        string pacijentId, int uslugaId, int pomakDana, int sat)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        var termin = TerminControllerPodaci.NoviTermin(default, doktorId, pacijentId, uslugaId, pomakDana, sat);

        // Act
        await _controller.Create(termin);

        // Assert
        Assert.AreEqual(korisnikId, termin.PacijentID);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.NoviTerminiDoktora), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Create_Post_Doktor_PostavljaPrijavljenogDoktora(string korisnikId, string uloga, string doktorId,
        string pacijentId, int uslugaId, int pomakDana, int sat)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        var termin = TerminControllerPodaci.NoviTermin(default, doktorId, pacijentId, uslugaId, pomakDana, sat);

        // Act
        await _controller.Create(termin);

        // Assert
        Assert.AreEqual(korisnikId, termin.DoktorID);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.AdminStatus), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Create_Post_AdministratorZadaoStatus_ZadrzavaStatus(string korisnikId, string uloga, string doktorId,
        string pacijentId, int uslugaId, int pomakDana, int sat, Status status)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        var termin = TerminControllerPodaci.NoviTermin(default, doktorId, pacijentId, uslugaId, pomakDana, sat);
        termin.Status = status;

        // Act
        await _controller.Create(termin);

        // Assert
        Assert.AreEqual(status, termin.Status);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.NoviTermini), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task Create_Post_ValidanTermin_ZakazujeTermin(string korisnikId, string uloga, string doktorId,
        string pacijentId, int uslugaId, int pomakDana, int sat)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        var termin = TerminControllerPodaci.NoviTermin(default, doktorId, pacijentId, uslugaId, pomakDana, sat);

        // Act
        await _controller.Create(termin);

        // Assert
        _terminServis.Verify(s => s.ZakaziTerminAsync(termin), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.NoviTermini), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Create_Post_ValidanTermin_PreusmjeravaNaIndex(string korisnikId, string uloga, string doktorId,
        string pacijentId, int uslugaId, int pomakDana, int sat)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        var termin = TerminControllerPodaci.NoviTermin(default, doktorId, pacijentId, uslugaId, pomakDana, sat);

        // Act
        var rezultat = await _controller.Create(termin) as RedirectToActionResult;

        // Assert
        Assert.AreEqual(nameof(TerminController.Index), rezultat?.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.NoviTermini), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Create_Post_NevalidanModel_NeZakazujeTermin(string korisnikId, string uloga, string doktorId,
        string pacijentId, int uslugaId, int pomakDana, int sat)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        _controller.ModelState.AddModelError(nameof(Termin.Datum), string.Empty);
        var termin = TerminControllerPodaci.NoviTermin(default, doktorId, pacijentId, uslugaId, pomakDana, sat);

        // Act
        await _controller.Create(termin);

        // Assert
        _terminServis.Verify(s => s.ZakaziTerminAsync(It.IsAny<Termin>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.NoviTermini), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Create_Post_NevalidanModel_VracaFormuSaIstimTerminom(string korisnikId, string uloga, string doktorId,
        string pacijentId, int uslugaId, int pomakDana, int sat)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        _controller.ModelState.AddModelError(nameof(Termin.Datum), string.Empty);
        var termin = TerminControllerPodaci.NoviTermin(default, doktorId, pacijentId, uslugaId, pomakDana, sat);

        // Act
        var rezultat = await _controller.Create(termin) as ViewResult;

        // Assert
        Assert.AreSame(termin, rezultat?.Model);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Greske), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Create_Post_ServisBaciIzuzetak_PorukaIdeUzPoljeVrijeme(int terminId, string korisnikId, string uloga,
        string doktorId, int uslugaId, int pomakDana, int sat, string poruka)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        _terminServis.Setup(s => s.ZakaziTerminAsync(It.IsAny<Termin>())).ThrowsAsync(new Exception(poruka));
        var termin = TerminControllerPodaci.NoviTermin(default, doktorId, string.Empty, uslugaId, pomakDana, sat);

        // Act
        await _controller.Create(termin);

        // Assert
        Assert.AreEqual(poruka, _controller.ModelState[nameof(Termin.Vrijeme)]!.Errors.Single().ErrorMessage);
    }

    // ─── CREATE (POST): LISTA ČEKANJA ─────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.ListaCekanja), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task Create_Post_DodajNaListuListaPostoji_UpisujePacijentaNaListu(string korisnikId, string uloga, string doktorId,
        int listaId)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        _listaCekanjaServis.Setup(s => s.GetListaZaDoktoraAsync(doktorId))
                           .ReturnsAsync(new ListaCekanja { ListaId = listaId, DoktorID = doktorId });
        var termin = new Termin { DoktorID = doktorId };

        // Act
        await _controller.Create(termin, dodajNaListu: true);

        // Assert
        _korisnikNaListiServis.Verify(s => s.AddAsync(It.Is<KorisnikNaListi>(k =>
            k.ListaID == listaId && k.KorisnikID == korisnikId)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.ListaCekanja), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Create_Post_DodajNaListu_NeZakazujeTermin(string korisnikId, string uloga, string doktorId,
        int listaId)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        _listaCekanjaServis.Setup(s => s.GetListaZaDoktoraAsync(doktorId))
                           .ReturnsAsync(new ListaCekanja { ListaId = listaId, DoktorID = doktorId });
        var termin = new Termin { DoktorID = doktorId };

        // Act
        await _controller.Create(termin, dodajNaListu: true);

        // Assert
        _terminServis.Verify(s => s.ZakaziTerminAsync(It.IsAny<Termin>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.ListaCekanja), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Create_Post_DodajNaListuListaPostoji_PostavljaPorukuUspjeha(string korisnikId, string uloga, string doktorId,
        int listaId)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        _listaCekanjaServis.Setup(s => s.GetListaZaDoktoraAsync(doktorId))
                           .ReturnsAsync(new ListaCekanja { ListaId = listaId, DoktorID = doktorId });
        var termin = new Termin { DoktorID = doktorId };

        // Act
        await _controller.Create(termin, dodajNaListu: true);

        // Assert
        Assert.IsNotNull(_controller.TempData["Success"]);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.BezListe), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Create_Post_DodajNaListuDoktorNemaListu_PostavljaInfoPoruku(string korisnikId, string uloga, string doktorId)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        var termin = new Termin { DoktorID = doktorId };

        // Act
        await _controller.Create(termin, dodajNaListu: true);

        // Assert
        Assert.IsNotNull(_controller.TempData["Info"]);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Greske), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Create_Post_DodajNaListuServisBaciIzuzetak_PorukaIdeUTempData(int terminId, string korisnikId,
        string uloga, string doktorId, int uslugaId, int pomakDana, int sat, string poruka)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        _listaCekanjaServis.Setup(s => s.GetListaZaDoktoraAsync(doktorId)).ThrowsAsync(new Exception(poruka));
        var termin = new Termin { DoktorID = doktorId };

        // Act
        await _controller.Create(termin, dodajNaListu: true);

        // Assert
        Assert.AreEqual(poruka, _controller.TempData["Info"]);
    }

    // ─── OTKAZIVANJE I POTVRDA ────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.PristupAkciji), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Otkazi_RazliciteUlogeIId_VracaOcekivaniRezultat(int id, string korisnikId, string uloga,
        string ocekivaniRezultat)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.Otkazi(id);

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Otkazivanje), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task Otkazi_VlasnikIliAdministrator_OtkazujeTermin(int id, string korisnikId, string uloga)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.Otkazi(id);

        // Assert
        _terminServis.Verify(s => s.OtkaziTerminAsync(id), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Greske), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Otkazi_ServisBaciIzuzetak_PorukaIdeUTempData(int terminId, string korisnikId, string uloga,
        string doktorId, int uslugaId, int pomakDana, int sat, string poruka)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        _terminServis.Setup(s => s.OtkaziTerminAsync(terminId)).ThrowsAsync(new Exception(poruka));

        // Act
        await _controller.Otkazi(terminId);

        // Assert
        Assert.AreEqual(poruka, _controller.TempData["Greška"]);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Mail), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OtkaziIzMaila_TerminPostoji_PrikazujeStranicuOtkazivanja(int id, string pogledOtkazan, string pogledPotvrdjen)
    {
        // Arrange (servis je pripremljen u Setup)

        // Act
        var rezultat = await _controller.OtkaziIzMaila(id) as ViewResult;

        // Assert
        Assert.AreEqual(pogledOtkazan, rezultat?.ViewName);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Mail), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OtkaziIzMaila_TerminPostoji_OtkazujeTermin(int id, string pogledOtkazan, string pogledPotvrdjen)
    {
        // Arrange (servis je pripremljen u Setup)

        // Act
        await _controller.OtkaziIzMaila(id);

        // Assert
        _terminServis.Verify(s => s.OtkaziTerminAsync(id), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Greske), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OtkaziIzMaila_ServisBaciIzuzetak_VracaTekstGreske(int terminId, string korisnikId, string uloga,
        string doktorId, int uslugaId, int pomakDana, int sat, string poruka)
    {
        // Arrange
        _terminServis.Setup(s => s.OtkaziTerminAsync(terminId)).ThrowsAsync(new Exception(poruka));

        // Act
        var rezultat = await _controller.OtkaziIzMaila(terminId) as ContentResult;

        // Assert
        Assert.AreEqual(poruka, rezultat?.Content);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Mail), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PotvrdiIzMaila_TerminPostoji_PrikazujeStranicuPotvrde(int id, string pogledOtkazan, string pogledPotvrdjen)
    {
        // Arrange (servis je pripremljen u Setup)

        // Act
        var rezultat = await _controller.PotvrdiIzMaila(id) as ViewResult;

        // Assert
        Assert.AreEqual(pogledPotvrdjen, rezultat?.ViewName);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Mail), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PotvrdiIzMaila_TerminPostoji_PotvrdjujeTermin(int id, string pogledOtkazan, string pogledPotvrdjen)
    {
        // Arrange (servis je pripremljen u Setup)

        // Act
        await _controller.PotvrdiIzMaila(id);

        // Assert
        _terminServis.Verify(s => s.OznaciKaoPotvrdjenAsync(id), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Greske), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PotvrdiIzMaila_ServisBaciIzuzetak_VracaTekstGreske(int terminId, string korisnikId, string uloga,
        string doktorId, int uslugaId, int pomakDana, int sat, string poruka)
    {
        // Arrange
        _terminServis.Setup(s => s.OznaciKaoPotvrdjenAsync(terminId)).ThrowsAsync(new Exception(poruka));

        // Act
        var rezultat = await _controller.PotvrdiIzMaila(terminId) as ContentResult;

        // Assert
        Assert.AreEqual(poruka, rezultat?.Content);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Mail), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OznaciAktivan_TerminPostoji_PotvrdjujeTermin(int id, string pogledOtkazan, string pogledPotvrdjen)
    {
        // Arrange (servis je pripremljen u Setup)

        // Act
        await _controller.OznaciAktivan(id);

        // Assert
        _terminServis.Verify(s => s.OznaciKaoPotvrdjenAsync(id), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Mail), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task OznaciAktivan_TerminPostoji_PreusmjeravaNaIndex(int id, string pogledOtkazan, string pogledPotvrdjen)
    {
        // Arrange (servis je pripremljen u Setup)

        // Act
        var rezultat = await _controller.OznaciAktivan(id) as RedirectToActionResult;

        // Assert
        Assert.AreEqual(nameof(TerminController.Index), rezultat?.ActionName);
    }

    // ─── EDIT (POST) ──────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.RazlicitId), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Edit_Post_IdIzRuteRazlicitOdTermina_VracaNotFound(int id, int terminId)
    {
        // Arrange
        var termin = new Termin { TerminId = terminId };

        // Act
        var rezultat = await _controller.Edit(id, termin);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.PristupAkciji), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Edit_Post_RazliciteUlogeIId_VracaOcekivaniRezultat(int id, string korisnikId, string uloga,
        string ocekivaniRezultat)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        var termin = new Termin { TerminId = id };

        // Act
        var rezultat = await _controller.Edit(id, termin);

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Izmjene), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task Edit_Post_ValidanTermin_MijenjaDatumPostojecegTermina(int id, string korisnikId, string uloga,
        string doktorId, string pacijentId, int uslugaId, int pomakDana, int sat, Status poslaniStatus, Status ocekivaniStatus)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        var termin = TerminControllerPodaci.NoviTermin(id, doktorId, pacijentId, uslugaId, pomakDana, sat);

        // Act
        await _controller.Edit(id, termin);

        // Assert
        Assert.AreEqual(termin.Datum, TerminControllerPodaci.Pronadji(_termini, id).Datum);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Izmjene), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Edit_Post_ValidanTermin_MijenjaUsluguPostojecegTermina(int id, string korisnikId, string uloga,
        string doktorId, string pacijentId, int uslugaId, int pomakDana, int sat, Status poslaniStatus, Status ocekivaniStatus)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        var termin = TerminControllerPodaci.NoviTermin(id, doktorId, pacijentId, uslugaId, pomakDana, sat);

        // Act
        await _controller.Edit(id, termin);

        // Assert
        Assert.AreEqual(uslugaId, TerminControllerPodaci.Pronadji(_termini, id).UslugaID);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Izmjene), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Edit_Post_PoslanDrugiStatus_ZadrzavaStatusIzBaze(int id, string korisnikId, string uloga,
        string doktorId, string pacijentId, int uslugaId, int pomakDana, int sat, Status poslaniStatus, Status ocekivaniStatus)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        var termin = TerminControllerPodaci.NoviTermin(id, doktorId, pacijentId, uslugaId, pomakDana, sat);
        termin.Status = poslaniStatus;

        // Act
        await _controller.Edit(id, termin);

        // Assert
        Assert.AreEqual(ocekivaniStatus, TerminControllerPodaci.Pronadji(_termini, id).Status);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Izmjene), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Edit_Post_ValidanTermin_SnimaPromjene(int id, string korisnikId, string uloga,
        string doktorId, string pacijentId, int uslugaId, int pomakDana, int sat, Status poslaniStatus, Status ocekivaniStatus)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        var termin = TerminControllerPodaci.NoviTermin(id, doktorId, pacijentId, uslugaId, pomakDana, sat);

        // Act
        await _controller.Edit(id, termin);

        // Assert
        _terminServis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Izmjene), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Edit_Post_NevalidanModel_NeMijenjaTermin(int id, string korisnikId, string uloga,
        string doktorId, string pacijentId, int uslugaId, int pomakDana, int sat, Status poslaniStatus, Status ocekivaniStatus)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        _controller.ModelState.AddModelError(nameof(Termin.Datum), string.Empty);
        var termin = TerminControllerPodaci.NoviTermin(id, doktorId, pacijentId, uslugaId, pomakDana, sat);

        // Act
        await _controller.Edit(id, termin);

        // Assert
        _terminServis.Verify(s => s.Update(It.IsAny<Termin>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Izmjene), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Edit_Post_NevalidanModel_VracaFormuSaPoslanimTerminom(int id, string korisnikId, string uloga,
        string doktorId, string pacijentId, int uslugaId, int pomakDana, int sat, Status poslaniStatus, Status ocekivaniStatus)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        _controller.ModelState.AddModelError(nameof(Termin.Datum), string.Empty);
        var termin = TerminControllerPodaci.NoviTermin(id, doktorId, pacijentId, uslugaId, pomakDana, sat);

        // Act
        var rezultat = await _controller.Edit(id, termin) as ViewResult;

        // Assert
        Assert.AreSame(termin, rezultat?.Model);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Konkurentnost), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Edit_Post_KonfliktTerminUMedjuvremenuObrisan_VracaNotFound(int id, string korisnikId, string uloga,
        string poruka)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        _terminServis.SetupSequence(s => s.GetByIdAsync(id))
                     .ReturnsAsync(TerminControllerPodaci.Pronadji(_termini, id))
                     .ReturnsAsync((Termin?)null);
        _terminServis.Setup(s => s.SaveChangesAsync()).ThrowsAsync(new DbUpdateConcurrencyException(poruka));
        var termin = new Termin { TerminId = id };

        // Act
        var rezultat = await _controller.Edit(id, termin);

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Konkurentnost), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task Edit_Post_KonfliktTerminJosPostoji_PonovoBacaIzuzetak(int id, string korisnikId, string uloga,
        string poruka)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);
        _terminServis.Setup(s => s.SaveChangesAsync()).ThrowsAsync(new DbUpdateConcurrencyException(poruka));
        var termin = new Termin { TerminId = id };

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(() => _controller.Edit(id, termin));

        // Assert
        Assert.AreEqual(poruka, izuzetak.Message);
    }

    // ─── DELETE (POST) ────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.PristupAkciji), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task DeleteConfirmed_RazliciteUlogeIId_VracaOcekivaniRezultat(int id, string korisnikId, string uloga,
        string ocekivaniRezultat)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.DeleteConfirmed(id);

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Otkazivanje), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task DeleteConfirmed_VlasnikIliAdministrator_BriseTermin(int id, string korisnikId, string uloga)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _terminServis.Verify(s => s.Delete(It.Is<Termin>(t => t.TerminId == id)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Otkazivanje), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task DeleteConfirmed_VlasnikIliAdministrator_SnimaPromjene(int id, string korisnikId, string uloga)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.DeleteConfirmed(id);

        // Assert
        _terminServis.Verify(s => s.SaveChangesAsync(), Times.Once);
    }

    // ─── KALENDAR (JSON) ──────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.ZauzetiSlotovi), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Timeout(2000)]
    public async Task GetZauzetiSlotovi_DatumIDoktor_VracaZauzetaVremenaBezOtkazanih(string datum, string doktorId,
        string[] ocekivaniSlotovi)
    {
        // Arrange (termini su pripremljeni u Setup)

        // Act
        var rezultat = await _controller.GetZauzetiSlotovi(datum, doktorId);

        // Assert
        CollectionAssert.AreEqual(ocekivaniSlotovi, TerminControllerPodaci.JsonLista(rezultat, "zauzetiSlotovi"));
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.NeispravanDatum), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetZauzetiSlotovi_NeispravanDatum_VracaPraznuListu(string datum, string doktorId)
    {
        // Arrange (termini su pripremljeni u Setup)

        // Act
        var rezultat = await _controller.GetZauzetiSlotovi(datum, doktorId);

        // Assert
        Assert.AreEqual(0, TerminControllerPodaci.JsonLista(rezultat, "zauzetiSlotovi").Length);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.ZauzetiDani), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetZauzetiDani_Doktor_VracaDaneSaSvimSlotovimaZauzetim(string doktorId, string[] ocekivaniDani)
    {
        // Arrange (termini su pripremljeni u Setup)

        // Act
        var rezultat = await _controller.GetZauzetiDani(doktorId);

        // Assert
        CollectionAssert.AreEqual(ocekivaniDani, TerminControllerPodaci.JsonLista(rezultat, "zauzetiDani"));
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.MjesecZauzet), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task GetMjesecZauzet_DoktorIMjesec_VracaDaLiJeMjesecPopunjen(string doktorId, int godina, int mjesec,
        bool ocekivano)
    {
        // Arrange (termini su pripremljeni u Setup)

        // Act
        var rezultat = await _controller.GetMjesecZauzet(doktorId, godina, mjesec);

        // Assert
        Assert.AreEqual(ocekivano, TerminControllerPodaci.JsonVrijednost(rezultat, "zauzet"));
    }

    // ─── PRIORITET PACIJENTA ──────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.PrioritetPristup), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task PromijeniPrioritet_Get_RazliciteSituacije_VracaOcekivaniRezultat(string pacijentId, string korisnikId,
        string uloga, string ocekivaniRezultat)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.PromijeniPrioritet(pacijentId);

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.PrioritetPrikaz), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniPrioritet_Get_MojPacijent_PrikazujeImePacijenta(string pacijentId, string korisnikId,
        string uloga, string ocekivanoIme, string ocekivaniPrioritet)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.PromijeniPrioritet(pacijentId);

        // Assert
        Assert.AreEqual(ocekivanoIme, _controller.ViewData["PacijentIme"]);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.PrioritetPrikaz), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniPrioritet_Get_MojPacijent_PrikazujeTrenutniPrioritet(string pacijentId, string korisnikId,
        string uloga, string ocekivanoIme, string ocekivaniPrioritet)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.PromijeniPrioritet(pacijentId);

        // Assert
        Assert.AreEqual(ocekivaniPrioritet, _controller.ViewData["TrenutniPrioritet"]);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.PrioritetIzmjena), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniPrioritet_Post_RazliciteSituacije_VracaOcekivaniRezultat(string pacijentId, string korisnikId,
        string uloga, string prioritet, string ocekivaniRezultat)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.PromijeniPrioritet(pacijentId, prioritet);

        // Assert
        Assert.AreEqual(ocekivaniRezultat, rezultat.GetType().Name);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.PrioritetNovi), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task PromijeniPrioritet_Post_IspravanPrioritet_PostavljaNoviPrioritet(string pacijentId, string korisnikId,
        string uloga, string prioritet)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.PromijeniPrioritet(pacijentId, prioritet);

        // Assert
        Assert.AreEqual(Enum.Parse<Prioritet>(prioritet), TerminControllerPodaci.NadjiKorisnika(_korisnici, pacijentId).Prioritet);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.PrioritetNovi), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniPrioritet_Post_IspravanPrioritet_SnimaPacijenta(string pacijentId, string korisnikId,
        string uloga, string prioritet)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.PromijeniPrioritet(pacijentId, prioritet);

        // Assert
        _korisnikServis.Verify(s => s.AzurirajAsync(It.Is<Korisnik>(k => k.Id == pacijentId)), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.PrioritetNovi), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniPrioritet_Post_IspravanPrioritet_PreusmjeravaNaMojePacijente(string pacijentId,
        string korisnikId, string uloga, string prioritet)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.PromijeniPrioritet(pacijentId, prioritet) as RedirectToActionResult;

        // Assert
        Assert.AreEqual(nameof(TerminController.MojiPacijenti), rezultat?.ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.PrioritetUklonjen), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniPrioritet_Post_PrazanPrioritet_UklanjaPrioritet(string pacijentId, string korisnikId,
        string uloga, string prioritet)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.PromijeniPrioritet(pacijentId, prioritet);

        // Assert
        Assert.IsNull(TerminControllerPodaci.NadjiKorisnika(_korisnici, pacijentId).Prioritet);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.PrioritetNeispravan), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniPrioritet_Post_NeispravanPrioritetIliTudjiPacijent_PostavljaGresku(string pacijentId,
        string korisnikId, string uloga, string prioritet)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.PromijeniPrioritet(pacijentId, prioritet);

        // Assert
        Assert.IsNotNull(_controller.TempData["Greska"]);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.PrioritetNeispravan), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task PromijeniPrioritet_Post_NeispravanPrioritetIliTudjiPacijent_NeSnimaPacijenta(string pacijentId,
        string korisnikId, string uloga, string prioritet)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.PromijeniPrioritet(pacijentId, prioritet);

        // Assert
        _korisnikServis.Verify(s => s.AzurirajAsync(It.IsAny<Korisnik>()), Times.Never);
    }

    // ─── MOJI PACIJENTI ───────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.MojiPacijenti), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    [Priority(1)]
    public async Task MojiPacijenti_Doktor_VracaPacijenteSaNeotkazanimTerminima(string korisnikId, string uloga,
        string[] ocekivaniPacijenti)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.MojiPacijenti() as ViewResult;

        // Assert
        Assert.IsNotNull(rezultat);
        CollectionAssert.AreEqual(ocekivaniPacijenti, TerminControllerPodaci.IdKorisnika(rezultat.Model));
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.BrojTermina), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task MojiPacijenti_Doktor_PrikazujeBrojTerminaPacijenta(string korisnikId, string uloga, string pacijentId,
        int ocekivaniBroj)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        await _controller.MojiPacijenti();

        // Assert
        Assert.AreEqual(ocekivaniBroj, _controller.ViewData[TerminControllerPodaci.KljucBrojaTermina(pacijentId)]);
    }

    [TestMethod]
    [DynamicData(nameof(TerminControllerPodaci.Neprijavljen), typeof(TerminControllerPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Stub"), TestCategory("Autorizacija")]
    [TestProperty("Autor", "Irhad Brescic")]
    public async Task MojiPacijenti_KorisnikNePostoji_VracaUnauthorized(string korisnikId, string uloga)
    {
        // Arrange
        TerminControllerPodaci.Prijavi(_controller, korisnikId, uloga);

        // Act
        var rezultat = await _controller.MojiPacijenti();

        // Assert
        Assert.IsInstanceOfType(rezultat, typeof(UnauthorizedResult));
    }
}
