using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SigaaMoodle.Bridge.Configuration;
using NSubstitute;
using Shouldly;
using SigaaMoodle.Bridge.Sync;
using SigaaMoodle.Bridge.Sync.Handlers;

namespace SigaaMoodle.Bridge.Tests.Sync;

public class SyncServiceTests
{
    [Fact]
    public async Task RunAsync_QuandoUmDominioFalha_DeveContinuarOsOutros()
    {
        // Arrange
        var ok = Substitute.For<ISyncDomainHandler>();
        ok.Domain.Returns(SyncDomain.Alunos);
        ok.SyncAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(new DomainSyncResult(SyncDomain.Alunos, true, 2, 2, null));

        var bad = Substitute.For<ISyncDomainHandler>();
        bad.Domain.Returns(SyncDomain.Notas);
        bad.SyncAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns<Task<DomainSyncResult>>(_ => throw new HttpRequestException("SIGAA fora"));

        var state = new AlwaysFullSyncStateProvider();
        var store = new InMemorySyncStatusStore();
        using var gate = new SyncExecutionGate();
        var service = new SyncService([ok, bad], state, store, NullLogger<SyncService>.Instance,
            Options.Create(new SyncOptions { EnabledDomains = ["Alunos", "Notas"] }), gate);

        // Act
        var result = await service.RunAsync(domains: null, CancellationToken.None);

        // Assert
        result.Domains.Count.ShouldBe(2);
        result.Domains.First(d => d.Domain == SyncDomain.Alunos).Success.ShouldBeTrue();
        var falha = result.Domains.First(d => d.Domain == SyncDomain.Notas);
        falha.Success.ShouldBeFalse();
        falha.Error!.ShouldContain("SIGAA fora");
        result.AllSucceeded.ShouldBeFalse();
        store.Last.ShouldBe(result);
    }
}
