using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ScreenMaster;

internal static class MonitorService
{
    private const uint OnlyActivePaths = 0x2;
    private const int InsufficientBuffer = 122;

    public static IReadOnlyList<MonitorInfo> GetConnectedMonitors()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            ThrowIfError(GetDisplayConfigBufferSizes(OnlyActivePaths, out var pathCount, out var modeCount));
            var paths = new DisplayConfigPathInfo[pathCount];
            var modes = new DisplayConfigModeInfo[modeCount];
            var result = QueryDisplayConfig(OnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (result == InsufficientBuffer)
            {
                continue;
            }

            ThrowIfError(result);
            var identities = MonitorIdentityService.GetIdentities();
            var monitors = new List<MonitorInfo>();
            var devicePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < pathCount; i++)
            {
                var path = paths[i];
                if (path.Target.TargetAvailable == 0)
                {
                    continue;
                }

                var name = new DisplayConfigTargetDeviceName
                {
                    Header = new DisplayConfigDeviceInfoHeader
                    {
                        Type = 2,
                        Size = (uint)Marshal.SizeOf<DisplayConfigTargetDeviceName>(),
                        AdapterId = path.Target.AdapterId,
                        Id = path.Target.Id
                    },
                    MonitorFriendlyDeviceName = string.Empty,
                    MonitorDevicePath = string.Empty
                };
                ThrowIfError(DisplayConfigGetDeviceInfo(ref name));
                if (!string.IsNullOrWhiteSpace(name.MonitorDevicePath) && !devicePaths.Add(name.MonitorDevicePath))
                {
                    continue;
                }

                var sourceIndex = path.Source.ModeInfoIndex;
                var source = sourceIndex < modeCount && modes[sourceIndex].InfoType == 1
                    ? modes[sourceIndex].SourceMode
                    : default;
                double? refreshRate = path.Target.RefreshRate.Denominator > 0
                    ? (double)path.Target.RefreshRate.Numerator / path.Target.RefreshRate.Denominator
                    : null;
                var identity = MonitorIdentityService.FindIdentity(name.MonitorDevicePath, identities);
                var edidIdsValid = (name.Flags & 0x4) != 0;
                monitors.Add(new MonitorInfo(
                    monitors.Count + 1,
                    string.IsNullOrWhiteSpace(name.MonitorFriendlyDeviceName) ? "Unknown monitor" : name.MonitorFriendlyDeviceName,
                    string.IsNullOrWhiteSpace(name.MonitorDevicePath) ? "Unavailable" : name.MonitorDevicePath,
                    source.Width,
                    source.Height,
                    source.Width > 0 && source.Height > 0 && source.Position.X == 0 && source.Position.Y == 0,
                    refreshRate)
                {
                    EdidManufacturerId = edidIdsValid ? name.EdidManufactureId : null,
                    EdidProductCodeId = edidIdsValid ? name.EdidProductCodeId : null,
                    Manufacturer = identity?.Manufacturer,
                    ProductCode = identity?.ProductCode,
                    SerialNumber = identity?.SerialNumber
                });
            }

            return monitors;
        }

        throw new Win32Exception(InsufficientBuffer, "The display configuration is changing. Please refresh the list.");
    }

    private static void ThrowIfError(int result)
    {
        if (result != 0)
        {
            throw new Win32Exception(result);
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint pathCount,
        [Out] DisplayConfigPathInfo[] paths,
        ref uint modeCount,
        [Out] DisplayConfigModeInfo[] modes,
        IntPtr currentTopologyId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigTargetDeviceName deviceName);

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rational
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathSourceInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIndex;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathTargetInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIndex;
        public uint OutputTechnology;
        public uint Rotation;
        public uint Scaling;
        public Rational RefreshRate;
        public uint ScanLineOrdering;
        public int TargetAvailable;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathInfo
    {
        public DisplayConfigPathSourceInfo Source;
        public DisplayConfigPathTargetInfo Target;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigSourceMode
    {
        public uint Width;
        public uint Height;
        public uint PixelFormat;
        public Point Position;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct DisplayConfigModeInfo
    {
        [FieldOffset(0)] public uint InfoType;
        [FieldOffset(4)] public uint Id;
        [FieldOffset(8)] public Luid AdapterId;
        [FieldOffset(16)] public DisplayConfigSourceMode SourceMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigDeviceInfoHeader
    {
        public uint Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayConfigTargetDeviceName
    {
        public DisplayConfigDeviceInfoHeader Header;
        public uint Flags;
        public uint OutputTechnology;
        public ushort EdidManufactureId;
        public ushort EdidProductCodeId;
        public uint ConnectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string MonitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string MonitorDevicePath;
    }
}
