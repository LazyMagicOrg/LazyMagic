namespace LazyMagic.Shared;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class ServiceCollectionExtensions
{
    public static void PrintRegistrationsAsTable(this IServiceCollection services)
    {
        var registrations = services
            .Select(s => new
            {
                ServiceTypeName = GetTypeName(s.ServiceType),
                Implementation = GetImplementationName(s),
                Lifetime = s.Lifetime.ToString(),
                ActualType = s.ServiceType
            })
            .OrderBy(r => r.ServiceTypeName)
            .ToList();

        if (!registrations.Any())
        {
            Console.WriteLine("No services registered.");
            return;
        }

        // Calculate column widths
        int serviceWidth = Math.Max("Service Type".Length,
            registrations.Max(r => r.ServiceTypeName.Length));
        int implWidth = Math.Max("Implementation".Length,
            registrations.Max(r => r.Implementation.Length));
        int lifetimeWidth = "Lifetime".Length + 2; // Lifetime enum values are short

        // Print header
        PrintLine(serviceWidth, implWidth, lifetimeWidth);
        Console.WriteLine($"| {"Service Type".PadRight(serviceWidth)} | {"Implementation".PadRight(implWidth)} | {"Lifetime".PadRight(lifetimeWidth)} |");
        PrintLine(serviceWidth, implWidth, lifetimeWidth);

        // Print rows
        foreach (var reg in registrations)
        {
            Console.WriteLine($"| {reg.ServiceTypeName.PadRight(serviceWidth)} | {reg.Implementation.PadRight(implWidth)} | {reg.Lifetime.PadRight(lifetimeWidth)} |");
        }

        PrintLine(serviceWidth, implWidth, lifetimeWidth);
        Console.WriteLine($"Total registrations: {registrations.Count}");
    }

    public static string GetRegistrationsAsTableString(this IServiceCollection services)
    {
        var sb = new StringBuilder();
        var registrations = services
            .Select(s => new
            {
                ServiceTypeName = GetTypeName(s.ServiceType),
                Implementation = GetImplementationName(s),
                Lifetime = s.Lifetime.ToString()
            })
            .OrderBy(r => r.ServiceTypeName)
            .ToList();

        if (!registrations.Any())
        {
            return "No services registered.";
        }

        // Calculate column widths
        int serviceWidth = Math.Max("Service Type".Length,
            registrations.Max(r => r.ServiceTypeName.Length));
        int implWidth = Math.Max("Implementation".Length,
            registrations.Max(r => r.Implementation.Length));
        int lifetimeWidth = "Lifetime".Length + 2;

        // Build header
        sb.AppendLine(GetLine(serviceWidth, implWidth, lifetimeWidth));
        sb.AppendLine($"| {"Service Type".PadRight(serviceWidth)} | {"Implementation".PadRight(implWidth)} | {"Lifetime".PadRight(lifetimeWidth)} |");
        sb.AppendLine(GetLine(serviceWidth, implWidth, lifetimeWidth));

        // Build rows
        foreach (var reg in registrations)
        {
            sb.AppendLine($"| {reg.ServiceTypeName.PadRight(serviceWidth)} | {reg.Implementation.PadRight(implWidth)} | {reg.Lifetime.PadRight(lifetimeWidth)} |");
        }

        sb.AppendLine(GetLine(serviceWidth, implWidth, lifetimeWidth));
        sb.AppendLine($"Total registrations: {registrations.Count}");

        return sb.ToString();
    }

    public static void PrintRegistrationsGroupedByLifetime(this IServiceCollection services)
    {
        var grouped = services
            .GroupBy(s => s.Lifetime)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            Console.WriteLine($"\n{group.Key} Services:");
            Console.WriteLine(new string('=', 50));

            var items = group
                .Select(s => new
                {
                    ServiceType = GetTypeName(s.ServiceType),
                    Implementation = GetImplementationName(s)
                })
                .OrderBy(i => i.ServiceType);

            int maxServiceLength = items.Any() ? items.Max(i => i.ServiceType.Length) : 0;

            foreach (var item in items)
            {
                Console.WriteLine($"  {item.ServiceType.PadRight(maxServiceLength)} -> {item.Implementation}");
            }
        }

        Console.WriteLine($"\nTotal: {services.Count} registrations");
    }

    public static void PrintRegistrationsAsMarkdownTable(this IServiceCollection services)
    {
        var registrations = services
            .Select(s => new
            {
                ServiceType = GetTypeName(s.ServiceType),
                Implementation = GetImplementationName(s),
                Lifetime = s.Lifetime.ToString()
            })
            .OrderBy(r => r.ServiceType)
            .ToList();

        Console.WriteLine("| Service Type | Implementation | Lifetime |");
        Console.WriteLine("|--------------|----------------|----------|");

        foreach (var reg in registrations)
        {
            Console.WriteLine($"| {reg.ServiceType} | {reg.Implementation} | {reg.Lifetime} |");
        }
    }

    private static void PrintLine(int serviceWidth, int implWidth, int lifetimeWidth)
    {
        Console.WriteLine($"+{new string('-', serviceWidth + 2)}+{new string('-', implWidth + 2)}+{new string('-', lifetimeWidth + 2)}+");
    }

    private static string GetLine(int serviceWidth, int implWidth, int lifetimeWidth)
    {
        return $"+{new string('-', serviceWidth + 2)}+{new string('-', implWidth + 2)}+{new string('-', lifetimeWidth + 2)}+";
    }

    private static string GetTypeName(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;

        var genericType = type.GetGenericTypeDefinition();
        var genericArgs = type.GetGenericArguments();
        var genericTypeName = genericType.Name.Substring(0, genericType.Name.IndexOf('`'));
        var genericArgsNames = string.Join(", ", genericArgs.Select(t => GetTypeName(t)));

        return $"{genericTypeName}<{genericArgsNames}>";
    }

    private static string GetImplementationName(ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationType != null)
        {
            return GetTypeName(descriptor.ImplementationType);
        }

        if (descriptor.ImplementationFactory != null)
        {
            return "Factory Method";
        }

        if (descriptor.ImplementationInstance != null)
        {
            return $"Instance ({GetTypeName(descriptor.ImplementationInstance.GetType())})";
        }

        return "Unknown";
    }

    public static void PrintDuplicateRegistrations(this IServiceCollection services)
    {
        var duplicates = services
            .GroupBy(s => s.ServiceType)
            .Where(g => g.Count() > 1)
            .OrderBy(g => g.Key.FullName);

        if (!duplicates.Any())
        {
            Console.WriteLine("No duplicate registrations found.");
            return;
        }

        foreach (var group in duplicates)
        {
            Console.WriteLine($"\n{GetTypeName(group.Key)} ({group.Count()} registrations):");
            Console.WriteLine(new string('-', 60));

            foreach (var service in group)
            {
                Console.WriteLine($"  - {GetImplementationName(service)} ({service.Lifetime})");
            }
        }
    }
}
