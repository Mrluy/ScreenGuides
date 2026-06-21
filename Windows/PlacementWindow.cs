using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ScreenGuides.Models;
using ScreenGuides.Services;
using InputCursors = System.Windows.Input.Cursors;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;

namespace ScreenGuides.Windows;

public sealed class PlacementWindow : Window
{
    private readonly GuideOrientation _orientation;
    private readonly Rect _bounds;
    private readonly Action<double> _placed;
    private readonly Action _cancelled;
    private bool _finished;
    private bool _suppressCallback;

    public PlacementWindow(
        GuideOrientation orientation,
        Rect bounds,
        string screenName,
        Action<double> placed,
        Action cancelled)
    {
        _orientation = orientation;
        _bounds = bounds;
        _placed = placed;
        _cancelled = cancelled;

        AllowsTransparency = true;
        Background = MediaBrushes.Transparent;
        Content = CreateContent(screenName);
        Cursor = orientation == GuideOrientation.Vertical ? InputCursors.SizeWE : InputCursors.SizeNS;
        Focusable = true;
        Height = Math.Max(1, bounds.Height);
        Left = bounds.Left;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = true;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.Manual;
        SnapsToDevicePixels = true;
        Top = bounds.Top;
        Topmost = true;
        UseLayoutRounding = true;
        Width = Math.Max(1, bounds.Width);
        WindowStyle = WindowStyle.None;

        Loaded += (_, _) =>
        {
            Focus();
            Keyboard.Focus(this);
        };
        SourceInitialized += PlacementWindow_SourceInitialized;
        MouseLeftButtonDown += PlacementWindow_MouseLeftButtonDown;
        MouseRightButtonDown += PlacementWindow_MouseRightButtonDown;
        PreviewKeyDown += PlacementWindow_PreviewKeyDown;
    }

    public void CloseWithoutCallback()
    {
        _suppressCallback = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!_finished && !_suppressCallback)
        {
            _cancelled();
        }

        base.OnClosed(e);
    }

    private UIElement CreateContent(string screenName)
    {
        var root = new Grid
        {
            Background = new SolidColorBrush(MediaColor.FromArgb(20, 79, 195, 247))
        };

        var instruction = _orientation == GuideOrientation.Vertical
            ? $"点击 {screenName} 任意位置添加竖线"
            : $"点击 {screenName} 任意位置添加横线";

        var panel = new Border
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            MaxWidth = 360,
            Padding = new Thickness(18, 12, 18, 12),
            Background = new SolidColorBrush(MediaColor.FromArgb(238, 17, 24, 39)),
            BorderBrush = new SolidColorBrush(MediaColor.FromRgb(79, 195, 247)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = instruction,
                        FontSize = 15,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = MediaBrushes.White,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Margin = new Thickness(0, 6, 0, 0),
                        Text = "Esc 或右键取消",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184)),
                        TextAlignment = TextAlignment.Center
                    }
                }
            }
        };

        root.Children.Add(panel);
        return root;
    }

    private void PlacementWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        NativeMethods.SetClickThrough(handle, false);
    }

    private void PlacementWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        PlaceAtCursor();
    }

    private void PlacementWindow_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        CancelPlacement();
    }

    private void PlacementWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        CancelPlacement();
    }

    private void PlaceAtCursor()
    {
        if (_finished)
        {
            return;
        }

        var cursor = NativeMethods.GetCursorScreenPosition();
        var position = _orientation == GuideOrientation.Vertical
            ? Math.Clamp(cursor.X, _bounds.Left, _bounds.Right)
            : Math.Clamp(cursor.Y, _bounds.Top, _bounds.Bottom);

        _finished = true;
        Close();
        _placed(position);
    }

    private void CancelPlacement()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        Close();
        _cancelled();
    }
}
