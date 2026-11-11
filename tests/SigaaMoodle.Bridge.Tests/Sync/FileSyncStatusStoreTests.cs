using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using SigaaMoodle.Bridge.Configuration;
using SigaaMoodle.Bridge.Contracts.Internal;
using SigaaMoodle.Bridge.Sync;

namespace SigaaMoodle.Bridge.Tests.Sync;

public sealed class FileSyncStatusStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"sync-status-{Guid.NewGuid():N}");

    [Fact]
    public void Set_DeveManterResultadoAposReinicio()
    {
        var result = new SyncRunResult(
            new DateTimeOffset(2026, 11, 3, 3, 0, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 11, 3, 3, 2, 30, TimeSpan.FromHours(-3)),
            [new DomainSyncResult(SyncDomain.Alunos, Success: false, Read: 120, Created: 100, Updated: 10,
                Unchanged: 7, Skipped: 2, Errors: [new RecordError("2024300001", "Ana Souza", "Curso sem mapeamento.")],
                Error: "1 registro(s) recusado(s); os demais foram sincronizados.")]);

        Store().Set(result);
        var reloaded = Store().Last;

        reloaded.ShouldNotBeNull();
        reloaded.StartedAt.ShouldBe(result.StartedAt);
        reloaded.FinishedAt.ShouldBe(result.FinishedAt);
        var domain = reloaded.Domains.Single();
        domain.Domain.ShouldBe(SyncDomain.Alunos);
        domain.Success.ShouldBeFalse();
        domain.Read.ShouldBe(120);
        domain.Created.ShouldBe(100);
        domain.Updated.ShouldBe(10);
        domain.Unchanged.ShouldBe(7);
        domain.Skipped.ShouldBe(2);
        domain.Errors.Single().ShouldBe(new RecordError("2024300001", "Ana Souza", "Curso sem mapeamento."));
        domain.Error.ShouldNotBeNull();
    }

    [Fact]
    public void Last_QuandoArquivoAusente_DeveSerNulo()
    {
        Store().Last.ShouldBeNull();
    }

    [Fact]
    public void Last_QuandoArquivoCorrompido_DeveSerNulo()
    {
        var path = Path.Combine(_root, "data", "sync-status.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ invalido");

        Store().Last.ShouldBeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private FileSyncStatusStore Store()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.ContentRootPath.Returns(_root);
        return new FileSyncStatusStore(Options.Create(new SyncOptions()), environment,
            NullLogger<FileSyncStatusStore>.Instance);
    }
}
