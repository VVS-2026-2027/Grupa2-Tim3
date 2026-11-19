using System.Xml.Linq;
using HealthZone.Models;

namespace HealthZone.Tests;

// Pomoćne metode koje od podataka iz TestData prave objekte modela za testove TerminService.
public static class TerminTestFactory
{
    // Pravi novu listu termina iz XML elemenata ("baza" koju vraća mock ITerminRepository).
    public static List<Termin> NapraviBazu(IEnumerable<XElement> elementi) =>
        elementi.Select(NapraviTermin).ToList();

    // Vraća termin s traženim ID-jem iz "baze".
    public static Termin Pronadji(IEnumerable<Termin> baza, int terminId) =>
        baza.Single(t => t.TerminId == terminId);

    public static Termin NapraviTermin(XElement x) => new Termin
    {
        TerminId = (int)x.Element("id")!,
        DoktorID = (string)x.Element("doktorId")!,
        Doktor = x.Element("doktorIme") is null
            ? null
            : new Korisnik { Ime = (string)x.Element("doktorIme")!, Prezime = (string)x.Element("doktorPrezime")! },
        PacijentID = (string)x.Element("pacijentId")!,
        Pacijent = new Korisnik { Email = PrazanUNull((string)x.Element("email")!) },
        Datum = DateTime.Today.AddDays((int)x.Element("pomakDana")!),
        Vrijeme = new TimeOnly((int)x.Element("sat")!, 0),
        Status = Enum.Parse<Status>((string)x.Element("status")!)
    };

    // Novi termin koji pacijent želi zakazati (još nije u "bazi").
    public static Termin NoviTermin(string doktorId, string pacijentEmail, int pomakDana, int sat) => new Termin
    {
        DoktorID = doktorId,
        Datum = DateTime.Today.AddDays(pomakDana),
        Vrijeme = new TimeOnly(sat, 0),
        Pacijent = new Korisnik { Email = PrazanUNull(pacijentEmail) }
    };

    // Termin čiji je početak pomaknut za zadani broj sati od trenutnog vremena.
    public static Termin TerminSaPomakom(int terminId, double pomakSati)
    {
        var vrijeme = DateTime.Now.AddHours(pomakSati);
        return new Termin { TerminId = terminId, Datum = vrijeme.Date, Vrijeme = TimeOnly.FromDateTime(vrijeme) };
    }

    public static KorisnikNaListi NapraviKandidata(XElement x) => new KorisnikNaListi
    {
        Datum = DateTime.Today.AddDays((int)x.Element("pomakDana")!),
        Lista = new ListaCekanja { DoktorID = (string)x.Element("doktorId")! },
        Korisnik = new Korisnik
        {
            Email = (string)x.Element("email")!,
            Prioritet = Enum.Parse<Prioritet>((string)x.Element("prioritet")!)
        }
    };

    // Prazan string iz CSV/XML-a znači da pacijent nema email.
    public static string? PrazanUNull(string tekst) => string.IsNullOrEmpty(tekst) ? null : tekst;
}
