using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ScreenMaster;

public partial class MainWindow : Window
{
    private MonitorProfile _profile = new(MonitorProfileSerializer.CurrentVersion, []);
    private string _profileJson = string.Empty;
    private PreparedRegistryProfile? _pendingRegistryProfile;

    public MainWindow()
    {
        InitializeComponent();
        LoadProfile();
        RefreshMonitors();
    }

    private void RefreshInfo_Click(object sender, RoutedEventArgs e) => RefreshMonitors();

    private void LoadProfile()
    {
        try
        {
            _profile = RegistryProfileStore.Read() ?? _profile;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            MessageBox.Show(this, $"Unable to load saved profile:\n{exception.Message}",
                "ScreenMaster", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplySave_Click(object sender, RoutedEventArgs e)
    {
        if (MonitorList.ItemsSource is not IEnumerable<MonitorInfo> monitors)
        {
            return;
        }

        var errors = new List<string>();
        foreach (var monitor in monitors)
        {
            if (!monitor.IsConnected || string.IsNullOrWhiteSpace(monitor.WallpaperPath))
            {
                continue;
            }

            try
            {
                DesktopWallpaperService.Apply(monitor.Id, monitor.WallpaperPath);
            }
            catch (Exception exception) when (exception is COMException or IOException or UnauthorizedAccessException
                                             or ArgumentException or InvalidOperationException or NotSupportedException)
            {
                errors.Add($"Unable to set wallpaper for {monitor.Name}: {exception.Message}");
            }
        }

        try
        {
            RegistryProfileStore.Save(_profile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            errors.Add($"Unable to save profile: {exception.Message}");
        }

        if (errors.Count > 0)
        {
            MessageBox.Show(this, string.Join("\n", errors),
                "ScreenMaster", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

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

            SetWallpaperPath(monitor.Id, path);
        }
        catch (Exception exception) when (exception is COMException or IOException or UnauthorizedAccessException
                                         or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            MessageBox.Show(this, $"Unable to load wallpaper for {monitor.Name}:\n{exception.Message}",
                "ScreenMaster", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyWallpaper_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: MonitorInfo monitor }
            || !monitor.IsConnected || string.IsNullOrWhiteSpace(monitor.WallpaperPath))
        {
            return;
        }

        try
        {
            DesktopWallpaperService.Apply(monitor.Id, monitor.WallpaperPath);
        }
        catch (Exception exception) when (exception is COMException or IOException or UnauthorizedAccessException
                                         or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            MessageBox.Show(this, $"Unable to set wallpaper for {monitor.Name}:\n{exception.Message}",
                "ScreenMaster", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearWallpaper_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: MonitorInfo monitor })
        {
            SetWallpaperPath(monitor.Id, null);
        }
    }

    private void RemoveMonitor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: MonitorInfo monitor })
        {
            return;
        }

        _profile = _profile with
        {
            Configs = _profile.Configs.Where(config =>
                !string.Equals(config.Screen.Id, monitor.Id, StringComparison.OrdinalIgnoreCase)).ToArray()
        };
        UpdateProfileView();
    }

    private void SetWallpaperPath(string monitorId, string? path)
    {
        _profile = _profile with
        {
            Configs = _profile.Configs.Select(config =>
                string.Equals(config.Screen.Id, monitorId, StringComparison.OrdinalIgnoreCase)
                    ? config with { Wallpaper = config.Wallpaper with { Path = path } }
                    : config).ToArray()
        };
        UpdateProfileView();
    }

    private void UpdateProfileView()
    {
        MonitorList.ItemsSource = _profile.Configs.Select(config => config.Screen with
            { WallpaperPath = config.Wallpaper.Path }).ToArray();
        _pendingRegistryProfile = RegistryProfileStore.PrepareWrite(_profile);
        _profileJson = (string)_pendingRegistryProfile.Data;
    }

    private void RefreshMonitors()
    {
        try
        {
            var monitors = MonitorService.GetConnectedMonitors();
            _profile = MonitorProfileSerializer.Update(_profile, monitors);
            UpdateProfileView();
            MonitorMessage.Foreground = Brushes.DimGray;
            MonitorMessage.Text = "No active monitors found.";
            MonitorMessage.Visibility = monitors.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Win32Exception exception)
        {
            MonitorList.ItemsSource = null;
            MonitorMessage.Foreground = Brushes.Firebrick;
            MonitorMessage.Text = $"Unable to read connected monitors: {exception.Message} (Windows error {exception.NativeErrorCode}). Try Refresh Info.";
            MonitorMessage.Visibility = Visibility.Visible;
        }
    }
}
