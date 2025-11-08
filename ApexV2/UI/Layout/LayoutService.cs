using System.Text.Json;
using ApexV2.Core.Database;
using ApexV2.Core.Logging;

namespace ApexV2.UI.Layout;

public record WorkspaceLayoutSummary(string Name, bool IsDefault, DateTime LastModified);

public class LayoutService
{
    private readonly DatabaseService _databaseService;
    private readonly Logger _logger;

    public LayoutService(DatabaseService databaseService, Logger logger)
    {
        _databaseService = databaseService;
        _logger = logger;
    }

    public async Task<WorkspaceLayoutModel> LoadDefaultAsync()
    {
        try
        {
            using var ctx = new ApexDbContext(_databaseService.GetDbContextOptions());
            var entity = ctx.WorkspaceLayouts.FirstOrDefault(l => l.IsDefault) ?? ctx.WorkspaceLayouts.FirstOrDefault();
            if (entity == null)
            {
                _logger.Warn("No layout found; creating empty workspace");
                return new WorkspaceLayoutModel();
            }
            return SafeDeserialize(entity.LayoutData, entity.Name);
        }
        catch (Exception ex)
        {
            _logger.Error("LoadDefaultAsync failed", ex);
            return new WorkspaceLayoutModel();
        }
    }

    public async Task<WorkspaceLayoutModel?> LoadByNameAsync(string name)
    {
        try
        {
            using var ctx = new ApexDbContext(_databaseService.GetDbContextOptions());
            var entity = ctx.WorkspaceLayouts.FirstOrDefault(l => l.Name == name);
            return entity == null ? null : SafeDeserialize(entity.LayoutData, entity.Name);
        }
        catch (Exception ex)
        {
            _logger.Error($"LoadByNameAsync failed for {name}", ex);
            return null;
        }
    }

    public async Task<IReadOnlyList<WorkspaceLayoutSummary>> ListAsync()
    {
        try
        {
            using var ctx = new ApexDbContext(_databaseService.GetDbContextOptions());
            return ctx.WorkspaceLayouts
                .OrderByDescending(l => l.IsDefault)
                .ThenBy(l => l.Name)
                .Select(l => new WorkspaceLayoutSummary(l.Name, l.IsDefault, l.LastModified))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.Error("ListAsync failed", ex);
            return Array.Empty<WorkspaceLayoutSummary>();
        }
    }

    public async Task<bool> SaveAsync(WorkspaceLayoutModel model, string? name = null)
    {
        try
        {
            var targetName = name ?? model.Name;
            using var ctx = new ApexDbContext(_databaseService.GetDbContextOptions());
            var entity = ctx.WorkspaceLayouts.FirstOrDefault(l => l.Name == targetName);
            if (entity == null)
            {
                entity = new WorkspaceLayoutEntity
                {
                    Name = targetName,
                    Created = DateTime.Now,
                    LastModified = DateTime.Now,
                    IsDefault = false,
                    LayoutData = model.ToJson()
                };
                ctx.WorkspaceLayouts.Add(entity);
            }
            else
            {
                entity.LayoutData = model.ToJson();
                entity.LastModified = DateTime.Now;
            }
            model.Name = targetName; // ensure model updated
            await ctx.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("SaveAsync failed", ex);
            return false;
        }
    }

    public async Task<bool> SaveAsAsync(WorkspaceLayoutModel model, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return false;
        model.Name = newName.Trim();
        return await SaveAsync(model, model.Name);
    }

    public async Task<bool> SetDefaultAsync(string name)
    {
        try
        {
            using var ctx = new ApexDbContext(_databaseService.GetDbContextOptions());
            var target = ctx.WorkspaceLayouts.FirstOrDefault(l => l.Name == name);
            if (target == null) return false;
            foreach (var l in ctx.WorkspaceLayouts) l.IsDefault = false;
            target.IsDefault = true;
            await ctx.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("SetDefaultAsync failed", ex);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(string name)
    {
        try
        {
            using var ctx = new ApexDbContext(_databaseService.GetDbContextOptions());
            var entity = ctx.WorkspaceLayouts.FirstOrDefault(l => l.Name == name);
            if (entity == null) return false;
            ctx.WorkspaceLayouts.Remove(entity);
            await ctx.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("DeleteAsync failed", ex);
            return false;
        }
    }

    private WorkspaceLayoutModel SafeDeserialize(string json, string name)
    {
        try
        {
            var model = WorkspaceLayoutModel.FromJson(json);
            model.Name = name;
            return model;
        }
        catch (Exception ex)
        {
            _logger.Error("SafeDeserialize failed - returning empty layout", ex);
            return new WorkspaceLayoutModel { Name = name };
        }
    }
}
