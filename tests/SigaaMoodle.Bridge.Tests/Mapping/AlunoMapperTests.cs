using Shouldly;
using SigaaMoodle.Bridge.Contracts.Sigaa;
using SigaaMoodle.Bridge.Mapping;

namespace SigaaMoodle.Bridge.Tests.Mapping;

public class AlunoMapperTests
{
    [Fact]
    public void AlunoSemVinculo_NaoGeraPayload()
    {
        AlunoMapper.ToPayloads(new SigaaAluno("00000000000", "Maria", null, null)).ShouldBeEmpty();
    }

    [Fact]
    public void ToPayload_DeveMapearSomenteCamposNecessarios()
    {
        var aluno = new SigaaAluno(Login: "00000000000", Nome: "Maria Silva",
            Email: "maria@uni.br", Cursos: [new SigaaVinculo("2023001", 42, "ATIVO")]);

        var payload = AlunoMapper.ToPayloads(aluno).Single();

        payload.Matricula.ShouldBe("2023001");
        payload.Nome.ShouldBe("Maria Silva");
        payload.Email.ShouldBe("maria@uni.br");
        payload.IdCursoSigaa.ShouldBe(42);
        payload.Status.ShouldBe("ATIVO");
    }
}
