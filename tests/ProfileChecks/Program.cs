using System.Text.Json;
using Microsoft.Win32;
using ScreenMaster;

var first = new MonitorInfo(1, "Example display 1", @"\\?\DISPLAY#DEMO001#INSTANCE1#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}",
    2560, 1440, true, 144)
{
    WallpaperPath = @"C:\Wallpapers\Forest.jpg",
    Manufacturer = "DEM",
    ProductCode = "0001",
    SerialNumber = "EXAMPLE-001"
};
var second = new MonitorInfo(2, "Example display 2", @"\\?\DISPLAY#DEMO002#INSTANCE2#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}",
    1920, 1080, false, 60);
var profile = MonitorProfileSerializer.Create([first, second]);
var compact = MonitorProfileSerializer.Serialize(profile);
var checks = 0;

foreach (var format in Enum.GetValues<RegistryProfileFormat>())
{
    var prepared = RegistryProfileStore.PrepareWrite(profile, format);
    Check(prepared.KeyPath == @"HKEY_CURRENT_USER\Software\ScreenMaster" && prepared.ValueName == "Profile",
        $"Registry target: {format}");
    Check(prepared.Kind == (format == RegistryProfileFormat.MultiString ? RegistryValueKind.MultiString : RegistryValueKind.String),
        $"Registry type: {format}");
    Check(MonitorProfileSerializer.Serialize(RegistryProfileStore.Decode(prepared.Kind, prepared.Data)) == compact,
        $"JSON round trip: {format}");
}

Check(!compact.Contains('\r') && !compact.Contains('\n'), "Registry JSON is stored on one line");

var paths = MonitorProfileSerializer.GetWallpaperPaths(profile);
Check(paths.Count == 1 && paths[first.Id.ToUpperInvariant()] == first.WallpaperPath,
    "Unselected monitors are omitted; monitor IDs are case insensitive");

var updated = MonitorProfileSerializer.Update(profile, [first with { Width = 3840, Height = 2160 }]);
Check(updated.Configs.Count == 2 && updated.Configs.Single(config => config.Screen.Id == first.Id).Screen.Width == 3840,
    "Refresh updates metadata and retains disconnected monitor configurations");
Check(MonitorProfileSerializer.GetWallpaperPaths(updated)[first.Id] == first.WallpaperPath,
    "Refresh retains wallpaper assignments");
Check(MonitorProfileSerializer.Update(profile, []).Configs.Count == 2,
    "An empty connected-monitor list does not erase the saved profile");

var missing = updated.Configs.Single(config => config.Screen.Id == second.Id);
Check(!missing.Screen.IsConnected && missing.Wallpaper == profile.Configs[1].Wallpaper,
    "A missing monitor is marked disconnected and its wallpaper settings are retained");
Check(MonitorProfileSerializer.Update(profile, []).Configs.All(config => !config.Screen.IsConnected),
    "All saved monitors become missing when no displays are connected");

var freshFirst = first with
{
    Id = first.Id.ToUpperInvariant(), Name = "Updated name", Width = 3840, Height = 2160,
    RefreshRate = 60, IsPrimary = false, WallpaperPath = null
};
var refreshed = MonitorProfileSerializer.Update(profile, [freshFirst, second]);
Check(refreshed.Configs[0].Screen.Name == "Updated name" && refreshed.Configs[0].Screen.Width == 3840
    && refreshed.Configs[0].Screen.RefreshRate == 60 && !refreshed.Configs[0].Screen.IsPrimary,
    "A case-insensitive ID match refreshes live metadata");
Check(refreshed.Configs[0].Wallpaper.Path == first.WallpaperPath
    && refreshed.Configs[0].Screen.WallpaperPath == first.WallpaperPath,
    "An ID match restores the saved wallpaper without a live preview path");

var moved = first with { Id = "new-device-path", Manufacturer = "dem", ProductCode = " 0001 ", WallpaperPath = null };
var migrated = MonitorProfileSerializer.Update(profile, [moved]);
Check(migrated.Configs.Count == 2 && migrated.Configs[0].Screen.Id == moved.Id
    && migrated.Configs[0].Screen.IsConnected && migrated.Configs[0].Wallpaper.Path == first.WallpaperPath,
    "A unique manufacturer, product and serial match migrates the device path and wallpaper");
