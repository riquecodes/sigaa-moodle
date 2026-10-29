using System.Net;
using System.Net.Http.Json;
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
        Routes = new() { Alunos = "api/User", Cursos = "api/Course" }
    };

    private static AlunoPayload Student(string enrollment = "2023001") => new(enrollment, "Maria", null, 42);

    [Fact]
    public async Task Reenvio_CriaSomenteUmaVez_ComContratoDaApiLocal()
    {
        var exists = false;
        var posts = 0;
        using var handler = new StubHttpHandler(async (request, ct) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                request.RequestUri!.AbsolutePath.ShouldBe("/api/User/2023001");
                return exists ? StubHttpHandler.Json("""{"enrollmentNumber":2023001,"courseId":7,"userType":3,"completeName":"Maria","lockNumber":9,"licensePlate":"XYZ","isActive":false}""")
                    : new HttpResponseMessage(HttpStatusCode.NotFound);
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
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://local.test/") };
        var client = new InternalAppClient(http, Options.Create(Settings()));
        await client.SendAlunosAsync([Student()], default);
        await client.SendAlunosAsync([Student()], default);
        posts.ShouldBe(1);
    }

    [Fact]
    public async Task Atualizacao_PreservaCamposLocais_EUsaPutIndividual()
    {
        var puts = 0;
        using var handler = new StubHttpHandler(async (request, ct) =>
        {
            if (request.Method == HttpMethod.Get)
                return StubHttpHandler.Json("""{"enrollmentNumber":2023001,"courseId":7,"userType":3,"completeName":"Nome antigo","lockNumber":9,"licensePlate":"XYZ","isActive":false}""");
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
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://local.test/") };
        await new InternalAppClient(http, Options.Create(Settings())).SendAlunosAsync([Student()], default);
        puts.ShouldBe(1);
    }

    [Theory]
    [InlineData("2147483648")]
    [InlineData("002023001")]
    [InlineData("abc")]
    [InlineData("0")]
    public async Task MatriculaIncompativel_NaoEscreveNemTrunca(string enrollment)
    {
        using var handler = new StubHttpHandler((_, _) => throw new Exception("Não deveria acessar rede."));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://local.test/") };
        await Should.ThrowAsync<InvalidOperationException>(() => new InternalAppClient(http, Options.Create(Settings()))
            .SendAlunosAsync([Student(), Student(enrollment)], default));
    }

    [Fact]
    public async Task CursoSemCorrespondencia_NaoEnviaLoteParcial()
    {
        using var handler = new StubHttpHandler((_, _) => throw new Exception("Não deveria acessar rede."));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://local.test/") };
        await Should.ThrowAsync<InvalidOperationException>(() => new InternalAppClient(http, Options.Create(Settings()))
            .SendAlunosAsync([Student(), Student("2023002") with { IdCursoSigaa = 99 }], default));
    }

    [Fact]
    public async Task FalhaNaConsulta_NaoTentaCriarUsuario()
    {
        var calls = 0;
        using var handler = new StubHttpHandler((request, _) =>
        {
            request.Method.ShouldBe(HttpMethod.Get);
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://local.test/") };
        await Should.ThrowAsync<HttpRequestException>(() => new InternalAppClient(http, Options.Create(Settings()))
            .SendAlunosAsync([Student()], default));
        calls.ShouldBe(1);
    }

    [Fact]
    public async Task Cursos_AtualizaSomenteNomeDoIdMapeado()
    {
        var puts = 0;
        using var handler = new StubHttpHandler(async (request, ct) =>
        {
            request.RequestUri!.AbsolutePath.ShouldBe("/api/Course/7");
            if (request.Method == HttpMethod.Get) return StubHttpHandler.Json("""{"courseName":"Nome antigo"}""");
            request.Method.ShouldBe(HttpMethod.Put);
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            document.RootElement.GetProperty("courseName").GetString().ShouldBe("Informática");
            document.RootElement.TryGetProperty("durationInSemester", out _).ShouldBeFalse();
            puts++;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://local.test/") };
        await new InternalAppClient(http, Options.Create(Settings())).SendCursosAsync([new("TEC", "Informática", 42)], default);
        puts.ShouldBe(1);
    }

    [Fact]
    public void DominiosSemDestino_NaoInventamRotas()
    {
        using var http = new HttpClient();
        var client = new InternalAppClient(http, Options.Create(Settings()));
        Should.Throw<NotSupportedException>(() => client.SendNotasAsync([], default));
        Should.Throw<NotSupportedException>(() => client.SendMatriculasAsync([], default));
    }
}
