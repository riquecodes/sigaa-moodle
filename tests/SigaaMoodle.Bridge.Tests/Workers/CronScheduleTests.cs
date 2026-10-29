using Cronos;
using Shouldly;

namespace SigaaMoodle.Bridge.Tests.Workers;

public class CronScheduleTests
{
    [Fact]
    public void Cron_Default_NoFusoBrasilia_DeveApontarPara3DaManhaLocal()
    {
        // Arrange — cron de 6 campos (com segundos), igual ao usado no worker, no fuso BR (-03).
        var expression = CronExpression.Parse("0 0 3 * * *", CronFormat.IncludeSeconds);
        var brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var from = new DateTimeOffset(2026, 6, 25, 10, 0, 0, TimeSpan.Zero); // 07:00 em BR

        // Act
        var next = expression.GetNextOccurrence(from, brasilia);

        // Assert — Cronos retorna em UTC: 03:00 BR (-03) == 06:00 UTC.
        next.ShouldNotBeNull();
        next!.Value.UtcDateTime.Hour.ShouldBe(6);
        TimeZoneInfo.ConvertTime(next.Value, brasilia).Hour.ShouldBe(3);
        next.Value.UtcDateTime.Date.ShouldBe(new DateTime(2026, 6, 26)); // próxima madrugada
    }

    [Fact]
    public void FusoBrasilia_DeveTerOffsetDeMenos3Horas()
    {
        var brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        brasilia.GetUtcOffset(new DateTimeOffset(2026, 6, 25, 0, 0, 0, TimeSpan.Zero))
            .ShouldBe(TimeSpan.FromHours(-3));
    }
}
