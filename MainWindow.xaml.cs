using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ScreenMaster;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, string> _wallpaperPaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions ProfileJsonOptions = new(JsonSerializerDefaults.Web);
    private string _profileJson = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        RefreshMonitors();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshMonitors();

    private void WallpaperPreview_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: MonitorInfo monitor })
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = $"Choose wallpaper for {monitor.Name}",
            Filter = "Image files|*.jpg;*.jpeg;*.jpe;*.jfif;*.png;*.bmp;*.dib;*.gif;*.tif;*.tiff|JPEG images|*.jpg;*.jpeg;*.jpe;*.jfif|PNG images|*.png|Bitmap images|*.bmp;*.dib|GIF images|*.gif|TIFF images|*.tif;*.tiff",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var path = Path.GetFullPath(dialog.FileName);
            var preview = new WallpaperPreviewConverter().Convert(path, typeof(ImageSource), null!, CultureInfo.CurrentCulture);
            if (preview is null)
            {
                throw new InvalidDataException("The selected file could not be opened as an image.");
            }

            DesktopWallpaperService.Apply(monitor.Id, path);
            _wallpaperPaths[monitor.Id] = path;
            RefreshMonitors();
        }
        catch (Exception exception) when (exception is COMException or IOException or UnauthorizedAccessException
                                         or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            MessageBox.Show(this, $"Unable to set wallpaper for {monitor.Name}:\n{exception.Message}",
                "ScreenMaster", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshMonitors()
    {
        try
        {
            var monitors = MonitorService.GetConnectedMonitors();
            var configuredMonitors = monitors
                .Select(monitor => monitor with { WallpaperPath = _wallpaperPaths.GetValueOrDefault(monitor.Id) })
                .ToArray();
            MonitorList.ItemsSource = configuredMonitors;
            var configs = configuredMonitors
                .Select(monitor => new MonitorConfig(monitor, new WallpaperConfig(monitor.WallpaperPath, "Fill")))
                .ToArray();
            _profileJson = JsonSerializer.Serialize(new MonitorProfile(1, configs), ProfileJsonOptions);
            // TODO: Persist and restore Profile JSON in HKCU\Software\WallpaperRestore when profile storage is implemented.
            MonitorMessage.Foreground = Brushes.DimGray;
            MonitorMessage.Text = "No active monitors found.";
            MonitorMessage.Visibility = monitors.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Win32Exception exception)
        {
            MonitorList.ItemsSource = null;
            MonitorMessage.Foreground = Brushes.Firebrick;
            MonitorMessage.Text = $"Unable to read connected monitors: {exception.Message} (Windows error {exception.NativeErrorCode}). Try Refresh.";
            MonitorMessage.Visibility = Visibility.Visible;
        }
    }
}
