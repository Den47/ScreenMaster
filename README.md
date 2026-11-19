# ScreenMaster

ScreenMaster lets you set a different wallpaper on each monitor in Windows. It remembers your choices, even for screens you unplug between sessions.

## Set your wallpapers

1. Open ScreenMaster and find your monitor in the list. A star marks your primary screen.
2. Click the preview in the **Background** column and choose an image. Repeat for any other screens you want to change.
3. Click **Apply&Save** to set the wallpapers on your connected screens and save your choices for next time.

To try an image on just one screen before saving, right-click its row and choose **Apply Wallpaper**. The same menu lets you clear the image selection or remove the monitor from your list.

After plugging in or unplugging a screen, click **Refresh Info**. Disconnected monitors stay in the list with their saved selections. For details on how ScreenMaster stores them, see the [registry profile](REGISTRY.md).

## Run from source

Install the .NET 10 SDK on Windows x64, then start the app from the project folder.

```powershell
dotnet run --project ScreenMaster.csproj
```

Run the profile checks with this command.

```powershell
dotnet run --project tests/ProfileChecks/ProfileChecks.csproj --configuration Release
```

## Publish for Windows x64

```powershell
dotnet publish ScreenMaster.csproj --configuration Release --output publish
```

This creates `publish\ScreenMaster.exe`, which runs without a separate .NET installation.
