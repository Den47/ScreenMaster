using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ScreenMaster;

internal sealed record MonitorIdentity(string? Manufacturer, string? ProductCode, string? SerialNumber);

internal static class MonitorIdentityService
{
    public static IReadOnlyDictionary<string, MonitorIdentity> GetIdentities()
    {
        var identities = new Dictionary<string, MonitorIdentity>(StringComparer.OrdinalIgnoreCase);
        object? locator = null;
        object? services = null;
        object? items = null;
        try
        {
            locator = Activator.CreateInstance(Type.GetTypeFromProgID("WbemScripting.SWbemLocator", true)!);
            services = ((dynamic)locator!).ConnectServer(".", @"root\wmi");
            items = ((dynamic)services!).ExecQuery(
                "SELECT * FROM WmiMonitorID",
                "WQL", 0);
            foreach (var item in (IEnumerable)items!)
            {
                try
                {
                    dynamic monitor = item;
                    string instanceName = ReadProperty(monitor, "InstanceName");
                    identities[instanceName] = new MonitorIdentity(
                        DecodeText(ReadProperty(monitor, "ManufacturerName")),
                        DecodeText(ReadProperty(monitor, "ProductCodeID")),
                        DecodeText(ReadProperty(monitor, "SerialNumberID")));
                }
                finally
                {
                    Marshal.ReleaseComObject(item);
                }
            }
        }
        catch (COMException exception)
        {
            Debug.WriteLine($"Monitor identity information is unavailable: {exception.Message}");
        }
        finally
        {
            if (items is not null) Marshal.ReleaseComObject(items);
            if (services is not null) Marshal.ReleaseComObject(services);
            if (locator is not null) Marshal.ReleaseComObject(locator);
        }

        return identities;
    }

    public static MonitorIdentity? FindIdentity(string devicePath, IReadOnlyDictionary<string, MonitorIdentity> identities)
    {
        var instanceId = GetDeviceInstanceId(devicePath);
        if (instanceId is null)
        {
            return null;
        }

        if (identities.TryGetValue(instanceId, out var identity))
        {
            return identity;
        }

        var matches = identities.Where(pair =>
            pair.Key.StartsWith(instanceId + "_", StringComparison.OrdinalIgnoreCase)
            && uint.TryParse(pair.Key.AsSpan(instanceId.Length + 1), out _)).ToArray();
        return matches.Length == 1 ? matches[0].Value : null;
    }

    private static string? DecodeText(object? value)
    {
        if (value is not Array codes)
        {
            return null;
        }

        var text = new string(codes.Cast<object>().Select(code => (char)Convert.ToUInt16(code)).TakeWhile(code => code != '\0').ToArray()).Trim();
        return text.Length == 0 ? null : text;
    }

    private static object ReadProperty(dynamic monitor, string name)
    {
        object properties = monitor.Properties_;
        object? property = null;
        try
        {
            property = ((dynamic)properties).Item(name);
            return ((dynamic)property).Value;
        }
        finally
        {
            if (property is not null) Marshal.ReleaseComObject(property);
            Marshal.ReleaseComObject(properties);
        }
    }

    private static string? GetDeviceInstanceId(string devicePath)
    {
        var devices = SetupDiCreateDeviceInfoList(IntPtr.Zero, IntPtr.Zero);
        if (devices == new IntPtr(-1))
        {
            return null;
        }

        try
        {
            var deviceInterface = new DeviceInterfaceData { Size = (uint)Marshal.SizeOf<DeviceInterfaceData>() };
            if (!SetupDiOpenDeviceInterface(devices, devicePath, 0, ref deviceInterface))
            {
                return null;
            }

            var deviceInfo = new DeviceInfoData { Size = (uint)Marshal.SizeOf<DeviceInfoData>() };
            if (!SetupDiGetDeviceInterfaceDetail(devices, ref deviceInterface, IntPtr.Zero, 0, out _, ref deviceInfo)
                && Marshal.GetLastWin32Error() != 122)
            {
                return null;
            }

            var instanceId = new StringBuilder(256);
            return SetupDiGetDeviceInstanceId(devices, ref deviceInfo, instanceId, (uint)instanceId.Capacity, out _)
                ? instanceId.ToString()
                : null;
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(devices);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInterfaceData
    {
        public uint Size;
        public Guid InterfaceClass;
        public uint Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInfoData
    {
        public uint Size;
        public Guid ClassGuid;
        public uint DeviceInstance;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiCreateDeviceInfoList(IntPtr classGuid, IntPtr parent);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiOpenDeviceInterface(IntPtr devices, string path, uint flags, ref DeviceInterfaceData deviceInterface);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr devices, ref DeviceInterfaceData deviceInterface,
        IntPtr detail, uint detailSize, out uint requiredSize, ref DeviceInfoData deviceInfo);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInstanceId(IntPtr devices, ref DeviceInfoData deviceInfo,
        StringBuilder instanceId, uint size, out uint requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr devices);
}
