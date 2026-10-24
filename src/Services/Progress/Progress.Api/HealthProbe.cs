namespace Progress.Api;

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
