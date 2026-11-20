namespace HealthZone.Services
{
    public class PrikazCekanjaService : IPrikazCekanjaService
    {
        private const int MinutaUSatu = 60;

        private const string PorukaNegativnoVrijeme = "Vrijeme čekanja ne može biti negativno.";
        private const string TekstSljedeci = "Vi ste sljedeći";

        public string FormatirajVrijemeCekanja(int minute)
        {
            if (minute < 0)
                throw new Exception(PorukaNegativnoVrijeme);

            if (minute == 0)
                return TekstSljedeci;

            if (minute < MinutaUSatu)
                return FormatirajMinute(minute);

            var sati = minute / MinutaUSatu;
            var ostatakMinuta = minute % MinutaUSatu;

            return ostatakMinuta == 0
                ? FormatirajSate(sati)
                : $"{FormatirajSate(sati)} {FormatirajMinute(ostatakMinuta)}";
        }

        private static string FormatirajSate(int sati) => $"{sati} h";

        private static string FormatirajMinute(int minute) => $"{minute} min";
    }
}
