namespace HealthZone.Services
{
    public interface IOgranicenjeZakazivanjaService
    {
        bool MozeZakazatiNoviTermin(IEnumerable<DateTime> datumiOtkazivanja, DateTime sada);
    }
}
