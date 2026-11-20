namespace HealthZone.Services
{
    public class PrikazCekanjaService : IPrikazCekanjaService
    {
        public string FormatirajVrijemeCekanja(int minute)
        {
            if (minute < 0)
            {
                throw new Exception("Vrijeme čekanja ne može biti negativno.");
            }
            else
            {
                if (minute == 0)
                {
                    return "Vi ste sljedeći";
                }
                else
                {
                    if (minute < 60)
                    {
                        return minute + " min";
                    }
                    else
                    {
                        if (minute % 60 == 0)
                        {
                            return minute / 60 + " h";
                        }
                        else
                        {
                            return minute / 60 + " h " + minute % 60 + " min";
                        }
                    }
                }
            }
        }
    }
}
