

namespace LazyMagic.Client.Auth;

public interface IMethodMapWrapper
{
    Dictionary<string, MethodMapEntry> MethodMap { get; }
}

public class MethodMapWrapper : IMethodMapWrapper
{
    public Dictionary<string, MethodMapEntry> MethodMap { get; init; } = new();
}

public class MethodMapEntry
{
    public int SecurityLevel { get; set; } = 0; 
}