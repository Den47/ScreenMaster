using System.IO;
using System.Runtime.InteropServices;

namespace ScreenMaster;

internal static class DesktopWallpaperService
{
    private const string GraphicsRecoveryInstructions =
        "Try restarting Windows graphics by pressing Win+Ctrl+Shift+B. " +
        "Then click Refresh Info and try applying the wallpaper again.";

    public static void Apply(string monitorId, string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(monitorId);
        var fullPath = Path.GetFullPath(imagePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The selected image no longer exists.", fullPath);
        }

        if (!MonitorService.GetConnectedMonitors().Any(monitor =>
                string.Equals(monitor.Id, monitorId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("This monitor is disconnected. Refresh the monitor list.");
        }

        IDesktopWallpaper? desktop = null;
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD"), true)!;
            desktop = (IDesktopWallpaper)Activator.CreateInstance(type)!;
            desktop.GetMonitorDevicePathCount(out var count);
            string? targetId = null;
            for (uint i = 0; i < count; i++)
            {
                desktop.GetMonitorDevicePathAt(i, out var id);
                if (string.Equals(id, monitorId, StringComparison.OrdinalIgnoreCase))
                {
                    targetId = id;
                    break;
                }
            }

            if (targetId is null)
            {
                throw new InvalidOperationException(
                    "Windows detects this monitor, but its wallpaper service cannot identify it.\n\n" +
                    GraphicsRecoveryInstructions);
            }

            desktop.GetPosition(out var previousPosition);
            desktop.SetPosition(DesktopWallpaperPosition.Fill);
            try
            {
                desktop.SetWallpaper(targetId, fullPath);
            }
            catch (COMException)
            {
                desktop.SetPosition(previousPosition);
                throw;
            }
        }
        catch (COMException exception) when (exception.HResult is unchecked((int)0x80004005) or unchecked((int)0x8000FFFF))
        {
            throw new InvalidOperationException(
                "Windows could not apply the wallpaper.\n\n" +
                GraphicsRecoveryInstructions, exception);
        }
        finally
        {
            if (desktop is not null)
            {
                Marshal.ReleaseComObject(desktop);
            }
        }
    }
}

internal enum DesktopWallpaperPosition
{
    Center,
    Tile,
    Stretch,
    Fit,
    Fill,
    Span
}

[ComImport]
[Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaper
{
    void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorId, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
    void GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorId, [MarshalAs(UnmanagedType.LPWStr)] out string wallpaper);
    void GetMonitorDevicePathAt(uint monitorIndex, [MarshalAs(UnmanagedType.LPWStr)] out string monitorId);
    void GetMonitorDevicePathCount(out uint count);
    [PreserveSig]
    int GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId, out DesktopRect displayRect);
    void SetBackgroundColor(uint color);
    void GetBackgroundColor(out uint color);
    void SetPosition(DesktopWallpaperPosition position);
    void GetPosition(out DesktopWallpaperPosition position);
}

[StructLayout(LayoutKind.Sequential)]
internal struct DesktopRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}
