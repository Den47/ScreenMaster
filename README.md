# ScreenMaster

A lightweight WPF application targeting .NET 10 for Windows x64.

The compact window lists saved and currently connected monitors with their friendly name and monitor device path. The first column has no heading and shows a star for the connected primary display, sized to match the height of the adjacent name and ID text. Resolution in pixels and refresh rate appear on separate lines in a single column. The Background column contains standard 112 × 70 buttons with Image content, independent of monitor resolution. Previews use UniformToFill to fill the available area while preserving aspect ratio and cropping overflow. Click a preview to open the standard image file picker (JPEG, PNG, BMP, GIF and TIFF). Cancel leaves the wallpaper and preview unchanged. Selecting a valid image immediately applies it to a connected monitor through `IDesktopWallpaper` and updates its preview. For a missing monitor, selection updates only the remembered preview path. Missing or unreadable files show an error. Row contents are vertically centered and rows cannot be selected. Matching dividers separate the table header and monitor rows. The vertical scrollbar is hidden while scrolling remains available. There is no list title, numbering column or footer text. Use Refresh Info to reload the list after changing the display configuration.

Monitor information comes from the Windows `QueryDisplayConfig` and `DisplayConfigGetDeviceInfo` APIs. Wallpaper assignments use the current monitor device path. Windows display enumeration includes only active displays; saved inactive or disconnected displays remain visible as missing. Images are applied in Fill mode; Windows uses one shared positioning mode for all monitors, while the selected image changes only on the chosen monitor. The registry profile is read once at startup to restore previews and reconcile monitors. Loading does not apply wallpapers. Apply&Save saves the profile to the registry. Windows manages its own wallpaper settings.

Monitor identity data is collected locally through Windows display APIs, SetupAPI and the WMI `WmiMonitorID` provider using built-in COM support. The profile JSON held in the `_profileJson` field contains `version` and a `configs` array. Each configuration has a `screen` section with monitor device path and name, resolution, primary status, refresh rate, EDID manufacturer and product codes, manufacturer identifier, product code and serial number. Its `wallpaper` section contains the chosen image `path` and the `position` mode (currently `Fill`). Unavailable identity fields and unassigned image paths are `null`. Windows uses one shared positioning mode for all monitors even though it is stored with each configuration. The JSON is refreshed in memory when the monitor list reloads or a wallpaper is chosen. UI-only labels, row numbers and connection state are omitted.

Matching first reserves case-insensitive device-path matches, then compares manufacturer, product code and serial number when all three fields are present and identify exactly one unmatched saved and live monitor. Resolution, refresh rate, name and primary status do not determine identity. Matched configurations retain wallpaper selections and receive current metadata, including a changed device path. Previously known identity fields survive temporary failures to obtain those fields. Missing monitors remain in the list with a pale red background and retain their configurations. New monitors are appended with no wallpaper selected. Refresh Info repeats this reconciliation in memory, allowing returning monitors to recover without duplicate entries.

Use Apply&Save next to Refresh Info to apply each connected monitor's selected preview image again and save the complete session profile. Missing monitors and monitors without a selected image are skipped when applying wallpapers; all configurations remain in the saved profile. If applying an image fails, the other monitors are still processed and the profile is still saved. Wallpaper and registry errors are shown together; saved selections describe the chosen images even if Windows could not apply one of them.

Apply&Save creates or updates `HKEY_CURRENT_USER\Software\ScreenMaster\Profile` as a `REG_SZ` value containing compact JSON on one line. Startup only reads the profile; Refresh Info and the image picker do not access it in the registry. Reconciled metadata and newly discovered monitors are written only when Apply&Save is clicked. Each refresh prepares the data in `_pendingRegistryProfile` in memory. Registry read errors show a message and allow current monitors to be listed. Registry save failures leave the in-memory selections usable. See [REGISTRY.md](REGISTRY.md) for the selected parameter format and exact sample data.

Refresh Info and Apply&Save are left-aligned above the list, in that order, and both are 112 pixels wide. The Actions column has two vertically stacked buttons per monitor, with their top and bottom edges aligned to the preview: clear sets `wallpaper.path` to null and empties the preview; remove deletes the monitor's entire configuration and list row. Both edit only the in-memory profile, leave desktop wallpapers unchanged and require Apply&Save to save. They do not trigger monitor discovery, so a removed connected monitor stays removed until Refresh Info or the next launch discovers it again, with no wallpaper selected.

Monitor IDs appear on a single line, with character ellipsis when they exceed the available width.

## Development

Requires Windows and the .NET 10 SDK.

```powershell
dotnet build ScreenMaster.slnx --configuration Release
dotnet run --project ScreenMaster.csproj
```

Profile serialization, registry representation, invalid-data handling and disconnected-monitor retention can be checked without changing the registry or wallpapers:

```powershell
dotnet run --project tests/ProfileChecks/ProfileChecks.csproj --configuration Release
```

## Publish

```powershell
dotnet publish ScreenMaster.csproj --configuration Release --output publish
```

The output is a single self-contained `ScreenMaster.exe` for Windows x64. The target machine does not need .NET installed. Native libraries may be extracted to `%TEMP%\.net` at runtime.
