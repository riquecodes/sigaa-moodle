using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Shouldly;
using SigaaMoodle.Bridge.Clients.Sigaa;
using SigaaMoodle.Bridge.Configuration;
using SigaaMoodle.Bridge.Mapping;

namespace SigaaMoodle.Bridge.Tests.Clients;

public class SigaaClientTests
{
    private static SigaaOptions Settings() => new()
    {
        Unidade = 20, PageSize = 1,
        Routes = new() { Alunos = "sig/sigaa/alunos/paginate" }
    };

    [Fact]
    public async Task Alunos_LeTodasPaginas_PreservaFiltrosEVinculos()
    {
        var requests = 0;
        using var handler = new StubHttpHandler((request, _) =>
        {
            request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/sig/sigaa/alunos/paginate");
            var query = QueryHelpers.ParseQuery(request.RequestUri.Query);
            query["unidade"].ToString().ShouldBe("20");
            query["per_page"].ToString().ShouldBe("1");
            query.ContainsKey("since").ShouldBeFalse();
            var page = ++requests;
            query["page"].ToString().ShouldBe(page.ToString());
            return Task.FromResult(StubHttpHandler.Json($$"""
                {"current_page":{{page}},"per_page":1,"total":2,"data":[
                  {"login":"00000000000","nome_completo":"Maria","email":null,"cursos":[
                    {"matricula":"202300{{page}}","id_curso":42,"status_discente":"ATIVO"},
                    {"matricula":"202400{{page}}","id_curso":43,"status_discente":"ATIVO"}]}]}
                """));
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://sigaa.test/api/v1/") };
        var client = new SigaaClient(http, Options.Create(Settings()));
        var result = await client.GetAlunosAsync(null, default);
        result.Count.ShouldBe(2);
        result.SelectMany(AlunoMapper.ToPayloads).Count().ShouldBe(4);
        result[0].Email.ShouldBeNull();
        requests.ShouldBe(2);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"data\":[],\"current_page\":1}")]
    [InlineData("{\"data\":[],\"current_page\":2,\"last_page\":2}")]
    [InlineData("{\"data\":[],\"current_page\":1,\"last_page\":1,\"total\":5}")]
    public async Task Alunos_RejeitaRespostaInvalidaOuPaginaRepetida(string json)
    {
        using var handler = new StubHttpHandler((_, _) => Task.FromResult(StubHttpHandler.Json(json)));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://sigaa.test/api/v1/") };
        var client = new SigaaClient(http, Options.Create(Settings()));
        await Should.ThrowAsync<JsonException>(() => client.GetAlunosAsync(null, default));
    }

    [Fact]
    public async Task Alunos_InterrompeAoExcederLimiteDePaginas()
    {
        var settings = Settings();
        settings.MaxPages = 1;
        using var handler = new StubHttpHandler((_, _) => Task.FromResult(StubHttpHandler.Json(
            """{"current_page":1,"last_page":2,"data":[{"login":"x","nome_completo":"Maria","cursos":[]}]}""")));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://sigaa.test/api/v1/") };
        await Should.ThrowAsync<InvalidOperationException>(() => new SigaaClient(http, Options.Create(settings)).GetAlunosAsync(null, default));
    }

    [Fact]
    public async Task Alunos_UsaRotaConfigurada()
    {
        using var handler = new StubHttpHandler((request, _) =>
        {
            request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/sig/sigaa/alunos/paginate");
            return Task.FromResult(StubHttpHandler.Json("""
                {"current_page":1,"last_page":1,"data":[
                  {"login":"00000000000","nome_completo":"Maria","cursos":[
                    {"matricula":"2023001","id_curso":42}]}]}
                """));
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://sigaa.test/api/v1/") };

        var alunos = await new SigaaClient(http, Options.Create(Settings())).GetAlunosAsync(null, default);

        alunos.Single().Cursos!.Single().Matricula.ShouldBe("2023001");
    }


    [Fact]
    public async Task RejeitaDeltaAntesDeEnviar()
    {
        using var handler = new StubHttpHandler((_, _) => throw new Exception("Não deveria acessar rede."));
        using var http = new HttpClient(handler);
        var client = new SigaaClient(http, Options.Create(Settings()));

        await Should.ThrowAsync<NotSupportedException>(() => client.GetAlunosAsync(DateTimeOffset.UtcNow, default));
    }
}
