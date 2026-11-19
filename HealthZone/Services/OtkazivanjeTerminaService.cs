using HealthZone.Models;

namespace HealthZone.Services
{
    public class OtkazivanjeTerminaService : IOtkazivanjeTerminaService
    {
        // Najmanje vremena prije početka termina kada se termin još smije otkazati.
        private const double MinimalnoSatiZaOtkazivanje = 2;

        // Od ovoliko sati prije početka otkazivanje je dozvoljeno bez upozorenja.
        private const double SatiZaOtkazivanjeBezUpozorenja = 24;

        private const string PorukaTerminProsao = "Termin je već prošao.";
        private const string PorukaPrekasnoZaOtkazivanje = "Termin se ne može otkazati manje od 2 sata prije početka.";

        public RezultatOtkazivanja ProvjeriOtkazivanje(DateTime pocetakTermina, DateTime sada)
        {
            var satiDoTermina = (pocetakTermina - sada).TotalHours;

            if (satiDoTermina <= 0)
                throw new Exception(PorukaTerminProsao);

            if (satiDoTermina < MinimalnoSatiZaOtkazivanje)
                throw new Exception(PorukaPrekasnoZaOtkazivanje);

            return satiDoTermina < SatiZaOtkazivanjeBezUpozorenja
                ? RezultatOtkazivanja.DozvoljenoUzUpozorenje
                : RezultatOtkazivanja.Dozvoljeno;
        }
    }
}
