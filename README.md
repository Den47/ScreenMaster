# ScreenMaster

A lightweight WPF application targeting .NET 10 for Windows x64.

The compact window lists active connected monitors with their friendly name and monitor device path. The first column has no heading and shows a star for the primary display, sized to match the height of the adjacent name and ID text. Resolution in pixels and refresh rate appear on separate lines in a single column. The Background column contains standard 112 × 70 buttons with Image content, independent of monitor resolution. Previews use UniformToFill to fill the available area while preserving aspect ratio and cropping overflow. Click a preview to open the standard image file picker (JPEG, PNG, BMP, GIF and TIFF). Cancel leaves the wallpaper and preview unchanged. Selecting a valid image immediately applies it to that monitor through `IDesktopWallpaper` and updates its preview. Missing or unreadable files show an error. Row contents are vertically centered and rows cannot be selected. Matching dividers separate the table header and monitor rows. There is no list title, numbering column or footer text. Use Refresh to reload the list after changing the display configuration.

Monitor information comes from the Windows `QueryDisplayConfig` and `DisplayConfigGetDeviceInfo` APIs. Wallpaper assignments use the monitor device path. Connected displays disabled in Windows display settings are not included. Images are applied in Fill mode; Windows uses one shared positioning mode for all monitors, while the selected image changes only on the chosen monitor. Chosen paths remain in memory for this session, including after Refresh. The application does not save a profile to the registry or a configuration file. Windows manages its own wallpaper settings.

Monitor identity data is collected locally through Windows display APIs, SetupAPI and the WMI `WmiMonitorID` provider using built-in COM support. The profile JSON held in the `_profileJson` field contains `version` and a `configs` array. Each configuration has a `screen` section with monitor device path and name, resolution, primary status, refresh rate, EDID manufacturer and product codes, manufacturer identifier, product code and serial number. Its `wallpaper` section contains the chosen image `path` and the `position` mode (currently `Fill`). Unavailable identity fields and unassigned image paths are `null`. Windows uses one shared positioning mode for all monitors even though it is stored with each configuration. The JSON is refreshed when the monitor list reloads or a wallpaper is chosen. UI-only labels and row numbers are omitted. Registry persistence is deferred with a TODO in the source; no profile file is written.

## Development

Requires Windows and the .NET 10 SDK.

```powershell
dotnet build ScreenMaster.slnx --configuration Release
dotnet run --project ScreenMaster.csproj
```

## Publish

```powershell
dotnet publish ScreenMaster.csproj --configuration Release --output publish
```

The output is a single self-contained `ScreenMaster.exe` for Windows x64. The target machine does not need .NET installed. Native libraries may be extracted to `%TEMP%\.net` at runtime.
