using HealthZone.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HealthZone.Tests;

// ─── Izvori podataka i pomoćne metode za ApplicationDbContextTests ──────
public static class ApplicationDbContextPodaci
{
    public static DbContextOptions<ApplicationDbContext> Opcije(string imeBaze)
        => new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(imeBaze).Options;

    // Model onako kako je konfigurisan u OnModelCreating (s ToTable, HasPrecision, ...).
    public static IModel DizajnModel(ApplicationDbContext kontekst)
        => kontekst.GetService<IDesignTimeModel>().Model;

    public static IEntityType Entitet(IModel model, string naziv)
        => model.FindEntityType("HealthZone.Models." + naziv)!;

    public static IForeignKey StraniKljuc(IModel model, string entitet, string svojstvo)
        => Entitet(model, entitet).GetForeignKeys().First(fk => fk.Properties.Any(p => p.Name == svojstvo));

    public static IEnumerable<object[]> Tabele() =>
        CsvLoader.Ucitaj("ApplicationDbContext_tabele.csv").Select(r => new object[] { r[0], r[1] });

    public static IEnumerable<object[]> PonasanjaBrisanja() =>
        CsvLoader.Ucitaj("ApplicationDbContext_brisanje.csv").Select(r => new object[] { r[0], r[1], r[2] });

    public static IEnumerable<object[]> Preciznost() =>
        CsvLoader.Ucitaj("ApplicationDbContext_preciznost.csv")
                 .Select(r => new object[] { r[0], r[1], int.Parse(r[2]), int.Parse(r[3]) });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class ApplicationDbContextTests
{
    // Testovi samo čitaju metapodatke modela, pa mogu dijeliti iste opcije (jednu InMemory bazu).
    private static DbContextOptions<ApplicationDbContext> _opcije = null!;

    private ApplicationDbContext _context = null!;
    private IModel _model = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _opcije = ApplicationDbContextPodaci.Opcije(Guid.NewGuid().ToString());
    }

    [TestInitialize]
    public void Setup()
    {
        _context = new ApplicationDbContext(_opcije);
        _model = ApplicationDbContextPodaci.DizajnModel(_context);
    }

    [TestCleanup]
    public void Cleanup() => _context.Dispose();

    [TestMethod]
    [DynamicData(nameof(ApplicationDbContextPodaci.Tabele), typeof(ApplicationDbContextPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public void OnModelCreating_Entitet_MapiraSeNaOcekivanuTabelu(string entitet, string ocekivanaTabela)
    {
        // Act
        var tabela = ApplicationDbContextPodaci.Entitet(_model, entitet).GetTableName();

        // Assert
        Assert.AreEqual(ocekivanaTabela, tabela);
    }

    [TestMethod]
    [DynamicData(nameof(ApplicationDbContextPodaci.PonasanjaBrisanja), typeof(ApplicationDbContextPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void OnModelCreating_StraniKljucNaKorisnika_NemaKaskadnogBrisanja(string entitet, string svojstvo,
        string ocekivanoPonasanje)
    {
        // Act
        var ponasanje = ApplicationDbContextPodaci.StraniKljuc(_model, entitet, svojstvo).DeleteBehavior;

        // Assert
        Assert.AreEqual(Enum.Parse<DeleteBehavior>(ocekivanoPonasanje), ponasanje);
    }

    [TestMethod]
    [DynamicData(nameof(ApplicationDbContextPodaci.Preciznost), typeof(ApplicationDbContextPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void OnModelCreating_DecimalnoPolje_ImaOcekivanuPreciznost(string entitet, string svojstvo,
        int preciznost, int skala)
    {
        // Act
        var rezultat = ApplicationDbContextPodaci.Entitet(_model, entitet).FindProperty(svojstvo)!.GetPrecision();

        // Assert
        Assert.AreEqual((int?)preciznost, rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(ApplicationDbContextPodaci.Preciznost), typeof(ApplicationDbContextPodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Fake")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public void OnModelCreating_DecimalnoPolje_ImaOcekivanuSkalu(string entitet, string svojstvo,
        int preciznost, int skala)
    {
        // Act
        var rezultat = ApplicationDbContextPodaci.Entitet(_model, entitet).FindProperty(svojstvo)!.GetScale();

        // Assert
        Assert.AreEqual((int?)skala, rezultat);
    }
}
