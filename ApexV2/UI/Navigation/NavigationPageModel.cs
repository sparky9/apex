namespace ApexV2.UI.Navigation;

using ApexV2.UI.Layout;

public class NavigationPageModel
{
    public string Name { get; set; } = string.Empty;
    public string LayoutName { get; set; } = string.Empty; // persisted workspace layout name
    public WorkspaceLayoutModel? CachedLayout { get; set; } // optional in-memory cache
}
