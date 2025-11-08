using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows; // WPF Application
using System.Windows.Media;
using ApexV2.Core.Logging;
using ApexV2.Core.Config;

namespace ApexV2.UI.Themes;

public class ThemeManager
{
    private readonly Dictionary<string, Uri> _themeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        {"Dark", new Uri("/ApexV2;component/UI/Themes/Dark.xaml", UriKind.Relative)} ,
        {"Light", new Uri("/ApexV2;component/UI/Themes/Light.xaml", UriKind.Relative)} ,
        {"Professional Blue", new Uri("/ApexV2;component/UI/Themes/ProfessionalBlue.xaml", UriKind.Relative)} ,
    };

    private readonly Logger _logger;
    private readonly SettingsService _settingsService;
    private ResourceDictionary? _currentDictionary;

    public event EventHandler<string>? ThemeChanged;

    public string CurrentTheme { get; private set; } = "Dark";
    public string CurrentAccentHex { get; private set; } = string.Empty;

    public ThemeManager(Logger logger, SettingsService settingsService)
    {
        _logger = logger; _settingsService = settingsService;
    }

    public IReadOnlyList<string> GetAvailableThemes() => _themeMap.Keys.ToList();

    public bool ApplyTheme(string name, bool persist = true, string? overrideAccentHex = null)
    {
        if (!_themeMap.TryGetValue(name, out var uri))
        {
            _logger.Warn($"Unknown theme '{name}'");
            return false;
        }
        try
        {
            var app = System.Windows.Application.Current; // fully qualified
            if (app == null) return false;
            var dict = new ResourceDictionary { Source = uri };
            if (_currentDictionary != null)
            {
                app.Resources.MergedDictionaries.Remove(_currentDictionary);
            }
            app.Resources.MergedDictionaries.Add(dict);
            _currentDictionary = dict;
            CurrentTheme = name;

            if (!string.IsNullOrWhiteSpace(overrideAccentHex))
            {
                TryApplyAccent(overrideAccentHex, app);
            }

            RunContrastCheck();
            _logger.Info($"Applied theme {name}");
            ThemeChanged?.Invoke(this, name);
            if (persist)
            {
                _ = PersistThemeAsync();
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("ApplyTheme failed", ex);
            return false;
        }
    }

    private void TryApplyAccent(string hex, System.Windows.Application app)
    {
        try
        {
            var colorObj = System.Windows.Media.ColorConverter.ConvertFromString(hex);
            if (colorObj is System.Windows.Media.Color mediaColor)
            {
                if (IsContrastAcceptable(mediaColor))
                {
                    app.Resources["AccentColor"] = new SolidColorBrush(mediaColor);
                    CurrentAccentHex = hex;
                }
                else
                {
                    _logger.Warn($"Accent color {hex} rejected due to insufficient contrast.");
                }
            }
        }
        catch (Exception)
        {
            _logger.Warn($"Invalid accent color '{hex}'" );
        }
    }

    private void RunContrastCheck()
    {
        try
        {
            if (System.Windows.Application.Current?.Resources["Brush.WindowBackground"] is SolidColorBrush bg &&
                System.Windows.Application.Current.Resources["Brush.TextPrimary"] is SolidColorBrush fg)
            {
                double contrast = ContrastRatio(bg.Color, fg.Color);
                if (contrast < 4.5)
                {
                    _logger.Warn($"Low contrast detected: {contrast:F2} (<4.5) between background and primary text");
                }
            }
        }
        catch { }
    }

    private static double ContrastRatio(System.Windows.Media.Color a, System.Windows.Media.Color b)
    {
        double L(System.Windows.Media.Color c)
        {
            double Srgb(double ch)
            {
                ch /= 255.0;
                return ch <= 0.03928 ? ch / 12.92 : Math.Pow((ch + 0.055)/1.055, 2.4);
            }
            var r = Srgb(c.R); var g = Srgb(c.G); var bl = Srgb(c.B);
            return 0.2126*r + 0.7152*g + 0.0722*bl;
        }
        var l1 = L(a) + 0.05; var l2 = L(b) + 0.05;
        return l1 > l2 ? l1 / l2 : l2 / l1;
    }

    private bool IsContrastAcceptable(System.Windows.Media.Color accent)
    {
        try
        {
            if (System.Windows.Application.Current?.Resources["Brush.WindowBackground"] is SolidColorBrush bg)
            {
                var ratio = ContrastRatio(bg.Color, accent);
                return ratio >= 3.0; // accent elements can be slightly lower than text WCAG threshold
            }
        }
        catch { }
        return true; // fallback permissive
    }

    private async System.Threading.Tasks.Task PersistThemeAsync()
    {
        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            settings.Appearance.Theme = CurrentTheme;
            if (!string.IsNullOrWhiteSpace(CurrentAccentHex))
                settings.Appearance.AccentColor = CurrentAccentHex;
            await _settingsService.SaveSettingsAsync(settings);
        }
        catch
        {
            _logger.Warn("PersistThemeAsync failed");
        }
    }
}
