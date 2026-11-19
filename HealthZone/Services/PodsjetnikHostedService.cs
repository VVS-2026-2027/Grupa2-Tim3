using HealthZone.Models;

namespace HealthZone.Services
{
    public class PodsjetnikHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PodsjetnikHostedService> _logger;

        // Čekanje i trenutno vrijeme su izdvojeni da bi se u unit testovima mogli zamijeniti.
        // Bez toga bi test morao stvarno čekati do 08:00 sljedećeg dana.
        // U aplikaciji su to obični Task.Delay i DateTime.Now, pa se ponašanje ne mijenja.
        private readonly Func<TimeSpan, CancellationToken, Task> _cekaj;
        private readonly Func<DateTime> _sada;

        // Ovaj konstruktor koristi aplikacija (DI ga bira jer ne zna napraviti Func parametre).
        public PodsjetnikHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<PodsjetnikHostedService> logger)
            : this(scopeFactory, logger, (trajanje, token) => Task.Delay(trajanje, token), () => DateTime.Now)
        {
        }

        // Ovaj konstruktor koriste testovi: čekanje i trenutno vrijeme se prosljeđuju izvana.
        public PodsjetnikHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<PodsjetnikHostedService> logger,
            Func<TimeSpan, CancellationToken, Task> cekaj,
            Func<DateTime> sada)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _cekaj = cekaj;
            _sada = sada;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.WhenAll(
                PodsjetnikLoopAsync(stoppingToken),
                StatusLoopAsync(stoppingToken)
            );
        }

        // Svaki dan u 08:00 šalje podsjetnik email
        private async Task PodsjetnikLoopAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var sada = _sada();
                var sljedeće = sada.Date.AddDays(1).AddHours(8);
                var čekanje = sljedeće - sada;

                _logger.LogInformation("Podsjetnik: sljedeće slanje u {Sljedece}", sljedeće);
                await _cekaj(čekanje, stoppingToken);

                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var terminService = scope.ServiceProvider.GetRequiredService<ITerminService>();
                    await terminService.PošaljiPodsjetnikeSutraAsync();
                    _logger.LogInformation("Podsjetnici poslani za {Datum}", _sada().Date.AddDays(1).ToString("dd.MM.yyyy"));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Greška pri slanju podsjetnika.");
                }
            }
        }

        // Svake minute provjerava i mijenja statuse termina
        private async Task StatusLoopAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var terminService = scope.ServiceProvider.GetRequiredService<ITerminService>();

                    var sada = _sada();

                    var sviTermini = await terminService.GetAllAsync();

                    foreach (var termin in sviTermini)
                    {
                        // Precizno poređenje — datum + vrijeme zajedno
                        var vrijemeTermina = termin.Datum.Date.Add(termin.Vrijeme.ToTimeSpan());

                        // Koliko vremena ostaje do termina
                        var preostalo = vrijemeTermina - sada;

                        if (termin.Status == Status.NaCekanju)
                        {
                            // Ako termin za manje od 24h → treba potvrda 12h ranije
                            // Ako termin za 24h+ → treba potvrda 24h ranije
                            bool trebaPotvrdu = preostalo.TotalHours <= 12;

                            if (trebaPotvrdu)
                            {
                                await terminService.OtkaziTerminAsync(termin.TerminId);
                                _logger.LogInformation(
                                    "Termin {Id} automatski otkazan (nije potvrđen na vrijeme, preostalo {H:F1}h).",
                                    termin.TerminId, preostalo.TotalHours);
                            }
                        }
                        else if (termin.Status == Status.Potvrdjen)
                        {
                            // Termin se desio → postavi na Aktivan
                            if (vrijemeTermina <= sada)
                            {
                                await terminService.OznaciKaoAktivanAsync(termin.TerminId);
                                _logger.LogInformation("Termin {Id} postavljen na Aktivan.", termin.TerminId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Greška pri provjeri statusa termina.");
                }

                await _cekaj(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}