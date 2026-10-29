using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using SigaaMoodle.Bridge.Clients.Internal;
using SigaaMoodle.Bridge.Clients.Sigaa;
using SigaaMoodle.Bridge.Configuration;
using SigaaMoodle.Bridge.Sync;
using SigaaMoodle.Bridge.Sync.Handlers;
using SigaaMoodle.Bridge.Tests.Clients;

namespace SigaaMoodle.Bridge.Tests.Sync;

public class BridgeFlowTests
{
    [Fact]
    public async Task FluxoCompleto_OAuthAlunosVinculosBaseLocal_ReenvioSemDuplicacao()
    {
        var users = new Dictionary<int, JsonElement>();
        var posts = 0;
        using var transport = new StubHttpHandler((request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath == "/oauth/token")
                return Task.FromResult(StubHttpHandler.Json("""{"access_token":"test-token","expires_in":3600}"""));
            request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
            request.Headers.Authorization.Parameter.ShouldBe("test-token");
            return Task.FromResult(StubHttpHandler.Json("""
                {"current_page":1,"per_page":100,"total":2,"data":[
                  {"login":"00000000000","nome_completo":"Maria","email":null,"cursos":[
                    {"matricula":"2023001","id_curso":42},{"matricula":"2024001","id_curso":42}]},
                  {"login":"00000000001","nome_completo":"Especial","tipo_aluno":"especial"}]}
                """));
        });
        using var tokenHttp = new HttpClient(transport, disposeHandler: false);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("sigaa-token").Returns(tokenHttp);
        var sourceOptions = Options.Create(new SigaaOptions
        {
            TokenUrl = "https://sigaa.test/oauth/token", ClientId = "client", ClientSecret = "test-secret",
            Routes = new() { Alunos = "sig/sigaa/alunos/paginate" }
        });
        using var tokenHandler = new SigaaTokenHandler(new SigaaTokenProvider(factory, sourceOptions)) { InnerHandler = transport };
        using var sourceHttp = new HttpClient(tokenHandler) { BaseAddress = new Uri("https://sigaa.test/api/v1/") };
        using var destinationHandler = new StubHttpHandler(async (request, ct) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                var id = int.Parse(request.RequestUri!.Segments.Last());
                return users.TryGetValue(id, out var existing) ? StubHttpHandler.Json(existing.GetRawText())
                    : new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            request.Method.ShouldBe(HttpMethod.Post);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var item = body.RootElement[0];
            users.Add(item.GetProperty("enrollmentNumber").GetInt32(), item.Clone());
            posts++;
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        using var destinationHttp = new HttpClient(destinationHandler) { BaseAddress = new Uri("https://local.test/") };
        var source = new SigaaClient(sourceHttp, sourceOptions);
        var destination = new InternalAppClient(destinationHttp, Options.Create(new InternalAppOptions
        {
            StudentUserType = 3, CourseMap = new() { [42] = 7 }, Routes = new() { Alunos = "api/User" }
        }));
        using var gate = new SyncExecutionGate();
        var service = new SyncService([new AlunosSyncHandler(source, destination)], new AlwaysFullSyncStateProvider(),
            new InMemorySyncStatusStore(), NullLogger<SyncService>.Instance, Options.Create(new SyncOptions()), gate);
        for (var run = 0; run < 2; run++)
        {
            var result = await service.RunAsync(null, default);
            result.AllSucceeded.ShouldBeTrue();
            result.Domains.Single().Read.ShouldBe(2);
            result.Domains.Single().Sent.ShouldBe(2);
        }
        users.Count.ShouldBe(2);
        posts.ShouldBe(2);
    }

    [Fact]
    public async Task DisparoGeral_RespeitaDominiosHabilitados_EBloqueiaDesabilitado()
    {
        var enabled = Substitute.For<ISyncDomainHandler>();
        enabled.Domain.Returns(SyncDomain.Alunos);
        enabled.SyncAsync(null, Arg.Any<CancellationToken>()).Returns(new DomainSyncResult(SyncDomain.Alunos, true, 0, 0, null));
        var disabled = Substitute.For<ISyncDomainHandler>();
        disabled.Domain.Returns(SyncDomain.Cursos);
        using var gate = new SyncExecutionGate();
        var service = new SyncService([enabled, disabled], new AlwaysFullSyncStateProvider(), new InMemorySyncStatusStore(),
            NullLogger<SyncService>.Instance, Options.Create(new SyncOptions()), gate);
        (await service.RunAsync(null, default)).Domains.Count.ShouldBe(1);
        await disabled.DidNotReceive().SyncAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>());
        await Should.ThrowAsync<ArgumentException>(() => service.RunAsync([SyncDomain.Cursos], default));
    }

    [Fact]
    public async Task ExecucoesConcorrentes_CompartilhamGateEntreInstancias()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var handler = Substitute.For<ISyncDomainHandler>();
        handler.Domain.Returns(SyncDomain.Alunos);
        handler.SyncAsync(null, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                entered.SetResult();
                await release.Task;
            }
            return new DomainSyncResult(SyncDomain.Alunos, true, 0, 0, null);
        });
        using var gate = new SyncExecutionGate();
        SyncService Service() => new([handler], new AlwaysFullSyncStateProvider(), new InMemorySyncStatusStore(),
            NullLogger<SyncService>.Instance, Options.Create(new SyncOptions()), gate);
        var first = Service().RunAsync(null, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = Service().RunAsync(null, default);
        try
        {
            calls.ShouldBe(1);
            second.IsCompleted.ShouldBeFalse();
        }
        finally { release.TrySetResult(); }
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        calls.ShouldBe(2);
    }
}
