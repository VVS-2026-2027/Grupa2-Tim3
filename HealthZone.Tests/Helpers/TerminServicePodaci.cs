using System.Globalization;
using System.Xml.Linq;
using HealthZone.Models;

namespace HealthZone.Tests;

// Izvori podataka za [DynamicData] u TerminServiceTests.
// Svaka metoda čita jedan CSV ili XML fajl iz TestData i pretvara redove u parametre testa.
public static class TerminServicePodaci
{
    // ───────────── CSV ─────────────

    public static IEnumerable<object[]> NevalidnoVrijeme() =>
        CsvLoader.Ucitaj("TerminService_nevalidnoVrijeme.csv")
                 .Select(r => new object[] { int.Parse(r[0]), double.Parse(r[1], CultureInfo.InvariantCulture), r[2] });

    public static IEnumerable<object[]> TerminNePostoji() =>
        CsvLoader.Ucitaj("TerminService_terminNePostoji.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> GreskaEmaila() =>
        CsvLoader.Ucitaj("TerminService_greskaEmaila.csv")
                 .Select(r => new object[] { r[0], r[1], int.Parse(r[2]), int.Parse(r[3]), r[4] });

    public static IEnumerable<object[]> Otkazivanje() =>
        CsvLoader.Ucitaj("TerminService_otkazivanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> OtkazivanjeStatus() =>
        CsvLoader.Ucitaj("TerminService_otkazivanjeStatus.csv")
                 .Select(r => new object[] { int.Parse(r[0]), Enum.Parse<Status>(r[1]) });

    public static IEnumerable<object[]> PromjenaTermina() =>
        CsvLoader.Ucitaj("TerminService_promjenaTermina.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), int.Parse(r[2]) });

    public static IEnumerable<object[]> PromjenaTerminaStatus() =>
        CsvLoader.Ucitaj("TerminService_promjenaTermina.csv")
                 .Select(r => new object[] { int.Parse(r[0]), int.Parse(r[1]), int.Parse(r[2]), Enum.Parse<Status>(r[3]) });

    public static IEnumerable<object[]> Podsjetnici() =>
        CsvLoader.Ucitaj("TerminService_podsjetnici.csv")
                 .Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> PodsjetniciGreska() =>
        CsvLoader.Ucitaj("TerminService_podsjetnici.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1] });

    public static IEnumerable<object[]> SlobodniTermini() =>
        CsvLoader.Ucitaj("TerminService_slobodniTermini.csv")
                 .Select(r => new object[] { r[0], int.Parse(r[1]), int.Parse(r[2]) });

    public static IEnumerable<object[]> TerminiDoktoraNaDan() =>
        CsvLoader.Ucitaj("TerminService_terminiDoktoraNaDan.csv")
                 .Select(r => new object[] { r[0], int.Parse(r[1]), int.Parse(r[2]) });

    public static IEnumerable<object[]> TerminiDoktora() =>
        CsvLoader.Ucitaj("TerminService_terminiDoktora.csv")
                 .Select(r => new object[] { r[0], int.Parse(r[1]) });

    public static IEnumerable<object[]> Dostupnost() =>
        CsvLoader.Ucitaj("TerminService_dostupnost.csv")
                 .Select(r => new object[] { r[0], int.Parse(r[1]), int.Parse(r[2]), bool.Parse(r[3]) });

    public static IEnumerable<object[]> PostojeciTermin() =>
        CsvLoader.Ucitaj("TerminService_postojeciTermin.csv")
                 .Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> Snimanje() =>
        CsvLoader.Ucitaj("TerminService_snimanje.csv")
                 .Select(r => new object[] { int.Parse(r[0]) });

    public static IEnumerable<object[]> NotifikacijaPodaci() =>
        CsvLoader.Ucitaj("TerminService_notifikacija.csv")
                 .Select(r => new object[] { int.Parse(r[0]), r[1], Enum.Parse<Status>(r[2]) });

    // ───────────── XML ─────────────

    public static IEnumerable<object[]> Zakazivanje() =>
        XmlLoader.Ucitaj("TerminService_zakazivanje.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("doktorId")!, (string)x.Element("pacijentEmail")!,
                     (int)x.Element("pomakDana")!, (int)x.Element("sat")!
                 });

    public static IEnumerable<object[]> ZakazivanjeStatus() =>
        XmlLoader.Ucitaj("TerminService_zakazivanje.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("doktorId")!, (string)x.Element("pacijentEmail")!,
                     (int)x.Element("pomakDana")!, (int)x.Element("sat")!,
                     Enum.Parse<Status>((string)x.Element("ocekivaniStatus")!)
                 });

    public static IEnumerable<object[]> ZakazivanjeEmail() =>
        XmlLoader.Ucitaj("TerminService_zakazivanje.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (string)x.Element("doktorId")!, (string)x.Element("pacijentEmail")!,
                     (int)x.Element("pomakDana")!, (int)x.Element("sat")!,
                     (int)x.Element("ocekivaniBrojMailova")!
                 });

    public static IEnumerable<object[]> NedostupanZakazivanje() =>
        XmlLoader.Ucitaj("TerminService_nedostupan.xml", "zakazivanje")
                 .Select(x => new object[]
                 {
                     (string)x.Element("doktorId")!, (int)x.Element("pomakDana")!,
                     (int)x.Element("sat")!, (string)x.Element("poruka")!
                 });

    public static IEnumerable<object[]> NedostupanPromjena() =>
        XmlLoader.Ucitaj("TerminService_nedostupan.xml", "promjena")
                 .Select(x => new object[]
                 {
                     (int)x.Element("terminId")!, (int)x.Element("pomakDana")!,
                     (int)x.Element("sat")!, (string)x.Element("poruka")!
                 });

    public static IEnumerable<object[]> OtkazanPotvrda() => OtkazanTermin("potvrda");
    public static IEnumerable<object[]> OtkazanOtkazivanje() => OtkazanTermin("otkazivanje");
    public static IEnumerable<object[]> OtkazanPromjena() => OtkazanTermin("promjena");

    private static IEnumerable<object[]> OtkazanTermin(string element) =>
        XmlLoader.Ucitaj("TerminService_otkazanTermin.xml", element)
                 .Select(x => new object[] { (int)x.Element("terminId")!, (string)x.Element("poruka")! });

    public static IEnumerable<object[]> StatusPotvrda() => Statusi("potvrda");
    public static IEnumerable<object[]> StatusAktivacija() => Statusi("aktivacija");

    private static IEnumerable<object[]> Statusi(string element) =>
        XmlLoader.Ucitaj("TerminService_statusi.xml", element)
                 .Select(x => new object[]
                 {
                     (int)x.Element("terminId")!, Enum.Parse<Status>((string)x.Element("ocekivaniStatus")!)
                 });

    public static IEnumerable<object[]> KandidatiNaListi() =>
        XmlLoader.Ucitaj("TerminService_listaCekanja.xml", "scenarij")
                 .Select(x => new object[]
                 {
                     (int)x.Element("terminId")!,
                     x.Elements("kandidat").Select(TerminTestFactory.NapraviKandidata).ToList(),
                     (string)x.Element("ocekivaniEmail")!
                 });

    public static IEnumerable<object[]> BrojTerminaPacijenta() =>
        XmlLoader.Ucitaj("TerminService_terminiPacijenta.xml", "scenarij")
                 .Select(x => new object[] { (string)x.Element("pacijentId")!, (int)x.Element("ocekivaniBroj")! });

    public static IEnumerable<object[]> NajnovijiTerminPacijenta() =>
        XmlLoader.Ucitaj("TerminService_terminiPacijenta.xml", "scenarij")
                 .Select(x => new object[] { (string)x.Element("pacijentId")!, (int)x.Element("ocekivaniPrviId")! });
}
