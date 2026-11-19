using System.Net.Mail;
using System.Xml.Linq;
using HealthZone.Services;
using Microsoft.Extensions.Options;
using Moq;

namespace HealthZone.Tests;

// Jedan red iz EmailService_obavjestenja.csv (jedna vrsta maila).
public sealed class ObavjestenjeRed
{
    public string Metoda { get; init; } = null!;
    public string Email { get; init; } = null!;
    public string Ime { get; init; } = null!;
    public string Doktor { get; init; } = null!;
    public DateTime Datum { get; init; }
    public TimeOnly Vrijeme { get; init; }
    public string Usluga { get; init; } = null!;
    public int TerminId { get; init; }
    public string OcekivaniNaslov { get; init; } = null!;
}

// ─── Izvori podataka i pomoćne metode za EmailServiceTests ──────────────
public static class EmailServicePodaci
{
    private static XElement PostavkeXml() => XmlLoader.Ucitaj("EmailService_postavke.xml", "postavke").Single();

    public static EmailSettings Postavke()
    {
        var x = PostavkeXml();
        return new EmailSettings
        {
            Host = (string)x.Attribute("host")!,
            Port = (int)x.Attribute("port")!,
            EnableSsl = (bool)x.Attribute("ssl")!,
            Username = (string)x.Attribute("korisnik")!,
            Password = (string)x.Attribute("lozinka")!,
            FromEmail = (string)x.Attribute("fromEmail")!,
            FromName = (string)x.Attribute("fromName")!
        };
    }

    public static string FormatDatuma() => (string)PostavkeXml().Attribute("formatDatuma")!;
    public static string FormatVremena() => (string)PostavkeXml().Attribute("formatVremena")!;

    private static IEnumerable<string[]> ObavjestenjaCsv() => CsvLoader.Ucitaj("EmailService_obavjestenja.csv");

    public static ObavjestenjeRed Obavjestenje(string metoda)
    {
        var r = ObavjestenjaCsv().Single(red => red[0] == metoda);
        return new ObavjestenjeRed
        {
            Metoda = r[0],
            Email = r[1],
            Ime = r[2],
            Doktor = r[3],
            Datum = DateTime.Today.AddDays(int.Parse(r[4])),
            Vrijeme = TimeOnly.Parse(r[5]),
            Usluga = r[6],
            TerminId = int.Parse(r[7]),
            OcekivaniNaslov = r[8]
        };
    }

    // Poziva metodu servisa čije je ime u redu (svaka metoda ima drugačije parametre).
    public static Task Posalji(EmailService servis, ObavjestenjeRed r)
    {
        var akcije = new Dictionary<string, Func<Task>>
        {
            [nameof(EmailService.PošaljiPotvrduTerminaAsync)] =
                () => servis.PošaljiPotvrduTerminaAsync(r.Email, r.Ime, r.Doktor, r.Datum, r.Vrijeme, r.Usluga, r.TerminId),
            [nameof(EmailService.PošaljiOtkazTerminaAsync)] =
                () => servis.PošaljiOtkazTerminaAsync(r.Email, r.Ime, r.Doktor, r.Datum, r.Vrijeme),
            [nameof(EmailService.PošaljiPodsjetnikAsync)] =
                () => servis.PošaljiPodsjetnikAsync(r.Email, r.Ime, r.Doktor, r.Datum, r.Vrijeme, r.Usluga),
            [nameof(EmailService.PošaljiObavještenjeListe)] =
                () => servis.PošaljiObavještenjeListe(r.Email, r.Ime, r.Doktor, r.Datum, r.Vrijeme)
        };
        return akcije[r.Metoda]();
    }

    public static IEnumerable<object[]> SvaObavjestenja() =>
        ObavjestenjaCsv().Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> ObavjestenjaSUslugom() =>
        ObavjestenjaCsv().Where(r => r[6] != string.Empty).Select(r => new object[] { r[0] });

