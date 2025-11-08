using System.Windows;
using System.Windows.Input;

namespace ApexV2.UI.ContextMenus;

/// <summary>
/// Static helper methods for context menu functionality
/// </summary>
public static class ContextMenuHelper
{
    /// <summary>
    /// Adds a context menu to a FrameworkElement with proper event handling
    /// </summary>
    public static void AttachContextMenu(FrameworkElement element, System.Windows.Controls.ContextMenu contextMenu)
    {
        element.ContextMenu = contextMenu;
        
        // Ensure context menu opens on right-click
        element.MouseRightButtonUp += (sender, e) =>
        {
            if (sender is FrameworkElement fe && fe.ContextMenu != null)
            {
                fe.ContextMenu.PlacementTarget = fe;
                fe.ContextMenu.IsOpen = true;
                e.Handled = true;
            }
        };
    }

    /// <summary>
    /// Creates a styled separator for context menus
    /// </summary>
    public static System.Windows.Controls.Separator CreateSeparator()
    {
        return new System.Windows.Controls.Separator
        {
            Margin = new Thickness(0, 2, 0, 2)
        };
    }

    /// <summary>
    /// Creates a menu item with icon support
    /// </summary>
    public static System.Windows.Controls.MenuItem CreateMenuItem(string header, string? iconPath = null, ICommand? command = null, object? commandParameter = null)
    {
        var menuItem = new System.Windows.Controls.MenuItem
        {
            Header = header,
            Command = command,
            CommandParameter = commandParameter
        };

        if (!string.IsNullOrEmpty(iconPath))
        {
            try
            {
                var image = new System.Windows.Controls.Image
                {
                    Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath, UriKind.RelativeOrAbsolute)),
                    Width = 16,
                    Height = 16
                };
                menuItem.Icon = image;
            }
            catch
            {
                // Ignore icon loading errors
            }
        }

        return menuItem;
    }

    /// <summary>
    /// Shows a context menu at the current mouse position
    /// </summary>
    public static void ShowContextMenuAtMouse(System.Windows.Controls.ContextMenu contextMenu, FrameworkElement relativeTo)
    {
        var position = Mouse.GetPosition(relativeTo);
        contextMenu.PlacementTarget = relativeTo;
        contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        contextMenu.IsOpen = true;
    }

    /// <summary>
    /// Adds keyboard shortcut text to menu item headers
    /// </summary>
    public static string AddShortcutText(string header, string shortcut)
    {
        return $"{header}\t{shortcut}";
    }
}
