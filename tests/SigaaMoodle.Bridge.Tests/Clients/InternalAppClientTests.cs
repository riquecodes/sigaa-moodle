using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Shouldly;
using SigaaMoodle.Bridge.Clients.Internal;
using SigaaMoodle.Bridge.Configuration;
using SigaaMoodle.Bridge.Contracts.Internal;

namespace SigaaMoodle.Bridge.Tests.Clients;

public class InternalAppClientTests
{
    private static InternalAppOptions Settings() => new()
    {
        StudentUserType = 3, CourseMap = new() { [42] = 7 },
        InactiveStatuses = ["CANCELADO", "CONCLUÍDO"],
        Routes = new() { Alunos = "api/User" }
    };

    private static AlunoPayload Student(string enrollment = "2023001", string? status = null)
        => new(enrollment, "Maria", null, 42, status);

    private static InternalAppClient Client(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://local.test/") }, Options.Create(Settings()));

    private static HttpResponseMessage Existing(string name = "Maria", bool active = true) => StubHttpHandler.Json(
        $$"""{"enrollmentNumber":2023001,"courseId":7,"userType":3,"completeName":"{{name}}","lockNumber":9,"licensePlate":"XYZ","isActive":{{(active ? "true" : "false")}}}""");

    [Fact]
    public async Task Reenvio_CriaSomenteUmaVez_ComContratoDaApiLocal()
    {
        var exists = false;
        var posts = 0;
        using var handler = new StubHttpHandler(async (request, ct) =>
        {
            request.Headers.Authorization.ShouldBeNull();
            if (request.Method == HttpMethod.Get)
            {
                request.RequestUri!.AbsolutePath.ShouldBe("/api/User/2023001");
                return exists ? Existing() : new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            request.Method.ShouldBe(HttpMethod.Post);
            request.RequestUri!.AbsolutePath.ShouldBe("/api/User");
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            document.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
            var user = document.RootElement[0];
            user.GetProperty("enrollmentNumber").GetInt32().ShouldBe(2023001);
            user.GetProperty("courseId").GetInt32().ShouldBe(7);
            user.GetProperty("userType").GetInt32().ShouldBe(3);
            user.TryGetProperty("email", out _).ShouldBeFalse();
            posts++;
            exists = true;
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        var client = Client(handler);

        var first = await client.SendAlunosAsync([Student(status: "ATIVO")], default);
        var second = await client.SendAlunosAsync([Student(status: "ATIVO")], default);

        posts.ShouldBe(1);
        first.Created.ShouldBe(1);
        second.Unchanged.ShouldBe(1);
        second.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task Atualizacao_SemStatus_PreservaCamposLocaisEEstadoAtivo()
    {
        var puts = 0;
        using var handler = new StubHttpHandler(async (request, ct) =>
        {
            request.Headers.Authorization.ShouldBeNull();
            if (request.Method == HttpMethod.Get)
                return Existing("Nome antigo", active: false);
            request.Method.ShouldBe(HttpMethod.Put);
            request.RequestUri!.AbsolutePath.ShouldBe("/api/User/2023001");
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var user = document.RootElement;
            user.GetProperty("completeName").GetString().ShouldBe("Maria");
            foreach (var field in new[] { "lockNumber", "licensePlate", "isActive", "createdAtUtc", "email" })
                user.TryGetProperty(field, out _).ShouldBeFalse();
            puts++;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var result = await Client(handler).SendAlunosAsync([Student()], default);

        puts.ShouldBe(1);
        result.Updated.ShouldBe(1);
    }

    [Theory]
    [InlineData("CANCELADO")]
    [InlineData("concluido")]
    public async Task StatusInativo_DesativaUsuarioExistente(string status)
    {
        bool? sentActive = null;
        using var handler = new StubHttpHandler(async (request, ct) =>
        {
            if (request.Method == HttpMethod.Get)
                return Existing();
            request.Method.ShouldBe(HttpMethod.Put);
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            sentActive = document.RootElement.GetProperty("isActive").GetBoolean();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var result = await Client(handler).SendAlunosAsync([Student(status: status)], default);

        sentActive.ShouldBe(false);
        result.Updated.ShouldBe(1);
    }

    [Fact]
    public async Task StatusAtivo_ReativaUsuarioInativo()
    {
        bool? sentActive = null;
        using var handler = new StubHttpHandler(async (request, ct) =>
        {
            if (request.Method == HttpMethod.Get)
                return Existing(active: false);
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            sentActive = document.RootElement.GetProperty("isActive").GetBoolean();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        await Client(handler).SendAlunosAsync([Student(status: "ATIVO")], default);

        sentActive.ShouldBe(true);
    }

    [Fact]
    public async Task AlunoNovoJaInativo_NaoECriado()
    {
        var posts = 0;
        using var handler = new StubHttpHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Post) posts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var result = await Client(handler).SendAlunosAsync([Student(status: "CANCELADO")], default);

        posts.ShouldBe(0);
        result.Skipped.ShouldBe(1);
    }

    [Theory]
    [InlineData("2147483648")]
    [InlineData("002023001")]
    [InlineData("abc")]
    [InlineData("0")]
    public async Task MatriculaIncompativel_RecusaSoORegistroENaoTrunca(string enrollment)
    {
        var requested = new List<string>();
        using var handler = new StubHttpHandler((request, _) =>
        {
            requested.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.NotFound) : new HttpResponseMessage(HttpStatusCode.Created));
        });

        var result = await Client(handler).SendAlunosAsync([Student(), Student(enrollment)], default);

        result.Created.ShouldBe(1);
        result.Errors.Single().Matricula.ShouldBe(enrollment);
        requested.ShouldAllBe(path => path == "/api/User/2023001" || path == "/api/User");
    }

    [Fact]
    public async Task CursoSemCorrespondencia_RecusaSoORegistro()
    {
        using var handler = new StubHttpHandler((request, _) => Task.FromResult(request.Method == HttpMethod.Get
            ? new HttpResponseMessage(HttpStatusCode.NotFound) : new HttpResponseMessage(HttpStatusCode.Created)));

        var result = await Client(handler).SendAlunosAsync(
            [Student(), Student("2023002") with { IdCursoSigaa = 99 }], default);

        result.Created.ShouldBe(1);
        var error = result.Errors.Single();
        error.Matricula.ShouldBe("2023002");
        error.Nome.ShouldBe("Maria");
        error.Motivo.ShouldContain("99");
    }

    [Fact]
    public async Task MatriculaRepetidaComDadosDivergentes_RecusaAmbasSemAcessarRede()
    {
        using var handler = new StubHttpHandler((_, _) => throw new Exception("Não deveria acessar rede."));

        var result = await Client(handler).SendAlunosAsync(
            [Student(), Student() with { Nome = "Outra pessoa" }], default);

        result.Errors.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ErroHttpNaConsulta_RecusaORegistroSemTentarCriar()
    {
        var calls = 0;
        using var handler = new StubHttpHandler((request, _) =>
        {
            request.Method.ShouldBe(HttpMethod.Get);
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        });

        var result = await Client(handler).SendAlunosAsync([Student()], default);

        calls.ShouldBe(1);
        result.Errors.Single().Motivo.ShouldContain("500");
    }

    [Fact]
    public async Task ApiLocalIndisponivel_InterrompeAExecucao()
    {
        using var handler = new StubHttpHandler((_, _) => throw new HttpRequestException("Conexão recusada."));

        await Should.ThrowAsync<HttpRequestException>(() => Client(handler).SendAlunosAsync([Student()], default));
    }
}
