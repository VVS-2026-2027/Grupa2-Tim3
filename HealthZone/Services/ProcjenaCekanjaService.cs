namespace HealthZone.Services
{
    public class ProcjenaCekanjaService : IProcjenaCekanjaService
    {
        public int IzracunajMinuteCekanja(int pozicija, int trajanjePregledaMin)
        {
            if (pozicija < 1)
            {
                throw new Exception("Pozicija na listi mora biti veća od 0.");
            }
            else
            {
                if (trajanjePregledaMin <= 0)
                {
                    throw new Exception("Trajanje pregleda mora biti veće od 0.");
                }
                else
                {
                    int minute = 0;
                    for (int i = 1; i < pozicija; i++)
                    {
                        minute = minute + trajanjePregledaMin;
                    }
                    return minute;
                }
            }
        }
    }
}
