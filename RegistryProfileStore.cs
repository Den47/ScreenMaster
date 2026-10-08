using System.IO;
using Microsoft.Win32;

namespace ScreenMaster;

internal enum RegistryProfileFormat
{
    CompactString,
    IndentedString,
    MultiString
}

internal sealed record PreparedRegistryProfile(string KeyPath, string ValueName, RegistryValueKind Kind, object Data);

internal static class RegistryProfileStore
{
    public const string SubKey = @"Software\ScreenMaster";
    public const string ValueName = "Profile";

    public static PreparedRegistryProfile PrepareWrite(MonitorProfile profile,
        RegistryProfileFormat format = RegistryProfileFormat.CompactString)
    {
        var data = format switch
        {
            RegistryProfileFormat.CompactString => (object)MonitorProfileSerializer.Serialize(profile),
            RegistryProfileFormat.IndentedString => MonitorProfileSerializer.Serialize(profile, indented: true),
            RegistryProfileFormat.MultiString => MonitorProfileSerializer.Serialize(profile, indented: true)
                .Split('\n').Select(line => line.TrimEnd('\r')).ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        return new PreparedRegistryProfile($@"HKEY_CURRENT_USER\{SubKey}", ValueName,
            format == RegistryProfileFormat.MultiString ? RegistryValueKind.MultiString : RegistryValueKind.String, data);
    }

    public static void Save(MonitorProfile profile)
    {
        var prepared = PrepareWrite(profile, RegistryProfileFormat.CompactString);
        using var key = Registry.CurrentUser.CreateSubKey(SubKey, writable: true)
            ?? throw new IOException("Unable to open the ScreenMaster registry key for writing.");
        key.SetValue(prepared.ValueName, prepared.Data, prepared.Kind);
    }

    public static MonitorProfile? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SubKey, writable: false);
        var data = key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return data is null ? null : Decode(key!.GetValueKind(ValueName), data);
    }

    public static MonitorProfile Decode(RegistryValueKind kind, object data)
    {
        var json = (kind, data) switch
        {
            (RegistryValueKind.String, string text) => text,
            (RegistryValueKind.MultiString, string[] lines) => string.Join('\n', lines),
            _ => throw new InvalidDataException("The Profile value must contain JSON as REG_SZ or REG_MULTI_SZ.")
        };

        return MonitorProfileSerializer.Deserialize(json);
    }
}
