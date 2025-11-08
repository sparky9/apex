using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ApexV2.Extensions.Plugins;

namespace ApexV2.Windows
{
    /// <summary>
    /// Plugin Manager Window - Provides a UI for managing APEX plugins
    /// </summary>
    public partial class PluginManagerWindow : Window
    {
        private readonly PluginManager _pluginManager;
        private readonly ObservableCollection<PluginInfoViewModel> _pluginInfos;
        private readonly ObservableCollection<ConfigurationItem> _configurationItems;
        private readonly ObservableCollection<string> _logEntries;
        private PluginInfoViewModel? _selectedPlugin;

        public PluginManagerWindow(PluginManager pluginManager)
        {
            InitializeComponent();
            
            _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            _pluginInfos = new ObservableCollection<PluginInfoViewModel>();
            _configurationItems = new ObservableCollection<ConfigurationItem>();
            _logEntries = new ObservableCollection<string>();

            PluginsDataGrid.ItemsSource = _pluginInfos;
            ConfigurationDataGrid.ItemsSource = _configurationItems;
            LogListBox.ItemsSource = _logEntries;

            Loaded += async (_, __) => await LoadPluginsAsync();
        }

        private async Task LoadPluginsAsync()
        {
            try
            {
                StatusText.Text = "Loading plugins...";
                
                _pluginInfos.Clear();
                var plugins = _pluginManager.GetAllPlugins();
                
                foreach (var plugin in plugins)
                {
                    _pluginInfos.Add(new PluginInfoViewModel(plugin));
                }

                PluginCountText.Text = $"{_pluginInfos.Count} plugins loaded";
                StatusText.Text = "Ready";
                
                AddLogEntry($"Loaded {_pluginInfos.Count} plugins");
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error loading plugins";
                AddLogEntry($"Error loading plugins: {ex.Message}");
                MessageBox.Show($"Error loading plugins: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PluginsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedPlugin = PluginsDataGrid.SelectedItem as PluginInfoViewModel;
            UpdatePluginDetails();
            UpdateButtonStates();
            LoadPluginConfiguration();
        }

        private void UpdatePluginDetails()
        {
            if (_selectedPlugin == null)
            {
                DetailNameText.Text = "";
                DetailVersionText.Text = "";
                DetailAuthorText.Text = "";
                DetailCategoryText.Text = "";
                DetailStateText.Text = "";
                DetailAssemblyText.Text = "";
                DetailPermissionsText.Text = "";
                DetailDescriptionText.Text = "";
                DetailTagsText.Text = "";
                return;
            }

            var plugin = _selectedPlugin.PluginInfo;
            DetailNameText.Text = plugin.Name;
            DetailVersionText.Text = plugin.Version?.ToString() ?? "Unknown";
            DetailAuthorText.Text = plugin.Author;
            DetailCategoryText.Text = plugin.Category;
            DetailStateText.Text = _selectedPlugin.State;
            DetailAssemblyText.Text = plugin.AssemblyPath;
            DetailPermissionsText.Text = plugin.RequiredPermissions.ToString();
            DetailDescriptionText.Text = plugin.Description;
            DetailTagsText.Text = string.Join(", ", plugin.Tags);
        }

        private void UpdateButtonStates()
        {
            var hasSelection = _selectedPlugin != null;
            var canStart = hasSelection && _selectedPlugin.State == "Loaded";
            var canStop = hasSelection && _selectedPlugin.State == "Running";
            var canReload = hasSelection && (_selectedPlugin.State == "Loaded" || _selectedPlugin.State == "Error");
            var canUnload = hasSelection && _selectedPlugin.State != "Unloaded";

            StartPluginButton.IsEnabled = canStart;
            StopPluginButton.IsEnabled = canStop;
            ReloadPluginButton.IsEnabled = canReload;
            UnloadPluginButton.IsEnabled = canUnload;
            SaveConfigButton.IsEnabled = hasSelection;
            ResetConfigButton.IsEnabled = hasSelection;
        }

        private async void LoadPluginConfiguration()
        {
            try
            {
                _configurationItems.Clear();
                
                if (_selectedPlugin == null)
                    return;

                var config = await _pluginManager.GetPluginConfigurationAsync(_selectedPlugin.PluginInfo.Id);
                if (config != null)
                {
                    foreach (var setting in config.Settings)
                    {
                        _configurationItems.Add(new ConfigurationItem
                        {
                            Key = setting.Key,
                            Value = setting.Value?.ToString() ?? ""
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error loading configuration for {_selectedPlugin.Name}: {ex.Message}");
                MessageBox.Show($"Failed to load plugin configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StartPluginButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlugin == null) return;

            try
            {
                StatusText.Text = $"Starting {_selectedPlugin.Name}...";
                var success = await _pluginManager.StartPluginAsync(_selectedPlugin.PluginInfo.Id);
                
                if (success)
                {
                    _selectedPlugin.State = "Running";
                    AddLogEntry($"Started plugin: {_selectedPlugin.Name}");
                    StatusText.Text = "Plugin started successfully";
                }
                else
                {
                    AddLogEntry($"Failed to start plugin: {_selectedPlugin.Name}");
                    StatusText.Text = "Failed to start plugin";
                }
                
                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error starting plugin {_selectedPlugin.Name}: {ex.Message}");
                StatusText.Text = "Error starting plugin";
            }
        }

        private async void StopPluginButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlugin == null) return;

            try
            {
                StatusText.Text = $"Stopping {_selectedPlugin.Name}...";
                var success = await _pluginManager.StopPluginAsync(_selectedPlugin.PluginInfo.Id);
                
                if (success)
                {
                    _selectedPlugin.State = "Loaded";
                    AddLogEntry($"Stopped plugin: {_selectedPlugin.Name}");
                    StatusText.Text = "Plugin stopped successfully";
                }
                else
                {
                    AddLogEntry($"Failed to stop plugin: {_selectedPlugin.Name}");
                    StatusText.Text = "Failed to stop plugin";
                }
                
                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error stopping plugin {_selectedPlugin.Name}: {ex.Message}");
                StatusText.Text = "Error stopping plugin";
            }
        }

        private async void ReloadPluginButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlugin == null) return;

            try
            {
                StatusText.Text = $"Reloading {_selectedPlugin.Name}...";
                var success = await _pluginManager.ReloadPluginAsync(_selectedPlugin.PluginInfo.Id);
                
                if (success)
                {
                    _selectedPlugin.State = "Loaded";
                    AddLogEntry($"Reloaded plugin: {_selectedPlugin.Name}");
                    StatusText.Text = "Plugin reloaded successfully";
                }
                else
                {
                    AddLogEntry($"Failed to reload plugin: {_selectedPlugin.Name}");
                    StatusText.Text = "Failed to reload plugin";
                }
                
                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error reloading plugin {_selectedPlugin.Name}: {ex.Message}");
                StatusText.Text = "Error reloading plugin";
            }
        }

        private async void UnloadPluginButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlugin == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to unload the plugin '{_selectedPlugin.Name}'?",
                "Confirm Unload",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                StatusText.Text = $"Unloading {_selectedPlugin.Name}...";
                var success = await _pluginManager.UnloadPluginAsync(_selectedPlugin.PluginInfo.Id);
                
                if (success)
                {
                    _selectedPlugin.State = "Unloaded";
                    AddLogEntry($"Unloaded plugin: {_selectedPlugin.Name}");
                    StatusText.Text = "Plugin unloaded successfully";
                }
                else
                {
                    AddLogEntry($"Failed to unload plugin: {_selectedPlugin.Name}");
                    StatusText.Text = "Failed to unload plugin";
                }
                
                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error unloading plugin {_selectedPlugin.Name}: {ex.Message}");
                StatusText.Text = "Error unloading plugin";
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadPluginsAsync();
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error refreshing plugins: {ex.Message}");
                StatusText.Text = "Error refreshing plugins";
                MessageBox.Show($"Error refreshing plugins: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadPluginButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Plugin Assembly",
                Filter = "Plugin Files (*.dll)|*.dll|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                StatusText.Text = "Loading plugin...";
                
                // For now, just refresh the list - in a real implementation,
                // we would copy the plugin to the plugins directory and load it
                MessageBox.Show(
                    "Plugin loading from external files is not yet implemented.\n" +
                    "Please copy plugin files to the plugins directory and restart the application.",
                    "Not Implemented",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                
                StatusText.Text = "Ready";
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error loading plugin: {ex.Message}");
                StatusText.Text = "Error loading plugin";
                MessageBox.Show($"Error loading plugin: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InstallPluginButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Plugin installation from packages is not yet implemented.\n" +
                "This feature will be available in a future version.",
                "Not Implemented",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void SaveConfigButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlugin == null) return;

            try
            {
                StatusText.Text = "Saving configuration...";
                
                var settings = new Dictionary<string, object>();
                foreach (var item in _configurationItems)
                {
                    if (!string.IsNullOrWhiteSpace(item.Key))
                    {
                        settings[item.Key] = item.Value ?? "";
                    }
                }

                var configuration = new PluginConfiguration
                {
                    PluginId = _selectedPlugin.PluginInfo.Id,
                    IsEnabled = _selectedPlugin.IsEnabled,
                    Settings = settings,
                    LastUpdated = DateTime.UtcNow
                };

                await _pluginManager.UpdatePluginConfigurationAsync(_selectedPlugin.PluginInfo.Id, configuration);
                
                AddLogEntry($"Saved configuration for {_selectedPlugin.Name}");
                StatusText.Text = "Configuration saved successfully";
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error saving configuration: {ex.Message}");
                StatusText.Text = "Error saving configuration";
                MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ResetConfigButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlugin == null) return;

            var result = MessageBox.Show(
                "Are you sure you want to reset the configuration to defaults?",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                StatusText.Text = "Resetting configuration...";
                
                await _pluginManager.ResetPluginConfigurationAsync(_selectedPlugin.PluginInfo.Id);
                LoadPluginConfiguration();
                
                AddLogEntry($"Reset configuration for {_selectedPlugin.Name}");
                StatusText.Text = "Configuration reset successfully";
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error resetting configuration: {ex.Message}");
                StatusText.Text = "Error resetting configuration";
                MessageBox.Show($"Error resetting configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            _logEntries.Clear();
            AddLogEntry("Log cleared");
        }

        private void ExportLogButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export Log",
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"plugin-manager-log-{DateTime.Now:yyyy-MM-dd-HHmmss}.txt"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                File.WriteAllLines(dialog.FileName, _logEntries);
                AddLogEntry($"Log exported to {dialog.FileName}");
                StatusText.Text = "Log exported successfully";
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error exporting log: {ex.Message}");
                MessageBox.Show($"Error exporting log: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Plugin system settings are not yet implemented.\n" +
                "This feature will be available in a future version.",
                "Not Implemented",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AddLogEntry(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _logEntries.Add($"[{timestamp}] {message}");
            
            // Keep only the last 1000 entries
            while (_logEntries.Count > 1000)
            {
                _logEntries.RemoveAt(0);
            }
            
            // Auto-scroll to bottom
            if (LogListBox.Items.Count > 0)
            {
                LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
            }
        }
    }

    // View model for plugin information
    public class PluginInfoViewModel : INotifyPropertyChanged
    {
        public PluginInstance PluginInstance { get; }
        public PluginInfo PluginInfo => PluginInstance.Info;
        
        private string _state;

        public PluginInfoViewModel(PluginInstance pluginInstance)
        {
            PluginInstance = pluginInstance ?? throw new ArgumentNullException(nameof(pluginInstance));
            _state = pluginInstance.Status.ToString();
        }

        public string Name => PluginInfo.Name;
        public string Version => PluginInfo.Version?.ToString() ?? "Unknown";
        public string Author => PluginInfo.Author;
        public string Category => PluginInfo.Category;
        public string Description => PluginInfo.Description;
        public bool IsEnabled
        {
            get => PluginInfo.IsEnabled;
            set
            {
                if (PluginInfo.IsEnabled != value)
                {
                    PluginInfo.IsEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public string State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    // Update the plugin instance status if possible
                    if (Enum.TryParse<PluginStatus>(value, out var status))
                    {
                        PluginInstance.Status = status;
                    }
                    OnPropertyChanged(nameof(State));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Configuration item for the DataGrid
    public class ConfigurationItem : INotifyPropertyChanged
    {
        private string _key = string.Empty;
        private string _value = string.Empty;

        public string Key
        {
            get => _key;
            set
            {
                if (_key != value)
                {
                    _key = value;
                    OnPropertyChanged(nameof(Key));
                }
            }
        }

        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
