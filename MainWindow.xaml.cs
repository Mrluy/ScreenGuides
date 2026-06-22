using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ScreenGuides.Models;
using ScreenGuides.Services;
using ScreenGuides.Windows;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Rect = System.Windows.Rect;
using DrawingRectangle = System.Drawing.Rectangle;
using Forms = System.Windows.Forms;

namespace ScreenGuides;

public partial class MainWindow : Window
{
    private const int HotKeyToggleOverlay = 100;
    private const int HotKeyToggleLock = 101;
    private const double PreferredWindowWidth = 430;
    private const double PreferredWindowHeight = 1100;
    private const double StartupScreenMargin = 24;

    private readonly GuideState _state;
    private readonly List<ScreenOption> _screenOptions = [];
    private HwndSource? _source;
    private bool _isUpdatingScreenSelection;
    private bool _hasUserSelectedScreen;
    private PlacementWindow? _placementWindow;

    public MainWindow(GuideState state)
    {
        _state = state;

        InitializeComponent();
        DataContext = _state;

        PopulateScreenSelector();
        ConfigureStartupSize();
        RestorePosition();
        SelectCurrentScreen();
        SetDefaultAddPositions();

        _state.PropertyChanged += State_PropertyChanged;
        SourceInitialized += MainWindow_SourceInitialized;
        LocationChanged += MainWindow_LocationChanged;
        Closed += MainWindow_Closed;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        CancelGuidePlacement(null);
        base.OnClosing(e);
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _source?.AddHook(WndProc);
        RegisterHotKeys();
    }

    private void MainWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (!IsLoaded || WindowState != WindowState.Normal)
        {
            return;
        }

        _state.ControlLeft = Left;
        _state.ControlTop = Top;

        if (!_hasUserSelectedScreen)
        {
            SelectCurrentScreen();
            SetDefaultAddPositions();
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _state.PropertyChanged -= State_PropertyChanged;
        UnregisterHotKeys();
        _source?.RemoveHook(WndProc);
    }

