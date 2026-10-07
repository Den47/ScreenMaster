# ScreenMaster

A minimal WPF application targeting .NET 10 for Windows x64. The application currently opens an empty window titled ScreenMaster.

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
