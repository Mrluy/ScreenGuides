using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ScreenGuides.Controls;
using ScreenGuides.Models;
using ScreenGuides.Services;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace ScreenGuides.Windows;

public sealed class GuideLineWindow : Window
{
    private const double VerticalWindowWidth = 86;
    private const double HorizontalWindowHeight = 34;

    private readonly GuideState _state;
    private readonly GuideLine _guide;
    private readonly GuideLineElement _element;
    private bool _isDragging;
    private bool _isPendingRightClickDelete;
    private Point _dragStartScreen;
    private double _dragStartPosition;
    private double _previewPosition;
    private IntPtr _handle;

    public GuideLineWindow(GuideState state, GuideLine guide)
    {
        _state = state;
        _guide = guide;
        _element = new GuideLineElement(_state, _guide);

        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Content = _element;
        Focusable = false;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.Manual;
        Topmost = true;
        WindowStyle = WindowStyle.None;
        Cursor = _guide.Orientation == GuideOrientation.Vertical ? Cursors.SizeWE : Cursors.SizeNS;

        SourceInitialized += GuideLineWindow_SourceInitialized;
        Loaded += (_, _) => Reposition();
        MouseLeftButtonDown += GuideLineWindow_MouseLeftButtonDown;
        MouseLeftButtonUp += GuideLineWindow_MouseLeftButtonUp;
        MouseMove += GuideLineWindow_MouseMove;
        MouseRightButtonDown += GuideLineWindow_MouseRightButtonDown;
        MouseRightButtonUp += GuideLineWindow_MouseRightButtonUp;

        _state.PropertyChanged += State_PropertyChanged;
        _guide.PropertyChanged += Guide_PropertyChanged;
    }

    public GuideLine Guide => _guide;

    public void RefreshFromState()
    {
        ApplyClickThrough();
        Reposition();
        _element.InvalidateVisual();
    }

    public void Reposition()
    {
        Reposition(_guide.Position);
    }

    private void Reposition(double position)
    {
        var bounds = GetDrawingBounds();

        if (_guide.Orientation == GuideOrientation.Vertical)
        {
            Width = VerticalWindowWidth;
            Height = bounds.Height;
            Left = position - Width / 2;
            Top = bounds.Top;
        }
        else
        {
            Width = bounds.Width;
            Height = HorizontalWindowHeight;
            Left = bounds.Left;
            Top = position - Height / 2;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _state.PropertyChanged -= State_PropertyChanged;
        _guide.PropertyChanged -= Guide_PropertyChanged;
        base.OnClosed(e);
    }

    private void GuideLineWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _handle = new WindowInteropHelper(this).Handle;
        ApplyClickThrough();
    }

    private void State_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GuideState.IsLocked) or
            nameof(GuideState.LineColor) or
            nameof(GuideState.LineOpacity) or
            nameof(GuideState.LineThickness) or
            nameof(GuideState.ShowCoordinates))
        {
            RefreshFromState();
        }
    }

    private void Guide_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GuideLine.Position))
        {
            Reposition();
        }

        _element.InvalidateVisual();
    }

    private void GuideLineWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_state.IsLocked)
        {
            return;
        }

        _isDragging = true;
        _dragStartScreen = NativeMethods.GetCursorScreenPosition();
        _dragStartPosition = _guide.Position;
        _previewPosition = _guide.Position;
        _element.PreviewPosition = _previewPosition;
        CaptureMouse();
        e.Handled = true;
    }

    private void GuideLineWindow_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _state.IsLocked)
        {
            return;
        }

        var current = NativeMethods.GetCursorScreenPosition();
        var delta = _guide.Orientation == GuideOrientation.Vertical
            ? current.X - _dragStartScreen.X
            : current.Y - _dragStartScreen.Y;

        PreviewMoveGuide(_dragStartPosition + delta);
        e.Handled = true;
    }

    private void GuideLineWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        CommitPreviewPosition();
        ReleaseMouseCapture();
        e.Handled = true;
    }

    private void GuideLineWindow_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_state.IsLocked)
        {
            return;
        }

        _isPendingRightClickDelete = true;
        CaptureMouse();
        e.Handled = true;
    }

    private void GuideLineWindow_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPendingRightClickDelete)
        {
            return;
        }

        _isPendingRightClickDelete = false;
        ReleaseMouseCapture();
        e.Handled = true;

        Dispatcher.BeginInvoke(
            () => _state.RemoveGuide(_guide),
            DispatcherPriority.Background);
    }

    private void PreviewMoveGuide(double requestedPosition)
    {
        _previewPosition = NormalizePosition(requestedPosition);
        _element.PreviewPosition = _previewPosition;
        Reposition(_previewPosition);
        _element.InvalidateVisual();
    }

    private void CommitPreviewPosition()
    {
        _isDragging = false;
        _element.PreviewPosition = null;
        _guide.Position = _previewPosition;
        Reposition();
        _element.InvalidateVisual();
    }

    private double NormalizePosition(double requestedPosition)
    {
        var bounds = GetPositionBounds();
        var min = _guide.Orientation == GuideOrientation.Vertical ? bounds.Left : bounds.Top;
        var max = _guide.Orientation == GuideOrientation.Vertical ? bounds.Right : bounds.Bottom;
        var clamped = Math.Clamp(requestedPosition, min, max);
        return _state.SnapToInteger ? Math.Round(clamped) : clamped;
    }

    private Rect GetDrawingBounds()
    {
        if (_guide.SpanAcrossScreens)
        {
            return GetVirtualScreenBounds();
        }

        return GetPositionBounds();
    }

    private Rect GetPositionBounds()
    {
        if (_guide.HasScope)
        {
            return new Rect(_guide.ScopeLeft, _guide.ScopeTop, _guide.ScopeWidth, _guide.ScopeHeight);
        }

        return GetVirtualScreenBounds();
    }

    private static Rect GetVirtualScreenBounds()
    {
        return new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
    }

    private void ApplyClickThrough()
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.SetClickThrough(_handle, _state.IsLocked);
    }
}
