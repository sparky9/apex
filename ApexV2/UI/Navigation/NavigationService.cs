using ApexV2.UI.Layout;
using ApexV2.Core.Database;
using ApexV2.Core.Logging;
using Microsoft.EntityFrameworkCore;

namespace ApexV2.UI.Navigation;

public class NavigationService
{
    private readonly LayoutService _layoutService;
    private readonly Logger _logger;
    private readonly List<NavigationPageModel> _pages = new();
    public IReadOnlyList<NavigationPageModel> Pages => _pages;
    public NavigationPageModel? CurrentPage { get; private set; }

    public event EventHandler? CurrentPageChanged;
    public event EventHandler? PagesChanged; // fired on add/remove/rename

    private readonly DatabaseService _databaseService;

    public NavigationService(LayoutService layoutService, Logger logger, DatabaseService? databaseService = null)
    {
        _layoutService = layoutService;
        _logger = logger;
        _databaseService = databaseService ?? new DatabaseService();
    }

    public async Task InitializeAsync()
    {
        try
        {
            using var ctx = new ApexDbContext(_databaseService.GetDbContextOptions());
            var entities = ctx.NavigationPages.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToList();
            if (entities.Count == 0)
            {
                // seed two starter pages
                await AddPageAsync("Dashboard");
                await AddPageAsync("Analysis");
            }
            else
            {
                foreach (var e in entities)
                {
                    _pages.Add(new NavigationPageModel { Name = e.Name, LayoutName = e.LayoutName });
                }
                if (_pages.Count > 0) await SetCurrentPageAsync(_pages[0].Name);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Navigation Initialize failed", ex);
        }
    }

    public async Task<NavigationPageModel> AddPageAsync(string name, string? cloneFromLayout = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name required");
        if (_pages.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Page name already exists");

        WorkspaceLayoutModel layout;
        if (!string.IsNullOrWhiteSpace(cloneFromLayout))
        {
            layout = await _layoutService.LoadByNameAsync(cloneFromLayout) ?? new WorkspaceLayoutModel { Name = name + "_Layout" };
            layout.Name = name + "_Layout";
        }
        else
        {
            layout = new WorkspaceLayoutModel { Name = name + "_Layout" };
        }
        if (!layout.Panels.Any())
        {
            layout.Panels.Add(new LayoutPanelModel { Type = "chart", X = 50, Y = 50, Width = 800, Height = 600 });
        }
        await _layoutService.SaveAsync(layout);
        var model = new NavigationPageModel { Name = name, LayoutName = layout.Name, CachedLayout = layout };
        _pages.Add(model);
        await PersistPagesAsync();
        PagesChanged?.Invoke(this, EventArgs.Empty);
        if (CurrentPage == null) await SetCurrentPageAsync(model.Name);
        return model;
    }

    public async Task<bool> RemovePageAsync(string name)
    {
        var page = _pages.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (page == null) return false;
        _pages.Remove(page);
        await PersistPagesAsync();
        PagesChanged?.Invoke(this, EventArgs.Empty);
        if (CurrentPage == page)
        {
            CurrentPage = _pages.FirstOrDefault();
            CurrentPageChanged?.Invoke(this, EventArgs.Empty);
        }
        return true;
    }

    public async Task<bool> SetCurrentPageAsync(string name)
    {
        var page = _pages.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (page == null) return false;
        if (page.CachedLayout == null)
        {
            page.CachedLayout = await _layoutService.LoadByNameAsync(page.LayoutName) ?? new WorkspaceLayoutModel { Name = page.LayoutName };
        }
        CurrentPage = page;
        CurrentPageChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool RenamePage(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName)) return false;
        var page = _pages.FirstOrDefault(p => string.Equals(p.Name, oldName, StringComparison.OrdinalIgnoreCase));
        if (page == null) return false;
        if (_pages.Any(p => string.Equals(p.Name, newName, StringComparison.OrdinalIgnoreCase))) return false; // uniqueness
        page.Name = newName.Trim();
        _ = PersistPagesAsync();
        PagesChanged?.Invoke(this, EventArgs.Empty);
        if (CurrentPage == page) CurrentPageChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private async Task PersistPagesAsync()
    {
        try
        {
            using var ctx = new ApexDbContext(_databaseService.GetDbContextOptions());
            var existing = ctx.NavigationPages.ToList();
            // remove deleted
            foreach (var e in existing)
            {
                if (!_pages.Any(p => p.Name == e.Name)) ctx.NavigationPages.Remove(e);
            }
            // upsert current list
            int sort = 0;
            foreach (var p in _pages)
            {
                var entity = existing.FirstOrDefault(e => e.Name == p.Name);
                if (entity == null)
                {
                    ctx.NavigationPages.Add(new NavigationPageEntity
                    {
                        Name = p.Name,
                        LayoutName = p.LayoutName,
                        SortOrder = sort++,
                        CreatedUtc = DateTime.UtcNow,
                        UpdatedUtc = DateTime.UtcNow
                    });
                }
                else
                {
                    entity.SortOrder = sort++;
                    entity.LayoutName = p.LayoutName;
                    entity.UpdatedUtc = DateTime.UtcNow;
                }
            }
            await ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("PersistPages failed", ex);
        }
    }
}
