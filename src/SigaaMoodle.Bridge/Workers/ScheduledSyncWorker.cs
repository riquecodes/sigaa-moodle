using Cronos;
using Microsoft.Extensions.Options;
using SigaaMoodle.Bridge.Configuration;
using SigaaMoodle.Bridge.Sync;

namespace SigaaMoodle.Bridge.Workers;

// Roda a sincronização na madrugada conforme o cron configurado (UTC).
public sealed class ScheduledSyncWorker(
    IServiceProvider services,
    IOptions<SyncOptions> options,
    ILogger<ScheduledSyncWorker> logger) : BackgroundService
{
    private readonly SyncOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.ScheduledEnabled)
        {
            logger.LogInformation("Worker agendado desabilitado (Sync:ScheduledEnabled=false).");
            return;
        }

        var cron = CronExpression.Parse(_options.Cron, CronFormat.IncludeSeconds);
        var timeZone = ResolveTimeZone(_options.TimeZone, logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            var next = cron.GetNextOccurrence(DateTimeOffset.UtcNow, timeZone);
            if (next is null)
            {
                logger.LogWarning("Cron '{Cron}' sem próxima ocorrência. Encerrando worker.", _options.Cron);
                return;
            }

            var delay = next.Value - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                try { await Task.Delay(delay, stoppingToken); }
                catch (OperationCanceledException) { return; }
            }

            try
            {
                using var scope = services.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<ISyncService>();
                logger.LogInformation("Disparando sincronização agendada da madrugada.");
                await sync.RunAsync(domains: null, stoppingToken);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro na execução agendada de sincronização.");
            }
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string id, ILogger logger)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Fallback: Brasil é -03 fixo (sem horário de verão desde 2019).
            logger.LogWarning(ex, "Fuso '{TimeZone}' não encontrado. Usando offset fixo -03:00.", id);
            return TimeZoneInfo.CreateCustomTimeZone("BRT-3", TimeSpan.FromHours(-3), "Horário de Brasília", "BRT");
        }
    }
}