Check(!MonitorProfileSerializer.GetWallpaperPaths(migrated).ContainsKey(first.Id),
    "The migrated device path replaces the old path");
Check(MonitorProfileSerializer.Update(migrated, [moved]).Configs.Count == 2,
    "Repeated discovery does not duplicate a migrated monitor");

var newMonitor = second with { Id = "new-monitor", Name = "New display" };
var added = MonitorProfileSerializer.Update(profile, [first with { WallpaperPath = null }, newMonitor]);
Check(added.Configs.Count == 3 && added.Configs[2].Screen.IsConnected && added.Configs[2].Wallpaper.Path is null,
    "A new monitor is added with no selected wallpaper");
Check(MonitorProfileSerializer.Update(added, [first, second, newMonitor]).Configs.All(config => config.Screen.IsConnected),
    "A returning monitor clears the missing state");

var incompleteIdentity = first with { Manufacturer = null, ProductCode = null, SerialNumber = null, WallpaperPath = null };
var recoveredIdentity = MonitorProfileSerializer.Update(profile, [incompleteIdentity]);
Check(recoveredIdentity.Configs[0].Screen.Manufacturer == first.Manufacturer
    && recoveredIdentity.Configs[0].Screen.SerialNumber == first.SerialNumber,
    "Temporarily unavailable identity fields do not erase known metadata for an ID match");

var noSerialProfile = MonitorProfileSerializer.Create([first with { SerialNumber = null }]);
var noSerialMatch = MonitorProfileSerializer.Update(noSerialProfile, [moved with { SerialNumber = null }]);
Check(noSerialMatch.Configs.Count == 2 && !noSerialMatch.Configs[0].Screen.IsConnected,
    "Different device paths are not matched using manufacturer and model alone");
var otherSerial = MonitorProfileSerializer.Update(profile, [moved with { SerialNumber = "different-device" }]);
Check(otherSerial.Configs.Count == 3 && !otherSerial.Configs[0].Screen.IsConnected,
    "A different serial number is treated as a new monitor");

var ambiguousLive = MonitorProfileSerializer.Update(MonitorProfileSerializer.Create([first]),
    [moved, moved with { Id = "another-device-path" }]);
Check(ambiguousLive.Configs.Count == 3 && !ambiguousLive.Configs[0].Screen.IsConnected,
    "Ambiguous live hardware identity does not steal an assignment");
var twoSaved = MonitorProfileSerializer.Create([first, first with { Id = "another-saved-path" }]);
var ambiguousSaved = MonitorProfileSerializer.Update(twoSaved, [moved]);
Check(ambiguousSaved.Configs.Count == 3 && !ambiguousSaved.Configs[0].Screen.IsConnected
    && !ambiguousSaved.Configs[1].Screen.IsConnected,
    "Ambiguous saved hardware identity does not pick an arbitrary profile");

var reserved = MonitorProfileSerializer.Update(twoSaved, [first with { Id = "another-saved-path", WallpaperPath = null }]);
Check(!reserved.Configs[0].Screen.IsConnected && reserved.Configs[1].Screen.IsConnected,
    "All exact ID matches are reserved before hardware matching");
var reordered = MonitorProfileSerializer.Update(profile, [second, first with { WallpaperPath = null }]);
Check(reordered.Configs[0].Wallpaper.Path == first.WallpaperPath && reordered.Configs[1].Wallpaper.Path is null,
    "Display enumeration order does not change wallpaper assignments");

var unicodePath = @"C:\Обои\Лес \""зима\"".jpg";
var unicodeProfile = MonitorProfileSerializer.Create([first with { WallpaperPath = unicodePath }]);
Check(MonitorProfileSerializer.GetWallpaperPaths(MonitorProfileSerializer.Deserialize(
    MonitorProfileSerializer.Serialize(unicodeProfile)))[first.Id] == unicodePath, "Unicode and escaped paths round trip");
Check(MonitorProfileSerializer.Deserialize("{\"version\":1,\"configs\":[]}").Configs.Count == 0,
    "An empty profile is valid");

