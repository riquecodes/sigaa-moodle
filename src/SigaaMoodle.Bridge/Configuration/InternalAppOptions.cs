namespace SigaaMoodle.Bridge.Configuration;

public sealed class InternalAppOptions
{
    public const string SectionName = "InternalApp";

    public string BaseUrl { get; set; } = string.Empty;
    public int? StudentUserType { get; set; }
    public Dictionary<int, int> CourseMap { get; set; } = new();
    public string[] InactiveStatuses { get; set; } = [];

    public DomainRoutes Routes { get; set; } = new();

    public sealed class DomainRoutes
    {
        public string Alunos { get; set; } = string.Empty;
    }
}
