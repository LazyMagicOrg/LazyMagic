# LazyMagic.Client.FactoryGenerator

## Overview

LazyMagic.Client.FactoryGenerator is a C# source generator that automatically creates factory classes for dependency injection. It simplifies the creation of objects with mixed constructor parameters - some injected via DI and others passed at runtime. This is particularly useful for ViewModels and other classes that require both service dependencies and runtime data.

## Key Features

- **Automatic Factory Generation**: Generates factory interfaces and implementations for classes marked with `[Factory]`
- **Mixed Parameter Support**: Handles constructors with both DI-injected and runtime parameters
- **Service Registration**: Generates registration methods for easy DI container setup
- **Clean Separation**: Keeps DI concerns separate from business logic
- **Type Safety**: Maintains full IntelliSense and compile-time type checking

## How It Works

### Attributes

**[Factory]**
- Applied to classes that need a factory
- Triggers generation of factory interface and implementation

**[FactoryInject]**
- Applied to constructor parameters that should be injected by DI
- These parameters become dependencies of the factory class

### Generated Code

For each class marked with `[Factory]`, the generator creates:

1. **Factory Interface** (`I{ClassName}Factory`)
   - Contains a `Create` method with non-injected parameters
   
2. **Factory Implementation** (`{ClassName}Factory`)
   - Constructor accepts all `[FactoryInject]` parameters
   - `Create` method accepts runtime parameters
   - Combines both to instantiate the target class

Additionally, the generator creates per namespace:

3. **Factory Registration Class** (`{NamespaceNameNoDots}RegisterFactories`)
   - Static class with registration method for all factories in the namespace
   - Method name: `{NamespaceNameNoDots}Register`
   - Automatically registers all generated factories as transient services

## Usage Example

### Source Class
```csharp
[Factory]
public class SessionViewModel : LzViewModel
{
    public SessionViewModel(
        [FactoryInject] IAuthProcess authProcess,
        [FactoryInject] ILogger<SessionViewModel> logger,
        IUserViewModel userViewModel,
        Session session,
        bool? isLoaded = null
    ) : base(session, isLoaded)
    {
        // authProcess and logger are injected by DI
        // userViewModel, session, and isLoaded are passed at runtime
    }
}
```

### Generated Factory
```csharp
public interface ISessionViewModelFactory
{
    SessionViewModel Create(
        IUserViewModel userViewModel,
        Session session,
        bool? isLoaded = null);
}

public class SessionViewModelFactory : ISessionViewModelFactory
{
    private IAuthProcess authProcess;
    private ILogger<SessionViewModel> logger;
    
    public SessionViewModelFactory(
        IAuthProcess authProcess, 
        ILogger<SessionViewModel> logger)
    {
        this.authProcess = authProcess;
        this.logger = logger;
    }
    
    public SessionViewModel Create(
        IUserViewModel userViewModel,
        Session session,
        bool? isLoaded = null)
    {
        return new SessionViewModel(
            authProcess,
            logger,
            userViewModel,
            session,
            isLoaded);
    }
}
```

### Generated Registration Class
```csharp
namespace MyApp.ViewModels;

public static class MyAppViewModelsRegisterFactories
{
    public static void MyAppViewModelsRegister(IServiceCollection services)
    {
        services.TryAddTransient<ISessionViewModelFactory, SessionViewModelFactory>();
        services.TryAddTransient<IUserViewModelFactory, UserViewModelFactory>();
        // ... other factories in the namespace
    }
}
```

### Using the Factory
```csharp
// In your consuming code
public class SessionService
{
    private readonly ISessionViewModelFactory _factory;
    
    public SessionService(ISessionViewModelFactory factory)
    {
        _factory = factory;
    }
    
    public SessionViewModel CreateSessionViewModel(IUserViewModel user, Session session)
    {
        // Factory handles DI dependencies internally
        return _factory.Create(user, session);
    }
}
```

## Installation

Add to your project file:
```xml
<PackageReference Include="LazyMagic.Client.FactoryGenerator" 
                  Version="3.0.0" 
                  PrivateAssets="all" 
                  OutputItemType="Analyzer" />
```

Note: The `OutputItemType="Analyzer"` attribute is required for the generator to work properly.

## Service Registration

The generator creates a registration helper per namespace:

```csharp
// In Program.cs or Startup.cs
using MyApp.ViewModels;

// Register all factories in the namespace
MyAppViewModelsRegisterFactories.MyAppViewModelsRegister(services);
```

This automatically registers all generated factories as transient services using `TryAddTransient` to avoid duplicate registrations.

## Best Practices

1. **Use for ViewModels**: Particularly useful for MVVM patterns where ViewModels need both services and model data
2. **Keep Factories Simple**: Let the generator handle the boilerplate
3. **Group by Namespace**: Factories are registered per namespace for better organization
4. **Combine with DI Attributes**: Works well with LazyMagic's DI helper attributes

## Technical Details

- Built as an Incremental Source Generator for performance
- Targets .NET Standard 2.0 for broad compatibility
- Generates clean, formatted code
- Includes diagnostic reporting for troubleshooting
- Handles edge cases like optional parameters and default values
- Uses `TryAddTransient` to prevent duplicate service registrations

## Common Scenarios

### ViewModels with Authentication
```csharp
[Factory]
public class UserViewModel : LzItemViewModel<User>
{
    public UserViewModel(
        [FactoryInject] IAuthService auth,
        User user) : base(user)
    {
        // Auth service injected, user passed at creation
    }
}
```

### Nested ViewModels
```csharp
[Factory]
public class ParentViewModel : LzViewModel
{
    public ParentViewModel(
        [FactoryInject] IChildViewModelFactory childFactory,
        ParentModel model) : base(model)
    {
        // Can use injected factory to create children
    }
}
```

### Service with Configuration
```csharp
[Factory]
public class DataService
{
    public DataService(
        [FactoryInject] IHttpClient http,
        [FactoryInject] ILogger logger,
        string endpoint)
    {
        // HTTP client and logger from DI, endpoint at runtime
    }
}
```