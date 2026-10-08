using System.IO;
using System.Text.Json;

namespace ScreenMaster;

internal static class MonitorProfileSerializer
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions CompactOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions IndentedOptions = new(CompactOptions) { WriteIndented = true };

    public static MonitorProfile Create(IEnumerable<MonitorInfo> monitors) => new(CurrentVersion,
        monitors.Select(monitor => new MonitorConfig(monitor, new WallpaperConfig(monitor.WallpaperPath, "Fill"))).ToArray());

    public static string Serialize(MonitorProfile profile, bool indented = false)
    {
        Validate(profile);
        return JsonSerializer.Serialize(profile, indented ? IndentedOptions : CompactOptions);
    }

    public static MonitorProfile Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("The monitor profile is empty.");
        }

        MonitorProfile profile;
        try
        {
            profile = JsonSerializer.Deserialize<MonitorProfile>(json, CompactOptions)
                ?? throw new InvalidDataException("The monitor profile cannot be null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The monitor profile contains invalid JSON.", exception);
        }

        Validate(profile);
        return profile;
    }

    public static Dictionary<string, string> GetWallpaperPaths(MonitorProfile profile)
    {
        Validate(profile);
        return profile.Configs.Where(config => config.Wallpaper.Path is not null)
            .ToDictionary(config => config.Screen.Id, config => config.Wallpaper.Path!, StringComparer.OrdinalIgnoreCase);
    }

    public static MonitorProfile Update(MonitorProfile profile, IEnumerable<MonitorInfo> connectedMonitors)
    {
        Validate(profile);
        var connected = connectedMonitors.ToArray();
        var connectedById = connected.Select((monitor, index) => (monitor.Id, Index: index))
            .ToDictionary(item => item.Id, item => item.Index, StringComparer.OrdinalIgnoreCase);
        var matches = new int?[profile.Configs.Count];
        var used = new HashSet<int>();

        for (var i = 0; i < profile.Configs.Count; i++)
        {
            if (connectedById.TryGetValue(profile.Configs[i].Screen.Id, out var index))
            {
                matches[i] = index;
                used.Add(index);
            }
        }

        var unmatched = Enumerable.Range(0, profile.Configs.Count).Where(i => matches[i] is null).ToArray();
        foreach (var i in unmatched)
        {
            var candidates = Enumerable.Range(0, connected.Length)
                .Where(index => !used.Contains(index) && SameIdentity(profile.Configs[i].Screen, connected[index]))
                .ToArray();
            if (candidates.Length == 1
                && unmatched.Count(other => SameIdentity(profile.Configs[other].Screen, connected[candidates[0]])) == 1)
            {
                matches[i] = candidates[0];
                used.Add(candidates[0]);
            }
        }

        var configs = new List<MonitorConfig>();
        for (var i = 0; i < profile.Configs.Count; i++)
        {
            var saved = profile.Configs[i];
            var screen = matches[i] is { } index
                ? connected[index] with
                {
                    IsConnected = true,
                    WallpaperPath = saved.Wallpaper.Path,
                    Manufacturer = connected[index].Manufacturer ?? saved.Screen.Manufacturer,
                    ProductCode = connected[index].ProductCode ?? saved.Screen.ProductCode,
                    SerialNumber = connected[index].SerialNumber ?? saved.Screen.SerialNumber,
                    EdidManufacturerId = connected[index].EdidManufacturerId ?? saved.Screen.EdidManufacturerId,
                    EdidProductCodeId = connected[index].EdidProductCodeId ?? saved.Screen.EdidProductCodeId
                }
                : saved.Screen with { IsConnected = false, WallpaperPath = saved.Wallpaper.Path };
            configs.Add(saved with { Screen = screen });
        }

        configs.AddRange(Create(connected.Where((_, index) => !used.Contains(index))
            .Select(monitor => monitor with { IsConnected = true })).Configs);
        var updated = new MonitorProfile(CurrentVersion, configs);
        Validate(updated);
        return updated;
    }

    private static bool SameIdentity(MonitorInfo first, MonitorInfo second) =>
        SameNonEmpty(first.Manufacturer, second.Manufacturer)
        && SameNonEmpty(first.ProductCode, second.ProductCode)
        && SameNonEmpty(first.SerialNumber, second.SerialNumber);

    private static bool SameNonEmpty(string? first, string? second) =>
        !string.IsNullOrWhiteSpace(first) && !string.IsNullOrWhiteSpace(second)
        && string.Equals(first.Trim(), second.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void Validate(MonitorProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Version != CurrentVersion)
        {
            throw new InvalidDataException($"Unsupported monitor profile version: {profile.Version}.");
        }

        if (profile.Configs is null)
        {
            throw new InvalidDataException("The monitor profile must contain a configs array.");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var config in profile.Configs)
        {
            if (config?.Screen is null || config.Wallpaper is null || string.IsNullOrWhiteSpace(config.Screen.Id))
            {
                throw new InvalidDataException("Each configuration must contain a screen ID and wallpaper settings.");
            }

            if (!ids.Add(config.Screen.Id))
            {
                throw new InvalidDataException($"Duplicate monitor ID in profile: {config.Screen.Id}.");
            }

            if (config.Wallpaper.Position != "Fill")
            {
                throw new InvalidDataException($"Unsupported wallpaper position: {config.Wallpaper.Position}.");
            }

            if (config.Wallpaper.Path is { } path && (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)))
            {
                throw new InvalidDataException("A wallpaper path must be an absolute path or null.");
            }
        }
    }
}
