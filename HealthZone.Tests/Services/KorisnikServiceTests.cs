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

    // Podaci iz CSV-a: id korisnika koji ne postoji i očekivana poruka izuzetka.
    public static IEnumerable<object[]> KorisnikNePostoji() =>
        CsvLoader.Ucitaj("KorisnikService_korisnikNePostoji.csv")
                 .Select(r => new object[] { r[0], r[1] });

    // Podaci iz XML-a: uloge koje korisnik ima i uloga koju servis treba vratiti.
    public static IEnumerable<object[]> UlogeKorisnika() =>
        XmlLoader.Ucitaj("KorisnikService_uloga.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     ((string)x.Element("uloge")!).Split(',', StringSplitOptions.RemoveEmptyEntries),
                     (string)x.Element("ocekivanaUloga")!
                 });

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
}
