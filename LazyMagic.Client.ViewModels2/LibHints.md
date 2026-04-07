# LazyMagic.Client.ViewModels2 Library Hints

This document provides guidance for working with the LazyMagic.Client.ViewModels2 library.

## Overview

LazyMagic.Client.ViewModels2 is a foundational MVVM library providing:
- ReactiveUI-based ViewModel base classes
- CRUD operations for single items (LzItemViewModel)
- Collection management for lists of items (LzItemsViewModel)
- Session management (LzSessionViewModel, LzSessionsViewModel)
- FluentValidation integration
- Edit context with Blazor Forms integration

**Target Framework:** net9.0
**Dependencies:** ReactiveUI, ReactiveUI.Fody, FluentValidation, DeepCloner, DynamicData, Microsoft.AspNetCore.Components.Forms, LazyMagic.Client.Base, LazyMagic.Shared

---

## Project Structure

```
LazyMagic.Client.ViewModels2/
├── Base/
│   ├── LzViewModel.cs           # Base ViewModel class
│   ├── LzViewModelFactory.cs    # Auto-registration factory
│   ├── IItemId.cs               # Generic Id interface
│   └── CloningExtensions.cs     # Event subscription preservation
├── Item/
│   ├── ILzItemViewModel.cs      # Single item interface
│   ├── LzItemViewModel.cs       # Single item CRUD ViewModel
│   ├── LzItemViewModelState.cs  # State enum + notification docs
│   ├── ILzItemViewModelData.cs  # Data interface
│   └── LzEditContext.cs         # Edit context with validation
├── Items/
│   ├── ILzItemsViewModel.cs     # Collection interface
│   ├── LzItemsViewModel.cs      # Collection management ViewModel
│   └── ILzParentViewModel.cs    # Parent callbacks interface
├── Session/
│   ├── ILzSessionViewModel.cs   # Session interface
│   ├── LzSessionViewModel.cs    # Session ViewModel
│   ├── ILzSessionsViewModel.cs  # Multi-session interface
│   └── LzSessionsViewModel.cs   # Multi-session management
├── DTOs/
│   ├── ITenantConfig.cs         # Tenant config interface
│   └── TenantConfig.cs          # Tenant config DTO
├── Config/
│   └── ConfigureLazyMagicClientViewModels.cs
├── TenantConfigViewModel.cs     # Tenant config ViewModel
└── GlobalUsings.cs
```

---

## Core Concepts

### ViewModel State Machine

`LzItemViewModelState` defines the lifecycle states for item ViewModels:

| State | Description |
|-------|-------------|
| `New` | Item created locally, not yet persisted |
| `Edit` | Item being edited (copy saved for cancel) |
| `Current` | Item loaded from storage, not being edited |
| `Deleted` | Item deleted from storage |

### Type Parameters Convention

Throughout the library, these type parameter names are used:

| Parameter | Description |
|-----------|-------------|
| `TId` | Type of the item identifier (string, Guid, etc.) |
| `TDTO` | Data Transfer Object type (plain class) |
| `TModel` | Extended model (must inherit from TDTO, implement IRegisterObservables) |
| `TVM` | ViewModel type (must implement ILzItemViewModel) |

---

## Base Classes

### LzViewModel
**Location:** `Base/LzViewModel.cs`

Abstract base class for all ViewModels with logging and subscription disposal.

**Key Features:**
- Inherits from `ReactiveObject`
- Implements `IDisposable`
- Provides `CompositeDisposable Subscriptions` for managing Rx subscriptions
- Logging methods for error reporting

```csharp
public abstract class LzViewModel : ReactiveObject, IDisposable
{
    protected readonly ILogger _logger;
    protected readonly CompositeDisposable Subscriptions = new();

    protected virtual string Log(MethodBase m, string msg)
    protected virtual string Log(string userMsg, string detailedMsg)
}
```

### LzViewModelFactory
**Location:** `Base/LzViewModelFactory.cs`

Auto-registration utility for ViewModels based on marker interfaces.

**Marker Interfaces:**
| Interface | Registration |
|-----------|--------------|
| `ILzSingleton` | Singleton lifetime |
| `ILzTransient` | Transient lifetime |
| `ILzScoped` | Scoped lifetime |

**Usage:**
```csharp
// In Program.cs or Startup
LzViewModelFactory.RegisterLz(services, typeof(MyViewModel).Assembly);
```

---

## Item ViewModel

### ILzItemViewModel<TId, TModel>
**Location:** `Item/ILzItemViewModel.cs`

Interface for single-item CRUD ViewModels.

