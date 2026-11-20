namespace HealthZone.Services
{
    public class OgranicenjeZakazivanjaService : IOgranicenjeZakazivanjaService
    {
        // Koliko dana unazad se gledaju otkazivanja.
        private const int PeriodUDanima = 30;

        // Od ovoliko otkazivanja u periodu pacijent više ne može zakazati novi termin.
        private const int MaksimalnoOtkazivanja = 3;

        private const string PorukaListaJeNull = "Lista otkazivanja ne smije biti null.";

        public bool MozeZakazatiNoviTermin(IEnumerable<DateTime> datumiOtkazivanja, DateTime sada)
        {
            if (datumiOtkazivanja == null)
                throw new Exception(PorukaListaJeNull);

            return BrojOtkazivanjaUPeriodu(datumiOtkazivanja, sada) < MaksimalnoOtkazivanja;
        }

        // Broje se samo otkazivanja poslije početka perioda i najkasnije do "sada".
        private static int BrojOtkazivanjaUPeriodu(IEnumerable<DateTime> datumiOtkazivanja, DateTime sada)
        {
            var pocetakPerioda = sada.AddDays(-PeriodUDanima);
            return datumiOtkazivanja.Count(datum => datum > pocetakPerioda && datum <= sada);
        }
    }
}
