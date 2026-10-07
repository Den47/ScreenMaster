namespace ScreenMaster;

internal sealed record MonitorProfile(int Version, IReadOnlyList<MonitorConfig> Configs);

internal sealed record MonitorConfig(MonitorInfo Screen, WallpaperConfig Wallpaper);

internal sealed record WallpaperConfig(string? Path, string Position);
