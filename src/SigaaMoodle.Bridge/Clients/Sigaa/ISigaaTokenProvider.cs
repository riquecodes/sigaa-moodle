namespace SigaaMoodle.Bridge.Clients.Sigaa;

public interface ISigaaTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken ct);
}
