# LazyMagic.Client.FactoryGenerator Library Hints

This document provides guidance for working with the LazyMagic.Client.FactoryGenerator library.

## Overview

LazyMagic.Client.FactoryGenerator is a Roslyn incremental source generator that automatically creates factory classes and interfaces for classes annotated with `[Factory]`. It simplifies the factory pattern for ViewModels by separating DI-injected dependencies from runtime parameters.

**Target Framework:** netstandard2.0 (required for Roslyn analyzers)
**Dependencies:** Microsoft.CodeAnalysis.CSharp

---

## Project Structure

```
LazyMagic.Client.FactoryGenerator/
├── LazyMagicAttributes.cs       # Factory and FactoryInject attributes
├── LazyMagicFactoryGenerator.cs # The incremental source generator
└── GlobalUsing.cs               # Roslyn API imports
```

---

## Attributes

### FactoryAttribute
**Location:** `LazyMagicAttributes.cs`

Marks a class for factory generation.

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class FactoryAttribute : Attribute { }
```

### FactoryInjectAttribute
**Location:** `LazyMagicAttributes.cs`

Marks constructor parameters that should be injected into the factory (via DI) rather than passed to the `Create()` method.

```csharp
[AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = true)]
public sealed class FactoryInjectAttribute : Attribute { }
```

---

## How It Works

### Input: Annotated Class

```csharp
[Factory]
public class CustomerViewModel : LzItemViewModel<string, CustomerDTO, CustomerModel>
{
    public CustomerViewModel(
        [FactoryInject] ILoggerFactory loggerFactory,    // Injected via DI
        [FactoryInject] ICustomerService customerService, // Injected via DI
        CustomerDTO? dto = null)                          // Passed to Create()
        : base(loggerFactory, dto: dto)
    {
        // ...
    }
}
```

### Output: Generated Factory Interface and Class

The generator produces `ICustomerViewModelFactory.g.cs`:

```csharp
namespace MyApp.ViewModels
{
    public interface ICustomerViewModelFactory
    {
        CustomerViewModel Create(CustomerDTO? dto = null);
    }

    public class CustomerViewModelFactory : ICustomerViewModelFactory
    {
        private ILoggerFactory loggerFactory;
        private ICustomerService customerService;

        public CustomerViewModelFactory(ILoggerFactory loggerFactory, ICustomerService customerService)
        {
            this.loggerFactory = loggerFactory;
            this.customerService = customerService;
        }

        public CustomerViewModel Create(CustomerDTO? dto = null)
        {
            return new CustomerViewModel(loggerFactory, customerService, dto);
        }
    }
}
```

### Output: Registration Class

The generator also produces `MyApp.ViewModels.RegisterFactories.g.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MyApp.ViewModels;

public static class MyAppViewModelsRegisterFactories
{
    public static void MyAppViewModelsRegister(IServiceCollection services)
    {
        services.TryAddTransient<ICustomerViewModelFactory, CustomerViewModelFactory>();
        // ... other factories in same namespace
    }
}
```

---

## Generator Logic

### LazyMagicFactoryGenerator
**Location:** `LazyMagicFactoryGenerator.cs`

Implements `IIncrementalGenerator` for efficient, incremental code generation.

**Key Steps:**

1. **Find Target Classes** - Identifies classes with `[Factory]` attribute
2. **Group by Namespace** - Groups classes for registration file generation
3. **Generate Factory** - For each class:
   - Extracts constructor parameters
   - Separates `[FactoryInject]` parameters (for DI) from others (for `Create()`)
   - Generates interface with `Create()` method
   - Generates factory class with DI constructor
4. **Generate Registration** - Creates bulk registration method per namespace

**Diagnostic:**
- ID: `LMF0001` - Info-level messages during generation

---

## Usage Patterns

### Basic ViewModel Factory

```csharp
using LazyMagic.Client.FactoryGenerator;

namespace MyApp.ViewModels;

[Factory]
public class ProductViewModel : LzItemViewModel<string, ProductDTO, ProductModel>
{
    public ProductViewModel(
        [FactoryInject] ILoggerFactory loggerFactory,
        [FactoryInject] IProductService productService,
        ProductDTO? dto = null,
        bool loadChildren = true)
        : base(loggerFactory, dto: dto)
    {
        _service = productService;
        AutoLoadChildren = loadChildren;
    }

    // ...
}
```

**Generated Interface:**
```csharp
public interface IProductViewModelFactory
{
    ProductViewModel Create(ProductDTO? dto = null, bool loadChildren = true);
}
```

### Using the Factory

```csharp
public class ProductsViewModel : LzItemsViewModel<string, ProductViewModel, ProductDTO, ProductModel>
{
    private readonly IProductViewModelFactory _factory;

    public ProductsViewModel(
        ILoggerFactory loggerFactory,
        IProductViewModelFactory factory)  // Inject the factory
        : base(loggerFactory)
    {
        _factory = factory;
    }

