using ReactiveUI.Builder;

namespace LazyMagic.Blazor;

/// <summary>
/// Centralized ReactiveUI initialization for LazyMagic client apps.
///
/// ReactiveUI 23.x removed automatic initialization (20.x auto-initialized).
/// It must now be initialized explicitly via the builder BEFORE any
/// WhenAnyValue / [Reactive] usage — and the LazyMagic / BaseApp ViewModel
/// base classes call WhenAnyValue in their constructors, which run during the
/// first component render. Without initialization, the first reactive call
/// throws TypeInitializationException on ReactiveNotifyPropertyChangedMixin.
///
/// Call the platform-appropriate method ONCE at app startup, before the host
/// runs (WASM: top of Program.Main; MAUI: in MauiProgram before Build()).
/// Both methods are idempotent.
/// </summary>
public static class LzReactiveUI
{
    private static readonly object _gate = new();
    private static bool _initialized;

    /// <summary>
    /// Initialize ReactiveUI for a Blazor WebAssembly host (single-threaded
    /// WASM scheduler). Idempotent.
    /// </summary>
    public static void InitializeWasm()
    {
        lock (_gate)
        {
            if (_initialized) return;
            RxAppBuilder.CreateReactiveUIBuilder()
                .WithBlazorWasm()
                .BuildApp();
            _initialized = true;
        }
    }

    /// <summary>
    /// Initialize ReactiveUI for a MAUI Blazor Hybrid host (Blazor components
    /// rendered in a BlazorWebView on the native MAUI runtime; dispatcher-based
    /// scheduler). Idempotent.
    /// </summary>
    public static void InitializeHybrid()
    {
        lock (_gate)
        {
            if (_initialized) return;
            RxAppBuilder.CreateReactiveUIBuilder()
                .WithBlazor()
                .BuildApp();
            _initialized = true;
        }
    }
}
