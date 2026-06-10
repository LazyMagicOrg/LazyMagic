// Copyright (c) 2022 .NET Foundation and Contributors. All rights reserved.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// LazyMagic adaptation of ReactiveUI.Blazor 23.x's internal ReactiveComponentState<T>
// pattern — extracts the reactive subscription plumbing shared by
// LzComponentBase<T> and LzLayoutComponentBase<T> so it lives in one place.

namespace LazyMagic.Blazor;

/// <summary>
/// Encapsulates the reactive subscription state used by
/// <see cref="LzComponentBase{T}"/> and <see cref="LzLayoutComponentBase{T}"/>.
/// Owns the Activated / Deactivated subjects, the CompositeDisposable, and the
/// "trigger StateHasChanged on ViewModel or Messages change" pipeline.
/// </summary>
/// <typeparam name="TViewModel">The component's ViewModel type.</typeparam>
internal sealed class LzComponentState<TViewModel> : IDisposable
    where TViewModel : class, INotifyPropertyChanged
{
    private readonly Subject<Unit> _initSubject = new();

    [SuppressMessage("Design", "CA2213: Dispose object", Justification = "Used for deactivation.")]
    private readonly Subject<Unit> _deactivateSubject = new();

    private readonly CompositeDisposable _compositeDisposable = new();

    private bool _initSignaled;
    private bool _disposed;

    /// <summary>
    /// Fires when the component is initialized. Guaranteed to fire at most once
    /// per component lifetime — Blazor invokes both OnInitialized and
    /// OnInitializedAsync, but only the first call to <see cref="SignalInitialized"/>
    /// produces an emission.
    /// </summary>
    public IObservable<Unit> Activated => _initSubject.AsObservable();

    /// <summary>
    /// Fires when the component is being disposed.
    /// </summary>
    public IObservable<Unit> Deactivated => _deactivateSubject.AsObservable();

    /// <summary>
    /// Idempotent signal that the owning component has initialized. The owning
    /// component should call this from BOTH OnInitialized() and
    /// OnInitializedAsync() — the second call is a no-op, which fixes the
    /// double-fire bug present in the pre-refresh hand-ported version.
    /// </summary>
    public void SignalInitialized()
    {
        if (_initSignaled) return;
        _initSignaled = true;
        _initSubject.OnNext(Unit.Default);
    }

    /// <summary>
    /// Wires up the reactive subscriptions that trigger StateHasChanged when
    /// either the ViewModel or the Messages service raises PropertyChanged.
    /// Call from the owning component's OnAfterRender(firstRender: true).
    /// </summary>
    /// <param name="viewModelChanges">Source observable for ViewModel changes.</param>
    /// <param name="messagesChanges">Source observable for Messages changes.</param>
    /// <param name="triggerStateHasChanged">Component callback that schedules a re-render.</param>
    public void WireUpSubscriptions(
        IObservable<TViewModel?> viewModelChanges,
        IObservable<ILzMessages?> messagesChanges,
        Action triggerStateHasChanged)
    {
        SubscribeWithPropertyChanged(viewModelChanges, triggerStateHasChanged);
        SubscribeWithPropertyChanged(messagesChanges, triggerStateHasChanged);
    }

    /// <summary>
    /// For a sequence of INotifyPropertyChanged-bearing values, schedule
    /// StateHasChanged both when the reference itself changes AND when the
    /// current reference raises PropertyChanged.
    /// </summary>
    private void SubscribeWithPropertyChanged<TSource>(
        IObservable<TSource?> source,
        Action triggerStateHasChanged)
        where TSource : class, INotifyPropertyChanged
    {
        // Publish + RefCount(2) so the upstream is subscribed only once,
        // but both downstream branches observe the same value stream.
        var shared = source.Where(x => x is not null).Publish().RefCount(2);

        shared
            .Subscribe(_ => triggerStateHasChanged())
            .DisposeWith(_compositeDisposable);

        shared
            .WhereNotNull()
            .Select(x => x!.ToPropertyChangedObservable())
            .Switch()
            .Subscribe(_ => triggerStateHasChanged())
            .DisposeWith(_compositeDisposable);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _initSubject.Dispose();
        _compositeDisposable.Dispose();
        _deactivateSubject.OnNext(Unit.Default);
    }
}

/// <summary>
/// Extension methods bridging INotifyPropertyChanged to IObservable&lt;Unit&gt;.
/// Centralizes the FromEvent boilerplate that previously appeared four times
/// across LzComponentBase&lt;T&gt; and LzLayoutComponentBase&lt;T&gt;.
/// </summary>
internal static class NotifyPropertyChangedObservableExtensions
{
    /// <summary>
    /// Returns an observable that emits Unit each time the source raises
    /// PropertyChanged. Subscription attaches the handler, disposal detaches it.
    /// </summary>
    public static IObservable<Unit> ToPropertyChangedObservable(this INotifyPropertyChanged source)
        => Observable.FromEvent<PropertyChangedEventHandler?, Unit>(
            eventHandler =>
            {
                void Handler(object? sender, PropertyChangedEventArgs e) => eventHandler(Unit.Default);
                return Handler;
            },
            eh => source.PropertyChanged += eh,
            eh => source.PropertyChanged -= eh);
}
