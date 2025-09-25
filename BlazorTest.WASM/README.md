# BlazorTest.WASM - MVVM Architecture Overview

This Blazor WebAssembly application demonstrates a clean MVVM (Model-View-ViewModel) architecture using the LazyMagic framework ecosystem.

## Architecture Layers

### View Layer (Blazor Components)
- **Main.razor**: Root component handling app initialization and routing
- **Pages**: Login, Sessions, Home pages inheriting from `LzComponentBase` variants
- **Layout**: MainLayout and NavMenu components for app structure
- Components utilize `LzComponentBaseInjectViewModel<T>` for automatic ViewModel injection

### ViewModel Layer
- **SessionsViewModel**: Extends `LzSessionsViewModelAuth<ISessionViewModel>` managing session lifecycle
- **SessionViewModel**: Implements authentication flow, marked with `[Factory]` for source generation
- ViewModels use ReactiveUI with Fody for automatic property change notifications
- Factory pattern implemented via source generators for dependency injection

### Service Layer (LazyMagic Libraries)

#### Core Services
- **ILzHost**: Manages environment configuration (API URLs, platform detection)
- **ILzHttpClient**: HTTP client with authentication support (SigV4 signing)
- **ILzClientConfig**: Client configuration management with dynamic loading
- **IStaticAssets**: Static asset management for multi-tenant scenarios

#### Authentication Services (LazyMagic.Client.Auth.Cognito)
- **IAuthProcess**: Authentication workflow management
- **IAuthProviderCognito**: AWS Cognito integration
- Supports email/password and phone authentication flows

#### UI Services (LazyMagic.Blazor)
- **ILzJsUtilities**: JavaScript interop utilities
- **IConnectivityService**: Network connectivity monitoring
- **IOSAccess**: Platform-specific functionality abstraction
- **ILzMessages**: Localization and messaging system

#### State Management
- **LzViewModel**: Base class for all ViewModels
- **LzItemViewModel<T>**: CRUD operations for single items
- **LzItemsViewModel<T>**: Collection management with pagination
- State tracking via `LzItemViewModelState` enum

## Service Registration

The application uses a modular service registration approach:

1. **Program.cs**: Registers core services (HttpClient, IStaticAssets, ILzHost)
2. **ConfigApp.AddApp()**: Orchestrates modular registration
3. **ConfigBlazorUI.AddBlazorUI()**: Adds LazyMagic authentication services
4. **ConfigViewModels.AddViewModels()**: Registers ViewModels and supporting services
5. **ViewModelsRegisterFactories**: Auto-generated factory registrations

## Key Patterns

- **Dependency Injection**: Manual service registration with modular configuration methods
- **Factory Generation**: [Factory] and [FactoryInject] attributes for source generation
- **Reactive State**: Fody.ReactiveUI integration for automatic INotifyPropertyChanged
- **Authentication Flow**: Centralized through IAuthProcess with provider abstraction
- **Multi-tenancy**: Built-in support via IStaticAssets and configuration system





