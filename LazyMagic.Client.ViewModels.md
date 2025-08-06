# LazyMagic.Client.ViewModels

## Overview

LazyMagic.Client.ViewModels provides a comprehensive MVVM (Model-View-ViewModel) architecture for building reactive client applications. Built on ReactiveUI, it offers base classes and patterns for managing single items, collections, sessions, and complex parent-child relationships with full CRUD support, validation, and state management.

## Key Features

- **Reactive ViewModels**: Base classes with ReactiveUI integration for automatic UI updates
- **CRUD Operations**: Built-in support for Create, Read, Update, Delete operations
- **State Management**: Sophisticated state tracking for items and collections
- **Validation**: Integration with FluentValidation and Blazor's EditContext
- **Parent-Child Relationships**: Callback-based communication between collections and items
- **Session Management**: Orchestration of services and application state
- **Connectivity Awareness**: Built-in support for online/offline scenarios

## Core Classes

### Base ViewModel

**LzViewModel**
- Abstract base class for all ViewModels
- Inherits from ReactiveUI's `ReactiveObject`
- Provides logging infrastructure and disposal pattern
- Manages subscriptions through `CompositeDisposable`

### Item ViewModels

**ILzItemViewModel&lt;TModel&gt; / LzItemViewModel&lt;TDTO, TModel&gt;**
- Manages single data items with full CRUD support
- State tracking: New, Edit, Current, Deleted
- Authorization flags for operations (CanCreate, CanRead, etc.)
- Optimistic concurrency through version tracking
- Parent-child relationship support

**LzItemViewModelState**
```csharp
public enum LzItemViewModelState
{
    New,      // Creating new item
    Edit,     // Editing existing item
    Current,  // Viewing current item
    Deleted   // Item has been deleted
}
```

**LzEditContext&lt;TDTO, TModel&gt;**
- Form editing capabilities with validation
- Integrates with Blazor's EditContext
- FluentValidation support
- Dirty state tracking
- Create/Update operations with optimistic locking

### Collection ViewModels

**ILzItemsViewModel&lt;TVM, TDTO, TModel&gt; / LzItemsViewModel&lt;TVM, TDTO, TModel&gt;**
- Manages collections of ViewModels
- Implements `INotifyCollectionChanged` for UI binding
- Tracks CurrentViewModel, EditViewModel, LastViewModel
- Automatic child loading support
- Navigation between items (previous/next)
- CRUD callbacks from child ViewModels

### Session Management

**ILzSessionViewModel / LzSessionViewModel**
- Orchestrates connection to services
- Manages connectivity state
- Handles message localization
- Session lifecycle management (Init, Load, Unload)

**ILzSessionsViewModel&lt;T&gt; / LzSessionsViewModel&lt;T&gt;**
- Manages multiple session instances
- Session creation, deletion, and switching
- Tracks session logins

## Usage Examples

### Creating an Item ViewModel
```csharp
public class UserModel : User
{
    // User extends the User DTO with additional properties
}

public class UserViewModel : LzItemViewModel<User, UserModel>
{
    public UserViewModel(
        ILzParentViewModel parentViewModel,
        User user,
        bool? isLoaded = null
    ) : base(user, isLoaded)
    {
        ParentViewModel = parentViewModel;
    }
    
    protected override async Task<(bool, string)> CreateAsync()
    {
        // Custom create logic
        var result = await UserService.CreateAsync(Model);
        return (result.Success, result.Message);
    }
}
```

### Creating a Collection ViewModel
```csharp
public class UsersViewModel : LzItemsViewModel<UserViewModel, User, UserModel>
{
    private readonly IUserService _userService;
    
    public UsersViewModel(IUserService userService)
    {
        _userService = userService;
    }
    
    protected override async Task<List<User>> ReadDataAsync()
    {
        return await _userService.GetUsersAsync();
    }
    
    protected override UserViewModel CreateViewModel(User dto)
    {
        return new UserViewModel(this, dto);
    }
}
```

### Using Edit Context
```csharp
// In a Blazor component
@inherits LzComponentBase<UserViewModel>

<EditForm EditContext="@ViewModel.EditContext.EditContext">
    <DataAnnotationsValidator />
    <ValidationSummary />
    
    <InputText @bind-Value="ViewModel.Model.Name" />
    
    <button @onclick="@(() => ViewModel.UpdateCommand.Execute().Subscribe())">
        Save
    </button>
</EditForm>
```

### Session Management
```csharp
public class AppSessionViewModel : LzSessionViewModel
{
    protected override async Task<(bool, string)> InitAsync()
    {
        // Initialize session services
        await AuthService.InitializeAsync();
        return (true, "Session initialized");
    }
    
    protected override async Task<(bool, string)> LoadAsync()
    {
        // Load session data
        await LoadUserDataAsync();
        return (true, "Session loaded");
    }
}
```

## Key Patterns

### Generic Type System
- **TDTO**: Data Transfer Object (raw data from services)
- **TModel**: Extended model (inherits from TDTO, adds reactive properties)
- **TVM**: ViewModel type for collections

### Parent-Child Communication
```csharp
public interface ILzParentViewModel
{
    void ItemCreated(object item);
    void ItemDeleted(object item);
    void ItemUpdated(object item);
    void ItemUpdateCanceled(object item);
    void ItemRead(object item);
}
```

### State Management Flow
1. **New**: Item being created, not yet saved
2. **Edit**: Existing item being modified
3. **Current**: Item in read-only state
4. **Deleted**: Item marked for deletion

### Validation Integration
- FluentValidation for business rules
- Blazor EditContext for form validation
- Automatic validation state management
- Custom validation message handling

## Advanced Features

### Connectivity Service
```csharp
public interface IConnectivityService
{
    bool IsOnline { get; set; }
    IObservable<bool> IsOnlineObservable { get; }
}
```

### Factory Support
- Works with LazyMagic.Client.FactoryGenerator
- Automatic DI registration through LzViewModelFactory
- Convention-based interface registration

### Data Cloning
- Preserves event subscriptions during cloning
- Uses Force.DeepCloner for deep object copying
- Special handling for reactive properties

## Best Practices

1. **Use Factory Pattern**: Leverage FactoryGenerator for ViewModels with dependencies
2. **Implement Validation**: Use FluentValidation for complex business rules
3. **Handle Disposal**: Always dispose ViewModels to prevent memory leaks
4. **Use Reactive Properties**: Mark properties with `[Reactive]` for automatic UI updates
5. **Manage State**: Use the state enum to control UI behavior
6. **Error Handling**: Return tuple `(bool success, string message)` from async operations

## Dependencies

- ReactiveUI & ReactiveUI.Fody for reactive programming
- FluentValidation for model validation
- Force.DeepCloner for object cloning
- Microsoft.AspNetCore.Components.Forms for Blazor integration
- LazyMagic.Client.Base for base functionality