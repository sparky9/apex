using ApexV2.Core.Database;
using ApexV2.UI.Layout;
using ApexV2.Core.Logging;
using Xunit;

namespace ApexV2.Tests;

public class LayoutServiceTests
{
    private DatabaseService CreateTempDb()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"apexv2_test_{Guid.NewGuid():N}.db");
        return new DatabaseService(tempPath);
    }

    private LayoutService CreateService(out DatabaseService db)
    {
        db = CreateTempDb();
        var logManager = new LogManager { MinimumLevel = LogLevel.Error };
        var logger = logManager.GetLogger("Test");
        return new LayoutService(db, logger);
    }

    [Fact]
    public async Task SaveLoad_RoundTrip_PreservesPanels()
    {
        var svc = CreateService(out var db);
        await db.InitializeDatabaseAsync();
        var model = new WorkspaceLayoutModel { Name = "TestLayout" };
        model.Panels.Add(new LayoutPanelModel { Type = "chart", X=10, Y=20, Width=300, Height=200, ZIndex=2 });
        model.Panels.Add(new LayoutPanelModel { Type = "watchlist", X=50, Y=60, Width=250, Height=400, ZIndex=1 });
        Assert.True(await svc.SaveAsync(model));

        var loaded = await svc.LoadByNameAsync("TestLayout");
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Panels.Count);
        Assert.Contains(loaded.Panels, p => p.Type == "chart" && p.X==10 && p.ZIndex==2 && p.Width==300);
        Assert.Contains(loaded.Panels, p => p.Type == "watchlist" && p.Width==250 && p.ZIndex==1 && p.Height==400);
    }

    [Fact]
    public async Task SaveAs_CreatesNewCopy()
    {
        var svc = CreateService(out var db);
        await db.InitializeDatabaseAsync();
        var model = new WorkspaceLayoutModel { Name = "Base" };
        model.Panels.Add(new LayoutPanelModel { Type = "chart", Width=500 });
        await svc.SaveAsync(model);
        await svc.SaveAsAsync(model, "Clone");

        var list = await svc.ListAsync();
        Assert.Contains(list, l => l.Name == "Base");
        Assert.Contains(list, l => l.Name == "Clone");
    }

    [Fact]
    public async Task SetDefault_Works()
    {
        var svc = CreateService(out var db);
        await db.InitializeDatabaseAsync();
        await svc.SaveAsync(new WorkspaceLayoutModel { Name = "L1" });
        await svc.SaveAsync(new WorkspaceLayoutModel { Name = "L2" });
        Assert.True(await svc.SetDefaultAsync("L2"));
        var list = await svc.ListAsync();
        var def = list.First(l => l.IsDefault);
        Assert.Equal("L2", def.Name);
    }
}
