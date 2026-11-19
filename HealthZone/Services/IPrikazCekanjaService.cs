namespace HealthZone.Services
{
    public interface IPrikazCekanjaService
    {
        // Pretvara procijenjeno vrijeme čekanja (u minutama) u tekst koji pacijent vidi.
        string FormatirajVrijemeCekanja(int minute);
    }
}