**Key Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `Id` | TId? | Item identifier (abstract) |
| `UpdatedAt` | long | Timestamp for optimistic locking |
| `Data` | TModel? | The actual data model |
| `State` | LzItemViewModelState | Current state |
| `CanCreate/CanRead/CanUpdate/CanDelete` | bool | Permission flags |
| `IsLoaded` | bool | Data loaded from storage |
| `IsNew/IsEdit/IsCurrent/IsDeleted` | bool | State convenience properties |
| `IsDirty` | bool | Data modified since load |
| `IsBusy` | bool | Operation in progress |
| `ParentViewModel` | ILzParentViewModel<TId>? | Parent for callbacks |

**Key Methods:**
```csharp
Task<(bool, string)> CreateAsync()          // Persist new item
Task<(bool, string)> ReadAsync(TId? id)     // Load item from storage
Task<(bool, string)> UpdateAsync()          // Persist changes
Task<(bool, string)> DeleteAsync(TId? id)   // Remove from storage
Task<(bool, string)> SaveEditAsync()        // Create or Update based on state
Task<(bool, string)> OpenEditAsync(bool forceCopy = false)  // Enter edit mode
Task<(bool, string)> CancelEditAsync()      // Discard changes
Task<(bool, string)> ValidateAsync()        // Override for validation
Task<(bool, string)> ReadChildrenAsync(bool forceload)  // Load related data
```

### LzItemViewModel<TId, TDTO, TModel>
**Location:** `Item/LzItemViewModel.cs`

Abstract implementation of single-item CRUD operations.

**Constructor:**
```csharp
public LzItemViewModel(
    ILoggerFactory loggerFactory,
    TDTO? dto = null,           // Initialize from DTO
    TModel? model = null,       // Or initialize from Model
    bool? isLoaded = null)      // Override loaded state
```

**Protected Delegate Properties (set in derived class):**
```csharp
protected Func<TDTO, Task<TDTO>>? _DTOCreateAsync { get; init; }
protected Func<TId, Task<TDTO>>? _DTOReadAsync { get; init; }
protected Func<TDTO, Task<TDTO>>? _DTOUpdateAsync { get; init; }
protected Func<TId, Task>? _DTODeleteAsync { get; init; }
```

**Usage Pattern:**
```csharp
public class CustomerViewModel : LzItemViewModel<string, CustomerDTO, CustomerModel>
{
    public CustomerViewModel(
        ILoggerFactory loggerFactory,
        ICustomerService customerService,
        CustomerDTO? dto = null)
        : base(loggerFactory, dto: dto)
    {
        _DTOCreateAsync = async (dto) => await customerService.CreateAsync(dto);
        _DTOReadAsync = async (id) => await customerService.ReadAsync(id);
        _DTOUpdateAsync = async (dto) => await customerService.UpdateAsync(dto);
        _DTODeleteAsync = async (id) => await customerService.DeleteAsync(id);
    }

    public override string? Id => Data?.Id;
    public override long UpdatedAt => Data?.UpdatedAt ?? 0;
}
```

---

## Items ViewModel

### ILzItemsViewModel<TId, TVM, TDTO, TModel>
**Location:** `Items/ILzItemsViewModel.cs`

Interface for managing a collection of item ViewModels.

**Key Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `Id` | TId? | Optional list identifier |
| `ViewModels` | Dictionary<TId, TVM> | The collection |
| `CurrentViewModel` | TVM? | Currently selected item |
| `EditViewModel` | TVM? | Item being edited (or null for new) |
| `LastViewModel` | TVM? | Previously selected item |
| `IsLoaded` | bool | Collection loaded |
| `IsLoading` | bool | Load in progress |
| `AutoReadChildren` | bool | Auto-load item children |

**Key Methods:**
```csharp
void Clear()
Task<(bool, string)> ReadAsync(bool forceload = false)
Task<(bool, string)> ReadAsync(TId parentId, bool forceload = false)
(TVM viewmodel, TId id) NewViewModel(TDTO dto)  // Factory method (must implement)
```

### LzItemsViewModel<TId, TVM, TDTO, TModel>
**Location:** `Items/LzItemsViewModel.cs`

Abstract implementation of collection management.

**Protected Delegate Properties:**
```csharp
protected Func<TId, Task<ICollection<TDTO>>>? _DTOReadListIdAsync { get; init; }
protected Func<Task<ICollection<TDTO>>>? _DTOReadListAsync { get; init; }
```

**Parent Callbacks (called by item ViewModels):**
```csharp
Task ItemCreated(object itemViewModel, bool makeCurrentItem = true)
Task ItemDeleted(TId id)
Task ItemUpdated(TId id)
Task ItemUpdateCanceled(TId id)
Task ItemRead(TId id)
```

