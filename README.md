# ScreenGuides

一个 Windows 屏幕辅助线工具，用来像 Photoshop / Figma 一样在屏幕上常驻固定横线、竖线，方便对齐不同应用窗口里的组件。

## 功能

- 透明置顶 Overlay，覆盖整个虚拟桌面，支持多显示器。
- 在 GUI 中点击添加竖线 / 横线，再到目标屏幕点击位置放置参考线。
- 支持手动选择目标屏幕，默认使用控制面板当前所在屏幕。
- 参考线默认只显示在目标屏幕范围内。
- 可选择新参考线是否跨越所有屏幕，默认不跨越。
- 参考线列表支持直接编辑每条线的坐标。
- 解锁后可直接拖动参考线，右键删除参考线。
- 锁定后启用鼠标穿透，不影响点击下面的应用。
- 每条参考线使用独立轻量窗口，避免大面积透明 Overlay 导致卡顿。
- 颜色、透明度、线宽、整数坐标吸附可调。
- 设置自动保存到 `%AppData%\ScreenGuides\settings.json`。
- 关闭主窗口即退出软件，也可最小化到任务栏。

## 快捷键

- `Ctrl+Alt+G`：显示 / 隐藏参考线
- `Ctrl+Alt+L`：锁定 / 解锁鼠标穿透

添加和编辑参考线通过控制面板完成，线创建后会固定在对应屏幕坐标。

## 运行

```powershell
dotnet run
```

或者直接运行：

```powershell
.\bin\Debug\net8.0-windows\ScreenGuides.exe
```

## 发布

```powershell
dotnet publish -c Release
```

发布后的自包含单文件程序在：

```text
bin\Release\net8.0-windows\win-x64\publish\ScreenGuides.exe
```

该 exe 已包含 .NET 运行时和 WPF 原生依赖，可单独复制到其他 Windows x64 电脑运行。
