using System.Globalization;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ScreenGuides.Models;
using ScreenGuides.Services;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Rect = System.Windows.Rect;
using TextBox = System.Windows.Controls.TextBox;
using Forms = System.Windows.Forms;

namespace ScreenGuides;

public partial class MainWindow : Window
{
    private const int HotKeyToggleOverlay = 100;
    private const int HotKeyToggleLock = 101;

    private readonly GuideState _state;
    private readonly List<ScreenOption> _screenOptions = [];
    private HwndSource? _source;
    private bool _isUpdatingScreenSelection;
    private bool _hasUserSelectedScreen;

    public MainWindow(GuideState state)
    {
        _state = state;

        InitializeComponent();
        DataContext = _state;

        PopulateScreenSelector();
        RestorePosition();
        SelectCurrentScreen();
        SetDefaultAddPositions();

        SourceInitialized += MainWindow_SourceInitialized;
        LocationChanged += MainWindow_LocationChanged;
        Closed += MainWindow_Closed;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (Application.Current is App { IsShuttingDown: false })
        {
            e.Cancel = true;
            Hide();
            return;
        }

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
        UnregisterHotKeys();
        _source?.RemoveHook(WndProc);
    }

    private void AddVertical_Click(object sender, RoutedEventArgs e)
    {
        AddGuideFromInput(GuideOrientation.Vertical, VerticalPositionBox);
    }

    private void AddHorizontal_Click(object sender, RoutedEventArgs e)
    {
        AddGuideFromInput(GuideOrientation.Horizontal, HorizontalPositionBox);
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

        VerticalPositionBox.Text = verticalGuide.RelativePosition.ToString("0.#", CultureInfo.CurrentCulture);
        HorizontalPositionBox.Text = horizontalGuide.RelativePosition.ToString("0.#", CultureInfo.CurrentCulture);
        AddStatusText.Text = $"已在 {screen.DisplayName} 添加居中十字：X {verticalGuide.RelativePosition:0.#}，Y {horizontalGuide.RelativePosition:0.#}{GetSpanStatusText(verticalGuide)}";
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

    private void HidePanel_Click(object sender, RoutedEventArgs e)
    {
        Hide();
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

    private void PickMouseCoordinate_Click(object sender, RoutedEventArgs e)
    {
        var screen = GetSelectedScreenOption();
        var cursor = NativeMethods.GetCursorScreenPosition();
        var relativeX = Math.Clamp(cursor.X - screen.Bounds.Left, 0, screen.Bounds.Width);
        var relativeY = Math.Clamp(cursor.Y - screen.Bounds.Top, 0, screen.Bounds.Height);

        VerticalPositionBox.Text = relativeX.ToString("0.#", CultureInfo.CurrentCulture);
        HorizontalPositionBox.Text = relativeY.ToString("0.#", CultureInfo.CurrentCulture);
        AddStatusText.Text = $"已拾取鼠标坐标：X {relativeX:0.#}，Y {relativeY:0.#}。";
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
        if (!double.IsNaN(_state.ControlLeft) &&
            !double.IsNaN(_state.ControlTop) &&
            IsVisibleOnAnyScreen(savedBounds))
        {
            Left = _state.ControlLeft;
            Top = _state.ControlTop;
            return;
        }

        var workingArea = Forms.Screen.PrimaryScreen?.WorkingArea ?? Forms.Screen.AllScreens[0].WorkingArea;
        Left = workingArea.Right - Width - 32;
        Top = workingArea.Top + 48;
        _state.ControlLeft = Left;
        _state.ControlTop = Top;
    }

    private static bool IsVisibleOnAnyScreen(Rect windowBounds)
    {
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var area = screen.WorkingArea;
            var screenBounds = new Rect(area.Left, area.Top, area.Width, area.Height);
            var intersection = Rect.Intersect(windowBounds, screenBounds);

            if (!intersection.IsEmpty && intersection.Width >= 160 && intersection.Height >= 120)
            {
                return true;
            }
        }

        return false;
    }

    private void SetDefaultAddPositions()
    {
        var screen = GetSelectedScreenOption();
        var bounds = screen.Bounds;
        VerticalPositionBox.Text = Math.Round(bounds.Width / 2).ToString(CultureInfo.CurrentCulture);
        HorizontalPositionBox.Text = Math.Round(bounds.Height / 2).ToString(CultureInfo.CurrentCulture);
        ScreenStatusText.Text = screen.CoordinateRangeText;
        AddStatusText.Text = _state.SpanAcrossScreensForNewGuides
            ? "输入目标屏幕内坐标后添加参考线；新线会跨越所有屏幕。"
            : "输入目标屏幕内坐标后添加参考线；新线只显示在目标屏幕内。";
    }

    private void AddGuideFromInput(GuideOrientation orientation, TextBox input)
    {
        if (!TryParseCoordinate(input.Text, out var value))
        {
            AddStatusText.Text = "请输入有效数字坐标。";
            input.Focus();
            input.SelectAll();
            return;
        }

        EnsureOverlayVisible();

        var screen = GetSelectedScreenOption();
        var bounds = screen.Bounds;
        var clamped = orientation == GuideOrientation.Vertical
            ? Math.Clamp(value, 0, bounds.Width)
            : Math.Clamp(value, 0, bounds.Height);
        var absolutePosition = orientation == GuideOrientation.Vertical
            ? bounds.Left + clamped
            : bounds.Top + clamped;

        var guide = AddScopedGuide(orientation, absolutePosition, screen);
        input.Text = guide.RelativePosition.ToString("0.#", CultureInfo.CurrentCulture);
        AddStatusText.Text = orientation == GuideOrientation.Vertical
            ? $"已在 {screen.DisplayName} 添加固定竖线：X {guide.RelativePosition:0.#}{GetSpanStatusText(guide)}"
            : $"已在 {screen.DisplayName} 添加固定横线：Y {guide.RelativePosition:0.#}{GetSpanStatusText(guide)}";
    }

    private static bool TryParseCoordinate(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
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

    private Rect GetVirtualScreenBounds()
    {
        return new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
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
