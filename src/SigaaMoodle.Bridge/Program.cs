using Microsoft.Extensions.Options;
using Microsoft.Extensions.Http.Resilience;
using SigaaMoodle.Bridge.Clients.Internal;
using SigaaMoodle.Bridge.Clients.Sigaa;
using SigaaMoodle.Bridge.Configuration;
using SigaaMoodle.Bridge.Sync;
using SigaaMoodle.Bridge.Sync.Handlers;
using SigaaMoodle.Bridge.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Options
builder.Services.Configure<SigaaOptions>(builder.Configuration.GetSection(SigaaOptions.SectionName));
builder.Services.Configure<InternalAppOptions>(builder.Configuration.GetSection(InternalAppOptions.SectionName));
builder.Services.AddOptions<SyncOptions>().Bind(builder.Configuration.GetSection(SyncOptions.SectionName))
    .Validate(o => !o.DeltaEnabled, "DeltaEnabled não é suportado pela API documentada.")
    .Validate(o => o.EnabledDomains.Length > 0 && o.EnabledDomains.All(d =>
        d.Equals("Alunos", StringComparison.OrdinalIgnoreCase) || d.Equals("Cursos", StringComparison.OrdinalIgnoreCase)),
        "EnabledDomains aceita somente Alunos e Cursos; destino sem contratos de notas/matrículas.")
    .ValidateOnStart();

// OAuth2 token (SIGAA)
builder.Services.AddSingleton<ISigaaTokenProvider, SigaaTokenProvider>();
builder.Services.AddTransient<SigaaTokenHandler>();
builder.Services.AddHttpClient("sigaa-token"); // client cru p/ buscar token (sem o handler)

// SIGAA typed client (com OAuth2 + resiliência)
builder.Services.AddHttpClient<ISigaaClient, SigaaClient>((sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<SigaaOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(opt.BaseUrl))
        client.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
})
.AddHttpMessageHandler<SigaaTokenHandler>()
.AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());

// App interna typed client (sem auth + resiliência)
builder.Services.AddHttpClient<IInternalAppClient, InternalAppClient>((sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<InternalAppOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(opt.BaseUrl))
        client.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
})
.AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());

// Sync core
builder.Services.AddSingleton<ISyncStatusStore, InMemorySyncStatusStore>();
builder.Services.AddSingleton<ISyncStateProvider, AlwaysFullSyncStateProvider>();
builder.Services.AddSingleton<SyncExecutionGate>();
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<ISyncDomainHandler, AlunosSyncHandler>();
builder.Services.AddScoped<ISyncDomainHandler, MatriculasSyncHandler>();
builder.Services.AddScoped<ISyncDomainHandler, CursosSyncHandler>();
builder.Services.AddScoped<ISyncDomainHandler, NotasSyncHandler>();

// Worker agendado (madrugada)
builder.Services.AddHostedService<ScheduledSyncWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
