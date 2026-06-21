using System.Windows;
using ScreenGuides.Models;
using ScreenGuides.Services;
using ScreenGuides.Windows;
using Forms = System.Windows.Forms;

namespace ScreenGuides;

public partial class App : System.Windows.Application
{
    private GuideState? _state;
    private SettingsStore? _settingsStore;
    private DebouncedSettingsSaver? _settingsSaver;
    private OverlayWindow? _overlayWindow;
    private MainWindow? _controlWindow;
    private Forms.NotifyIcon? _trayIcon;
    private System.Drawing.Icon? _trayIconImage;

    public bool IsShuttingDown { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _settingsStore = new SettingsStore();
        _state = GuideState.FromSettings(_settingsStore.Load());
        _settingsSaver = new DebouncedSettingsSaver(_state, _settingsStore);

        _overlayWindow = new OverlayWindow(_state);
        _controlWindow = new MainWindow(_state);

        ConfigureTrayIcon();

        if (_state.IsOverlayVisible)
        {
            _overlayWindow.Show();
        }

        _controlWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        IsShuttingDown = true;
        _settingsSaver?.SaveNow();

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        _trayIconImage?.Dispose();
        _overlayWindow?.Dispose();

        base.OnExit(e);
    }

    public void ShowControlWindow()
    {
        if (_controlWindow is null)
        {
            return;
        }

        _controlWindow.Show();
        _controlWindow.WindowState = WindowState.Normal;
        _controlWindow.Activate();
    }

    public void ExitApplication()
    {
        IsShuttingDown = true;
        _settingsSaver?.SaveNow();
        _overlayWindow?.Dispose();
        _trayIcon?.Dispose();
        _trayIconImage?.Dispose();
        Shutdown();
    }

    private void ConfigureTrayIcon()
    {
        if (_state is null)
        {
            return;
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示控制面板", null, (_, _) => Dispatcher.Invoke(ShowControlWindow));
        menu.Items.Add("显示 / 隐藏参考线", null, (_, _) => Dispatcher.Invoke(() => _state.IsOverlayVisible = !_state.IsOverlayVisible));
        menu.Items.Add("锁定 / 解锁穿透", null, (_, _) => Dispatcher.Invoke(() => _state.IsLocked = !_state.IsLocked));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        _trayIconImage = LoadTrayIcon();
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _trayIconImage,
            Text = "屏幕辅助线工具 v0.1.0",
            ContextMenuStrip = menu,
            Visible = true
        };

        _trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                Dispatcher.Invoke(ShowControlWindow);
            }
        };
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        var iconStreamInfo = GetResourceStream(new Uri("pack://application:,,,/Assets/ScreenGuides.ico", UriKind.Absolute));
        if (iconStreamInfo is null)
        {
            return System.Drawing.SystemIcons.Application;
        }

        using var icon = new System.Drawing.Icon(iconStreamInfo.Stream);
        return (System.Drawing.Icon)icon.Clone();
    }
}
