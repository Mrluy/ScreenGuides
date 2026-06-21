using System.Runtime.InteropServices;
using System.Windows.Interop;
using WpfPoint = System.Windows.Point;
using WpfWindow = System.Windows.Window;

namespace ScreenGuides.Services;

internal static class NativeMethods
{
    public const int GwlExStyle = -20;
    public const int WsExTransparent = 0x00000020;
    public const int WsExToolWindow = 0x00000080;
    public const int WsExLayered = 0x00080000;

    public const int WmHotKey = 0x0312;
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;

    public const uint VkG = 0x47;
    public const uint VkL = 0x4C;
    public const uint VkV = 0x56;
    public const uint VkH = 0x48;
    public const uint VkC = 0x43;

    public static void SetClickThrough(IntPtr hwnd, bool enabled)
    {
        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        style |= WsExToolWindow | WsExLayered;

        if (enabled)
        {
            style |= WsExTransparent;
        }
        else
        {
            style &= ~((long)WsExTransparent);
        }

        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style));
    }

    public static WpfPoint GetCursorPosition(WpfWindow relativeTo)
    {
        if (!GetCursorPos(out var point))
        {
            return default;
        }

        return relativeTo.PointFromScreen(new WpfPoint(point.X, point.Y));
    }

    public static WpfPoint GetCursorScreenPosition()
    {
        return GetCursorPos(out var point)
            ? new WpfPoint(point.X, point.Y)
            : default;
    }

    public static bool RegisterHotKey(WpfWindow window, int id, uint modifiers, uint virtualKey)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        return RegisterHotKey(hwnd, id, modifiers, virtualKey);
    }

    public static void UnregisterHotKey(WpfWindow window, int id)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(hwnd, id);
        }
    }

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
