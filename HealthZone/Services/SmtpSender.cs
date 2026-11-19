using System.Net;
using System.Net.Mail;

namespace HealthZone.Services
{
    // Stvarno slanje maila preko System.Net.Mail.SmtpClient.
    // Namjerno nema nikakve logike, samo prosljeđuje poruku SMTP serveru.
    public class SmtpSender : ISmtpSender
    {
        public async Task PosaljiAsync(MailMessage poruka, string host, int port,
            string korisnik, string lozinka, bool ssl)
        {
            using var klijent = new SmtpClient(host, port)
            {
                EnableSsl = ssl,
                Credentials = new NetworkCredential(korisnik, lozinka)
            };

            await klijent.SendMailAsync(poruka);
        }
    }
}
