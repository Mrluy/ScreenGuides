using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ScreenGuides.Models;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using WpfFlowDirection = System.Windows.FlowDirection;

namespace ScreenGuides.Controls;

public sealed class GuideLineElement : FrameworkElement
{
    private readonly GuideState _state;
    private readonly GuideLine _guide;

    public GuideLineElement(GuideState state, GuideLine guide)
    {
        _state = state;
        _guide = guide;
        SnapsToDevicePixels = true;
    }

    public double? PreviewPosition { get; set; }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));

        var pen = CreateGuidePen();
        if (_guide.Orientation == GuideOrientation.Vertical)
        {
            var x = RenderSize.Width / 2;
            DrawSnappedLine(drawingContext, pen, new Point(x, 0), new Point(x, RenderSize.Height));
            if (_state.ShowCoordinates)
            {
                DrawLabel(drawingContext, GetCoordinateText(), new Point(x, 22), true);
            }
        }
        else
        {
            var y = RenderSize.Height / 2;
            DrawSnappedLine(drawingContext, pen, new Point(0, y), new Point(RenderSize.Width, y));
            if (_state.ShowCoordinates)
            {
                DrawLabel(drawingContext, GetCoordinateText(), new Point(24, y), false);
            }
        }
    }

    private string GetCoordinateText()
    {
        var position = PreviewPosition.HasValue
            ? GetRelativePosition(PreviewPosition.Value)
            : _guide.RelativePosition;

        return _guide.Orientation == GuideOrientation.Vertical
            ? $"X {position:0}"
            : $"Y {position:0}";
    }

    private double GetRelativePosition(double absolutePosition)
    {
        return _guide.Orientation == GuideOrientation.Vertical
            ? absolutePosition - _guide.ScopeLeft
            : absolutePosition - _guide.ScopeTop;
    }

    private void DrawLabel(DrawingContext drawingContext, string text, Point anchor, bool verticalGuide)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            WpfFlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            11,
            Brushes.White,
            dpi.PixelsPerDip);

        const double paddingX = 7;
        const double paddingY = 4;
        var width = formattedText.Width + paddingX * 2;
        var height = formattedText.Height + paddingY * 2;
        var left = verticalGuide ? anchor.X - width / 2 : anchor.X;
        var top = verticalGuide ? anchor.Y : anchor.Y - height / 2;

        left = Math.Clamp(left, 2, Math.Max(2, RenderSize.Width - width - 2));
        top = Math.Clamp(top, 2, Math.Max(2, RenderSize.Height - height - 2));

        var background = new SolidColorBrush(Color.FromArgb(210, 22, 27, 34));
        background.Freeze();
        var border = new Pen(new SolidColorBrush(ParseColor(_state.LineColor, 0.9)), 1);
        border.Freeze();

        var rect = new Rect(left, top, width, height);
        drawingContext.DrawRoundedRectangle(background, border, rect, 4, 4);
        drawingContext.DrawText(formattedText, new Point(left + paddingX, top + paddingY));
    }

    private static void DrawSnappedLine(DrawingContext drawingContext, Pen pen, Point start, Point end)
    {
        var guideline = new GuidelineSet();
        guideline.GuidelinesX.Add(Math.Round(start.X) + 0.5);
        guideline.GuidelinesY.Add(Math.Round(start.Y) + 0.5);
        drawingContext.PushGuidelineSet(guideline);
        drawingContext.DrawLine(pen, start, end);
        drawingContext.Pop();
    }

    private Pen CreateGuidePen()
    {
        var brush = new SolidColorBrush(ParseColor(_state.LineColor, _state.LineOpacity));
        brush.Freeze();

        var pen = new Pen(brush, _state.LineThickness);
        pen.Freeze();
        return pen;
    }

    private static Color ParseColor(string colorText, double opacity)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(colorText)!;
            color.A = (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255);
            return color;
        }
        catch
        {
            return Color.FromArgb((byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255), 0, 199, 217);
        }
    }
}
