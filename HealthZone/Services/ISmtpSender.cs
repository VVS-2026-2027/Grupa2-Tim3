using System.Net.Mail;

namespace HealthZone.Services
{
    // Apstrakcija nad slanjem maila preko SMTP-a.
    // EmailService i NalazService šalju mail kroz ovaj interfejs,
    // pa se u unit testovima može zamijeniti mockom (bez pravog SMTP servera).
    public interface ISmtpSender
    {
        Task PosaljiAsync(MailMessage poruka, string host, int port,
            string korisnik, string lozinka, bool ssl);
    }
}