    private void State_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GuideState.SpanAcrossScreensForNewGuides) &&
            _placementWindow is null)
        {
            SetDefaultAddPositions();
        }
    }

    private void AddVertical_Click(object sender, RoutedEventArgs e)
    {
        BeginGuidePlacement(GuideOrientation.Vertical);
    }

    private void AddHorizontal_Click(object sender, RoutedEventArgs e)
    {
        BeginGuidePlacement(GuideOrientation.Horizontal);
    }

    private void AddCenterCross_Click(object sender, RoutedEventArgs e)
    {
        EnsureOverlayVisible();
        var screen = GetSelectedScreenOption();
        var bounds = screen.Bounds;
        var centerX = bounds.Left + bounds.Width / 2;
        var centerY = bounds.Top + bounds.Height / 2;

        var verticalGuide = AddScopedGuide(GuideOrientation.Vertical, centerX, screen);
        var horizontalGuide = AddScopedGuide(GuideOrientation.Horizontal, centerY, screen);

        AddStatusText.Text = $"已在 {screen.DisplayName} 添加居中十字：X {GuideLine.FormatCoordinate(verticalGuide.RelativePosition)}，Y {GuideLine.FormatCoordinate(horizontalGuide.RelativePosition)}{GetSpanStatusText(verticalGuide)}";
    }

    private void ClearGuides_Click(object sender, RoutedEventArgs e)
    {
        var count = _state.Guides.Count;
        _state.ClearGuides();
        AddStatusText.Text = count == 0
            ? "当前没有参考线需要清空。"
            : $"已清空 {count} 条固定参考线。";
    }

    private void DeleteGuide_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: GuideLine guide })
        {
            _state.RemoveGuide(guide);
        }
    }

    private void MinimizePanel_Click(object sender, RoutedEventArgs e)
    {
        CancelGuidePlacement(null);
        WindowState = WindowState.Minimized;
    }

    private void ScreenSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingScreenSelection)
        {
            return;
        }

        _hasUserSelectedScreen = true;
        SetDefaultAddPositions();
    }

    private void UseCurrentScreen_Click(object sender, RoutedEventArgs e)
    {
        _hasUserSelectedScreen = false;
        SelectCurrentScreen();
        SetDefaultAddPositions();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.ExitApplication();
        }
    }

    private void EnsureOverlayVisible()
    {
        if (!_state.IsOverlayVisible)
        {
            _state.IsOverlayVisible = true;
        }
    }

    private void RestorePosition()
    {
        var savedBounds = new Rect(_state.ControlLeft, _state.ControlTop, Width, Height);
        var savedScreen = FindVisibleScreen(savedBounds);
        if (!double.IsNaN(_state.ControlLeft) &&
            !double.IsNaN(_state.ControlTop) &&
            savedScreen is not null)
        {
            var area = savedScreen.WorkingArea;
            Left = ClampWindowCoordinate(_state.ControlLeft, area.Left + StartupScreenMargin, area.Right - Width - StartupScreenMargin);
            Top = ClampWindowCoordinate(_state.ControlTop, area.Top + StartupScreenMargin, area.Bottom - Height - StartupScreenMargin);
            return;
        }

        var workingArea = Forms.Screen.PrimaryScreen?.WorkingArea ?? Forms.Screen.AllScreens[0].WorkingArea;
        Left = workingArea.Right - Width - 32;
        Top = workingArea.Top + 48;
        _state.ControlLeft = Left;
        _state.ControlTop = Top;
    }

    private void ConfigureStartupSize()
    {
        var workingArea = GetStartupWorkingArea();
        var availableHeight = Math.Max(MinHeight, workingArea.Height - StartupScreenMargin * 2);

        Width = PreferredWindowWidth;
        MaxHeight = availableHeight;
        Height = Math.Min(PreferredWindowHeight, availableHeight);
    }

    private DrawingRectangle GetStartupWorkingArea()
    {
        if (!double.IsNaN(_state.ControlLeft) && !double.IsNaN(_state.ControlTop))
        {
            var savedBounds = new Rect(_state.ControlLeft, _state.ControlTop, PreferredWindowWidth, PreferredWindowHeight);
            var savedScreen = FindVisibleScreen(savedBounds);
            if (savedScreen is not null)
            {
                return savedScreen.WorkingArea;
            }
        }

        return Forms.Screen.PrimaryScreen?.WorkingArea ?? Forms.Screen.AllScreens[0].WorkingArea;
    }

    private static bool IsVisibleOnAnyScreen(Rect windowBounds)
    {
        return FindVisibleScreen(windowBounds) is not null;
    }

    private static Forms.Screen? FindVisibleScreen(Rect windowBounds)
    {
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var area = screen.WorkingArea;
            var screenBounds = new Rect(area.Left, area.Top, area.Width, area.Height);
            var intersection = Rect.Intersect(windowBounds, screenBounds);

            if (!intersection.IsEmpty && intersection.Width >= 160 && intersection.Height >= 120)
            {
                return screen;
            }
        }

        return null;
    }

    private static double ClampWindowCoordinate(double value, double min, double max)
    {
        return max < min ? min : Math.Clamp(value, min, max);
    }

    private void SetDefaultAddPositions()
    {
        var screen = GetSelectedScreenOption();
        ScreenStatusText.Text = screen.CoordinateRangeText;
        AddStatusText.Text = _state.SpanAcrossScreensForNewGuides
            ? "点击竖线或横线后，在目标屏幕上点击放置；新线会跨越所有屏幕。"
            : "点击竖线或横线后，在目标屏幕上点击放置；新线只显示在目标屏幕内。";
    }

    private void BeginGuidePlacement(GuideOrientation orientation)
    {
        EnsureOverlayVisible();
        CancelGuidePlacement(null);

        var screen = GetSelectedScreenOption();
        var placementText = orientation == GuideOrientation.Vertical ? "竖线" : "横线";

        _placementWindow = new PlacementWindow(
            orientation,
            screen.Bounds,
            screen.DisplayName,
            absolutePosition =>
            {
                var guide = AddScopedGuide(orientation, absolutePosition, screen);
                _placementWindow = null;
                AddStatusText.Text = orientation == GuideOrientation.Vertical
                    ? $"已在 {screen.DisplayName} 添加固定竖线：X {GuideLine.FormatCoordinate(guide.RelativePosition)}{GetSpanStatusText(guide)}"
                    : $"已在 {screen.DisplayName} 添加固定横线：Y {GuideLine.FormatCoordinate(guide.RelativePosition)}{GetSpanStatusText(guide)}";
            },
            () =>
            {
                _placementWindow = null;
                AddStatusText.Text = "已取消放置参考线。";
            });

        AddStatusText.Text = $"正在放置{placementText}：请在 {screen.DisplayName} 上点击位置，Esc 或右键取消。";
        _placementWindow.Show();
        _placementWindow.Activate();
    }

    private void CancelGuidePlacement(string? statusText)
    {
        if (_placementWindow is null)
        {
            return;
        }

        var window = _placementWindow;
        _placementWindow = null;
        window.CloseWithoutCallback();

        if (!string.IsNullOrWhiteSpace(statusText))
        {
            AddStatusText.Text = statusText;
        }
    }

    private void PopulateScreenSelector()
    {
        _screenOptions.Clear();
        var screens = Forms.Screen.AllScreens;

        for (var index = 0; index < screens.Length; index++)
        {
            var screen = screens[index];
            var bounds = screen.Bounds;
            var title = $"屏幕 {index + 1}{(screen.Primary ? " · 主屏" : string.Empty)}";
            var resolution = $"{bounds.Width} × {bounds.Height}";
            var range = $"坐标范围  X: 0 - {bounds.Width}  Y: 0 - {bounds.Height}";
            _screenOptions.Add(new ScreenOption(
                screen.DeviceName,
                title,
                resolution,
                range,
                new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height)));
        }

        ScreenSelector.ItemsSource = _screenOptions;
    }

    private void SelectCurrentScreen()
    {
        var currentScreen = GetCurrentFormsScreen();
        var selected = _screenOptions.FirstOrDefault(option => option.DeviceName == currentScreen.DeviceName)
            ?? _screenOptions.FirstOrDefault();

        if (selected is null)
        {
            return;
        }

        _isUpdatingScreenSelection = true;
        try
        {
            ScreenSelector.SelectedItem = selected;
        }
        finally
        {
            _isUpdatingScreenSelection = false;
        }
    }

    private ScreenOption GetSelectedScreenOption()
    {
        if (ScreenSelector.SelectedItem is ScreenOption selected)
        {
            return selected;
        }

        SelectCurrentScreen();
        return ScreenSelector.SelectedItem as ScreenOption
            ?? _screenOptions.First();
    }

    private Forms.Screen GetCurrentFormsScreen()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            return Forms.Screen.FromHandle(handle);
        }

        return Forms.Screen.FromPoint(new System.Drawing.Point(
            (int)Math.Round(Left + Width / 2),
            (int)Math.Round(Top + Height / 2)));
    }

    private GuideLine AddScopedGuide(GuideOrientation orientation, double absolutePosition, ScreenOption screen)
    {
        return _state.AddGuide(
            orientation,
            absolutePosition,
            screen.Bounds.Left,
            screen.Bounds.Top,
            screen.Bounds.Width,
            screen.Bounds.Height,
            screen.DisplayName,
            screen.DeviceName,
            _state.SpanAcrossScreensForNewGuides);
    }

    private static string GetSpanStatusText(GuideLine guide)
    {
        return guide.SpanAcrossScreens ? "，跨越所有屏幕。" : "，仅限目标屏幕。";
    }

    private void RegisterHotKeys()
    {
        var modifiers = NativeMethods.ModControl | NativeMethods.ModAlt;
        var failures = new List<string>();

        Register(HotKeyToggleOverlay, modifiers, NativeMethods.VkG, "Ctrl+Alt+G");
        Register(HotKeyToggleLock, modifiers, NativeMethods.VkL, "Ctrl+Alt+L");

        HotkeyStatusText.Text = failures.Count == 0
            ? "Ctrl+Alt+G 显示/隐藏 · Ctrl+Alt+L 锁定"
            : $"部分快捷键被系统占用：{string.Join(", ", failures)}";

        void Register(int id, uint mod, uint key, string label)
        {
            if (!NativeMethods.RegisterHotKey(this, id, mod, key))
            {
                failures.Add(label);
            }
        }
    }

    private void UnregisterHotKeys()
    {
        NativeMethods.UnregisterHotKey(this, HotKeyToggleOverlay);
        NativeMethods.UnregisterHotKey(this, HotKeyToggleLock);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeMethods.WmHotKey)
        {
            return IntPtr.Zero;
        }

        handled = true;
        switch (wParam.ToInt32())
        {
            case HotKeyToggleOverlay:
                _state.IsOverlayVisible = !_state.IsOverlayVisible;
                break;
            case HotKeyToggleLock:
                _state.IsLocked = !_state.IsLocked;
                break;
        }

        return IntPtr.Zero;
    }

    private sealed record ScreenOption(
        string DeviceName,
        string DisplayName,
        string ResolutionText,
        string CoordinateRangeText,
        Rect Bounds)
    {
        public string Title => DisplayName;
    }
}
