using ApexV2.Core.Database;
using ApexV2.UI.Layout;
using ApexV2.Core.Logging;
using Xunit;

namespace ApexV2.Tests;

public class PanelSerializationTests
{
    private (LayoutService svc, DatabaseService db) Create()
    {
        var path = Path.Combine(Path.GetTempPath(), $"apexv2_panels_{Guid.NewGuid():N}.db");
        var db = new DatabaseService(path);
        var logger = new LogManager().GetLogger("SerializeTest");
        var svc = new LayoutService(db, logger);
        return (svc, db);
    }

    [Fact]
    public async Task Resize_Move_PersistsAccurately()
    {
        var (svc, db) = Create();
        await db.InitializeDatabaseAsync();
        var layout = new WorkspaceLayoutModel { Name = "Geom" };
        var panel = new LayoutPanelModel { Type = "chart", X = 15, Y = 25, Width = 420, Height = 310, ZIndex = 5 };
        layout.Panels.Add(panel);
        await svc.SaveAsync(layout);

        // simulate move + resize
        panel.X = 188.7; panel.Y = 299.2; panel.Width = 777.4; panel.Height = 522.9; panel.ZIndex = 9;
        await svc.SaveAsync(layout);

        var loaded = await svc.LoadByNameAsync("Geom");
        Assert.NotNull(loaded);
        var lp = loaded!.Panels.Single();
        Assert.Equal(188.7, lp.X, 1);
        Assert.Equal(299.2, lp.Y, 1);
        Assert.Equal(777.4, lp.Width, 1);
        Assert.Equal(522.9, lp.Height, 1);
        Assert.Equal(9, lp.ZIndex);
    }

    [Fact]
    public async Task MultiplePanels_ZIndexIntegrity()
    {
        var (svc, db) = Create();
        await db.InitializeDatabaseAsync();
        var layout = new WorkspaceLayoutModel { Name = "ZTest" };
        layout.Panels.Add(new LayoutPanelModel { Type = "chart", ZIndex = 1 });
        layout.Panels.Add(new LayoutPanelModel { Type = "watchlist", ZIndex = 2 });
        layout.Panels.Add(new LayoutPanelModel { Type = "news", ZIndex = 5 });
        await svc.SaveAsync(layout);

        // mutate z-order
        layout.Panels[0].ZIndex = 10;
        layout.Panels[1].ZIndex = 3;
        await svc.SaveAsync(layout);

        var loaded = await svc.LoadByNameAsync("ZTest");
        Assert.NotNull(loaded);
        var dict = loaded!.Panels.ToDictionary(p => p.Type, p => p.ZIndex);
        Assert.Equal(10, dict["chart"]);
        Assert.Equal(3, dict["watchlist"]);
        Assert.Equal(5, dict["news"]);
    }
}
