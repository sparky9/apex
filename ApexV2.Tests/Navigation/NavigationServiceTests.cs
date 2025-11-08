using Xunit;
using FluentAssertions;
using ApexV2.UI.Navigation;
using ApexV2.UI.Layout;
using ApexV2.Core.Database;

namespace ApexV2.Tests.Navigation;

public class NavigationServiceTests
{
    private NavigationService Create(DatabaseService db, out LayoutService layout)
    {
        layout = new LayoutService(db, App.LogManager.GetLogger("LayoutTest"));
        return new NavigationService(layout, App.LogManager.GetLogger("NavTest"), db);
    }

    [Fact]
    public async Task Initializes_WithSeedPages()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".db");
        var db = new DatabaseService(dbPath);
        await db.InitializeDatabaseAsync();
        var svc = Create(db, out var layout);
        await svc.InitializeAsync();
        svc.Pages.Should().NotBeEmpty();
        svc.CurrentPage.Should().NotBeNull();
    }

    [Fact]
    public async Task AddPage_AddsAndPersists()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".db");
        var db = new DatabaseService(dbPath);
        await db.InitializeDatabaseAsync();
        var svc = Create(db, out var layout);
        await svc.InitializeAsync();
        await svc.AddPageAsync("TestOne");
        svc.Pages.Should().Contain(p => p.Name == "TestOne");
    }

    [Fact]
    public async Task RenamePage_ChangesName()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".db");
        var db = new DatabaseService(dbPath);
        await db.InitializeDatabaseAsync();
        var svc = Create(db, out var layout);
        await svc.InitializeAsync();
        await svc.AddPageAsync("OldName");
        svc.RenamePage("OldName", "NewName").Should().BeTrue();
        svc.Pages.Should().Contain(p => p.Name == "NewName");
        svc.Pages.Should().NotContain(p => p.Name == "OldName");
    }

    [Fact]
    public async Task RemovePage_Removes()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".db");
        var db = new DatabaseService(dbPath);
        await db.InitializeDatabaseAsync();
        var svc = Create(db, out var layout);
        await svc.InitializeAsync();
        await svc.AddPageAsync("DeleteMe");
        (await svc.RemovePageAsync("DeleteMe")).Should().BeTrue();
        svc.Pages.Should().NotContain(p => p.Name == "DeleteMe");
    }
}
