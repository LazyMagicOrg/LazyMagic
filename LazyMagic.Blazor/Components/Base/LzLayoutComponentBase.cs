// Copyright (c) 2022 .NET Foundation and Contributors. All rights reserved.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// LazyMagic adaptation of ReactiveUI.Blazor's ReactiveLayoutComponentBase<T>.
// See LzComponentBase.cs for the equivalent ComponentBase variant — both share
// LzComponentState<T>, which centralizes the reactive subscription plumbing.

namespace LazyMagic.Blazor;

/// <summary>
/// A base layout component for handling property changes and updating the Blazor view appropriately.
/// </summary>
public class LzLayoutComponentBase : LayoutComponentBase
{
    [Inject]
    public ILzMessages? Messages { get; set; }
    protected virtual MarkupString Msg(string key, bool ignoreUseInspect = false) => (MarkupString)Messages!.Msg(key, ignoreUseInspect);
    protected virtual string Img(string key, bool ignoreUseInspect = false) => Messages!.Img(key, ignoreUseInspect);

    //protected virtual MarkupString Img(string key, bool ignoreUseInspect = false) => (MarkupString)Messages!.Img(key, ignoreUseInspect);
}

/// <summary>
/// A base layout component for handling property changes and updating the Blazor view appropriately.
/// </summary>
/// <typeparam name="T">The type of view model. Must support INotifyPropertyChanged.</typeparam>
public class LzLayoutComponentBase<T> : LzLayoutComponentBase, IViewFor<T>, INotifyPropertyChanged, ICanActivate, IDisposable
    where T : class, INotifyPropertyChanged
{
    private readonly LzComponentState<T> _state = new();

    private T? _viewModel;

    private bool _disposedValue; // To detect redundant calls

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    [Parameter]
    public T? ViewModel
    {
        get => _viewModel;
        set
        {
            if (EqualityComparer<T?>.Default.Equals(_viewModel, value))
            {
                return;
            }

            _viewModel = value;
            OnPropertyChanged();
        }
    }

    /// <inheritdoc />
    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = (T?)value;
    }

    /// <inheritdoc />
    public IObservable<Unit> Activated => _state.Activated;

    /// <inheritdoc />
    public IObservable<Unit> Deactivated => _state.Deactivated;

    /// <inheritdoc />
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) below.
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _state.SignalInitialized();
        base.OnInitialized();
    }

    /// <summary>
    /// Override of Blazor's async init hook. Like OnInitialized, signals the
    /// Activated observable — the call is idempotent so Activated still fires
    /// exactly once per component lifetime even though Blazor invokes both
    /// methods. (Upstream ReactiveUI.Blazor only signals from the sync hook.)
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        _state.SignalInitialized();
        await base.OnInitializedAsync();
    }

    /// <inheritdoc/>
    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            // Wire up subscriptions on first render rather than in OnInitialized
            // because some JavaScript frameworks conflict with reactive
            // subscriptions established during the initial sync init path.
            _state.WireUpSubscriptions(
                viewModelChanges: this.WhenAnyValue(x => x.ViewModel),
                messagesChanges: this.WhenAnyValue(x => x.Messages),
                triggerStateHasChanged: () => InvokeAsync(StateHasChanged));
        }

        base.OnAfterRender(firstRender);
    }

    /// <summary>
    /// Invokes the property changed event.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Cleans up the managed resources of the object.
    /// </summary>
    /// <param name="disposing">If it is getting called by the Dispose() method rather than a finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _state.Dispose();
            }

            _disposedValue = true;
        }
    }
}
