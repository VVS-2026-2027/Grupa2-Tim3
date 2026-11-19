using HealthZone.Models;

namespace HealthZone.Services
{
    public class OtkazivanjeTerminaService : IOtkazivanjeTerminaService
    {
        public RezultatOtkazivanja ProvjeriOtkazivanje(DateTime pocetakTermina, DateTime sada)
        {
            if (pocetakTermina <= sada)
            {
                throw new Exception("Termin je već prošao.");
            }
            else
            {
                if ((pocetakTermina - sada).TotalHours < 2)
                {
                    throw new Exception("Termin se ne može otkazati manje od 2 sata prije početka.");
                }
                else
                {
                    if ((pocetakTermina - sada).TotalHours < 24)
                    {
                        return RezultatOtkazivanja.DozvoljenoUzUpozorenje;
                    }
                    else
                    {
                        return RezultatOtkazivanja.Dozvoljeno;
                    }
                }
            }
        }
    }
}
