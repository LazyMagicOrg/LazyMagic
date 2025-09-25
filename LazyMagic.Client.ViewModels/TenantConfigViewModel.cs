using LazyMagic.Client.FactoryGenerator; // do not put in global using. Causes runtime error.
namespace LazyMagic.Client.ViewModels;


public interface ITenantConfigViewModel
{
    TenantConfig? TenantConfig { get; set; }  // DTO
    bool IsLoaded { get; set; } 
    Task ReadAsync(string url);
}

public class TenantConfigViewModel : LzViewModel, ITenantConfigViewModel
{
    public TenantConfigViewModel(
        ILoggerFactory loggerFactory,
		IStaticAssets staticAssets
        ) : base(loggerFactory)
    {
        this.staticAssets = staticAssets;
    }
    public TenantConfig? TenantConfig { get; set; }  // DTO
    [Reactive] public bool IsLoaded { get; set; } 
    private IStaticAssets staticAssets { get; set; }    

    public virtual async Task ReadAsync(string url)
    {
        if(IsLoaded) return;
        var jsonDoc = await staticAssets.ReadContentAsync(url);
        TenantConfig = JsonConvert.DeserializeObject<TenantConfig>(jsonDoc);
        IsLoaded = true;
    }
}
