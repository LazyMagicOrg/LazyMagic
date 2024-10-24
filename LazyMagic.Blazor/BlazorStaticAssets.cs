namespace LazyMagic.Blazor;

public class BlazorStaticAssets : IStaticAssets
{
    public BlazorStaticAssets(HttpClient httpClient) { 
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
           
            Console.WriteLine($"ReadAuthConfigAsync error reading: {url}, {ex.Message}");
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
            Console.WriteLine($"ReadTenancyConfigAsync error reading: {url}, {ex.Message}");
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
            Console.WriteLine($"ReadContentAsync error reading: {url}, {ex.Message}");
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
            Console.WriteLine($"HttpReadAsync error reading: {url}, {ex.Message}");
            return string.Empty;
        }
    }

}
