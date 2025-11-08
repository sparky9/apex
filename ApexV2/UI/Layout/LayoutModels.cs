using System.Collections.ObjectModel;
using System.Text.Json;

namespace ApexV2.UI.Layout;

// Core in-memory layout model (panels + positions) for v1 foundation
public class WorkspaceLayoutModel
{
    public ObservableCollection<LayoutPanelModel> Panels { get; } = new();
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Workspace";

    public string ToJson()
        => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = false });

    public static WorkspaceLayoutModel FromJson(string json)
        => JsonSerializer.Deserialize<WorkspaceLayoutModel>(json) ?? new WorkspaceLayoutModel();
}

public class LayoutPanelModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = "chart"; // chart, watchlist, news, fundamentals, etc.
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 400;
    public double Height { get; set; } = 300;
    public bool IsFloating { get; set; }
    public int ZIndex { get; set; }
}
