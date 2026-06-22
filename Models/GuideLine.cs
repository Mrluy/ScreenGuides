using System.Globalization;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScreenGuides.Models;

public sealed class GuideLine : INotifyPropertyChanged
{
    private double _position;

    public const int CoordinateDecimalPlaces = 1;

    public Guid Id { get; init; } = Guid.NewGuid();

    public GuideOrientation Orientation { get; init; }

    public string ScreenDeviceName { get; init; } = string.Empty;

    public string ScreenName { get; init; } = "全部屏幕";

    public double ScopeLeft { get; init; }

    public double ScopeTop { get; init; }

    public double ScopeWidth { get; init; }

    public double ScopeHeight { get; init; }

    public bool SpanAcrossScreens { get; init; }

    public double Position
    {
        get => _position;
        set
        {
            var normalized = NormalizeCoordinate(value);
            if (Math.Abs(_position - normalized) < 0.01)
            {
                return;
            }

            _position = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(CoordinateText));
            OnPropertyChanged(nameof(RelativePosition));
        }
    }

    public string DisplayName => Orientation == GuideOrientation.Vertical
        ? $"{ScreenName}  竖线  X {FormatCoordinate(RelativePosition)}"
        : $"{ScreenName}  横线  Y {FormatCoordinate(RelativePosition)}";

    public string OrientationText => Orientation == GuideOrientation.Vertical ? "竖线" : "横线";

    public string CoordinateAxis => Orientation == GuideOrientation.Vertical ? "X" : "Y";

    public string ScopeText => string.IsNullOrWhiteSpace(ScreenName) ? "全部屏幕" : ScreenName;

    public string SpanText => SpanAcrossScreens ? $"{ScopeText}，跨越所有屏幕" : ScopeText;

    public string CoordinateText => Orientation == GuideOrientation.Vertical
        ? $"x {FormatCoordinate(RelativePosition)}"
        : $"y {FormatCoordinate(RelativePosition)}";

    public double RelativePosition
    {
        get => Orientation == GuideOrientation.Vertical
            ? Position - ScopeLeft
            : Position - ScopeTop;
        set
        {
            var max = Orientation == GuideOrientation.Vertical ? ScopeWidth : ScopeHeight;
            var clamped = max > 0 ? Math.Clamp(value, 0, max) : value;
            var normalized = NormalizeCoordinate(clamped);
            Position = Orientation == GuideOrientation.Vertical
                ? ScopeLeft + normalized
                : ScopeTop + normalized;
        }
    }

    public bool HasScope => ScopeWidth > 0 && ScopeHeight > 0;

    public static double NormalizeCoordinate(double value)
    {
        return Math.Round(value, CoordinateDecimalPlaces, MidpointRounding.AwayFromZero);
    }

    public static string FormatCoordinate(double value)
    {
        return NormalizeCoordinate(value).ToString("0.0", CultureInfo.CurrentCulture);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
