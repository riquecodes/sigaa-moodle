using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SigaaMoodle.Bridge.Configuration;
using SigaaMoodle.Bridge.Sync;

namespace SigaaMoodle.Bridge.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SyncController(ISyncService syncService, ISyncStatusStore statusStore, IOptions<SyncOptions> options)
    : ControllerBase
{
    // Dispara a sincronização de todos os domínios (disparo manual).
    [HttpPost]
    public async Task<IActionResult> RunAll(CancellationToken ct)
        => Ok(await syncService.RunAsync(domains: null, ct));

    // Dispara um domínio específico.
    [HttpPost("{domain}")]
    public async Task<IActionResult> RunOne(string domain, CancellationToken ct)
    {
        if (!Enum.TryParse<SyncDomain>(domain, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            return BadRequest(new { erro = $"Domínio inválido: {domain}" });

        if (!options.Value.EnabledDomains.Contains(parsed.ToString(), StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { erro = $"Domínio desabilitado: {parsed}. Confira Sync:EnabledDomains." });

        return Ok(await syncService.RunAsync([parsed], ct));
    }

    // Último resultado de sincronização (em memória).
    [HttpGet("status")]
    public IActionResult Status()
        => statusStore.Last is null ? NoContent() : Ok(statusStore.Last);
}
