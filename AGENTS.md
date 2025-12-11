# AGENTS.md

## Build/Test Commands
```bash
dotnet build                                    # Build entire solution
dotnet build <Project>/<Project>.csproj         # Build specific project
dotnet test                                     # Run all tests
dotnet test <TestProject>.csproj --filter "FullyQualifiedName~TestMethodName"  # Single test
./DeleteObjAndBin.ps1 -RootPath .               # Clean artifacts
```

## Code Style
- **Framework**: .NET 8/9, C# with nullable enabled
- **Namespaces**: File-scoped (`namespace Foo;`), match folder structure
- **Types**: Use `var` for obvious types; explicit types for clarity
- **Naming**: PascalCase for public members, `_camelCase` for private fields
- **Properties**: Use `[Reactive]` attribute for observable properties (Fody.ReactiveUI)
- **Async**: Always suffix async methods with `Async`, return `Task<(bool, string)>` for operations
- **Error handling**: Return tuple `(success, message)` instead of throwing; use `try/catch` at boundaries
- **Generics**: Use `default` not `null` for default parameter values on generic types
- **DI**: Use `[Singleton]`, `[Scoped]`, `[Transient]` attributes for registration
- **Imports**: GlobalUsings.cs per project; avoid per-file using statements
- **Interfaces**: Prefix with `I`, define in separate files
- **ViewModels**: Inherit from `LzViewModel`/`LzItemViewModel<T>`; use `IRegisterObservables` pattern