    public static IEnumerable<object[]> LinkoviPotvrde() =>
        CsvLoader.Ucitaj("EmailService_linkovi.csv")
                 .Select(r => new object[] { r[0], r[1], r[2], int.Parse(r[3]), r[4], r[5], int.Parse(r[6]), r[7], r[8] });
}

// ─── Testovi ────────────────────────────────────────────────────────────
[TestClass]
public class EmailServiceTests
{
    private static EmailSettings _postavke = null!;   // samo za čitanje
    private static string _formatDatuma = null!;
    private static string _formatVremena = null!;

    private Mock<ISmtpSender> _smtp = null!;
    private EmailService _servis = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // SMTP postavke i formati datuma se čitaju jednom iz XML-a.
        _postavke = EmailServicePodaci.Postavke();
        _formatDatuma = EmailServicePodaci.FormatDatuma();
        _formatVremena = EmailServicePodaci.FormatVremena();
    }

    [TestInitialize]
    public void Setup()
    {
        _smtp = new Mock<ISmtpSender>();
        _servis = new EmailService(Options.Create(_postavke), _smtp.Object);
    }

    [TestCleanup]
    public void Cleanup() => _smtp.Reset();

    [TestMethod]
    [DynamicData(nameof(EmailServicePodaci.SvaObavjestenja), typeof(EmailServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task PosaljiMail_SvakaVrstaObavjestenja_ImaOcekivaniNaslov(string metoda)
    {
        // Arrange
        var red = EmailServicePodaci.Obavjestenje(metoda);

        // Act
        await EmailServicePodaci.Posalji(_servis, red);

        // Assert
        _smtp.Verify(s => s.PosaljiAsync(It.Is<MailMessage>(m => m.Subject == red.OcekivaniNaslov),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(EmailServicePodaci.SvaObavjestenja), typeof(EmailServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task PosaljiMail_SvakaVrstaObavjestenja_SaljeSeNaEmailPacijenta(string metoda)
    {
        // Arrange
        var red = EmailServicePodaci.Obavjestenje(metoda);

        // Act
        await EmailServicePodaci.Posalji(_servis, red);

        // Assert
        _smtp.Verify(s => s.PosaljiAsync(It.Is<MailMessage>(m => m.To.Single().Address == red.Email),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(EmailServicePodaci.SvaObavjestenja), typeof(EmailServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PosaljiMail_SvakaVrstaObavjestenja_PosiljalacJeIzPostavki(string metoda)
    {
        // Arrange
        var red = EmailServicePodaci.Obavjestenje(metoda);

        // Act
        await EmailServicePodaci.Posalji(_servis, red);

        // Assert
        _smtp.Verify(s => s.PosaljiAsync(
            It.Is<MailMessage>(m => m.From!.Address == _postavke.FromEmail && m.From.DisplayName == _postavke.FromName),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(EmailServicePodaci.SvaObavjestenja), typeof(EmailServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PosaljiMail_SvakaVrstaObavjestenja_PorukaJeHtml(string metoda)
    {
        // Arrange
        var red = EmailServicePodaci.Obavjestenje(metoda);

        // Act
        await EmailServicePodaci.Posalji(_servis, red);

        // Assert
        _smtp.Verify(s => s.PosaljiAsync(It.Is<MailMessage>(m => m.IsBodyHtml),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(EmailServicePodaci.SvaObavjestenja), typeof(EmailServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PosaljiMail_SvakaVrstaObavjestenja_TijeloSadrziImePacijenta(string metoda)
    {
        // Arrange
        var red = EmailServicePodaci.Obavjestenje(metoda);

        // Act
        await EmailServicePodaci.Posalji(_servis, red);

        // Assert
        _smtp.Verify(s => s.PosaljiAsync(It.Is<MailMessage>(m => m.Body.Contains(red.Ime)),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(EmailServicePodaci.SvaObavjestenja), typeof(EmailServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PosaljiMail_SvakaVrstaObavjestenja_TijeloSadrziDoktora(string metoda)
    {
        // Arrange
        var red = EmailServicePodaci.Obavjestenje(metoda);

        // Act
        await EmailServicePodaci.Posalji(_servis, red);

        // Assert
        _smtp.Verify(s => s.PosaljiAsync(It.Is<MailMessage>(m => m.Body.Contains(red.Doktor)),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(EmailServicePodaci.SvaObavjestenja), typeof(EmailServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PosaljiMail_SvakaVrstaObavjestenja_TijeloSadrziDatumTermina(string metoda)
    {
        // Arrange
        var red = EmailServicePodaci.Obavjestenje(metoda);

        // Act
        await EmailServicePodaci.Posalji(_servis, red);

        // Assert
        _smtp.Verify(s => s.PosaljiAsync(It.Is<MailMessage>(m => m.Body.Contains(red.Datum.ToString(_formatDatuma))),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(EmailServicePodaci.SvaObavjestenja), typeof(EmailServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PosaljiMail_SvakaVrstaObavjestenja_TijeloSadrziVrijemeTermina(string metoda)
    {
        // Arrange
        var red = EmailServicePodaci.Obavjestenje(metoda);

        // Act
        await EmailServicePodaci.Posalji(_servis, red);

        // Assert
        _smtp.Verify(s => s.PosaljiAsync(It.Is<MailMessage>(m => m.Body.Contains(red.Vrijeme.ToString(_formatVremena))),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(EmailServicePodaci.ObavjestenjaSUslugom), typeof(EmailServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PosaljiMail_ObavjestenjeSUslugom_TijeloSadrziUslugu(string metoda)
    {
        // Arrange
        var red = EmailServicePodaci.Obavjestenje(metoda);

        // Act
        await EmailServicePodaci.Posalji(_servis, red);

        // Assert
        _smtp.Verify(s => s.PosaljiAsync(It.Is<MailMessage>(m => m.Body.Contains(red.Usluga)),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(EmailServicePodaci.SvaObavjestenja), typeof(EmailServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    [Priority(1)]
    public async Task PosaljiMail_SvakaVrstaObavjestenja_KoristiSmtpPostavke(string metoda)
    {
        // Arrange
        var red = EmailServicePodaci.Obavjestenje(metoda);

        // Act
        await EmailServicePodaci.Posalji(_servis, red);

        // Assert
        _smtp.Verify(s => s.PosaljiAsync(It.IsAny<MailMessage>(), _postavke.Host, _postavke.Port,
            _postavke.Username, _postavke.Password, _postavke.EnableSsl), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(EmailServicePodaci.LinkoviPotvrde), typeof(EmailServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PosaljiPotvrduTerminaAsync_ZakazanTermin_TijeloSadrziLinkZaPotvrdu(string email, string ime,
        string doktor, int pomakDana, string vrijeme, string usluga, int terminId, string linkPotvrde, string linkOtkaza)
    {
        // Act
        await _servis.PošaljiPotvrduTerminaAsync(email, ime, doktor, DateTime.Today.AddDays(pomakDana),
            TimeOnly.Parse(vrijeme), usluga, terminId);

        // Assert
        _smtp.Verify(s => s.PosaljiAsync(It.Is<MailMessage>(m => m.Body.Contains(linkPotvrde)),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(EmailServicePodaci.LinkoviPotvrde), typeof(EmailServicePodaci), DynamicDataSourceType.Method)]
    [TestCategory("DataDriven"), TestCategory("Mock")]
    [TestProperty("Autor", "Ahmed Spahic")]
    public async Task PosaljiPotvrduTerminaAsync_ZakazanTermin_TijeloSadrziLinkZaOtkaz(string email, string ime,
        string doktor, int pomakDana, string vrijeme, string usluga, int terminId, string linkPotvrde, string linkOtkaza)
    {
        // Act
        await _servis.PošaljiPotvrduTerminaAsync(email, ime, doktor, DateTime.Today.AddDays(pomakDana),
            TimeOnly.Parse(vrijeme), usluga, terminId);

        // Assert
        _smtp.Verify(s => s.PosaljiAsync(It.Is<MailMessage>(m => m.Body.Contains(linkOtkaza)),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }
}
