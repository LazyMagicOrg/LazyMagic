namespace LazyMagic.Blazor;

public class BlazorOSAccess : IOSAccess
{
    public BlazorOSAccess(HttpClient httpClient) { 
        this.httpClient = httpClient;
    }
    HttpClient httpClient;
    IJSRuntime? jSRuntime;
    BlazorContentAccess? blazorContentAccess;

    public void SetJSRuntime(object jSRuntime)
    {
        this.jSRuntime = (JSRuntime)jSRuntime;
    }

    public virtual async Task<string> ReadAuthConfigAsync(string url)
    {
        try
        {
            if (jSRuntime == null)
                throw new Exception("JSRuntime not set.");
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
        if(jSRuntime == null)
            throw new Exception("JSRuntime not set.");  

        // Currently, there is no way to read a file from the Blazor app's wwwroot folder directly 
        // from C# when running in a MAUI Hybrid app. So, we use JSInterop to read the file. If 
        // you are running in a Blazor app, you could use httpClient.GetStringAsync(url) instead, but
        // for consistency, we use JSInterop in both cases. Under the covers, the JavaScript function 
        // called by BlazorContentAccess is using fetch() to read the file. Blazor overrides fetch() 
        // to use the Blazor app's wwwroot folder in a hybrid app.
        try
        {
            //blazorContentAccess ??= new BlazorContentAccess(jSRuntime); 
            //var text = await blazorContentAccess.GetBlazorContentAsync(url);
            var text = await httpClient.GetStringAsync(url);
            return text;
        } catch 
        {
            return string.Empty;
        }
    }
    public virtual async Task<string> ReadContentAsync(string url)
    {
        if (jSRuntime == null)
            throw new Exception("JSRuntime not set.");

        // Currently, there is no way to read a file from the Blazor app's wwwroot folder directly 
        // from C# when running in a MAUI Hybrid app. So, we use JSInterop to read the file. If 
        // you are running in a Blazor app, you could use httpClient.GetStringAsync(url) instead, but
        // for consistency, we use JSInterop in both cases. Under the covers, the JavaScript function 
        // called by BlazorContentAccess is using fetch() to read the file. Blazor overrides fetch() 
        // to use the Blazor app's wwwroot folder in a hybrid app.
        try
        {
            blazorContentAccess ??= new BlazorContentAccess(jSRuntime);
            var text = await blazorContentAccess.GetBlazorContentAsync(url);
            if(text.StartsWith("Failed to fetch"))
                return string.Empty;
            return text;
        }
        catch
        {
            return string.Empty;
        }
    }
    public virtual async Task<string> HttpReadAsync(string url)
    {
        try
        {
            if (jSRuntime == null)
                throw new Exception("JSRuntime not set.");

            var text = await httpClient.GetStringAsync(url);
            return text;
        }

        catch
        {
            return string.Empty;
        }
    }
    public virtual Task LocalCreateAsync(string filepath, string content)
    {
        throw new NotImplementedException();
    }

    public virtual Task<string> LocalReadAsync(string filepath)
    {
        throw new NotImplementedException();
    }

    public virtual Task LocalUpdateAsync(string filepath, string content)
    {
        throw new NotImplementedException();
    }

    public virtual Task LocalDeleteAsync(string filepath)
    {
        throw new NotImplementedException();
    }

    public virtual Task S3CreateAsync(string path, string content)
    {
        throw new NotImplementedException();
    }

    public virtual Task<string> S3ReadAsync(string path)
    {
        throw new NotImplementedException();
    }

    public virtual Task S3UpdateAsync(string path, string content)
    {
        throw new NotImplementedException();
    }

    public virtual Task S3DeleteAsync(string path)
    {
        throw new NotImplementedException();
    }


}
