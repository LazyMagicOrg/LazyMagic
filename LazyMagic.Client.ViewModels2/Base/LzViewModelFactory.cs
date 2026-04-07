namespace LazyMagic.Client.ViewModels;

public static class LzViewModelFactory
{
    public static void RegisterLz(IServiceCollection services, Assembly assembly)
    {
        Type[] iTypes = { typeof(ILzSingleton), typeof(ILzTransient), typeof(ILzScoped) };
        var factoryTypes = assembly
            .GetTypes()
            .Where(t =>
                iTypes.Any(iType => iType.IsAssignableFrom(t) && !t.IsAbstract));
        foreach (var type in factoryTypes)
        {
            var interfaces = type.GetInterfaces();
            // First find the Lz interface to determine the scope
            string scope = "";
            if (typeof(ILzSingleton).IsAssignableFrom(type))
                scope = "Singleton";
            else if (typeof(ILzTransient).IsAssignableFrom(type))
                scope = "Transient";
            else if (typeof(ILzScoped).IsAssignableFrom(type))
                scope = "Scoped";

            if (!string.IsNullOrEmpty(scope))
            {
                // Then find the matching interface for registration
                var iTypeName = "I" + type.Name;
                var serviceInterface = interfaces.FirstOrDefault(i => i.Name.Equals(iTypeName));
                if (serviceInterface != null)
                {
                    switch (scope)
                    {
                        case "Singleton":
                            services.TryAddScoped(serviceInterface, type);
                            break;
                        case "Transient":
                            services.TryAddTransient(serviceInterface, type);
                            break;
                        case "Scoped":
                            services.TryAddScoped(serviceInterface, type);
                            break;
                    }
                    Console.WriteLine($"Registered {type.Name} as {scope}");
                }
            }
        }
    }
}
