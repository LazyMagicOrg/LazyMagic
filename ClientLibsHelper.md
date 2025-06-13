# ClientLibsHelper.md

This file provides hints and insights for LLM code agents working with LazyMagic client libraries, specifically focusing on ViewModels, Authentication, Notifications, and LzComponents.

## Library Hierarchy and Dependencies

```
LazyMagic.Shared
    ↓
LazyMagic.Shared.Notifications (adds notification models)
    ↓
LazyMagic.Client.Auth (authentication provider abstraction)
    ↓
LazyMagic.Client.ViewModels (base MVVM implementation)
    ↓
LazyMagic.Client.ViewModels.Auth (adds authentication to ViewModels)
    ↓
LazyMagic.Client.ViewModels.Auth.Notifications (adds real-time updates)
```

## Key Concepts for Code Agents

### 1. Progressive Enhancement Pattern
The libraries follow a progressive enhancement pattern. When implementing features:
- Start with `LazyMagic.Client.ViewModels` for basic MVVM
- Upgrade to `LazyMagic.Client.ViewModels.Auth` when authentication is needed
- Upgrade to `LazyMagic.Client.ViewModels.Auth.Notifications` for real-time features

### 2. Generic Type Conventions
When working with ViewModels, understand these generic parameters:
- `TDTO` - Data Transfer Object from API (minimal data contract)
- `TModel` - Extended model with UI-specific properties (inherits from TDTO)
- `TVM` - ViewModel type for collections (inherits from ILzItemViewModel<TModel>)

Example:
```csharp
public class UserDTO { public string Id; public string Name; }
public class UserModel : UserDTO { public bool IsSelected; }
public class UserViewModel : LzItemViewModelAuth<UserDTO, UserModel> { }
public class UsersViewModel : LzItemsViewModelAuth<UserViewModel, UserDTO, UserModel> { }
```

### 3. ViewModel State Management

**ItemViewModel States:**
- `None` - Default state
- `Create` - Creating new item (not yet saved)
- `Read` - Displaying existing item
- `Update` - Editing existing item
- `Delete` - Marked for deletion

**Edit Workflow:**
```csharp
// Always follow this pattern:
await itemViewModel.OpenEditAsync();    // Creates edit copy
itemViewModel.Data.Property = newValue;  // Modify the Data property
await itemViewModel.SaveEditAsync();     // Saves and exits edit mode
// OR
await itemViewModel.CancelEditAsync();   // Discards changes
```

### 4. ReactiveUI Integration

**Property Types:**
- `[Reactive]` - Two-way bindable properties
- `[ObservableAsProperty]` - Read-only computed properties
- Regular properties - Not reactive, avoid for UI binding

**Common Reactive Patterns:**
```csharp
// Watching property changes
this.WhenAnyValue(x => x.SomeProperty)
    .Subscribe(value => HandleChange(value));

// Creating derived properties
this.WhenAnyValue(x => x.FirstName, x => x.LastName)
    .Select(x => $"{x.Item1} {x.Item2}")
    .ToPropertyEx(this, x => x.FullName);
```

### 5. Service Function Pattern

ViewModels delegate CRUD operations through injected functions:
```csharp
public LzItemViewModel(
    Func<TModel, Task<TModel>> createAsync,    // Create function
    Func<TModel, Task<TModel>> readAsync,      // Read function
    Func<TModel, Task<TModel>> updateAsync,    // Update function
    Func<TModel, Task> deleteAsync)            // Delete function
```

This allows flexible backend integration without coupling ViewModels to specific services.

### 6. Authentication Flow

**AuthProcess States:**
1. `StartSignIn` → `VerifyLogin` → `VerifyPassword` → Authenticated
2. `StartSignUp` → `VerifyLogin` → `VerifyPassword` → `VerifyCode` → Authenticated
3. Various challenge flows (MFA, password reset, etc.)

**Key Authentication Properties:**
- `AuthProcess.IsAuthenticated` - Current auth status
- `AuthProcess.AuthState` - Detailed state machine state
- `AuthProcess.Creds` - Current credentials/tokens

### 7. Notification Integration

