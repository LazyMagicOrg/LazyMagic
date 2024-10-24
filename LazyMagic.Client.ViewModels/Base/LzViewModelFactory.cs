namespace LazyMagic.Client.ViewModels;

public static class LzViewModelFactory
{
    public static void RegisterLz(IServiceCollection services, Assembly assembly)
    {
        Type[] iTypes = { typeof(ILzSingleton), typeof(ILzTransient), typeof(ILzScoped) };

        var factoryTypes = assembly
            .GetTypes()
            .Where(t => 
                iTypes.Any(iType =>  iType.IsAssignableFrom(t) && !t.IsAbstract));

        foreach (var type in factoryTypes)
        {
            var iTypeName = "I" + type.Name;
            var interfaces = type.GetInterfaces();
            foreach (var iface in interfaces)
                if (iface.Name.Equals(iTypeName))
                { 
                    string scope = "";  
                    var registered = true;
                    if (typeof(ILzSingleton).IsAssignableFrom(type))
                    {
                        scope = "Singleton";
                        services.TryAddSingleton(iface, type);
                    }
                    else
                    if (typeof(ILzTransient).IsAssignableFrom(type)) {
                        scope = "Transient";
                        services.TryAddTransient(iface, type);
                    }
                    else
                    if (typeof(ILzScoped).IsAssignableFrom(type)) {
                        scope = "Scoped";   
                        services.TryAddScoped(iface, type);
                    }
                    else registered = false;
                    if (registered)
                        Console.WriteLine($"Registered {type.Name} as {scope}");
                }
        }
    }
}
