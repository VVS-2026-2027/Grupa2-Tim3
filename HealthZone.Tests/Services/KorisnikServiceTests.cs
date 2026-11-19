using HealthZone.Models;
using HealthZone.Repositories;
using HealthZone.Services;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace HealthZone.Tests;

[TestClass]
public class KorisnikServiceTests
{
    private Mock<IKorisnikRepository> _korisnikRepo = null!;
    private Mock<UserManager<Korisnik>> _userManager = null!;
    private KorisnikService _servis = null!;

    [TestInitialize]
    public void Setup()
    {
        var store = new Mock<IUserStore<Korisnik>>();
        _userManager = new Mock<UserManager<Korisnik>>(store.Object,
            null!, null!, null!, null!, null!, null!, null!, null!);
        _korisnikRepo = new Mock<IKorisnikRepository>();
        _servis = new KorisnikService(_korisnikRepo.Object, _userManager.Object);
    }

    // ─── POMOĆNE METODE ───────────────────────────────────────────────────

    // Prazno polje iz CSV-a se tretira kao null.
    private static string? Prazno(string s) => string.IsNullOrEmpty(s) ? null : s;

    private static T? EnumIliNull<T>(string s) where T : struct, Enum
        => string.IsNullOrEmpty(s) ? null : Enum.Parse<T>(s);

    private static string[] Lista(string? s)
        => (s ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);

    private static IdentityResult Neuspjeh(string[] greske)
        => IdentityResult.Failed(greske.Select(g => new IdentityError { Description = g }).ToArray());

    // ─── IZVORI PODATAKA (CSV) ────────────────────────────────────────────

    // Id korisnika koji ne postoji i očekivana poruka izuzetka.
    public static IEnumerable<object[]> KorisnikNePostoji() =>
        CsvLoader.Ucitaj("KorisnikService_korisnikNePostoji.csv")
                 .Select(r => new object[] { r[0], r[1] });

