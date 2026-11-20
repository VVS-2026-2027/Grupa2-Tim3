namespace HealthZone.Services
{
    public class OgranicenjeZakazivanjaService : IOgranicenjeZakazivanjaService
    {
        public bool MozeZakazatiNoviTermin(IEnumerable<DateTime> datumiOtkazivanja, DateTime sada)
        {
            if (datumiOtkazivanja == null)
            {
                throw new Exception("Lista otkazivanja ne smije biti null.");
            }
            else
            {
                int broj = 0;
                foreach (var datum in datumiOtkazivanja)
                {
                    if (datum > sada.AddDays(-30))
                    {
                        if (datum <= sada)
                        {
                            broj++;
                        }
                    }
                }

                if (broj >= 3)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }
    }
}
