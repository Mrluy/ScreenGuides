using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScreenGuides.Models;

public sealed class GuideState : INotifyPropertyChanged
{
    private bool _isOverlayVisible = true;
    private bool _isLocked = true;
    private bool _showCoordinates = true;
    private bool _snapToInteger = true;
    private bool _spanAcrossScreensForNewGuides;
    private string _lineColor = "#00C7D9";
    private double _lineOpacity = 0.88;
    private double _lineThickness = 1.25;
    private double _controlLeft = double.NaN;
    private double _controlTop = double.NaN;

    public GuideState()
    {
        Guides.CollectionChanged += Guides_CollectionChanged;
    }

    public ObservableCollection<GuideLine> Guides { get; } = [];

    public bool IsOverlayVisible
    {
        get => _isOverlayVisible;
        set => SetField(ref _isOverlayVisible, value);
    }

    public bool IsLocked
    {
        get => _isLocked;
        set => SetField(ref _isLocked, value);
    }

    public bool ShowCoordinates
    {
        get => _showCoordinates;
        set => SetField(ref _showCoordinates, value);
    }

    public bool SnapToInteger
    {
        get => _snapToInteger;
        set => SetField(ref _snapToInteger, value);
    }

    public bool SpanAcrossScreensForNewGuides
    {
        get => _spanAcrossScreensForNewGuides;
        set => SetField(ref _spanAcrossScreensForNewGuides, value);
    }

    public string LineColor
    {
        get => _lineColor;
        set => SetField(ref _lineColor, string.IsNullOrWhiteSpace(value) ? "#00C7D9" : value);
    }

    public double LineOpacity
    {
        get => _lineOpacity;
        set => SetField(ref _lineOpacity, Math.Clamp(value, 0.1, 1.0));
    }

    public double LineThickness
    {
        get => _lineThickness;
        set => SetField(ref _lineThickness, Math.Clamp(value, 1.0, 8.0));
    }

    public double ControlLeft
    {
        get => _controlLeft;
        set => SetField(ref _controlLeft, value);
    }

    public double ControlTop
    {
        get => _controlTop;
        set => SetField(ref _controlTop, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? StateChanged;

    public static GuideState FromSettings(GuideSettings settings)
    {
        var state = new GuideState
        {
            _isOverlayVisible = settings.IsOverlayVisible,
            _isLocked = settings.IsLocked,
            _showCoordinates = settings.ShowCoordinates,
            _snapToInteger = settings.SnapToInteger,
            _spanAcrossScreensForNewGuides = settings.SpanAcrossScreensForNewGuides,
            _lineColor = settings.LineColor,
            _lineOpacity = Math.Clamp(settings.LineOpacity, 0.1, 1.0),
            _lineThickness = Math.Clamp(settings.LineThickness, 1.0, 8.0),
            _controlLeft = settings.ControlLeft,
            _controlTop = settings.ControlTop
        };

        foreach (var guide in settings.Guides)
        {
            state.Guides.Add(new GuideLine
            {
                Id = guide.Id,
                Orientation = guide.Orientation,
                Position = guide.Position,
                ScreenDeviceName = guide.ScreenDeviceName,
                ScreenName = string.IsNullOrWhiteSpace(guide.ScreenName) ? "全部屏幕" : guide.ScreenName,
                ScopeLeft = guide.ScopeLeft,
                ScopeTop = guide.ScopeTop,
                ScopeWidth = guide.ScopeWidth,
                ScopeHeight = guide.ScopeHeight,
                SpanAcrossScreens = guide.SpanAcrossScreens
            });
        }

        return state;
    }

    public GuideSettings ToSettings()
    {
        return new GuideSettings
        {
            IsOverlayVisible = IsOverlayVisible,
            IsLocked = IsLocked,
            ShowCoordinates = ShowCoordinates,
            SnapToInteger = SnapToInteger,
            SpanAcrossScreensForNewGuides = SpanAcrossScreensForNewGuides,
            LineColor = LineColor,
            LineOpacity = LineOpacity,
            LineThickness = LineThickness,
            ControlLeft = ControlLeft,
            ControlTop = ControlTop,
            Guides = Guides.Select(guide => new GuideLineSettings
            {
                Id = guide.Id,
                Orientation = guide.Orientation,
                Position = guide.Position,
                ScreenDeviceName = guide.ScreenDeviceName,
                ScreenName = guide.ScreenName,
                ScopeLeft = guide.ScopeLeft,
                ScopeTop = guide.ScopeTop,
                ScopeWidth = guide.ScopeWidth,
                ScopeHeight = guide.ScopeHeight,
                SpanAcrossScreens = guide.SpanAcrossScreens
            }).ToList()
        };
    }

    public GuideLine AddGuide(GuideOrientation orientation, double position)
    {
        return AddGuide(orientation, position, 0, 0, 0, 0, "全部屏幕", string.Empty);
    }

    public GuideLine AddGuide(
        GuideOrientation orientation,
        double position,
        double scopeLeft,
        double scopeTop,
        double scopeWidth,
        double scopeHeight,
        string screenName,
        string screenDeviceName,
        bool spanAcrossScreens = false)
    {
        var guide = new GuideLine
        {
            Orientation = orientation,
            Position = SnapToInteger
                ? Math.Round(position, 0, MidpointRounding.AwayFromZero)
                : GuideLine.NormalizeCoordinate(position),
            ScopeLeft = scopeLeft,
            ScopeTop = scopeTop,
            ScopeWidth = scopeWidth,
            ScopeHeight = scopeHeight,
            ScreenName = string.IsNullOrWhiteSpace(screenName) ? "全部屏幕" : screenName,
            ScreenDeviceName = screenDeviceName,
            SpanAcrossScreens = spanAcrossScreens
        };

        Guides.Add(guide);
        return guide;
    }

    public void RemoveGuide(GuideLine guide)
    {
        Guides.Remove(guide);
    }

    public void ClearGuides()
    {
        while (Guides.Count > 0)
        {
            Guides.RemoveAt(Guides.Count - 1);
        }
    }

    private void Guides_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (GuideLine guide in e.OldItems)
            {
                guide.PropertyChanged -= Guide_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (GuideLine guide in e.NewItems)
            {
                guide.PropertyChanged += Guide_PropertyChanged;
            }
        }

        OnPropertyChanged(nameof(Guides));
        OnStateChanged();
    }

    private void Guide_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnStateChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        OnStateChanged();
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
