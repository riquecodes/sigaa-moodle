namespace SigaaMoodle.Bridge.Configuration;

public sealed class SyncOptions
{
    public const string SectionName = "Sync";

    // Cron da madrugada no fuso configurado em TimeZone. Default 03:00. Formato de 6 campos (com segundos).
    public string Cron { get; set; } = "0 0 3 * * *";

    // Fuso (IANA) usado para interpretar o Cron. Brasil = America/Sao_Paulo (-03, sem horário de verão).
    public string TimeZone { get; set; } = "America/Sao_Paulo";

    // Liga/desliga o worker agendado (endpoint manual continua funcionando).
    public bool ScheduledEnabled { get; set; } = false;

    // Seam de delta: false = manda tudo (atual); true = só alterados (futuro).
    public bool DeltaEnabled { get; set; } = false;

    // Domínios habilitados.
    public string[] EnabledDomains { get; set; } = ["Alunos"];
}
