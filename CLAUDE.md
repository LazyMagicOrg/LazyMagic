# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

LazyMagic is a comprehensive .NET framework/library ecosystem for building full-stack applications with AWS cloud services, authentication, and modern UI frameworks. It provides abstractions for authentication, data access, and UI components while supporting multiple UI frameworks (Blazor, Blazorise, MudBlazor).

## Key Architecture Concepts

### Package Structure
- **LazyMagic.Client.***: Client-side libraries including ViewModels, Auth, and UI components
- **LazyMagic.Service.***: Server-side libraries for AWS services, data repositories, and WebSocket notifications
- **LazyMagic.Shared.***: Shared interfaces and models between client and server
- **LazyMagic.Blazor***: UI components for different Blazor frameworks
- **LazyMagic.BlazorSvg**: SVG manipulation and visualization components with advanced geometric algorithms

### Core Patterns
1. **MVVM with ReactiveUI**: All ViewModels inherit from NotifyBase and use ReactiveUI for state management
2. **Repository Pattern**: DynamoDB access through IDYDBRepository with envelope-based data storage
3. **Source Generators**: Heavy use of code generation for factories, models, and ViewModels
4. **Authentication Abstraction**: IAuthProvider interface with AWS Cognito implementation

## Common Development Commands

### Building
```bash
# Build entire solution (automatically creates NuGet packages in ./Packages/)
dotnet build

# Build specific project
dotnet build LazyMagic.Client.ViewModels/LazyMagic.Client.ViewModels.csproj

# Clean all build artifacts
./DeleteObjAndBin.ps1 -RootPath .
```

### Testing
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test LazyMagic.AuthTest/LazyMagic.AuthTest.csproj
dotnet test LazyMagic.Service.Test/LazyMagic.Service.Test.csproj
```

### Running Sample Applications
```bash
# Run Blazor WebAssembly test apps
dotnet run --project BlazorTest.WASM/BlazorTest.WASM.csproj
dotnet run --project BlazorizeTest.WASM/BlazorizeTest.WASM.csproj
dotnet run --project MudBlazorTest.WASM/MudBlazorTest.WASM.csproj
```

### Package Management
- Version is managed in CommonPackageHandling.targets (currently 3.0.1)
- Packages auto-build to ./Packages/ folder
- All projects use Central Package Management via Directory.Packages.props

## Key Technical Details

### ViewModels Architecture
- Base classes: LzViewModel, LzItemViewModel<T>, LzItemsViewModel<T>
- State management through LzItemViewModelState enum (None, Create, Read, Update, Delete)
- Automatic property change notifications via Fody.ReactiveUI
- Factory pattern using source-generated factories

### Authentication Flow
- AuthProcess manages authentication state and operations
- Supports email/password and phone authentication
- Integration with AWS Cognito for token management
- SigV4 signing for authenticated API requests

### Data Access
- DYDBRepository provides CRUD operations for DynamoDB
- Envelope pattern wraps items with metadata (Id, CreatedAt, UpdatedAt, etc.)
- Query operations return partial content with pagination support
- Tenancy support built into repository pattern

### UI Component Integration
- Components inherit from LzComponentBase variants
- Support for ViewModel injection, assignment, or passing
- JavaScript interop through IJSModule pattern
- Localization through LzMessages system

### SVG Components (LazyMagic.BlazorSvg)
- **SvgViewer Component**: Interactive SVG rendering with pan/zoom capabilities
- **Geometric Algorithms**: Max-inscribed rectangle computation, boardroom layout generation
- **Build-Time Precomputation**: Node.js-based pipeline for SVG polygon analysis
- **Embedded Data Pattern**: Precomputed geometric data embedded directly in SVG files
- **Test Infrastructure**: Playwright-based automated testing for visual verification
- **Documentation**: Comprehensive algorithm documentation in LazyMagic.BlazorSvg/Documentation/

## Important Conventions

1. **Fody Integration**: ViewModels and other reactive classes use Fody.ReactiveUI - properties automatically implement INotifyPropertyChanged
2. **Dependency Injection**: Use [Singleton], [Scoped], or [Transient] attributes for automatic DI registration
3. **Factory Generation**: Use [LzFactory] attribute to generate factory classes
4. **Model Generation**: Use partial classes with [LzModel] for automatic model generation
5. **JavaScript Modules**: Place .js files in wwwroot and create corresponding C# wrapper classes

## Recent Features (V3.0.x)

### Client Services
1. **ConnectivityService**: IConnectivityService for network connectivity monitoring
2. **Cookie Management**: Cookie handling methods in LzJsUtilities (GetCookie, SetCookie, DeleteCookie)
3. **Window Management**: WindowFade and WindowResize components for responsive UI
4. **Browser Fingerprinting**: BrowserFingerprintService for device identification
5. **Clipboard Service**: IClipboardService for clipboard operations
6. **Static Assets Management**: IStaticAssets interface and BlazorStaticAssets implementation

### SVG & Geometric Capabilities (LazyMagic.BlazorSvg)
1. **Max-Inscribed Rectangle Algorithm**: Finds largest rectangle that fits within irregular polygons
   - Binary search expansion with configurable aspect ratios
   - Multiple centroid sampling for optimal placement
   - Polylabel algorithm for optimal centroid selection
2. **Boardroom Layout System**: Automatic furniture placement within complex room geometries
   - Layout configuration via JSON
   - Collision detection and boundary validation
   - Support for tables, chairs, and other furniture elements
3. **SVG Data Embedding**: Build-time precomputation pipeline
   - Node.js scripts for polygon analysis
   - Embedded data stored in SVG metadata
   - Runtime lookup for instant access
4. **Geometric Primitives**: Point-in-polygon, line intersection, bounding boxes, convex hull
5. **FloorMat**: Build-time pipeline for generating precomputed layout data

### Source Generators
1. **LazyMagic.LzItemViewModelGenerator**: Generates ViewModel boilerplate code
2. **LazyMagic.Client.TreeViewModel**: Tree view model generation with ILzTreeNode support

### Testing & Development
- Three test applications demonstrating different UI frameworks:
  - **BlazorTest.WASM**: Standard Blazor components (includes SVG test pages)
  - **BlazoriseTest.WASM**: Blazorise framework integration (includes Camera component demo)
  - **MudBlazorTest.WASM**: MudBlazor framework integration
- All test apps share common ViewModels from BlazorTest.ViewModels project
- FloorMat pipeline for layout data generation in LazyMagic.BlazorSvg/FloorMat/

### JavaScript Interop Patterns
- **Base class**: LzBaseJSModule for all JS module wrappers
- **Standard pattern**: Override ModuleFileName property to specify JS file path
- **Initialization**: Use Initialize() method with DotNetObjectReference for callbacks
- **Safe invocation**: InvokeSafeAsync/InvokeSafeVoidAsync methods for error handling
- **SVG-specific**: SvgViewerJS class wraps Snap.svg for interactive SVG manipulation

## Key Documentation Locations

### LazyMagic.BlazorSvg Documentation
Located in `LazyMagic.BlazorSvg/Documentation/`:
- **InscribedRectangle-Guide.md**: Complete system architecture and usage guide
- **Algorithms-Implementation.md**: Low-level algorithm implementations with mathematical foundations
- **BoardroomLayoutPipeline.md**: Boardroom layout system documentation
- **EmbedData-QuickRef.md**: Quick command reference for data regeneration

### General Documentation
- **CLAUDE.md**: This file - guidance for AI assistants working with this codebase
- **RUN_BOARDROOM_TEST.md**: Instructions for running boardroom layout tests