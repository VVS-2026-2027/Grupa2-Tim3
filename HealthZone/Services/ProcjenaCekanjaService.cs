namespace HealthZone.Services
{
    public class ProcjenaCekanjaService : IProcjenaCekanjaService
    {
        // Prva validna pozicija na listi čekanja (pacijent koji je sljedeći na redu).
        private const int PrvaPozicija = 1;

        private const string PorukaNevalidnaPozicija = "Pozicija na listi mora biti veća od 0.";
        private const string PorukaNevalidnoTrajanje = "Trajanje pregleda mora biti veće od 0.";

        // Pacijent čeka dok se ne obave pregledi svih koji su ispred njega na listi.
        public int IzracunajMinuteCekanja(int pozicija, int trajanjePregledaMin)
        {
            if (pozicija < PrvaPozicija)
                throw new Exception(PorukaNevalidnaPozicija);

            if (trajanjePregledaMin <= 0)
                throw new Exception(PorukaNevalidnoTrajanje);

            return BrojPacijenataIspred(pozicija) * trajanjePregledaMin;
        }

        private static int BrojPacijenataIspred(int pozicija) => pozicija - PrvaPozicija;
    }
}
