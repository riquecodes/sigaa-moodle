namespace SigaaMoodle.Bridge.Configuration;

public sealed class InternalAppOptions
{
    public const string SectionName = "InternalApp";

    // App interna da faculdade — sem autenticação (rede interna).
    public string BaseUrl { get; set; } = string.Empty;
    public int? StudentUserType { get; set; }
    // Chave: id_curso SIGAA; valor: ID de curso já cadastrado na base local.
    public Dictionary<int, int> CourseMap { get; set; } = new();

    public DomainRoutes Routes { get; set; } = new();

    public sealed class DomainRoutes
    {
        public string Alunos { get; set; } = string.Empty;
        public string Matriculas { get; set; } = string.Empty;
        public string Cursos { get; set; } = string.Empty;
        public string Notas { get; set; } = string.Empty;
    }
}
