using System.Windows.Threading;
using ScreenGuides.Models;

namespace ScreenGuides.Services;

public sealed class DebouncedSettingsSaver
{
    private readonly GuideState _state;
    private readonly SettingsStore _settingsStore;
    private readonly DispatcherTimer _timer;

    public DebouncedSettingsSaver(GuideState state, SettingsStore settingsStore)
    {
        _state = state;
        _settingsStore = settingsStore;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _timer.Tick += (_, _) => SaveNow();
        _state.StateChanged += (_, _) =>
        {
            _timer.Stop();
            _timer.Start();
        };
    }

    public void SaveNow()
    {
        _timer.Stop();
        _settingsStore.Save(_state.ToSettings());
    }
}
