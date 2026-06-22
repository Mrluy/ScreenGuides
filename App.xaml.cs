using System.Windows;
using ScreenGuides.Models;
using ScreenGuides.Services;
using ScreenGuides.Windows;

namespace ScreenGuides;

public partial class App : System.Windows.Application
{
    private GuideState? _state;
    private SettingsStore? _settingsStore;
    private DebouncedSettingsSaver? _settingsSaver;
    private OverlayWindow? _overlayWindow;
    private MainWindow? _controlWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnMainWindowClose;

        _settingsStore = new SettingsStore();
        _state = GuideState.FromSettings(_settingsStore.Load());
        _settingsSaver = new DebouncedSettingsSaver(_state, _settingsStore);

        _overlayWindow = new OverlayWindow(_state);
        _controlWindow = new MainWindow(_state);
        MainWindow = _controlWindow;

        if (_state.IsOverlayVisible)
        {
            _overlayWindow.Show();
        }

        _controlWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _settingsSaver?.SaveNow();
        _overlayWindow?.Dispose();

        base.OnExit(e);
    }

    public void ExitApplication()
    {
        _settingsSaver?.SaveNow();
        Shutdown();
    }
}
