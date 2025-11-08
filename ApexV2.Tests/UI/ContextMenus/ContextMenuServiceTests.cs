using Xunit;
using ApexV2.Core.Logging;

namespace ApexV2.Tests.UI.ContextMenus;

public class ContextMenuServiceTests
{
    private readonly ApexV2.UI.ContextMenus.ContextMenuService _service;

    public ContextMenuServiceTests()
    {
        var logManager = new LogManager();
        var logger = logManager.GetLogger("Test");
        _service = new ApexV2.UI.ContextMenus.ContextMenuService(logger);
    }

    [Fact]
    public void ContextMenuService_ShouldInitialize()
    {
        // Arrange & Act
        var logManager = new LogManager();
        var logger = logManager.GetLogger("Test");
        var service = new ApexV2.UI.ContextMenus.ContextMenuService(logger);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void ContextMenuService_CreateNavigationTabMenu_ShouldNotThrow()
    {
        // Arrange
        var pageName = "TestPage";
        var renameCallCount = 0;
        var closeCallCount = 0;
        var duplicateCallCount = 0;

        // Act & Assert - Just verify no exceptions are thrown
        var exception = Record.Exception(() => 
            _service.CreateNavigationTabMenu(
                pageName,
                _ => renameCallCount++,
                _ => closeCallCount++,
                _ => duplicateCallCount++
            )
        );

        Assert.Null(exception);
    }

    [Fact]
    public void ContextMenuService_CreatePanelMenu_ShouldNotThrow()
    {
        // Arrange
        var panelId = "TestPanel";
        var panelType = "Chart";
        var configureCallCount = 0;
        var bringToFrontCallCount = 0;
        var closeCallCount = 0;

        // Act & Assert - Just verify no exceptions are thrown
        var exception = Record.Exception(() => 
            _service.CreatePanelMenu(
                panelId,
                panelType,
                _ => closeCallCount++,
                _ => configureCallCount++,
                _ => bringToFrontCallCount++
            )
        );

        Assert.Null(exception);
    }

    [Fact]
    public void ContextMenuService_CreateWorkspaceMenu_ShouldNotThrow()
    {
        // Arrange
        var addChartCallCount = 0;
        var addWatchlistCallCount = 0;
        var addNewsCallCount = 0;
        var pasteLayoutCallCount = 0;

        // Act & Assert - Just verify no exceptions are thrown
        var exception = Record.Exception(() => 
            _service.CreateWorkspaceMenu(
                () => addChartCallCount++,
                () => addWatchlistCallCount++,
                () => addNewsCallCount++,
                () => pasteLayoutCallCount++
            )
        );

        Assert.Null(exception);
    }
}
