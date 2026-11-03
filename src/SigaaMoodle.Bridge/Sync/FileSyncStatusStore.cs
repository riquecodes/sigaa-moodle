using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SigaaMoodle.Bridge.Configuration;

namespace SigaaMoodle.Bridge.Sync;

// Guarda o último resultado em arquivo JSON local para sobreviver a reinícios (a ponte não tem banco).
public sealed class FileSyncStatusStore : ISyncStatusStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly ILogger<FileSyncStatusStore> _logger;
    private readonly Lock _lock = new();
    private volatile SyncRunResult? _last;

    public FileSyncStatusStore(
        IOptions<SyncOptions> options, IHostEnvironment environment, ILogger<FileSyncStatusStore> logger)
    {
        _path = Path.GetFullPath(options.Value.StatusFilePath, environment.ContentRootPath);
        _logger = logger;
        _last = Load();
    }

    public SyncRunResult? Last => _last;

    public void Set(SyncRunResult result)
    {
        _last = result;
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var temp = _path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(result, JsonOptions));
                File.Move(temp, _path, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Falha ao gravar o status da sincronização em {Path}.", _path);
            }
        }
    }

    private SyncRunResult? Load()
    {
        if (!File.Exists(_path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<SyncRunResult>(File.ReadAllText(_path), JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Status da sincronização ilegível em {Path}. Iniciando sem resultado anterior.", _path);
            return null;
        }
    }
}
