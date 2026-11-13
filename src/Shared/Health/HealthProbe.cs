namespace Longevia.Shared.Health;

/// <summary>
/// Minimal liveness probe used by container health checks. The runtime images are
/// chiseled (no shell, curl or wget), so every service exposes
/// <c>--health-check &lt;url&gt;</c> and this helper performs the HTTP request.
/// </summary>
public static class HealthProbe
{
    public static async Task<int> RunAsync(string address)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            using var response = await client.GetAsync(address);
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return 1;
        }
    }
}
