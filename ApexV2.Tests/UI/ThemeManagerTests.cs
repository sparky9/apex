using System.Threading.Tasks;
using ApexV2.UI.Themes;
using ApexV2.Core.Logging;
using ApexV2.Core.Config;
using ApexV2.Core.Database;
using FluentAssertions;
using Xunit;
using System.Windows;

namespace ApexV2.Tests.UI;

public class ThemeManagerTests
{
    private ThemeManager CreateThemeManager()
    {
        var logManager = new LogManager { MinimumLevel = LogLevel.Error };
        var settings = new SettingsService(new DatabaseService());
        return new ThemeManager(logManager.GetLogger("ThemeManagerTest"), settings);
    }

    [Fact]
    public void ApplyTheme_Sets_CurrentTheme()
    {
        var tm = CreateThemeManager();
        var ok = tm.ApplyTheme("Dark", persist:false);
        ok.Should().BeTrue();
        tm.CurrentTheme.Should().Be("Dark");
    }

    [Fact]
    public void Accent_Rejected_When_LowContrast()
    {
        var tm = CreateThemeManager();
        tm.ApplyTheme("Dark", persist:false);
        var before = tm.CurrentAccentHex;
        // Provide a very similar background color that should likely fail >=3 contrast (near window background)
        tm.ApplyTheme("Dark", persist:false, overrideAccentHex:"#2C2C2C");
        tm.CurrentAccentHex.Should().Be(before); // unchanged
    }
}
