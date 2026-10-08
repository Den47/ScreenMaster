# Registry profile

Apply&Save saves the complete session profile to one named value under the current Windows user:

```text
Key:   HKEY_CURRENT_USER\Software\ScreenMaster
Name:  Profile
Type:  REG_SZ
Data:  Complete JSON on one line
```

`Profile` is a named value; the unnamed `(Default)` value is not used. The JSON schema remains `version`, `configs`, `screen` and `wallpaper`. The profile is read at startup and written only by Apply&Save.

## Selected value format

The writer always uses `CompactString`: one JSON string stored as `RegistryValueKind.String` (`REG_SZ`). Formatting alternatives remain available for review and parser checks, but are not used for saving.

The samples use invented monitor IDs and paths. The first monitor has an assigned image; the second has `wallpaper.path: null`. No image bytes are included in the value.

| Preview | File |
| --- | --- |
| Exact one-line REG_SZ data | [profile.compact.json](examples/registry/profile.compact.json) |
| Exact formatted REG_SZ data | [profile.indented.json](examples/registry/profile.indented.json) |
| REG_MULTI_SZ strings shown one per line | [profile.multi-string.txt](examples/registry/profile.multi-string.txt) |
| Text preview of a registry export for compact REG_SZ | [profile.compact.reg.txt](examples/registry/profile.compact.reg.txt) |

The `.reg.txt` file is a text preview only. Its extra backslashes and escaped quotes belong to the registry export syntax. Registry Editor's value data contains plain JSON like `profile.compact.json`, without that outer export escaping.

## Implementation

- `MonitorProfileSerializer.Create` builds the established profile structure from monitors and their chosen preview paths.
- `Serialize` produces compact or indented JSON. `Deserialize` checks JSON structure, version, configuration presence, monitor IDs, duplicate IDs, absolute wallpaper paths and the supported `Fill` mode. Invalid data raises `InvalidDataException`; unsupported future versions are not silently interpreted as version 1.
- `GetWallpaperPaths` returns selections keyed by monitor device path, using case-insensitive matching and skipping null paths. It does not open image files or apply wallpapers. A missing image remains an assignment; preview/application handles the missing file as before.
- `Update` reconciles saved configurations with currently connected monitors. Device-path matches have priority. A fallback compares nonempty manufacturer, product code and serial number, ignoring text case and surrounding whitespace, and requires a unique match in both sets. Resolution, refresh rate, name and primary status are not used to identify monitors. Matched entries receive current metadata and device paths without losing wallpaper selections. Missing identity data does not erase previously known values. Unmatched saved entries are retained and marked missing for the UI; unmatched live monitors are added with no chosen image. Connection state is excluded from JSON.
- `RegistryProfileStore.PrepareWrite` returns a `PreparedRegistryProfile` containing the key path, value name, `RegistryValueKind` and data. The data is a `string` for REG_SZ or a `string[]` for REG_MULTI_SZ. This method does not access the registry.
- `RegistryProfileStore.Save` validates and prepares compact JSON before opening or creating `HKCU\Software\ScreenMaster`, then sets the named `Profile` value explicitly as REG_SZ. An existing value is replaced on the next Apply&Save click. Other values in the key are not modified.
- `RegistryProfileStore.Read` opens the existing key with `writable: false`. A missing key/value returns null. Present values must be REG_SZ or REG_MULTI_SZ and contain a valid profile. Registry access errors are allowed to reach the caller. The loader never creates a key or writes a value.

`MainWindow.RefreshMonitors` maintains `_profile`, `_profileJson` and `_pendingRegistryProfile` in memory and displays all configurations. Missing rows have a pale red background. Only `ApplySave_Click` calls `Save`, after attempting selected wallpapers for connected monitors. Missing monitors and null selections are skipped when applying images and retained in the saved profile. Saving still occurs if one wallpaper cannot be applied, or no monitor has an image selected. If monitor enumeration failed and there is no monitor list, Apply&Save returns without saving. Wallpaper and registry errors are combined into one dialog, and registry failures do not erase the in-memory selections or undo applied wallpapers.

Startup reads the profile once without writing it or applying wallpapers. Refresh Info and image selection do not access the profile in the registry. Clicking a connected monitor's preview still applies the chosen image immediately, but saving it requires Apply&Save. A missing monitor's preview can be changed in memory without attempting to apply an image to a disconnected display.

## Profile editing

Each monitor row has clear above remove in the Actions column. clear sets its `wallpaper.path` to null while retaining the screen configuration and positioning mode. remove deletes the entire configuration and row, for either a connected or missing monitor. These actions update the list and `_profileJson` immediately, without querying displays, changing Windows wallpapers or writing to the registry. Apply&Save saves the resulting profile. A removed monitor that is still connected is discovered again on Refresh Info or the next launch and starts with no selected wallpaper. A removed disconnected monitor stays absent until it is connected and discovered again.

## Startup loading

The constructor initializes the window, calls `RegistryProfileStore.Read`, then enumerates and reconciles current monitors with the restored `_profile`. Missing keys or values are treated as an empty profile. Invalid JSON, unsupported versions and registry access errors are reported, and current monitor discovery continues with the empty in-memory profile. An existing value is never overwritten automatically during startup or Refresh Info.

Reconciliation restores previews, updates current metadata and retains disconnected configurations. Changes are visible immediately in the list and in-memory JSON; Apply&Save is required to persist them. No startup entry or registry import is created.

## Verification

Run the dependency-free profile checks:

```powershell
dotnet run --project tests/ProfileChecks/ProfileChecks.csproj --configuration Release
```

To regenerate the review samples from the production serializer:

```powershell
dotnet run --project tests/ProfileChecks/ProfileChecks.csproj --configuration Release -- --examples examples/registry
```

The checks exercise all three representations, round trips, null selections, Unicode and JSON escaping, ID and hardware matching, ambiguous identities, metadata refresh, missing and returning monitors, disconnected-monitor retention and invalid inputs. They do not call the registry loader or any wallpaper APIs.
