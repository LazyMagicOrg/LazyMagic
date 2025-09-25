# LazyMagic.Blazor

## Overview

LazyMagic.Blazor is a comprehensive Blazor component library that provides foundational infrastructure for building reactive Blazor applications. It integrates ReactiveUI for state management, offers JavaScript interop capabilities, and provides specialized base components for different ViewModel patterns.

## Key Features

- **Reactive Components**: Base component classes with ReactiveUI integration for automatic UI updates
- **JavaScript Interop**: Comprehensive JavaScript module system with safe invocation patterns
- **Platform Abstraction**: Support for both pure Blazor and MAUI Hybrid applications
- **Browser Utilities**: Cookie management, local storage, clipboard access, and browser fingerprinting
- **Responsive Design**: Window size tracking and responsive layout components
- **Localization**: Built-in message localization system support

## Core Classes

### Base Components

**LzComponentBase**
- Base class for all LazyMagic Blazor components
- Provides message localization support through `ILzMessages`

**LzComponentBase&lt;T&gt;**
- Generic reactive component implementing `IViewFor<T>`
- Automatic property change notifications and UI updates
- Three variants for different ViewModel patterns:
  - `LzComponentBaseInjectViewModel<T>` - Dependency injection
  - `LzComponentBaseAssignViewModel<T>` - Manual assignment
  - `LzComponentBasePassViewModel<T>` - Parameter passing

**LzLayoutComponentBase&lt;T&gt;**
- Layout-specific versions of the reactive components
- Same three ViewModel pattern variants as regular components

### JavaScript Services

**LzJsUtilities**
- Browser utilities: localStorage, cookies, sharing, memory management
- Asset caching and service worker management
- JSON cookie serialization/deserialization

**BrowserFingerprintService**
- Device and browser fingerprinting
- User agent, timezone, and language detection

**ConnectivityService**
- Network connectivity monitoring
- Reactive state updates for online/offline status

**ClipboardService**
- Clipboard read/write operations
- Text content management

### UI Components

**LzAppLoading**
- Animated loading progress component
- Customizable duration and acceleration

**WindowFade**
- Fade-in effect component
- Automatic resize handling

**WindowResize**
- Window size tracking
- Cascading values for responsive design

## Platform Support

**BlazorOSAccess**
- Implements `IOSAccess` for Blazor applications
- Handles differences between pure Blazor and MAUI Hybrid

**BlazorStaticAssets**
- Static content and configuration access
- Support for both deployment models

## Usage Example

```csharp
// In Program.cs
builder.Services.AddLazyMagicBlazor();

// Component using dependency injection
@inherits LzComponentBaseInjectViewModel<MyViewModel>

<div>
    <h1>@ViewModel.Title</h1>
    <button @onclick="@(() => ViewModel.UpdateCommand.Execute().Subscribe())">
        Update
    </button>
</div>

// JavaScript interop
@inject ILzJsUtilities JsUtilities

@code {
    protected override async Task OnInitializedAsync()
    {
        // Set a cookie
        await JsUtilities.SetCookie("user-pref", "dark-mode");
        
        // Check connectivity
        var isOnline = ConnectivityService.IsOnline;
    }
}
```

## Configuration

```csharp
// Program.cs
builder.Services.AddLazyMagicBlazor();
builder.Services.AddLazyMagicBlazorMessages("Messages");
```

## Key Patterns

1. **ReactiveUI Integration**: All components support reactive ViewModels with automatic UI updates
2. **Safe JavaScript Interop**: Error handling and disconnection scenarios managed automatically
3. **Disposal Patterns**: Proper cleanup with both `IDisposable` and `IAsyncDisposable`
4. **Message Localization**: Built-in support for localized UI messages

## Dependencies

- ReactiveUI.Blazor for reactive programming
- BlazorPro.BlazorSize for window size detection
- Microsoft.JSInterop for JavaScript interop
- Microsoft.AspNetCore.Components.WebAssembly