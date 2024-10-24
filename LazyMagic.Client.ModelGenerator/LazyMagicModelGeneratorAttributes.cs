namespace LazyMagic.Client.ModelGenerator;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class LzModelAttribute : Attribute 
{
    public string BaseClassName { get; set; }
    public LzModelAttribute(string baseClassName)
    {
        BaseClassName = baseClassName;
    }
}
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class LzModelValidatorAttribute : Attribute 
{
    public string BaseClassName { get; set; }
    public bool IncludeDTO { get; set; }
    public LzModelValidatorAttribute(string baseClassName, bool includeDTO)
    {
        BaseClassName = baseClassName;
        IncludeDTO = includeDTO;
    }
}

