namespace LazyMagic.Client.FactoryGenerator;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class FactoryAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = true)]
public sealed class FactoryInjectAttribute : Attribute { } 