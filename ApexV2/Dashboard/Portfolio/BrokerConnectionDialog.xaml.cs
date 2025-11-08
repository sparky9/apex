#nullable disable
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ApexV2.Dashboard.Portfolio
{
    /// <summary>
    /// Broker connection dialog for configuring broker API connections
    /// </summary>
    public partial class BrokerConnectionDialog : Window
    {
        private readonly HttpClient _httpClient;
        
        public BrokerConfig BrokerConfig { get; private set; }
        public string SelectedBrokerName { get; private set; }
        
        public BrokerConnectionDialog()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
            
            // Subscribe to text changed events to enable/disable connect button
            AlpacaApiKeyTextBox.TextChanged += ValidateInput;
            AlpacaSecretKeyPasswordBox.PasswordChanged += ValidateInput;
        }
        
        #region Event Handlers
        
        private void BrokerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = BrokerComboBox.SelectedItem as ComboBoxItem;
            var brokerTag = selectedItem?.Tag?.ToString();
            
            // Hide all panels
            AlpacaPanel.Visibility = Visibility.Collapsed;
            IBKRPanel.Visibility = Visibility.Collapsed;
            TDAmeritradePanel.Visibility = Visibility.Collapsed;
            TradeStationPanel.Visibility = Visibility.Collapsed;
            
            // Show selected panel
            switch (brokerTag)
            {
                case "Alpaca":
                    AlpacaPanel.Visibility = Visibility.Visible;
                    break;
                case "IBKR":
                    IBKRPanel.Visibility = Visibility.Visible;
                    break;
                case "TDAmeritrade":
                    TDAmeritradePanel.Visibility = Visibility.Visible;
                    break;
                case "TradeStation":
                    TradeStationPanel.Visibility = Visibility.Visible;
                    break;
            }
            
            ValidateInput(null, null);
        }
        
        private void ValidateInput(object sender, EventArgs e)
        {
            var selectedItem = BrokerComboBox.SelectedItem as ComboBoxItem;
            var brokerTag = selectedItem?.Tag?.ToString();
            
            bool isValid = false;
            
            switch (brokerTag)
            {
                case "Alpaca":
                    isValid = !string.IsNullOrWhiteSpace(AlpacaApiKeyTextBox.Text) &&
                             !string.IsNullOrWhiteSpace(AlpacaSecretKeyPasswordBox.Password);
                    break;
                // Add validation for other brokers when implemented
            }
            
            ConnectButton.IsEnabled = isValid;
        }
        
        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = BrokerComboBox.SelectedItem as ComboBoxItem;
            var brokerTag = selectedItem?.Tag?.ToString();
            
            if (brokerTag != "Alpaca")
            {
                ShowTestResult("Test connection not available for this broker", false);
                return;
            }
            
            var apiKey = AlpacaApiKeyTextBox.Text?.Trim();
            var secretKey = AlpacaSecretKeyPasswordBox.Password?.Trim();
            
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secretKey))
            {
                ShowTestResult("Please enter both API Key and Secret Key", false);
                return;
            }
            
            TestConnectionButton.IsEnabled = false;
            ShowTestResult("Testing connection...", null);
            
            try
            {
                var environment = ((ComboBoxItem)AlpacaEnvironmentComboBox.SelectedItem).Tag.ToString();
                var baseUrl = environment == "live" 
                    ? "https://api.alpaca.markets" 
                    : "https://paper-api.alpaca.markets";
                
                var success = await TestAlpacaConnection(baseUrl, apiKey, secretKey);
                ShowTestResult(success ? "Connection successful!" : "Connection failed", success);
            }
            catch (Exception ex)
            {
                ShowTestResult($"Connection error: {ex.Message}", false);
            }
            finally
            {
                TestConnectionButton.IsEnabled = true;
            }
        }
        
        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = BrokerComboBox.SelectedItem as ComboBoxItem;
            var brokerTag = selectedItem?.Tag?.ToString();
            
            if (brokerTag == "Alpaca")
            {
                var environment = ((ComboBoxItem)AlpacaEnvironmentComboBox.SelectedItem).Tag.ToString();
                var isPaper = environment == "paper";
                
                BrokerConfig = new BrokerConfig
                {
                    BrokerName = "Alpaca",
                    ApiKey = AlpacaApiKeyTextBox.Text?.Trim(),
                    SecretKey = AlpacaSecretKeyPasswordBox.Password?.Trim(),
                    Environment = environment,
                    BaseUrl = isPaper ? "https://paper-api.alpaca.markets" : "https://api.alpaca.markets",
                    AdditionalSettings = new Dictionary<string, string>
                    {
                        ["IsPaper"] = isPaper.ToString(),
                        ["SaveCredentials"] = SaveCredentialsCheckBox.IsChecked.ToString()
                    }
                };
                
                SelectedBrokerName = "Alpaca";
            }
            
            if (BrokerConfig != null)
            {
                DialogResult = true;
                Close();
            }
        }
        
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        #endregion
        
        #region Helper Methods
        
        private async Task<bool> TestAlpacaConnection(string baseUrl, string apiKey, string secretKey)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/v2/account");
                
                // Add Alpaca authentication headers
                var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{secretKey}"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
                
                // Add additional headers that Alpaca might require
                request.Headers.Add("APCA-API-KEY-ID", apiKey);
                request.Headers.Add("APCA-API-SECRET-KEY", secretKey);
                
                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return !string.IsNullOrWhiteSpace(content) && content.Contains("id");
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        private void ShowTestResult(string message, bool? isSuccess)
        {
            TestResultTextBlock.Text = message;
            TestResultTextBlock.Visibility = Visibility.Visible;
            
            if (isSuccess.HasValue)
            {
                TestResultTextBlock.Foreground = isSuccess.Value 
                    ? new SolidColorBrush(Color.FromRgb(78, 201, 176))  // Green
                    : new SolidColorBrush(Color.FromRgb(241, 76, 76));   // Red
            }
            else
            {
                TestResultTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)); // Gray
            }
        }
        
        #endregion
        
        #region Cleanup
        
        protected override void OnClosed(EventArgs e)
        {
            _httpClient?.Dispose();
            base.OnClosed(e);
        }
        
        #endregion
    }
}
