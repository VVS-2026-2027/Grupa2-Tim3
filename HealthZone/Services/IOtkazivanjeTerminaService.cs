using HealthZone.Models;

namespace HealthZone.Services
{
    public interface IOtkazivanjeTerminaService
    {
        RezultatOtkazivanja ProvjeriOtkazivanje(DateTime pocetakTermina, DateTime sada);
    }
}
