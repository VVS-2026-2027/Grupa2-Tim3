namespace HealthZone.Services
{
    public interface IProcjenaCekanjaService
    {
        // Procjena koliko minuta pacijent čeka na pregled, prema poziciji na listi čekanja.
        int IzracunajMinuteCekanja(int pozicija, int trajanjePregledaMin);
    }
}
