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
    public async Task RunAsync_QuandoAlunoFalha_DeveRegistrarFalha()
    {
        // Arrange
        var handler = Substitute.For<ISyncDomainHandler>();
        handler.Domain.Returns(SyncDomain.Alunos);
        handler.SyncAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns<Task<DomainSyncResult>>(_ => throw new HttpRequestException("SIGAA fora"));

        var state = new AlwaysFullSyncStateProvider();
        var store = new InMemorySyncStatusStore();
        using var gate = new SyncExecutionGate();
        var service = new SyncService([handler], state, store, NullLogger<SyncService>.Instance,
            Options.Create(new SyncOptions { EnabledDomains = ["Alunos"] }), gate);

        // Act
        var result = await service.RunAsync(domains: null, CancellationToken.None);

        // Assert
        var falha = result.Domains.Single();
        falha.Domain.ShouldBe(SyncDomain.Alunos);
        falha.Success.ShouldBeFalse();
        falha.Error!.ShouldContain("SIGAA fora");
        result.AllSucceeded.ShouldBeFalse();
        store.Last.ShouldBe(result);
    }
}