**Usage Pattern:**
```csharp
public class CustomersViewModel : LzItemsViewModel<string, CustomerViewModel, CustomerDTO, CustomerModel>
{
    private readonly ICustomerService _service;
    private readonly ILoggerFactory _loggerFactory;

    public CustomersViewModel(
        ILoggerFactory loggerFactory,
        ICustomerService service)
        : base(loggerFactory)
    {
        _service = service;
        _loggerFactory = loggerFactory;
        _DTOReadListAsync = async () => await service.ListAsync();
    }

    public override (CustomerViewModel, string) NewViewModel(CustomerDTO dto)
    {
        var vm = new CustomerViewModel(_loggerFactory, _service, dto);
        vm.ParentViewModel = this;
        return (vm, dto.Id!);
    }
}
```

### ILzParentViewModel<TId>
**Location:** `Items/ILzParentViewModel.cs`

Callback interface for parent-child ViewModel communication.

```csharp
public interface ILzParentViewModel<TId>
{
    Task ItemCreated(object itemViewModel, bool makeCurrentItem = true);
    Task ItemDeleted(TId id);
    Task ItemUpdated(TId id);
    Task ItemUpdateCanceled(TId id);
    Task ItemRead(TId id);
}
```

---

## Session ViewModel

### ILzSessionViewModel
**Location:** `Session/ILzSessionViewModel.cs`

Interface for session-level state management.

**Key Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `ConnectivityService` | IConnectivityService | Network monitoring |
| `SessionName` | string | Display name |
| `SessionId` | string | Unique identifier |
| `IsOnline` | bool | Network status |
| `IsLoaded` | bool | Session data loaded |
| `IsLoading` | bool | Load in progress |
| `MessageSetSelector` | LzMessageSetSelector | Culture/units selection |

### LzSessionViewModel
**Location:** `Session/LzSessionViewModel.cs`

Base session ViewModel with connectivity and localization.

**Key Features:**
- Tracks network connectivity via `IConnectivityService`
- Manages localization via `ILzMessages`
- Auto-updates message set when `MessageSetSelector` changes

```csharp
public abstract class LzSessionViewModel : LzViewModel, ILzSessionViewModel
{
    public IConnectivityService ConnectivityService { get; set; }
    public ILzMessages Messages { get; set; }
    [ObservableAsProperty] public bool IsOnline { get; }
    [Reactive] public LzMessageSetSelector MessageSetSelector { get; set; }

    public virtual Task InitAsync();
    public virtual Task LoadAsync();
    public virtual Task UnloadAsync();
}
```

### LzSessionsViewModel<T>
**Location:** `Session/LzSessionsViewModel.cs`

Multi-session management for applications supporting multiple user sessions.

```csharp
public abstract class LzSessionsViewModel<T> : LzViewModel, ILzSessionsViewModel<T>
    where T : ILzSessionViewModel
{
    [Reactive] public T? SessionViewModel { get; set; }
    public IDictionary<string, string> SessionLogins { get; }

    public virtual Task<bool> CreateSessionAsync();
    public virtual T CreateSessionViewModel();  // Factory (must implement)
    public virtual Task DeleteAsync(string sessionId);
    public virtual Task SetAsync(string sessionId);
}
```

---

## Edit Context

### LzEditContext<T1, T2>
**Location:** `Item/LzEditContext.cs`

Blazor EditContext wrapper with FluentValidation integration.

**Key Features:**
- Creates EditContext for Blazor Forms
- Integrates with FluentValidation validators
- Tracks modified state and validation
- Provides Create/Update operations

**Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `EditContext` | EditContext | Blazor edit context |
| `CanUpdate` | bool | Valid and modified (not new) |
| `CanCreate` | bool | Valid and modified (new) |
| `IsNew` | bool | Creating new vs editing existing |

**Constructor:**
```csharp
public LzEditContext(
    T1 baseItem,                                    // Source item
    Func<T1, Task<(bool, string, T1?)>> createAsync, // Create callback
    Func<T1, Task<(bool, string, T1?)>> updateAsync, // Update callback
    bool isNew = false)                             // Is this a new item?
```

**Usage:**
```csharp
// In your component
var editContext = new LzEditContext<CustomerDTO, CustomerModel>(
    customer,
    async (dto) => await service.CreateAsync(dto),
    async (dto) => await service.UpdateAsync(dto),
    isNew: true);

// In Blazor form
<EditForm EditContext="@editContext.EditContext">
    <DataAnnotationsValidator />
    <!-- fields -->
    <button disabled="@(!editContext.CanCreate)" @onclick="editContext.CreateAsync">Save</button>
</EditForm>
```

---

## Notification Support

The `LzItemViewModelState.cs` file contains extensive documentation about notification handling:

### Notification Edit Options

When an item is being edited and a notification arrives:

| Option | Behavior |
|--------|----------|
| `Cancel` | Cancel edit, update from notification |
| `Merge` | Maintain DataCopy, NotificationData, and Data; let UI decide |