    public override (ProductViewModel, string) NewViewModel(ProductDTO dto)
    {
        var vm = _factory.Create(dto);  // Use factory to create instance
        vm.ParentViewModel = this;
        return (vm, dto.Id!);
    }
}
```

### Registering Factories

In `Program.cs` or service configuration:

```csharp
// Option 1: Use generated registration method
MyAppViewModelsRegisterFactories.MyAppViewModelsRegister(services);

// Option 2: Register individually
services.AddTransient<IProductViewModelFactory, ProductViewModelFactory>();
```

---

## Project Reference Configuration

To use this generator in a consuming project:

```xml
<ItemGroup>
    <ProjectReference
        Include="..\LazyMagic.Client.FactoryGenerator\LazyMagic.Client.FactoryGenerator.csproj"
        OutputItemType="Analyzer"
        ReferenceOutputAssembly="true" />
</ItemGroup>
```

**Important:** Both `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="true"` are required:
- `OutputItemType="Analyzer"` - Loads the generator as a Roslyn analyzer
- `ReferenceOutputAssembly="true"` - Makes attributes available at compile time

---

## Generated Files

| File Pattern | Description |
|--------------|-------------|
| `I{ClassName}Factory.g.cs` | Interface and factory class for each `[Factory]` class |
| `{Namespace}.RegisterFactories.g.cs` | Bulk registration method per namespace |

---

## Key Design Decisions

1. **Incremental Generator** - Uses `IIncrementalGenerator` for efficient rebuilds (only regenerates when source changes)
2. **Attribute-Based Separation** - `[FactoryInject]` clearly distinguishes DI parameters from runtime parameters
3. **Per-Namespace Registration** - Groups factories by namespace for organized registration
4. **TryAddTransient** - Uses `TryAdd` to avoid duplicate registration errors
5. **Normalized Whitespace** - Generated code is formatted for readability

---

## Common Pitfalls

1. **Attribute Import** - Must import `LazyMagic.Client.FactoryGenerator` namespace to use attributes
2. **Build Order** - Generator runs at compile time; changes require rebuild
3. **Constructor Required** - Class must have a constructor for parameter analysis
4. **No Partial Support** - Generator creates complete factory; cannot be partial
5. **Namespace in Registration** - Registration method name removes dots from namespace (e.g., `MyApp.ViewModels` → `MyAppViewModelsRegister`)

---

## Debugging Generator Issues

1. **View Generated Files** - In Visual Studio, expand Dependencies → Analyzers → LazyMagic.Client.FactoryGenerator
2. **Check Build Output** - Generator emits `LMF0001` info messages
3. **Clean & Rebuild** - Source generators cache; clean build may be needed
4. **Check Attribute Namespace** - Ensure `using LazyMagic.Client.FactoryGenerator;` is present

---

## Example: Complete ViewModel with Factory

```csharp
using LazyMagic.Client.FactoryGenerator;
using LazyMagic.Client.ViewModels;

namespace MyApp.ViewModels;

[Factory]
public class OrderViewModel : LzItemViewModel<string, OrderDTO, OrderModel>
{
    private readonly IOrderService _service;
    private readonly IProductViewModelFactory _productFactory;

    public OrderViewModel(
        [FactoryInject] ILoggerFactory loggerFactory,
        [FactoryInject] IOrderService orderService,
        [FactoryInject] IProductViewModelFactory productFactory,
        OrderDTO? dto = null)
        : base(loggerFactory, dto: dto)
    {
        _service = orderService;
        _productFactory = productFactory;
        _EntityName = "Order";

        _DTOCreateAsync = async (d) => await _service.CreateAsync(d);
        _DTOReadAsync = async (id) => await _service.GetAsync(id);
        _DTOUpdateAsync = async (d) => await _service.UpdateAsync(d);
        _DTODeleteAsync = async (id) => await _service.DeleteAsync(id);
    }

    public override string? Id => Data?.Id;
    public override long UpdatedAt => Data?.UpdatedAt ?? 0;

    // Factory creates: new OrderViewModel(loggerFactory, orderService, productFactory, dto)
    // Where loggerFactory, orderService, productFactory come from DI
    // And dto is passed to Create(dto)
}
```

**Usage:**
```csharp
// In DI setup
services.AddTransient<IOrderViewModelFactory, OrderViewModelFactory>();

// In consuming code
public class OrdersPage
{
    private readonly IOrderViewModelFactory _factory;

    public OrdersPage(IOrderViewModelFactory factory)
    {
        _factory = factory;
    }

    public OrderViewModel CreateNewOrder()
    {
        return _factory.Create();  // dto defaults to null
    }

    public OrderViewModel LoadOrder(OrderDTO dto)
    {
        return _factory.Create(dto);
    }
}
```
