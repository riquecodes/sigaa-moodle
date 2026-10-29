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
        Unidade = 20, PeriodoLetivo = "2026/0", PageSize = 1,
        Routes = new() { Alunos = "sig/sigaa/alunos/paginate", Cursos = "sig/sigaa/cursos",
            Matriculas = "sig/sigaa/matriculados", Notas = "sig/sigaa/alunos/desempenho" }
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
    public async Task MatriculasENotas_MapeiamDisciplinasSemInventarNotaZero()
    {
        using var handler = new StubHttpHandler((request, _) =>
        {
            QueryHelpers.ParseQuery(request.RequestUri!.Query)["periodo_letivo"].ToString().ShouldBe("2026/0");
            return Task.FromResult(StubHttpHandler.Json("""
                [{"matricula":"2023001","disciplinas":[
                  {"id_turma":123,"situacao_matricula":"MATRICULADO","media_final":8.2},
                  {"id_turma":124,"situacao_matricula":"MATRICULADO","media_final":null}]}]
                """));
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://sigaa.test/api/v1/") };
        var client = new SigaaClient(http, Options.Create(Settings()));
        var enrollments = (await client.GetMatriculasAsync(null, default)).SelectMany(MatriculaMapper.ToPayloads).ToList();
        enrollments.Count.ShouldBe(2);
        enrollments[0].CodigoTurma.ShouldBe("123");
        var grades = (await client.GetNotasAsync(null, default)).SelectMany(NotaMapper.ToPayloads).ToList();
        grades.Count.ShouldBe(1);
        grades[0].Valor.ShouldBe(8.2m);
    }

    [Fact]
    public async Task Cursos_UsaContratoRealSemForcarPaginacaoAmbigua()
    {
        using var handler = new StubHttpHandler((request, _) =>
        {
            request.RequestUri!.Query.ShouldNotContain("per_page");
            return Task.FromResult(StubHttpHandler.Json("""[{"id_curso":42,"codigo":"TEC","curso":"Informática","modalidade_educacao":null}]"""));
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://sigaa.test/api/v1/") };
        var result = await new SigaaClient(http, Options.Create(Settings())).GetCursosAsync(null, default);
        result.Single().Id.ShouldBe(42);
        CursoMapper.ToPayload(result.Single()).Nome.ShouldBe("Informática");
    }

    [Fact]
    public async Task RejeitaDeltaEPeriodoAusenteAntesDeEnviar()
    {
        using var handler = new StubHttpHandler((_, _) => throw new Exception("Não deveria acessar rede."));
        using var http = new HttpClient(handler);
        var settings = Settings();
        settings.PeriodoLetivo = "";
        var client = new SigaaClient(http, Options.Create(settings));
        await Should.ThrowAsync<NotSupportedException>(() => client.GetAlunosAsync(DateTimeOffset.UtcNow, default));
        await Should.ThrowAsync<InvalidOperationException>(() => client.GetNotasAsync(null, default));
    }
}