### Notification Subscription Pattern
```csharp
// In your ViewModel constructor
this.WhenAnyValue(x => x.NotificationSvc.Notification)
    .Where(x => x.PayloadId.Equals(Id))
    .Subscribe(x => UpdateFromNotification(x.Payload));
```

---

## DTOs and Configuration

### TenantConfig
**Location:** `DTOs/TenantConfig.cs`

Simple tenant configuration DTO.

```csharp
public class TenantConfig : ITenantConfig
{
    public string SeeMoreUrl { get; set; }
    public string TenantName { get; set; }
}
```

### TenantConfigViewModel
**Location:** `TenantConfigViewModel.cs`

ViewModel for loading tenant configuration.

```csharp
public class TenantConfigViewModel : LzViewModel, ITenantConfigViewModel
{
    public TenantConfig? TenantConfig { get; set; }
    [Reactive] public bool IsLoaded { get; set; }

    public virtual async Task ReadAsync(string url);
}
```

---

## Service Registration

### ConfigureLazyMagicClientViewModels
**Location:** `Config/ConfigureLazyMagicClientViewModels.cs`

```csharp
services.AddLazyMagicClientViewModels();
```

Registers:
- `ITenantConfigViewModel` → `TenantConfigViewModel` (Scoped)

---

## Usage Patterns

### Complete Item ViewModel Example

```csharp
public class ProductViewModel : LzItemViewModel<string, ProductDTO, ProductModel>
{
    private readonly IProductService _service;

    public ProductViewModel(
        ILoggerFactory loggerFactory,
        IProductService service,
        ProductDTO? dto = null)
        : base(loggerFactory, dto: dto)
    {
        _service = service;
        _EntityName = "Product";

        // Wire up CRUD delegates
        _DTOCreateAsync = async (dto) => await _service.CreateAsync(dto);
        _DTOReadAsync = async (id) => await _service.GetAsync(id);
        _DTOUpdateAsync = async (dto) => await _service.UpdateAsync(dto);
        _DTODeleteAsync = async (id) => await _service.DeleteAsync(id);
    }

    public override string? Id => Data?.Id;
    public override long UpdatedAt => Data?.UpdatedAt ?? 0;

    // Custom validation
    public override async Task<(bool, string)> ValidateAsync()
    {
        if (string.IsNullOrEmpty(Data?.Name))
            return (false, "Name is required");
        return await base.ValidateAsync();
    }

    // Load related data
    public override async Task<(bool, string)> ReadChildrenAsync(bool forceload)
    {
        // Load related entities
        return (true, string.Empty);
    }
}
```

### UI Integration

```razor
@inject ProductViewModel ViewModel

<h1>@(ViewModel.IsNew ? "New Product" : "Edit Product")</h1>

<EditForm Model="@ViewModel.Data">
    <InputText @bind-Value="ViewModel.Data!.Name" />

    @if (ViewModel.IsNew)
    {
        <button @onclick="CreateProduct" disabled="@ViewModel.IsBusy">Create</button>
    }
    else
    {
        <button @onclick="SaveProduct" disabled="@(!ViewModel.IsDirty || ViewModel.IsBusy)">Save</button>
        <button @onclick="CancelEdit">Cancel</button>
    }
</EditForm>

@code {
    async Task CreateProduct() => await ViewModel.CreateAsync();
    async Task SaveProduct() => await ViewModel.UpdateAsync();
    async Task CancelEdit() => await ViewModel.CancelEditAsync();
}
```

---

## Key Design Decisions

1. **Tuple Return Pattern** - All async operations return `(bool success, string message)` for consistent error handling
2. **Delegate-Based CRUD** - Service operations injected as delegates for flexibility and testability
3. **JSON-Based Copy/Restore** - Uses JSON serialization for edit backup to preserve event subscriptions
4. **Parent-Child Callbacks** - Items notify parent collections via callbacks instead of events
5. **Observable State** - State changes automatically compute `IsNew`, `IsEdit`, `IsCurrent`, `IsDeleted` via ReactiveUI
6. **Fody Integration** - `[Reactive]` and `[ObservableAsProperty]` attributes for automatic property notifications

---

## Common Pitfalls

1. **Must set `_DTO*Async` delegates** - CRUD operations throw if delegates not assigned
2. **Override `Id` and `UpdatedAt`** - Abstract properties must be implemented
3. **Call `RegisterObservables()` on Model** - TModel must implement IRegisterObservables
4. **Set `ParentViewModel`** - For callbacks to work, assign ParentViewModel when creating items
5. **Don't use DeepCloner for Data** - Uses JSON instead to preserve event subscriptions
6. **Dispose subscriptions** - Add Rx subscriptions to `Subscriptions` composite disposable
7. **EditViewModel navigation** - Set EditViewModel before navigating to edit view; null = create new