    public static IEnumerable<object[]> PromjenaUlogeNepostojeci() =>
        CsvLoader.Ucitaj("KorisnikService_promjenaUlogeNepostojeci.csv")
                 .Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> LozinkaNepostojeci() =>
        CsvLoader.Ucitaj("KorisnikService_lozinkaNepostojeci.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> PostojeciKorisnici() =>
        CsvLoader.Ucitaj("KorisnikService_postojeciKorisnik.csv")
                 .Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> BrojeviPromjena() =>
        CsvLoader.Ucitaj("KorisnikService_snimanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> PodaciZaAzuriranje() =>
        CsvLoader.Ucitaj("KorisnikService_azuriranje.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7] });

    public static IEnumerable<object[]> NepostojeceUloge() =>
        CsvLoader.Ucitaj("KorisnikService_nepostojecaUloga.csv")
                 .Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> PrazneLozinke() =>
        CsvLoader.Ucitaj("KorisnikService_praznaLozinka.csv")
                 .Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> NoviPacijenti() =>
        CsvLoader.Ucitaj("KorisnikService_noviPacijent.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], r[3], r[4], int.Parse(r[5]) });

    public static IEnumerable<object[]> PacijentiSaKartonom() =>
        CsvLoader.Ucitaj("KorisnikService_pacijentSaKartonom.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> DoktoriBezSpecijalizacije() =>
        CsvLoader.Ucitaj("KorisnikService_doktorBezSpecijalizacije.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> Osoblje() =>
        CsvLoader.Ucitaj("KorisnikService_osoblje.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> DodavanjeNepostojeceUloge() =>
        CsvLoader.Ucitaj("KorisnikService_dodavanjeNepostojecaUloga.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], r[3] });

    public static IEnumerable<object[]> PromjeneLozinke() =>
        CsvLoader.Ucitaj("KorisnikService_promjenaLozinke.csv")
                 .Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> NepostojeciDoktori() =>
        CsvLoader.Ucitaj("KorisnikService_nepostojeciDoktor.csv")
                 .Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> DoktoriPoId() =>
        CsvLoader.Ucitaj("KorisnikService_doktorPoId.csv")
                 .Select(r => new object[] { r[0], r[1], bool.Parse(r[2]), bool.Parse(r[3]) });

    // ─── IZVORI PODATAKA (XML) ────────────────────────────────────────────

    // Uloge koje korisnik ima i uloga koju servis treba vratiti.
    public static IEnumerable<object[]> UlogeKorisnika() =>
        XmlLoader.Ucitaj("KorisnikService_uloga.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     ((string)x.Element("uloge")!).Split(',', StringSplitOptions.RemoveEmptyEntries),
                     (string)x.Element("ocekivanaUloga")!
                 });

    public static IEnumerable<object[]> ListeKorisnika() =>
        XmlLoader.Ucitaj("KorisnikService_korisnici.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     Lista((string?)x.Element("korisnici")),
                     (int)x.Element("ocekivaniBroj")!
                 });

    public static IEnumerable<object[]> PromjeneUloga() =>
        XmlLoader.Ucitaj("KorisnikService_promjenaUloge.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("id")!,
                     Lista((string?)x.Element("trenutneUloge")),
                     (string)x.Element("novaUloga")!,
                     (bool)x.Element("uklanjaStare")!
                 });

    // Greške koje Identity vraća i poruka koju servis treba složiti od njih.
    public static IEnumerable<object[]> IdentityGreske() =>
        XmlLoader.Ucitaj("KorisnikService_identityGreske.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("id")!,
                     (string)x.Element("email")!,
                     (string)x.Element("lozinka")!,
                     (string)x.Element("novaLozinka")!,
                     (string)x.Element("uloga")!,
                     x.Element("greske")!.Elements("greska").Select(g => (string)g).ToArray(),
                     (string)x.Element("ocekivanaPoruka")!
                 });

    public static IEnumerable<object[]> KorisniciPoUlozi() =>
        XmlLoader.Ucitaj("KorisnikService_korisniciPoUlozi.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("metoda")!,
                     (string)x.Element("uloga")!,
                     Lista((string?)x.Element("korisnici")),
                     (int)x.Element("ocekivaniBroj")!
                 });

    public static IEnumerable<object[]> DoktoriPoSpecijalizaciji() =>
        XmlLoader.Ucitaj("KorisnikService_doktoriPoSpecijalizaciji.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("ulogaDoktora")!,
                     Lista((string?)x.Element("doktori")),
                     (string)x.Element("trazena")!,
                     (int)x.Element("ocekivaniBroj")!
                 });

    // ─── GET / UPDATE / SAVE (prosljeđivanje repozitoriju) ────────────────

    [TestMethod]
    [DynamicData(nameof(PostojeciKorisnici), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetByIdAsync_KorisnikPostoji_VracaKorisnikaIzRepozitorija(string id, string ime, string prezime)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id, Ime = ime, Prezime = prezime };
        _korisnikRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(korisnik);

        // Act
        var rezultat = await _servis.GetByIdAsync(id);

        // Assert
        Assert.AreSame(korisnik, rezultat);
        _korisnikRepo.Verify(r => r.GetByIdAsync(id), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(ListeKorisnika), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetAllAsync_RepozitorijImaKorisnike_VracaSveKorisnike(string[] ids, int ocekivaniBroj)
    {
        // Arrange
        var korisnici = ids.Select(id => new Korisnik { Id = id }).ToList();
        _korisnikRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(korisnici);

        // Act
        var rezultat = (await _servis.GetAllAsync()).ToList();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count);
        CollectionAssert.AreEqual(ids, rezultat.Select(k => k.Id).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(PostojeciKorisnici), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void Update_ProslijedjenKorisnik_PozivaRepozitorij(string id, string ime, string prezime)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id, Ime = ime, Prezime = prezime };

        // Act
        _servis.Update(korisnik);

        // Assert
        _korisnikRepo.Verify(r => r.Update(korisnik), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(BrojeviPromjena), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task SaveChangesAsync_RepozitorijSnimi_VracaBrojPromjena(int brojPromjena)
    {
        // Arrange
        _korisnikRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(brojPromjena);

        // Act
        var rezultat = await _servis.SaveChangesAsync();

        // Assert
        Assert.AreEqual(brojPromjena, rezultat);
        _korisnikRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ─── AŽURIRANJE ───────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisnikNePostoji), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task AzurirajAsync_KorisnikNePostoji_BacaException(string id, string ocekivanaPoruka)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync((Korisnik?)null);

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AzurirajAsync(korisnik));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(PodaciZaAzuriranje), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task AzurirajAsync_KorisnikPostoji_KopiraPoljaISnima(string id, string ime, string prezime,
        string email, string telefon, string brojKartona, string prioritet, string specijalizacija)
    {
        // Arrange
        var postojeci = new Korisnik { Id = id };
        var izmjene = new Korisnik
        {
            Id = id,
            Ime = ime,
            Prezime = prezime,
            Email = email,
            PhoneNumber = Prazno(telefon),
            BrojKartona = Prazno(brojKartona),
            Prioritet = EnumIliNull<Prioritet>(prioritet),
            Specijalizacija = EnumIliNull<Specijalizacija>(specijalizacija)
        };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(postojeci);
        _userManager.Setup(u => u.UpdateAsync(postojeci)).ReturnsAsync(IdentityResult.Success);

        // Act
        await _servis.AzurirajAsync(izmjene);

        // Assert
        Assert.AreEqual(izmjene.Ime, postojeci.Ime);
        Assert.AreEqual(izmjene.Prezime, postojeci.Prezime);
        Assert.AreEqual(izmjene.Email, postojeci.Email);
        Assert.AreEqual(izmjene.Email, postojeci.UserName);
        Assert.AreEqual(izmjene.PhoneNumber, postojeci.PhoneNumber);
        Assert.AreEqual(izmjene.BrojKartona, postojeci.BrojKartona);
        Assert.AreEqual(izmjene.Prioritet, postojeci.Prioritet);
        Assert.AreEqual(izmjene.Specijalizacija, postojeci.Specijalizacija);
        _userManager.Verify(u => u.UpdateAsync(postojeci), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(IdentityGreske), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AzurirajAsync_UpdateNeuspjesan_BacaExceptionSaGreskama(string id, string email,
        string lozinka, string novaLozinka, string uloga, string[] greske, string ocekivanaPoruka)
    {
        // Arrange
        var postojeci = new Korisnik { Id = id };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(postojeci);
        _userManager.Setup(u => u.UpdateAsync(postojeci)).ReturnsAsync(Neuspjeh(greske));

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(
            () => _servis.AzurirajAsync(new Korisnik { Id = id, Email = email }));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    // ─── PROMJENA ULOGE ───────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(PromjenaUlogeNepostojeci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PromijeniUloguAsync_KorisnikNePostoji_BacaException(string id, string novaUloga,
        string ocekivanaPoruka)
    {
        // Arrange
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync((Korisnik?)null);

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(
            () => _servis.PromijeniUloguAsync(id, novaUloga));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
        _userManager.Verify(u => u.AddToRoleAsync(It.IsAny<Korisnik>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(PromjeneUloga), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task PromijeniUloguAsync_UlogaPostoji_UklanjaStareIDodajeNovu(string id, string[] trenutneUloge,
        string novaUloga, bool uklanjaStare)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id };
        IList<string> uloge = trenutneUloge.ToList();
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(korisnik);
        _userManager.Setup(u => u.GetRolesAsync(korisnik)).ReturnsAsync(uloge);
        _userManager.Setup(u => u.RemoveFromRolesAsync(korisnik, uloge)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.AddToRoleAsync(korisnik, novaUloga)).ReturnsAsync(IdentityResult.Success);

        // Act
        await _servis.PromijeniUloguAsync(id, novaUloga);

        // Assert
        _userManager.Verify(u => u.RemoveFromRolesAsync(korisnik, uloge),
            uklanjaStare ? Times.Once() : Times.Never());
        _userManager.Verify(u => u.AddToRoleAsync(korisnik, novaUloga), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NepostojeceUloge), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PromijeniUloguAsync_UlogaNePostoji_BacaException(string id, string uloga, string ocekivanaPoruka)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(korisnik);
        _userManager.Setup(u => u.GetRolesAsync(korisnik)).ReturnsAsync(new List<string>());
        _userManager.Setup(u => u.AddToRoleAsync(korisnik, uloga)).ReturnsAsync(IdentityResult.Failed());

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.PromijeniUloguAsync(id, uloga));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    // ─── DODAVANJE KORISNIKA ──────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(PrazneLozinke), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task AddAsync_PraznaLozinka_BacaException(string email, string lozinka, string ocekivanaPoruka)
    {
        // Arrange
        var korisnik = new Korisnik { Email = email };

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(korisnik, lozinka));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
        _userManager.Verify(u => u.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(NoviPacijenti), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task AddAsync_PacijentBezKartona_GeneriseBrojKartona(string email, string lozinka, string uloga,
        string prefiks, string formatDatuma, int duzina)
    {
        // Arrange
        var korisnik = new Korisnik { Email = email };
        _userManager.Setup(u => u.CreateAsync(korisnik, lozinka)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.AddToRoleAsync(korisnik, uloga)).ReturnsAsync(IdentityResult.Success);

        // Act
        await _servis.AddAsync(korisnik, lozinka, uloga);

        // Assert
        Assert.AreEqual(email, korisnik.UserName);
        Assert.IsNotNull(korisnik.BrojKartona);
        StringAssert.StartsWith(korisnik.BrojKartona, prefiks);
        StringAssert.Contains(korisnik.BrojKartona, DateTime.Now.ToString(formatDatuma));
        Assert.AreEqual(duzina, korisnik.BrojKartona.Length);
        _userManager.Verify(u => u.CreateAsync(korisnik, lozinka), Times.Once);
        _userManager.Verify(u => u.AddToRoleAsync(korisnik, uloga), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(PacijentiSaKartonom), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_PacijentImaKarton_ZadrzavaPostojeciBroj(string email, string lozinka, string uloga,
        string brojKartona)
    {
        // Arrange
        var korisnik = new Korisnik { Email = email, BrojKartona = brojKartona };
        _userManager.Setup(u => u.CreateAsync(korisnik, lozinka)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.AddToRoleAsync(korisnik, uloga)).ReturnsAsync(IdentityResult.Success);

        // Act
        await _servis.AddAsync(korisnik, lozinka, uloga);

        // Assert
        Assert.AreEqual(brojKartona, korisnik.BrojKartona);
    }

    [TestMethod]
    [DynamicData(nameof(DoktoriBezSpecijalizacije), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_DoktorBezSpecijalizacije_BacaException(string email, string lozinka, string uloga,
        string ocekivanaPoruka)
    {
        // Arrange
        var korisnik = new Korisnik { Email = email };

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(korisnik, lozinka, uloga));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
        _userManager.Verify(u => u.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(Osoblje), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_OsobljeValidno_KreiraKorisnikaBezKartona(string email, string lozinka, string uloga,
        string specijalizacija)
    {
        // Arrange
        var korisnik = new Korisnik { Email = email, Specijalizacija = EnumIliNull<Specijalizacija>(specijalizacija) };
        _userManager.Setup(u => u.CreateAsync(korisnik, lozinka)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.AddToRoleAsync(korisnik, uloga)).ReturnsAsync(IdentityResult.Success);

        // Act
        await _servis.AddAsync(korisnik, lozinka, uloga);

        // Assert
        Assert.IsNull(korisnik.BrojKartona);
        Assert.AreEqual(email, korisnik.UserName);
        _userManager.Verify(u => u.AddToRoleAsync(korisnik, uloga), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(IdentityGreske), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_KreiranjeNeuspjesno_BacaExceptionSaGreskama(string id, string email,
        string lozinka, string novaLozinka, string uloga, string[] greske, string ocekivanaPoruka)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id, Email = email };
        _userManager.Setup(u => u.CreateAsync(korisnik, lozinka)).ReturnsAsync(Neuspjeh(greske));

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(korisnik, lozinka, uloga));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
        _userManager.Verify(u => u.AddToRoleAsync(It.IsAny<Korisnik>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(DodavanjeNepostojeceUloge), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task AddAsync_UlogaNePostoji_BacaException(string email, string lozinka, string uloga,
        string ocekivanaPoruka)
    {
        // Arrange
        var korisnik = new Korisnik { Email = email };
        _userManager.Setup(u => u.CreateAsync(korisnik, lozinka)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.AddToRoleAsync(korisnik, uloga)).ReturnsAsync(IdentityResult.Failed());

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.AddAsync(korisnik, lozinka, uloga));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    // ─── BRISANJE ─────────────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(PostojeciKorisnici), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteAsync_BrisanjeUspjesno_PozivaUserManager(string id, string ime, string prezime)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id, Ime = ime, Prezime = prezime };
        _userManager.Setup(u => u.DeleteAsync(korisnik)).ReturnsAsync(IdentityResult.Success);

        // Act
        await _servis.DeleteAsync(korisnik);

        // Assert
        _userManager.Verify(u => u.DeleteAsync(korisnik), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(IdentityGreske), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task DeleteAsync_BrisanjeNeuspjesno_BacaExceptionSaGreskama(string id, string email,
        string lozinka, string novaLozinka, string uloga, string[] greske, string ocekivanaPoruka)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id, Email = email };
        _userManager.Setup(u => u.DeleteAsync(korisnik)).ReturnsAsync(Neuspjeh(greske));

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(() => _servis.DeleteAsync(korisnik));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    [TestMethod]
    [DynamicData(nameof(PostojeciKorisnici), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Timeout(2000)]
    public void Delete_SinhroniPoziv_BriseKrozUserManager(string id, string ime, string prezime)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id, Ime = ime, Prezime = prezime };
        _userManager.Setup(u => u.DeleteAsync(korisnik)).ReturnsAsync(IdentityResult.Success);

        // Act
        _servis.Delete(korisnik);

        // Assert
        _userManager.Verify(u => u.DeleteAsync(korisnik), Times.Once);
    }

    // ─── PROMJENA LOZINKE ─────────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(LozinkaNepostojeci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PromijeniLozinkuAsync_KorisnikNePostoji_BacaException(string id, string staraLozinka,
        string novaLozinka, string ocekivanaPoruka)
    {
        // Arrange
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync((Korisnik?)null);

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(
            () => _servis.PromijeniLozinkuAsync(id, staraLozinka, novaLozinka));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
        _userManager.Verify(u => u.ChangePasswordAsync(It.IsAny<Korisnik>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(PromjeneLozinke), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task PromijeniLozinkuAsync_IspravnaLozinka_PozivaChangePassword(string id, string staraLozinka,
        string novaLozinka)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(korisnik);
        _userManager.Setup(u => u.ChangePasswordAsync(korisnik, staraLozinka, novaLozinka))
                    .ReturnsAsync(IdentityResult.Success);

        // Act
        await _servis.PromijeniLozinkuAsync(id, staraLozinka, novaLozinka);

        // Assert
        _userManager.Verify(u => u.ChangePasswordAsync(korisnik, staraLozinka, novaLozinka), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(IdentityGreske), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Izuzetak")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PromijeniLozinkuAsync_PromjenaNeuspjesna_BacaExceptionSaGreskama(string id, string email,
        string lozinka, string novaLozinka, string uloga, string[] greske, string ocekivanaPoruka)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id, Email = email };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(korisnik);
        _userManager.Setup(u => u.ChangePasswordAsync(korisnik, lozinka, novaLozinka))
                    .ReturnsAsync(Neuspjeh(greske));

        // Act
        var izuzetak = await Assert.ThrowsExceptionAsync<Exception>(
            () => _servis.PromijeniLozinkuAsync(id, lozinka, novaLozinka));

        // Assert
        Assert.AreEqual(ocekivanaPoruka, izuzetak.Message);
    }

    // ─── KORISNICI PO ULOZI ───────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(KorisniciPoUlozi), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetKorisniciPoUlozi_UlogaImaKorisnike_VracaKorisnikeTeUloge(string metoda, string uloga,
        string[] ids, int ocekivaniBroj)
    {
        // Arrange
        IList<Korisnik> korisnici = ids.Select(id => new Korisnik { Id = id }).ToList();
        _userManager.Setup(u => u.GetUsersInRoleAsync(uloga)).ReturnsAsync(korisnici);
        var metode = new Dictionary<string, Func<Task<IEnumerable<Korisnik>>>>
        {
            [nameof(KorisnikService.GetDoktoriAsync)] = _servis.GetDoktoriAsync,
            [nameof(KorisnikService.GetPacijentiAsync)] = _servis.GetPacijentiAsync,
            [nameof(KorisnikService.GetMedicinskeSestreAsync)] = _servis.GetMedicinskeSestreAsync
        };

        // Act
        var rezultat = (await metode[metoda]()).ToList();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count);
        _userManager.Verify(u => u.GetUsersInRoleAsync(uloga), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(DoktoriPoSpecijalizaciji), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetDoktoriPoSpecijalizacijiAsync_RazliciteSpecijalizacije_VracaSamoTrazenu(string ulogaDoktora,
        string[] specijalizacije, string trazena, int ocekivaniBroj)
    {
        // Arrange
        IList<Korisnik> doktori = specijalizacije
            .Select(s => new Korisnik { Specijalizacija = Enum.Parse<Specijalizacija>(s) })
            .ToList();
        _userManager.Setup(u => u.GetUsersInRoleAsync(ulogaDoktora)).ReturnsAsync(doktori);
        var trazenaSpecijalizacija = Enum.Parse<Specijalizacija>(trazena);

        // Act
        var rezultat = (await _servis.GetDoktoriPoSpecijalizacijiAsync(trazenaSpecijalizacija)).ToList();

        // Assert
        Assert.AreEqual(ocekivaniBroj, rezultat.Count);
        Assert.IsTrue(rezultat.All(d => d.Specijalizacija == trazenaSpecijalizacija));
    }

    // ─── ULOGA I DOKTOR PO ID ─────────────────────────────────────────────

    [TestMethod]
    [DynamicData(nameof(UlogeKorisnika), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Timeout(2000)]
    public async Task GetUlogaAsync_KorisnikImaUloge_VracaPrvuUlogu(string[] uloge, string ocekivanaUloga)
    {
        // Arrange
        var korisnik = new Korisnik();
        _userManager.Setup(u => u.GetRolesAsync(korisnik)).ReturnsAsync((IList<string>)uloge.ToList());

        // Act
        var rezultat = await _servis.GetUlogaAsync(korisnik);

        // Assert
        Assert.AreEqual(ocekivanaUloga, rezultat);
        _userManager.Verify(u => u.GetRolesAsync(korisnik), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NepostojeciDoktori), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetDoktorByIdAsync_KorisnikNePostoji_VracaNull(string id)
    {
        // Arrange
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync((Korisnik?)null);

        // Act
        var rezultat = await _servis.GetDoktorByIdAsync(id);

        // Assert
        Assert.IsNull(rezultat);
        _userManager.Verify(u => u.IsInRoleAsync(It.IsAny<Korisnik>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DynamicData(nameof(DoktoriPoId), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task GetDoktorByIdAsync_ProvjeraUloge_VracaSamoDoktora(string id, string uloga, bool jeUUlozi,
        bool ocekivanoPronadjen)
    {
        // Arrange
        var korisnik = new Korisnik { Id = id };
        _userManager.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(korisnik);
        _userManager.Setup(u => u.IsInRoleAsync(korisnik, uloga)).ReturnsAsync(jeUUlozi);

        // Act
        var rezultat = await _servis.GetDoktorByIdAsync(id);

        // Assert
        Assert.AreEqual(ocekivanoPronadjen, rezultat != null);
        _userManager.Verify(u => u.IsInRoleAsync(korisnik, uloga), Times.Once);
    }
}