When using `LazyMagic.Client.ViewModels.Auth.Notifications`:
- Items automatically update when notifications arrive
- No manual subscription needed - handled by base classes
- Notifications filtered by current user's subscriptions
- Updates respect ViewModel state (won't update during edit)

### 8. Session ViewModel Pattern

`ILzSessionViewModel` serves as the application hub:
```csharp
public interface ILzSessionViewModel
{
    ILzClientConfig Config { get; }
    IInternetConnectivity InternetConnectivity { get; }
    ILzMessages Msgs { get; }
    Task InitAsync();
}
```

All ViewModels receive the session in their constructor for access to global services.

## Common Implementation Patterns

### Creating a New ItemViewModel
Example using a `Pet` DTO and `PetModel'.
```csharp
namespace ViewModels;
using LazyMagic.Client.FactoryGenerator; // do not put in global using. Causes runtime error.

[Factory]
public class PetViewModel : LzItemViewModelAuthNotifications<Pet, PetModel>
{
    public PetViewModel(
        [FactoryInject] ILoggerFactory loggerFactory,   
        ISessionViewModel sessionViewModel,
        ILzParentViewModel parentViewModel,
        Pet pet,
        bool? isLoaded = null
        ) : base(loggerFactory, sessionViewModel, pet, model: null, isLoaded) 
    {
        _sessionViewModel = sessionViewModel;   
        ParentViewModel = parentViewModel;
        _DTOCreateAsync = sessionViewModel.Store.AddPetAsync;
        _DTOReadAsync = sessionViewModel.Store.GetPetByIdAsync;
        _DTOUpdateAsync = sessionViewModel.Store.UpdatePetAsync;
        _DTODeleteAsync = sessionViewModel.Store.DeletePetAsync;   
    }
    private ISessionViewModel _sessionViewModel;
    public override string Id => Data?.Id ?? string.Empty;
    public override long UpdatedAt => Data?.UpdateUtcTick ?? long.MaxValue;

}
```

### Creating a New ItemsViewModel
```csharp
namespace ViewModels;
using LazyMagic.Client.FactoryGenerator; // do not put in global using. Causes runtime error.
[Factory]
/// <inheritdoc/>
public class PetsViewModel : LzItemsViewModelAuthNotifications<PetViewModel, Pet, PetModel>
{
    public PetsViewModel(
        [FactoryInject]ILoggerFactory loggerFactory,
        ISessionViewModel sessionViewModel,
        [FactoryInject] IPetViewModelFactory petViewModelFactory) : base(loggerFactory, sessionViewModel)  
    { 
        _sessionViewModel = sessionViewModel;
        PetViewModelFactory = petViewModelFactory;
        _DTOReadListAsync = sessionViewModel.Store.ListPetsAsync;

    }
    private ISessionViewModel _sessionViewModel;
    public IPetViewModelFactory? PetViewModelFactory { get; init; }
    /// <inheritdoc/>
    public override (PetViewModel, string) NewViewModel(Pet dto)
        => (PetViewModelFactory!.Create(_sessionViewModel, this, dto), string.Empty);
    public Func<Task<string>>? SvcTestAsync { get; init; }
    public async Task<string> TestAsync()
    {
        if (SvcTestAsync is null)
            return string.Empty;
        return await SvcTestAsync();    
    }
    /// <inheritdoc/>
    public override async Task<(bool, string)> ReadAsync(bool forceload = false)
    => await base.ReadAsync(forceload);
}
```

### Using ClientSDK to call Service API
1. **We always use a ClientSDK ** to call the service API.
2. **We assign methods from the ClientSDK to the ViewModel _DTO* properties**
3. **The SessionViewModel contains properties that hold references to ViewModels used during the session**
4. **The SessionViewModel contains a reference to each ClientSDK instance used by the application.**
5. **The SessionsViewModel is injected. A SessionViewModel is created for each user session.**
6. **For WebApps the SessionsViewModel usuall creates only a single SessionViewModel. **


### Working with Edit Mode
```csharp
// In your UI code or command handler
var userVM = usersViewModel.CurrentViewModel;

// Start editing
await userVM.OpenEditAsync();

// Make changes
userVM.Data.Name = "New Name";
userVM.Data.Email = "new@email.com";

// Save or cancel
if (isValid)
    await userVM.SaveEditAsync();  // Persists to backend
else
    await userVM.CancelEditAsync(); // Reverts changes
```

## Blazor Integration

### Component Base Classes
LazyMagic.Blazor provides specialized component base classes for ViewModel integration:

1. **LzComponentBase** - Base class with Messages support
   - Provides `Msg()` and `Img()` helpers for localization
   - No ViewModel binding

2. **LzComponentBase<T>** - Reactive component base
   - Implements ReactiveUI's `IViewFor<T>` pattern
   - Auto-subscribes to ViewModel property changes
   - Triggers StateHasChanged on ViewModel updates
   - Three variants for ViewModel acquisition:
     - `LzComponentBaseInjectViewModel` - ViewModel from DI
     - `LzComponentBaseAssignViewModel` - ViewModel created in OnInitializedAsync
     - `LzComponentBasePassViewModel` - ViewModel as component parameter

3. **Layout Component Bases** - Mirror structure for LayoutComponentBase

### JavaScript Interop Patterns

1. **LzBaseJSModule** - Abstract base for JS modules
   ```csharp
   public class MyJSModule : LzBaseJSModule
   {
       protected override string ModulePath => "./_content/MyLib/myModule.js";
       
       public async Task<string> CallMyFunction(string arg)
           => await InvokeAsync<string>("myFunction", arg);
   }
   ```

2. **Built-in JS Services:**
   - `BlazorInternetConnectivity` - Network status monitoring
   - `ClipboardService` - Clipboard operations
   - `BlazorContentAccess` - Content fetching with hybrid support

### Blazor Component Patterns

1. **ViewModel Integration:**
   ```razor
   @inherits LzComponentBaseInjectViewModel<IUserViewModel>
   
   <div>
       <h3>@ViewModel.Data.Name</h3>
       <button @onclick="@(() => ViewModel.OpenEditAsync())">Edit</button>
   </div>
   ```

2. **Navigation Pattern:**
   - **We do not use route parameters in Blazor navigation**
   - **Inject SessionsViewModel and select ViewModels programmatically**
   ```razor
   @page "/users"
   @inject ISessionsViewModel SessionsVM
   
   @code {
       private IUsersViewModel UsersVM => SessionsVM.CurrentSession.UsersViewModel;
       
       protected override async Task OnInitializedAsync()
       {
           await UsersVM.ReadAsync();
       }
   }
   ```

3. **Reactive Subscriptions:**
   - Subscriptions deferred to OnAfterRender to avoid JS conflicts
   - Automatic disposal of subscriptions
   - StateHasChanged triggered automatically

### Platform-Specific Services

1. **IOSAccess Implementation:**
   - `BlazorOSAccess` for hybrid apps with JS fallback
   - `BlazorStaticAssets` for pure web apps using HttpClient

2. **Service Registration:**
   ```csharp
   // In Program.cs
   services.AddScoped<IOSAccess, BlazorOSAccess>();
   services.AddScoped<IStaticAssets, BlazorStaticAssets>();
   services.AddScoped<IInternetConnectivitySvc, BlazorInternetConnectivity>();
   ```

### Best Practices for Blazor Components

1. **Component Lifecycle:**
   - Initialize ViewModels in OnInitializedAsync
   - Defer JS interop to OnAfterRenderAsync
   - Dispose subscriptions in Dispose method

2. **Reactive Patterns:**
   - Prefer reactive bindings over manual StateHasChanged
   - Use ViewModel property changes to drive UI updates
   - Leverage Messages for dynamic localization

3. **JavaScript Interop:**
   - Extend LzBaseJSModule for new JS modules
   - Handle module disposal properly
   - Use safe invocation methods that check disposal state

4. **Error Handling:**
   - Wrap ViewModel operations in try-catch
   - Display errors using Messages system
   - Handle network connectivity issues gracefully

 
## Namespace Conventions
1. **We don't use the default namespace pattern where the namespace matches the folder structure.**
2. **We use a flat namespace structure for ViewModels and components.*
3. **We use global usings in a GlobalUsing.cs file in each project.**

## Blazor Component Conventions
1. **Do not use Cascading Parameters. Use ViewModel state instead.**
2. **Do not use Route Parameters. Use ViewModel state instead.**
3. **Use LzComponentBase variants for ViewModel integration.**
5. **Name all page components that use a ViewModel descended from LzItemViewModel with "EditPage" suffix (e.g., UserEditPage.razor).**
6. **Name all pages components that use a ViewModel descended from LzItemsViewModel with "ListPage" suffix (e.g., UsersListPage.razor).**
7. **Name all non-page components, that use a ViewModel descended from LzItemViewModel, with "Display" suffix (e.g., UserDisplay.razor).**


## Important Gotchas and Tips

1. **Always use Data property for model access** - Don't access Model directly
2. **Check CanEdit before OpenEditAsync** - Respects business rules
3. **SaveEditAsync can throw** - Always handle exceptions for API failures
4. **Use Factory pattern for ViewModels** - Enables proper DI and testing
5. **Only use propertes available in the ViewModel's <TModel>** - Avoid using properties not defined in the ViewModel's model

## Testing Considerations

When mocking these libraries:
1. Mock at the service function level, not the ViewModel level
2. Use `TestScheduler` for ReactiveUI time-based operations
3. Mock `ILzSessionViewModel` for isolated ViewModel testing
4. Test state transitions explicitly (None → Edit → None)
5. Verify Parent callback interactions for child ViewModels

## Performance Tips

1. Use `autoChildLoad: false` for large collections
2. Implement pagination at the service level
3. Dispose subscriptions in ViewModel disposal
4. Use `[ObservableAsProperty]` for expensive computations
5. Batch updates when possible to reduce notifications