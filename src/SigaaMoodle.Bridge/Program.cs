using Microsoft.Extensions.Options;
using Microsoft.Extensions.Http.Resilience;
using Scalar.AspNetCore;
using SigaaMoodle.Bridge.Clients.Internal;
using SigaaMoodle.Bridge.Clients.Sigaa;
using SigaaMoodle.Bridge.Configuration;
using SigaaMoodle.Bridge.Sync;
using SigaaMoodle.Bridge.Sync.Handlers;
using SigaaMoodle.Bridge.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.Configure<SigaaOptions>(builder.Configuration.GetSection(SigaaOptions.SectionName));
builder.Services.Configure<InternalAppOptions>(builder.Configuration.GetSection(InternalAppOptions.SectionName));
builder.Services.AddOptions<SyncOptions>().Bind(builder.Configuration.GetSection(SyncOptions.SectionName))
    .Validate(o => !o.DeltaEnabled, "DeltaEnabled não é suportado pela API documentada.")
    .Validate(o => o.EnabledDomains.Length > 0 && o.EnabledDomains.All(d =>
        d.Equals("Alunos", StringComparison.OrdinalIgnoreCase)),
        "EnabledDomains aceita somente Alunos.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.StatusFilePath), "StatusFilePath é obrigatório.")
    .ValidateOnStart();

builder.Services.AddSingleton<ISigaaTokenProvider, SigaaTokenProvider>();
builder.Services.AddTransient<SigaaTokenHandler>();
builder.Services.AddHttpClient("sigaa-token");

builder.Services.AddHttpClient<ISigaaClient, SigaaClient>((sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<SigaaOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(opt.BaseUrl))
        client.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
})
.AddHttpMessageHandler<SigaaTokenHandler>()
.AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());

builder.Services.AddHttpClient<IInternalAppClient, InternalAppClient>((sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<InternalAppOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(opt.BaseUrl))
        client.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
})
.AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());

builder.Services.AddSingleton<ISyncStatusStore, FileSyncStatusStore>();
builder.Services.AddSingleton<ISyncStateProvider, AlwaysFullSyncStateProvider>();
builder.Services.AddSingleton<SyncExecutionGate>();
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<ISyncDomainHandler, AlunosSyncHandler>();

builder.Services.AddHostedService<ScheduledSyncWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Mock"))
{
    app.MapOpenApi();
    app.MapScalarApiReference("/docs", options => options.WithTitle("Middleware SIGAA → LocalUsersDatabase"));
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