using (var json = JsonDocument.Parse(compact))
{
    var screen = json.RootElement.GetProperty("configs")[0].GetProperty("screen");
    Check(!screen.TryGetProperty("number", out _) && !screen.TryGetProperty("wallpaperPath", out _)
        && !screen.TryGetProperty("primaryMarker", out _) && !screen.TryGetProperty("resolution", out _)
        && !screen.TryGetProperty("isConnected", out _),
        "UI-only monitor properties are not persisted");
    Check(json.RootElement.GetProperty("configs")[1].GetProperty("wallpaper").GetProperty("path").ValueKind == JsonValueKind.Null,
        "An unselected wallpaper remains null");
}

Check(!MonitorProfileSerializer.Serialize(added).Contains("isConnected", StringComparison.Ordinal),
    "Disconnected UI state is excluded from persisted JSON");
Check(!MonitorProfileSerializer.Update(profile, []).Configs[0].Screen.PrimaryMarker.Any(),
    "A missing monitor does not show the active primary-display marker");

Reject(() => MonitorProfileSerializer.Deserialize(""), "Empty JSON");
Reject(() => MonitorProfileSerializer.Deserialize("{"), "Malformed JSON");
Reject(() => MonitorProfileSerializer.Deserialize("null"), "Null profile");
Reject(() => MonitorProfileSerializer.Deserialize("{}"), "Missing profile fields");
Reject(() => MonitorProfileSerializer.Deserialize("{\"version\":2,\"configs\":[]}"), "Unsupported version");
Reject(() => MonitorProfileSerializer.Deserialize("{\"version\":1,\"configs\":null}"), "Null configurations");
Reject(() => MonitorProfileSerializer.Deserialize("{\"version\":1,\"configs\":[null]}"), "Null configuration");
Reject(() => MonitorProfileSerializer.Serialize(MonitorProfileSerializer.Create([first, first with { Id = first.Id.ToUpperInvariant() }])),
    "Duplicate IDs differing only in case");
Reject(() => MonitorProfileSerializer.Serialize(MonitorProfileSerializer.Create([first with { Id = " " }])), "Blank ID");
Reject(() => MonitorProfileSerializer.Serialize(MonitorProfileSerializer.Create([first with { WallpaperPath = "forest.jpg" }])),
    "Relative wallpaper path");
Reject(() => MonitorProfileSerializer.Serialize(new MonitorProfile(1, [new MonitorConfig(first, new WallpaperConfig(null, "Span"))])),
    "Unsupported positioning mode");
Reject(() => RegistryProfileStore.Decode(RegistryValueKind.DWord, 1), "Incorrect registry value type");
Reject(() => RegistryProfileStore.Decode(RegistryValueKind.MultiString, compact), "Incorrect registry data representation");

if (args is ["--examples", var exampleDirectory])
{
    Directory.CreateDirectory(exampleDirectory);
    File.WriteAllText(Path.Combine(exampleDirectory, "profile.compact.json"), compact + "\n");
    File.WriteAllText(Path.Combine(exampleDirectory, "profile.indented.json"), MonitorProfileSerializer.Serialize(profile, true) + "\n");
    var multi = RegistryProfileStore.PrepareWrite(profile, RegistryProfileFormat.MultiString);
    File.WriteAllLines(Path.Combine(exampleDirectory, "profile.multi-string.txt"), (string[])multi.Data);
    var escaped = compact.Replace("\\", "\\\\").Replace("\"", "\\\"");
    File.WriteAllText(Path.Combine(exampleDirectory, "profile.compact.reg.txt"),
        $"Windows Registry Editor Version 5.00\r\n\r\n[HKEY_CURRENT_USER\\{RegistryProfileStore.SubKey}]\r\n\"{RegistryProfileStore.ValueName}\"=\"{escaped}\"\r\n");
}

Console.WriteLine($"Passed {checks} profile checks. No registry writes or wallpaper changes were performed.");

void Check(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Check failed: {name}");
    }

    checks++;
}

void Reject(Action action, string name)
{
    try
    {
        action();
    }
    catch (InvalidDataException)
    {
        checks++;
        return;
    }

    throw new InvalidOperationException($"Invalid input was accepted: {name}");
}
