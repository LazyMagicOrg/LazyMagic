namespace LazyMagic.Blazorise.Console;

public static class ConfigureLazyMagicBlazorConsole
{
    public static IServiceCollection AddLazyMagicBlazoriseConsole(this IServiceCollection services)
    {
        //services
        //    .AddScoped<IndexedDBManager>()
        //    .AddIndexedDB(options =>
        //    {
        //        options.DbName = "ClaudeConsoleLogsDB"; // This is what will show in browser
        //        options.Version = 1;
        //        options.Stores.Add(new StoreSchema
        //        {
        //            Name = "consoleLogs",
        //            PrimaryKey = new IndexSpec { Name = "timestamp", KeyPath = "timestamp", Auto = false },
        //            Indexes = new List<IndexSpec>
        //            {
        //                new IndexSpec { Name = "type", KeyPath = "type", Auto = false },
        //                new IndexSpec { Name = "message", KeyPath = "message", Auto = false }
        //            }
        //        });
        //    })
        //    .AddScoped<LzConsoleInterop>();



        return services;
    }
    public static bool IsServiceRegistered<TService>(this IServiceCollection services)
    {
        return services.Any(serviceDescriptor => serviceDescriptor.ServiceType == typeof(TService));
    }

}
