namespace LazyMagic.LzItemViewModelGenerator;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class LzViewModelNoDTOValidatorAttribute : Attribute { }
public sealed class LzViewModelNoModelAttribute : Attribute { }
public sealed class LzViewModelNoModelValidator : Attribute { }


