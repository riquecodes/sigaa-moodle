namespace SigaaMoodle.Bridge.Configuration;

public sealed class SigaaOptions
{
    public const string SectionName = "Sigaa";

    // A DEFINIR — credentials e URLs reais do SIGAA. URLs documentadas em appsettings; credenciais externas.
    public string BaseUrl { get; set; } = string.Empty;
    public string TokenUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public int? Unidade { get; set; }
    public string PeriodoLetivo { get; set; } = string.Empty;
    public int PageSize { get; set; } = 100;
    public int MaxPages { get; set; } = 10000;

    // Rotas por domínio (A DEFINIR). Ex.: "/alunos". Configuração atual usa rotas documentadas, relativas e sem barra inicial.
    public DomainRoutes Routes { get; set; } = new();

    public sealed class DomainRoutes
    {
        public string Alunos { get; set; } = string.Empty;
        public string Matriculas { get; set; } = string.Empty;
        public string Cursos { get; set; } = string.Empty;
        public string Notas { get; set; } = string.Empty;
    }
}
