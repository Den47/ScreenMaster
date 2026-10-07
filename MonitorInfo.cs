using System.Text.Json.Serialization;

namespace ScreenMaster;

internal sealed record MonitorInfo(
    [property: JsonIgnore] int Number,
    string Name,
    string Id,
    uint Width,
    uint Height,
    bool IsPrimary,
    double? RefreshRate)
{
    public ushort? EdidManufacturerId { get; init; }

    public ushort? EdidProductCodeId { get; init; }

    public string? Manufacturer { get; init; }

    public string? ProductCode { get; init; }

    public string? SerialNumber { get; init; }

    [JsonIgnore]
    public string? WallpaperPath { get; init; }

    [JsonIgnore]
    public string Resolution => Width > 0 && Height > 0 ? $"{Width} × {Height}" : "Unavailable";

    [JsonIgnore]
    public string PrimaryLabel => IsPrimary ? "Primary" : string.Empty;

    [JsonIgnore]
    public string PrimaryMarker => IsPrimary ? "★" : string.Empty;

    [JsonIgnore]
    public string RefreshRateLabel => RefreshRate is > 0 ? $"{RefreshRate:0.##} Hz" : "Unavailable";
}
