#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;

namespace ApexV2.Extensions.API
{
    public partial class ApiManagerWindow : Window
    {
        private readonly ApiManager _apiManager;
        private readonly ILogger<ApiManagerWindow> _logger;

        public ApiManagerWindow()
        {
            try
            {
                InitializeComponent();
                _logger = new TestLogger<ApiManagerWindow>();
                var apiManagerLogger = new ApiLogger<ApiManager>(_logger);
                _apiManager = new ApiManager(apiManagerLogger);
                Loaded += OnWindowLoaded;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing API Manager Window: {ex.Message}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _apiManager.InitializeAsync();
                await RefreshApiKeys();
                await RefreshServices();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading API Manager Window");
                MessageBox.Show($"Error loading API Manager: {ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RefreshApiKeys()
        {
            try
            {
                var keyService = _apiManager.GetApiKeyService();
                if (keyService != null)
                {
                    var keys = await keyService.GetApiKeysAsync();
                    ApiKeysDataGrid.ItemsSource = keys;
                }
                else
                {
                    ApiKeysDataGrid.ItemsSource = new List<ApiKeyResponse>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing API keys");
                MessageBox.Show($"Error refreshing API keys: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RefreshServices()
        {
            try
            {
                var services = _apiManager.GetAllServices();
                ServicesListBox.ItemsSource = services.Select(kvp => new
                {
                    Name = kvp.Key,
                    Type = kvp.Value.GetType().Name,
                    Status = "Active"
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing services");
                MessageBox.Show($"Error refreshing services: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CreateApiKeyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new CreateApiKeyDialog();
                if (dialog.ShowDialog() == true)
                {
                    var keyService = _apiManager.GetApiKeyService();
                    if (keyService != null)
                    {
                        var request = new ApiKeyRequest
                        {
                            Name = dialog.KeyName,
                            Description = dialog.KeyDescription,
                            Permissions = dialog.SelectedPermissions,
                            ExpiresAt = dialog.ExpirationDate
                        };

                        var apiKey = await keyService.CreateApiKeyAsync(request);
                        await RefreshApiKeys();

                        MessageBox.Show($"API Key created successfully!\n\nKey: {apiKey.Key}\n\nPlease save this key securely. It will not be shown again.", 
                            "API Key Created", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating API key");
                MessageBox.Show($"Error creating API key: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RevokeApiKeyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedKey = ApiKeysDataGrid.SelectedItem as ApiKeyResponse;
                if (selectedKey != null)
                {
                    var result = MessageBox.Show($"Are you sure you want to revoke the API key '{selectedKey.Name}'?\n\nThis action cannot be undone.", 
                        "Confirm Revocation", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        var keyService = _apiManager.GetApiKeyService();
                        if (keyService != null)
                        {
                            await keyService.RevokeApiKeyAsync(selectedKey.Id);
                            await RefreshApiKeys();
                            MessageBox.Show("API key revoked successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Please select an API key to revoke.", "No Selection", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking API key");
                MessageBox.Show($"Error revoking API key: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshApiKeys();
            await RefreshServices();
        }

        private async void TestApiButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedKey = ApiKeysDataGrid.SelectedItem as ApiKeyResponse;
                if (selectedKey != null)
                {
                    var isValid = await _apiManager.ValidateApiKeyAsync(selectedKey.Key);
                    var message = isValid ? "API key is valid and active." : "API key is invalid or inactive.";
                    var icon = isValid ? MessageBoxImage.Information : MessageBoxImage.Warning;
                    
                    MessageBox.Show(message, "API Key Test", MessageBoxButton.OK, icon);
                }
                else
                {
                    MessageBox.Show("Please select an API key to test.", "No Selection", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing API key");
                MessageBox.Show($"Error testing API key: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void HealthCheckButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Create a logger for ApiHealthCheck
                var healthLogger = new ApiLogger<ApiHealthCheck>(_logger);
                var healthCheck = new ApiHealthCheck(_apiManager, healthLogger);
                var health = await healthCheck.CheckHealthAsync();
                
                var status = health["Status"].ToString();
                var message = $"API Health Status: {status}\n\nTimestamp: {health["Timestamp"]}";
                
                if (health.ContainsKey("Errors"))
                {
                    var errors = health["Errors"] as List<string>;
                    if (errors.Count > 0)
                    {
                        message += $"\n\nErrors:\n{string.Join("\n", errors)}";
                    }
                }
                
                var icon = status == "Healthy" ? MessageBoxImage.Information : MessageBoxImage.Warning;
                MessageBox.Show(message, "Health Check", MessageBoxButton.OK, icon);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing health check");
                MessageBox.Show($"Error performing health check: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public partial class CreateApiKeyDialog : Window
    {
        public string KeyName { get; private set; }
        public string KeyDescription { get; private set; }
        public List<string> SelectedPermissions { get; private set; }
        public DateTime? ExpirationDate { get; private set; }

        public CreateApiKeyDialog()
        {
            InitializeComponent();
            LoadPermissions();
            ExpirationDatePicker.SelectedDate = DateTime.UtcNow.AddMonths(6);
        }

        private void LoadPermissions()
        {
            var permissions = new List<string>
            {
                "*", // All permissions
                "marketData:read",
                "marketData:write",
                "watchlist:read",
                "watchlist:write",
                "portfolio:read",
                "scanner:read",
                "scanner:write",
                "indicator:read",
                "indicator:write",
                "news:read",
                "alert:read",
                "alert:write"
            };

            PermissionsListBox.ItemsSource = permissions;
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("Please enter a name for the API key.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            KeyName = NameTextBox.Text;
            KeyDescription = DescriptionTextBox.Text;
            ExpirationDate = ExpirationDatePicker.SelectedDate;

            SelectedPermissions = new List<string>();
            foreach (var item in PermissionsListBox.SelectedItems)
            {
                SelectedPermissions.Add(item.ToString());
            }

            if (SelectedPermissions.Count == 0)
            {
                SelectedPermissions.Add("marketData:read"); // Default permission
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
