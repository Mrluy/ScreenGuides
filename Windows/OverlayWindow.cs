using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Win32;
using ScreenGuides.Models;

namespace ScreenGuides.Windows;

public sealed class OverlayWindow : IDisposable
{
    private readonly GuideState _state;
    private readonly Dictionary<Guid, GuideLineWindow> _windows = [];
    private bool _disposed;

    public OverlayWindow(GuideState state)
    {
        _state = state;
        _state.Guides.CollectionChanged += Guides_CollectionChanged;
        _state.PropertyChanged += State_PropertyChanged;
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

        foreach (var guide in _state.Guides)
        {
            AddGuideWindow(guide);
        }
    }

    public void Show()
    {
        foreach (var window in _windows.Values)
        {
            window.RefreshFromState();
            if (!window.IsVisible)
            {
                window.Show();
            }
        }
    }

    public void Hide()
    {
        foreach (var window in _windows.Values)
        {
            window.Hide();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _state.Guides.CollectionChanged -= Guides_CollectionChanged;
        _state.PropertyChanged -= State_PropertyChanged;
        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;

        foreach (var window in _windows.Values.ToList())
        {
            window.Close();
        }

        _windows.Clear();
    }

    private void Guides_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            SyncGuideWindows();
            return;
        }

        if (e.OldItems is not null)
        {
            foreach (GuideLine guide in e.OldItems)
            {
                if (_windows.Remove(guide.Id, out var window))
                {
                    window.Close();
                }
            }
        }

        if (e.NewItems is not null)
        {
            foreach (GuideLine guide in e.NewItems)
            {
                AddGuideWindow(guide);
            }
        }
    }

    private void State_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GuideState.IsOverlayVisible))
        {
            if (_state.IsOverlayVisible)
            {
                Show();
            }
            else
            {
                Hide();
            }
        }
        else if (e.PropertyName is nameof(GuideState.IsLocked) or
                 nameof(GuideState.LineColor) or
                 nameof(GuideState.LineOpacity) or
                 nameof(GuideState.LineThickness) or
                 nameof(GuideState.ShowCoordinates))
        {
            foreach (var window in _windows.Values)
            {
                window.RefreshFromState();
            }
        }
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        foreach (var window in _windows.Values)
        {
            window.Reposition();
        }
    }

    private void AddGuideWindow(GuideLine guide)
    {
        if (_windows.ContainsKey(guide.Id))
        {
            return;
        }

        var window = new GuideLineWindow(_state, guide);
        _windows.Add(guide.Id, window);

        if (_state.IsOverlayVisible)
        {
            window.Show();
        }
    }

    private void SyncGuideWindows()
    {
        var currentIds = _state.Guides.Select(guide => guide.Id).ToHashSet();
        foreach (var id in _windows.Keys.Where(id => !currentIds.Contains(id)).ToList())
        {
            var window = _windows[id];
            _windows.Remove(id);
            window.Close();
        }

        foreach (var guide in _state.Guides)
        {
            AddGuideWindow(guide);
        }
    }
}
