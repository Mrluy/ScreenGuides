namespace ScreenGuides.Models;

public sealed class GuideSettings
{
    public bool IsOverlayVisible { get; set; } = true;

    public bool IsLocked { get; set; } = true;

    public bool ShowCoordinates { get; set; } = true;

    public bool SnapToInteger { get; set; } = true;

    public bool SpanAcrossScreensForNewGuides { get; set; }

    public string LineColor { get; set; } = "#00C7D9";

    public double LineOpacity { get; set; } = 0.88;

    public double LineThickness { get; set; } = 1.25;

    public double ControlLeft { get; set; } = double.NaN;

    public double ControlTop { get; set; } = double.NaN;

    public List<GuideLineSettings> Guides { get; set; } = [];
}

public sealed class GuideLineSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public GuideOrientation Orientation { get; set; }

    public double Position { get; set; }

    public string ScreenDeviceName { get; set; } = string.Empty;

    public string ScreenName { get; set; } = "全部屏幕";

    public double ScopeLeft { get; set; }

    public double ScopeTop { get; set; }

    public double ScopeWidth { get; set; }

    public double ScopeHeight { get; set; }

    public bool SpanAcrossScreens { get; set; }
}
