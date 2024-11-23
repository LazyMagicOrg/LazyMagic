namespace LazyMagic.Blazor;

public class BlazorStaticAssets : IStaticAssets
{
    private readonly ILogger _logger;   
    public BlazorStaticAssets(ILoggerFactory loggerFactory, HttpClient httpClient) {
        _logger = loggerFactory.CreateLogger<BlazorStaticAssets>();
        this.httpClient = httpClient;
    }
    HttpClient httpClient;

    public virtual async Task<string> ReadAuthConfigAsync(string url)
    {
        try
        {
            var text = await httpClient.GetStringAsync(url);
            return text;
        }
        catch (Exception ex)
        {
           
            _logger.LogDebug($"ReadAuthConfigAsync error reading: {url}, {ex.Message}");
            return string.Empty;
        }
    }
    public virtual async Task<string> ReadTenancyConfigAsync(string url)
    {
        try
        {
            var text = await httpClient.GetStringAsync(url);
            return text;
        } catch (Exception ex)
        {
            _logger.LogDebug($"ReadTenancyConfigAsync error reading: {url}, {ex.Message}");
            return string.Empty;
        }
    }
    public virtual async Task<string> ReadContentAsync(string url)
    {
        try
        {
            var text = await httpClient.GetStringAsync(url);
            return text;
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"ReadContentAsync error reading: {url}, {ex.Message}");
            return string.Empty;
        }
    }
    public virtual async Task<string> HttpReadAsync(string url)
    {
        try
        {
            var text = await httpClient.GetStringAsync(url);
            return text;
        }

        catch (Exception ex)
        {
            _logger.LogDebug($"HttpReadAsync error reading: {url}, {ex.Message}");
            return string.Empty;
        }
    }

}
