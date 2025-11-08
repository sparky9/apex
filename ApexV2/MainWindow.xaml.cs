using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using ApexV2.Core.Database;
using ApexV2.Core; // For ServiceLocator
using ApexV2.Charts.Export; // For IChartLogger and LoggerAdapter
using ApexV2.Data.MarketData;
using ApexV2.Data.Trading;
using ApexV2.Core.Config; // ensure settings
using ApexV2.Data.Fundamentals; // added for fundamentals types
using ApexV2.UI.Layout;
using ApexV2.UI.ContextMenus;
using ApexV2.Extensions.Plugins;
using ApexV2.Extensions.API;
using ApexV2.Core.Validation; // Added for ValidationManager
using ApexV2.Validation; // Added for validation classes
using ApexV2.Diagnostics; // Added for UI diagnostics
using Microsoft.Extensions.Logging;

namespace ApexV2;

/// <summary>
/// Main window for APEX V2 - Professional Trading Analysis Platform
/// </summary>
public partial class MainWindow : Window
{
    private readonly DatabaseService _databaseService;
    private readonly DispatcherTimer _clockTimer;
    private IMarketDataProvider? _currentMarketDataProvider;
    private ITradingProvider? _currentTradingProvider;
    private bool _isPaperTradingMode = true; // re-added and now used for mode checks
    private readonly ApexV2.Core.Logging.Logger _logger = App.LogManager.GetLogger("MainWindow");
    private UserEntity? _currentUser; // added
    private ApexV2.Core.Database.UserProfileEntity? _currentUserProfile; // added
    private ApexV2.Data.MarketData.Engine.MarketDataEngine? _marketDataEngine; // added
    private SettingsService _settingsService; // added
    private SettingsModel? _settings; // added
    private FundamentalDataEngine? _fundamentalEngine; // added
    private LayoutService? _layoutService;
    private bool _layoutDirty = false;
    private MenuItem? _layoutsDynamicMenu; // for dynamic list
    private UI.Navigation.NavigationService? _navigationService; // added navigation
    private UI.ContextMenus.ContextMenuService? _contextMenuService;
    private MainWindowContextMenuHandler? _contextMenuHandler;
    private PluginManager? _pluginManager;
    private ApiManager? _apiManager;
    private ValidationManager? _validationManager; // Added validation manager

    public MainWindow()
    {
        InitializeComponent();
        _logger.Info("MainWindow initializing");
        
        _databaseService = new DatabaseService();
        _settingsService = new SettingsService(_databaseService);
        
        // Initialize database FIRST before authentication
        _ = InitializeDatabaseThenAuthentication();
        
        // Initialize clock timer
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += ClockTimer_Tick;
        _clockTimer.Start();
        
        // Initialize application
        _ = InitializeApplicationAsync();

        _layoutService = new LayoutService(new Core.Database.DatabaseService(), App.LogManager.GetLogger("LayoutService"));
        _navigationService = new UI.Navigation.NavigationService(_layoutService!, App.LogManager.GetLogger("Navigation"), _databaseService);
        _navigationService.CurrentPageChanged += Navigation_CurrentPageChanged;
        _navigationService.PagesChanged += (_, __) => Dispatcher.Invoke(SyncNavigationTabs);
        
        // Initialize context menu system
        _contextMenuService = new UI.ContextMenus.ContextMenuService(App.LogManager.GetLogger("ContextMenus"));
        _contextMenuHandler = new MainWindowContextMenuHandler(App.LogManager.GetLogger("ContextMenus"), _navigationService, WorkspaceHost);
        _contextMenuHandler.RegisterCommands(this);
        
        // Initialize validation manager
        InitializeValidationManager();
        
        // Add keyboard shortcut support
        this.KeyDown += MainWindow_KeyDown;
        
        // Add cleanup support
        this.Closing += MainWindow_Closing;
        
        Loaded += async (_, __) =>
        {
            await InitializeLayoutUiAsync();
            await _navigationService.InitializeAsync();
            await InitializeNavigationAsync();
            SyncNavigationTabs();
            HookNavigationTabsUi();
            HookWorkspaceContextMenu();
            
            // Run simplified diagnostics to see what's actually working
            try
            {
                ApexV2.Diagnostics.SimpleDiagnostic.RunBasicCheck(this);
            }
            catch (Exception ex)
            {
                _logger.Error("Diagnostic test failed", ex);
            }
        };
    }

    private async Task InitializeDatabaseThenAuthentication()
    {
        try
        {
            _logger.Info("Starting database initialization");
            
            // Initialize database first
            var dbInitialized = await _databaseService.InitializeDatabaseAsync();
            if (!dbInitialized)
            {
                _logger.Error("Database initialization failed");
                System.Windows.MessageBox.Show("Database initialization failed. Application will exit.", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Windows.Application.Current.Shutdown();
                return;
            }
            
            _logger.Info("Database initialized successfully, starting authentication");
            
            // Now initialize authentication with database ready
            InitializeAuthentication();
        }
        catch (Exception ex)
        {
            _logger.Error("Database initialization failed", ex);
            System.Windows.MessageBox.Show("Database initialization failed. Application will exit.", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            System.Windows.Application.Current.Shutdown();
        }
    }

    private async void InitializeAuthentication()
    {
        try
        {
            var dbOptions = _databaseService.GetDbContextOptions();
            var userRepo = new ApexV2.Core.Authentication.UserRepository(dbOptions);
            var hasher = new ApexV2.Core.Authentication.Pbkdf2PasswordHasher();
            var authService = new ApexV2.Core.Authentication.AuthService(userRepo, hasher, App.LogManager);
            // seed admin if none
            _ = ApexV2.Core.Authentication.UserSeeder.SeedAdminAsync(userRepo, authService, App.LogManager);
            var loginWindow = new ApexV2.Windows.LoginWindow(authService, userRepo, hasher);
            var result = loginWindow.ShowDialog();
            if (result != true)
            {
                _logger.Warn("User cancelled login - shutting down", new Dictionary<string, object?>{ {"user", loginWindow.AuthenticatedUser?.Username} });
                System.Windows.Application.Current.Shutdown();
                return;
            }
            _logger.Info("User logged in", new Dictionary<string, object?>{{"user", loginWindow.AuthenticatedUser?.Username ?? "unknown"}});
            _currentUser = loginWindow.AuthenticatedUser; // store
            if (_currentUser != null)
            {
                try
                {
                    var dbOptions2 = _databaseService.GetDbContextOptions();
                    var profileRepo = new ApexV2.Core.Profiles.UserProfileRepository(dbOptions2);
                    var userRepo2 = new ApexV2.Core.Authentication.UserRepository(dbOptions2);
                    var profileService = new ApexV2.Core.Profiles.ProfileService(profileRepo, userRepo2, App.LogManager);
                    _currentUserProfile = await profileService.EnsureProfileAsync(_currentUser.Id);
                    StatusText.Text = $"Welcome {_currentUserProfile.DisplayName}";
                    if (FindName("CurrentUserDisplay") is System.Windows.Controls.TextBlock t)
                        t.Text = $"User: {_currentUserProfile.DisplayName}";
                }
                catch (Exception ex)
                {
                    _logger.Error("Profile ensure failed", ex);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Authentication initialization failed", ex);
            System.Windows.MessageBox.Show("Authentication failure. Application will exit.", "Auth Error", MessageBoxButton.OK, MessageBoxImage.Error);
            System.Windows.Application.Current.Shutdown();
        }
    }

    private async Task InitializeApplicationAsync()
    {
        try
        {
            _logger.Info("Starting application initialization sequence");
            
            // Database is already initialized, just update status
            UpdateStatus("Database already initialized");
            DatabaseStatus.Fill = System.Windows.Media.Brushes.Green;
            DatabaseStatusText.Text = "Connected";

            // Load settings (for API keys etc.)
            try
            {
                _settings = await _settingsService.LoadSettingsAsync();
                _logger.Info("Settings loaded");
            }
            catch (Exception ex)
            {
                _logger.Error("Settings load failed", ex);
            }

            // Initialize default market data provider (Yahoo Finance - no API key needed)
            UpdateStatus("Initializing market data provider...");
            _currentMarketDataProvider = MarketDataProviderFactory.CreateProvider(
                MarketDataProviderFactory.ProviderType.YahooFinance);
            _marketDataEngine = new ApexV2.Data.MarketData.Engine.MarketDataEngine(_currentMarketDataProvider, App.LogManager, TimeSpan.FromSeconds(15));
            await _marketDataEngine.StartAsync();
            _marketDataEngine.Subscribe("RY"); // sample subscription
            _ = Task.Run(() => ConsumeQuoteUpdatesAsync());

            // Initialize service locator for dependency injection
            UpdateStatus("Initializing watchlist services...");
            try
            {
                ApexV2.Core.ServiceLocator.Initialize(_databaseService, new LoggerAdapter(_logger), _currentMarketDataProvider);
                _logger.Info("Service locator initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.Error($"Service locator initialization failed: {ex.Message}");
            }

            // Initialize fundamentals engine (Finnhub) if API key present
            var finnhubKey = _settings?.Data.FinnhubApiKey?.Trim();
            if (!string.IsNullOrWhiteSpace(finnhubKey))
            {
                try
                {
                    var finnhubProvider = new FinnhubFundamentalProvider(finnhubKey!);
                    _fundamentalEngine = new FundamentalDataEngine(finnhubProvider, App.LogManager, new FundamentalDataEngineOptions());
                    _logger.Info("Fundamental data engine initialized (Finnhub)");
                    // Kick off initial fundamentals fetch (non-blocking)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var snap = await _fundamentalEngine.GetSnapshotAsync("RY", FundamentalDataScope.Core);
                            if (snap != null)
                            {
                                _logger.Info($"Fundamentals loaded for {snap.Symbol} P/E={snap.Core.PERatio:F2} EPS={snap.Core.EPS:F2}");
                                Dispatcher.Invoke(() => ApplyFundamentalsToUi(snap));
                            }
                            else _logger.Warn("Initial fundamentals snapshot returned null");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("Initial fundamentals fetch failed", ex);
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.Error("Fundamental engine initialization failed", ex);
                }
            }
            else
            {
                _logger.Warn("Finnhub API key not set - fundamentals engine not initialized");
            }

            // Initialize default trading provider (Alpaca Paper Trading)
            _currentTradingProvider = TradingProviderFactory.CreateProvider(
                TradingProviderFactory.ProviderType.Alpaca, 
                "demo-key", "demo-secret", paperTrading: true);

            // Initialize Plugin System
            UpdateStatus("Initializing plugin system...");
            await InitializePluginSystemAsync();
            await InitializeApiSystemAsync();

            UpdateStatus("APEX V2 Ready - Professional Trading Analysis Platform");
            LastUpdateText.Text = DateTime.Now.ToString("HH:mm:ss");
            _logger.Info("Initialization sequence complete");
        }
        catch (Exception ex)
        {
            _logger.Error("Initialization error", ex);
            UpdateStatus($"Initialization error: {ex.Message}");
            DatabaseStatus.Fill = System.Windows.Media.Brushes.Red;
            DatabaseStatusText.Text = "Error";
        }
    }

    private void ClockTimer_Tick(object? sender, EventArgs e)
    {
        ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
    }

    private async Task InitializePluginSystemAsync()
    {
        try
        {
            _logger.Info("Starting plugin system initialization");

            // Create plugin directory if it doesn't exist
            var pluginDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            if (!System.IO.Directory.Exists(pluginDirectory))
            {
                System.IO.Directory.CreateDirectory(pluginDirectory);
                _logger.Info($"Created plugin directory: {pluginDirectory}");
            }

            // Create Microsoft.Extensions.Logging logger factory
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole()
                       .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
            });

            // Initialize plugin manager
            _pluginManager = new PluginManager(
                loggerFactory.CreateLogger<PluginManager>(),
                pluginDirectory);

            // Initialize the plugin manager
            await _pluginManager.InitializeAsync();

            // Load and start enabled plugins
            await _pluginManager.LoadAllPluginsAsync();
            await _pluginManager.StartAllEnabledPluginsAsync();

            var loadedCount = _pluginManager.GetLoadedPlugins().Count();
            var runningCount = _pluginManager.GetRunningPlugins().Count();

            _logger.Info($"Plugin system initialized - {loadedCount} plugins loaded, {runningCount} running");
            UpdateStatus($"Plugin system ready - {runningCount} plugins active");
        }
        catch (Exception ex)
        {
            _logger.Error("Plugin system initialization failed", ex);
            UpdateStatus("Plugin system initialization failed");
        }
    }

    private async Task InitializeApiSystemAsync()
    {
        try
        {
            _logger.Info("Starting API system initialization");

            // Create Microsoft.Extensions.Logging logger factory for API system
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole()
                       .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
            });

            // Initialize API manager
            _apiManager = new ApiManager(loggerFactory.CreateLogger<ApiManager>());

            // Initialize the API manager
            await _apiManager.InitializeAsync();

            var status = await _apiManager.GetHealthStatusAsync();
            var isInitialized = _apiManager.IsInitialized;
            var servicesCount = _apiManager.GetAllServices().Count;

            _logger.Info($"API system initialized - {servicesCount} services available");
            UpdateStatus($"API system ready - {servicesCount} services active");
        }
        catch (Exception ex)
        {
            _logger.Error("API system initialization failed", ex);
            UpdateStatus("API system initialization failed");
        }
    }

    private void InitializeValidationManager()
    {
        try
        {
            _logger.Info("Initializing validation manager");
            
            // Get the project path (go up from bin directory)
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            var projectPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(currentDir, "..", "..", ".."));
            
            _validationManager = new ValidationManager(projectPath, _logger);
            
            // Wire up validation events
            _validationManager.ValidationCompleted += OnValidationCompleted;
            _validationManager.IssueDetected += OnIssueDetected;
            _validationManager.AutoFixApplied += OnAutoFixApplied;
            
            _logger.Info("Validation manager initialized successfully");
            
            // Run initial validation after a short delay
            _ = Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(async _ => 
            {
                try
                {
                    await _validationManager.RunValidationAsync();
                }
                catch (Exception ex)
                {
                    _logger.Error("Initial validation failed", ex);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error("Validation manager initialization failed", ex);
            UpdateStatus("Validation system initialization failed");
        }
    }

    private void OnValidationCompleted(object? sender, ApexV2.Validation.ValidationReport report)
    {
        try
        {
            var healthStatus = report.IssuesFound == 0 ? "Excellent" :
                              report.IssuesFound < 3 ? "Good" :
                              report.IssuesFound < 10 ? "Fair" : "Needs Attention";
            
            Dispatcher.Invoke(() =>
            {
                UpdateStatus($"Validation complete: {healthStatus} - {report.IssuesFound} issues");
                
                // Update validation indicator if it exists
                if (FindName("ValidationIndicator") is TextBlock indicator)
                {
                    indicator.Text = $"Health: {healthStatus}";
                    indicator.Foreground = report.IssuesFound == 0 ? Brushes.Green :
                                         report.IssuesFound < 5 ? Brushes.Orange : Brushes.Red;
                }
            });
            
            _logger.Info($"Validation completed: {report.IssuesFound} issues found, {report.Duration.TotalSeconds:F1}s");
        }
        catch (Exception ex)
        {
            _logger.Error("Error processing validation results", ex);
        }
    }

    private void OnIssueDetected(object? sender, ApexV2.Validation.ValidationIssue issue)
    {
        var severityIcon = issue.Severity switch
        {
            ApexV2.Validation.Severity.Critical => "🔴",
            ApexV2.Validation.Severity.High => "🟠", 
            ApexV2.Validation.Severity.Medium => "🟡",
            ApexV2.Validation.Severity.Low => "🔵",
            _ => "⚪"
        };
        
        var autoFixText = issue.IsAutoFixable ? " [Auto-fixable]" : "";
        _logger.Warn($"{severityIcon} Issue detected: {issue.Category} - {issue.Description}{autoFixText}");
    }

    private void OnAutoFixApplied(object? sender, string fixDescription)
    {
        _logger.Info($"🔧 Auto-fix applied: {fixDescription}");
        Dispatcher.Invoke(() =>
        {
            UpdateStatus($"Auto-fix applied: {fixDescription}");
        });
    }

    private void UpdateStatus(string message)
    {
        StatusText.Text = message;
        _logger.Debug($"Status updated: {message}");
    }

    private async Task ConsumeQuoteUpdatesAsync()
    {
        if (_marketDataEngine == null) return;
        try
        {
            await foreach (var upd in _marketDataEngine.GetUpdatesAsync())
            {
                Dispatcher.Invoke(() =>
                {
                    if (QuoteUpdatesList.Items.Count > 200) QuoteUpdatesList.Items.RemoveAt(0);
                    QuoteUpdatesList.Items.Add($"{upd.Symbol} {upd.Fields} @ {DateTime.Now:HH:mm:ss} Last={upd.Last}");
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Error("ConsumeQuoteUpdates failed", ex);
        }
    }

    private void ApplyFundamentalsToUi(FundamentalSnapshot snap)
    {
        try
        {
            FundSymbolValue.Text = snap.Symbol;
            PERatioValue.Text = snap.Core.PERatio == 0 ? "-" : snap.Core.PERatio.ToString("F2");
            EPSValue.Text = snap.Core.EPS == 0 ? "-" : snap.Core.EPS.ToString("F2");
            DebtEquityValue.Text = snap.Core.DebtToEquity == 0 ? "-" : snap.Core.DebtToEquity.ToString("F2");
            PriceBookValue.Text = snap.Core.PriceToBook == 0 ? "-" : snap.Core.PriceToBook.ToString("F2");
            ROEValue.Text = snap.Core.ReturnOnEquity == 0 ? "-" : snap.Core.ReturnOnEquity.ToString("F2") + "%";
            DividendYieldValue.Text = snap.Core.DividendYield == 0 ? "-" : snap.Core.DividendYield.ToString("F2") + "%";
            FundamentalsUpdatedText.Text = $"Updated: {snap.RetrievedUtc:HH:mm:ss} UTC"; // corrected property
        }
        catch (Exception ex)
        {
            _logger.Error("ApplyFundamentalsToUi failed", ex);
        }
    }

    private async Task InitializeLayoutUiAsync()
    {
        if (_layoutService == null) return;
        // attempt restore last layout name from settings
        string? lastLayoutName = await GetUserSettingAsync("LastLayoutName");
        WorkspaceLayoutModel layout;
        if (!string.IsNullOrWhiteSpace(lastLayoutName))
        {
            var loaded = await _layoutService.LoadByNameAsync(lastLayoutName);
            layout = loaded ?? await _layoutService.LoadDefaultAsync();
        }
        else
        {
            layout = await _layoutService.LoadDefaultAsync();
        }
        if (!layout.Panels.Any())
        {
            layout.Panels.Add(new LayoutPanelModel { Type = "watchlist", X = 10, Y = 10, Width = 250, Height = 400 });
            layout.Panels.Add(new LayoutPanelModel { Type = "chart", X = 270, Y = 10, Width = 800, Height = 600 });
            layout.Panels.Add(new LayoutPanelModel { Type = "fundamentals", X = 10, Y = 420, Width = 1060, Height = 180 });
        }
        WorkspaceHost.SetLayout(layout);
        if (_settings?.Appearance != null)
        {
            // WorkspaceHost.GridSize = _settings.Appearance.LayoutGridSize; // placeholder
        }
        WorkspaceHost.LayoutChanged += (_, __2) => { _layoutDirty = true; UpdateDirtyIndicator(); };
        WorkspaceHost.LayoutChangedDebounced += async (_, __3) => await AutoSaveLayoutAsync();
        _layoutsDynamicMenu = FindMenuItemByHeaderPath(new[] { "_View", "_Layouts" });
        await RefreshLayoutsMenuAsync();
        UpdateDirtyIndicator();
    }

    private async Task InitializeNavigationAsync()
    {
        if (_navigationService == null) return;
        if (!_navigationService.Pages.Any())
        {
            await _navigationService.AddPageAsync("Dashboard");
            await _navigationService.AddPageAsync("Analysis");
        }
        // Apply current page layout to WorkspaceHost
        if (_navigationService.CurrentPage?.CachedLayout != null)
        {
            WorkspaceHost.SetLayout(_navigationService.CurrentPage.CachedLayout);
        }
    }

    private void Navigation_CurrentPageChanged(object? sender, EventArgs e)
    {
        if (_navigationService?.CurrentPage?.CachedLayout != null)
        {
            WorkspaceHost.SetLayout(_navigationService.CurrentPage.CachedLayout);
            _layoutDirty = false;
            UpdateDirtyIndicator();
            UpdateStatus($"Switched to page '{_navigationService.CurrentPage.Name}'");
            SyncNavigationTabs();
        }
    }

    private void SyncNavigationTabs()
    {
        if (_navigationService == null) return;
        if (FindName("NavigationTabs") is TabControl tc)
        {
            tc.SelectionChanged -= NavigationTabs_SelectionChanged;
            tc.Items.Clear();
            foreach (var p in _navigationService.Pages)
            {
                var tab = new TabItem { Tag = p.Name };
                tab.Header = BuildTabHeader(p.Name);
                tc.Items.Add(tab);
                if (_navigationService.CurrentPage == p) tc.SelectedItem = tab;
            }
            tc.SelectionChanged += NavigationTabs_SelectionChanged;
        }
    }

    private object BuildTabHeader(string name)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        var txt = new TextBlock { Text = name };
        sp.Children.Add(txt);
        var closeBtn = new Button { Content = "×", Width = 18, Height = 18, Padding = new Thickness(0), Margin = new Thickness(6,0,0,0), Tag = name };
        closeBtn.Click += async (s, e) => await RemoveNavigationPageAsync(name);
        sp.Children.Add(closeBtn);
        sp.Tag = name;
        return sp;
    }

    private void HookNavigationTabsUi()
    {
        if (FindName("NavigationTabs") is not TabControl tc) return;
        tc.MouseDoubleClick += (s, e) =>
        {
            // Add new page on blank area double-click
            if (e.Source == tc)
            {
                AddPageCommand();
            }
        };
        
        // Add right-click context menu support for tab items
        tc.MouseRightButtonUp += (s, e) =>
        {
            if (e.OriginalSource is FrameworkElement element)
            {
                // Find the tab item that was right-clicked
                var tabItem = element.FindAncestor<TabItem>();
                if (tabItem?.Tag is string pageName && _contextMenuService != null && _navigationService != null)
                {
                    var contextMenu = _contextMenuService.CreateNavigationTabMenu(
                        pageName,
                        async name => await OnTabRename(name),
                        async name => await OnTabClose(name),
                        async name => await OnTabDuplicate(name)
                    );
                    contextMenu.PlacementTarget = tabItem;
                    contextMenu.IsOpen = true;
                }
            }
        };
    }

    private void HookWorkspaceContextMenu()
    {
        if (WorkspaceHost == null || _contextMenuService == null) return;
        
        // Add right-click context menu to workspace background
        WorkspaceHost.MouseRightButtonUp += (s, e) =>
        {
            // Only show if clicked on background (not on a panel)
            if (e.OriginalSource == WorkspaceHost || e.OriginalSource is Canvas)
            {
                var contextMenu = _contextMenuService.CreateWorkspaceMenu(
                    () => WorkspaceHost.AddPanel("chart"),
                    () => WorkspaceHost.AddPanel("watchlist"),
                    () => WorkspaceHost.AddPanel("news"),
                    () => PasteLayout()
                );
                contextMenu.PlacementTarget = WorkspaceHost;
                contextMenu.IsOpen = true;
            }
        };
    }

    private void PasteLayout()
    {
        // TODO [REVIEWED]: Implement layout paste functionality
        MessageBox.Show("Paste Layout functionality not yet implemented.", "Paste Layout", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // Context menu handlers for navigation tabs
    private async Task OnTabRename(string pageName)
    {
        if (_navigationService == null) return;
        var newName = PromptForString("Rename Page", $"Enter new name for '{pageName}':");
        if (!string.IsNullOrWhiteSpace(newName))
        {
            if (_navigationService.RenamePage(pageName, newName.Trim()))
            {
                SyncNavigationTabs();
                _logger.Info($"Renamed page from '{pageName}' to '{newName}'");
            }
            else
            {
                MessageBox.Show("Rename failed (duplicate or invalid name)", "Rename Page", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private async Task OnTabClose(string pageName)
    {
        if (_navigationService == null) return;
        if (_navigationService.Pages.Count <= 1)
        {
            MessageBox.Show("At least one page must remain.", "Close Page", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        await _navigationService.RemovePageAsync(pageName);
        SyncNavigationTabs();
        _logger.Info($"Closed page '{pageName}'");
    }

    private async Task OnTabDuplicate(string pageName)
    {
        if (_navigationService == null) return;
        var newName = $"{pageName} (Copy)";
        var counter = 1;
        while (_navigationService.Pages.Any(p => p.Name == newName))
        {
            newName = $"{pageName} (Copy {++counter})";
        }
        
        var currentPage = _navigationService.Pages.FirstOrDefault(p => p.Name == pageName);
        await _navigationService.AddPageAsync(newName, currentPage?.LayoutName);
        SyncNavigationTabs();
        _logger.Info($"Duplicated page '{pageName}' as '{newName}'");
    }

    private async void AddPageCommand()
    {
        if (_navigationService == null) return;
        var name = PromptForString("Add Page", "Enter page name:");
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            await _navigationService.AddPageAsync(name.Trim());
            SyncNavigationTabs();
        }
        catch (Exception ex)
        {
            _logger.Warn("AddPage failed", new Dictionary<string, object?> { { "error", ex.Message } });
            MessageBox.Show(ex.Message, "Add Page", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task SetUserSettingAsync(string key, string value)
    {
        try
        {
            using var ctx = new ApexDbContext(_databaseService.GetDbContextOptions());
            var entity = ctx.UserSettings.FirstOrDefault(s => s.Key == key);
            if (entity == null)
            {
                ctx.UserSettings.Add(new UserSettingsEntity { Key = key, Value = value, Category = "Layout", LastModified = DateTime.Now });
            }
            else
            {
                entity.Value = value;
                entity.LastModified = DateTime.Now;
            }
            await ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("SetUserSettingAsync failed", ex);
        }
    }

    private async Task<string?> GetUserSettingAsync(string key)
    {
        try
        {
            using var ctx = new ApexDbContext(_databaseService.GetDbContextOptions());
            return ctx.UserSettings.FirstOrDefault(s => s.Key == key)?.Value;
        }
        catch (Exception ex)
        {
            _logger.Error("GetUserSettingAsync failed", ex);
            return null;
        }
    }

    private void UpdateDirtyIndicator()
    {
        if (WorkspaceHost.Layout == null) return;
        var baseTitle = "APEX V2 - Professional Trading Analysis Platform";
        Title = _layoutDirty ? baseTitle + " *" : baseTitle;
        StatusText.Text = _layoutDirty ? $"Layout '{WorkspaceHost.Layout.Name}' modified" : $"Layout '{WorkspaceHost.Layout.Name}'";
    }

    private async Task AutoSaveLayoutAsync()
    {
        if (!_layoutDirty || _layoutService == null || WorkspaceHost.Layout == null) return;
        var ok = await _layoutService.SaveAsync(WorkspaceHost.Layout);
        if (ok)
        {
            _layoutDirty = false;
            UpdateDirtyIndicator();
            await SetUserSettingAsync("LastLayoutName", WorkspaceHost.Layout.Name);
            _logger.Debug("Auto-saved layout");
        }
    }

    private async Task SaveCurrentLayoutAsync()
    {
        if (_layoutService == null || WorkspaceHost.Layout == null) return;
        var ok = await _layoutService.SaveAsync(WorkspaceHost.Layout);
        if (ok)
        {
            _layoutDirty = false;
            UpdateDirtyIndicator();
            await SetUserSettingAsync("LastLayoutName", WorkspaceHost.Layout.Name);
            UpdateStatus($"Layout saved ({WorkspaceHost.Layout.Name})");
        }
        else
        {
            UpdateStatus("Failed to save layout");
        }
    }

    // File menu overrides
    private void NewWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (!PromptSaveIfDirty()) return;
        var name = PromptForString("New Workspace", "Enter new workspace name:");
        if (string.IsNullOrWhiteSpace(name)) return;
        var model = new WorkspaceLayoutModel { Name = name.Trim() };
        model.Panels.Add(new LayoutPanelModel { Type = "chart", X = 50, Y = 50, Width = 800, Height = 600 });
        WorkspaceHost.SetLayout(model);
        _layoutDirty = true;
        UpdateStatus($"Created new workspace '{name}'");
    }

    private async void OpenWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (_layoutService == null) return;
        var list = await _layoutService.ListAsync();
        if (list.Count == 0)
        {
            System.Windows.MessageBox.Show("No saved workspaces.", "Open Workspace", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var names = string.Join("\n", list.Select(l => $" - {l.Name}{(l.IsDefault ? " (Default)" : string.Empty)}"));
        var name = PromptForString("Open Workspace", "Enter workspace name to open:\n" + names);
        if (string.IsNullOrWhiteSpace(name)) return;
        await OpenLayoutByNameAsync(name.Trim());
    }

    private async void SaveWorkspace_Click(object sender, RoutedEventArgs e)
    {
        await SaveCurrentLayoutAsync();
    }

    private async void SaveWorkspaceAs_Click(object sender, RoutedEventArgs e)
    {
        if (_layoutService == null || WorkspaceHost.Layout == null) return;
        var name = PromptForString("Save Workspace As", "Enter new name:");
        if (string.IsNullOrWhiteSpace(name)) return;
        var ok = await _layoutService.SaveAsAsync(WorkspaceHost.Layout, name.Trim());
        if (ok)
        {
            _layoutDirty = false;
            UpdateDirtyIndicator();
            await SetUserSettingAsync("LastLayoutName", WorkspaceHost.Layout.Name);
            UpdateStatus($"Workspace saved as '{name}'");
            await RefreshLayoutsMenuAsync();
        }
    }

    private string? PromptForString(string title, string message)
    {
        // simple modal input via InputBox style (using WPF window minimal) for now fallback Console style prompt
        return Microsoft.VisualBasic.Interaction.InputBox(message, title, "");
    }

    #region View Menu Events
    private void DarkTheme_Click(object sender, RoutedEventArgs e)
    {
        App.ThemeManager.ApplyTheme("Dark");
    }

    private void LightTheme_Click(object sender, RoutedEventArgs e)
    {
        App.ThemeManager.ApplyTheme("Light");
    }

    private void BlueTheme_Click(object sender, RoutedEventArgs e)
    {
        App.ThemeManager.ApplyTheme("Professional Blue");
    }

    private void SingleChartLayout_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Layout options will be implemented in Component #12 (Layout Engine)", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MultiChartLayout_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Multi-chart layout will be implemented in Component #12 (Layout Engine)", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BloombergLayout_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Bloomberg-style layout will be implemented in Component #12 (Layout Engine)", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CustomLayout_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Custom layout builder will be implemented in Component #12 (Layout Engine)", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ToggleMainToolbar_Click(object sender, RoutedEventArgs e)
    {
        // This works now!
        var toolbarTray = FindName("ToolBarTray") as ToolBarTray;
        if (toolbarTray != null)
        {
            toolbarTray.Visibility = toolbarTray.Visibility == Visibility.Visible ? 
                                    Visibility.Collapsed : Visibility.Visible;
        }
        _logger.Debug("Toggled main toolbar visibility");
    }

    private void ToggleQuickActions_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Quick Actions toolbar will be implemented in future components", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        _logger.Info("User opened Quick Actions placeholder");
    }

    private void ToggleStatusBar_Click(object sender, RoutedEventArgs e)
    {
        // This works now - StatusBar is named element in Grid.Row="3"
        var statusBars = FindName("StatusBar") as System.Windows.Controls.Primitives.StatusBar;
        if (statusBars == null)
        {
            // Find by type if name doesn't work
            var statusBarElement = this.FindName("StatusBar");
            if (statusBarElement is System.Windows.Controls.Primitives.StatusBar sb)
            {
                sb.Visibility = sb.Visibility == Visibility.Visible ? 
                               Visibility.Collapsed : Visibility.Visible;
            }
        }
    }

    private void FullScreen_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
    #endregion

    #region Data Menu Events
    private void SelectYahooProvider_Click(object sender, RoutedEventArgs e)
    {
        _currentMarketDataProvider = MarketDataProviderFactory.CreateProvider(
            MarketDataProviderFactory.ProviderType.YahooFinance);
        UpdateStatus("Switched to Yahoo Finance data provider");
        _logger.Info("Switched to Yahoo Finance data provider");
    }

    private void SelectAlphaVantageProvider_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Alpha Vantage provider selection will show API key dialog when implemented", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SelectIEXProvider_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("IEX Cloud provider selection will show API key dialog when implemented", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SelectPolygonProvider_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Polygon.io provider selection will show API key dialog when implemented", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SelectFinnhubProvider_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Finnhub provider selection will show API key dialog when implemented", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void ConnectDataFeed_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMarketDataProvider != null)
        {
            UpdateStatus("Connecting to market data...");
            _logger.Info("Connecting to market data...");
            var connected = await _currentMarketDataProvider.ConnectAsync();
            
            if (connected)
            {
                MarketDataStatus.Fill = System.Windows.Media.Brushes.Green;
                MarketDataStatusText.Text = $"Connected ({_currentMarketDataProvider.ProviderName})";
                StatusBarDataIndicator.Fill = System.Windows.Media.Brushes.Green;
                StatusBarDataText.Text = "Connected";
                ConnectionIndicator.Fill = System.Windows.Media.Brushes.Green;
                UpdateStatus("Market data connected successfully");
                _logger.Info("Market data connected successfully");
            }
            else
            {
                MarketDataStatus.Fill = System.Windows.Media.Brushes.Red;
                MarketDataStatusText.Text = "Connection Failed";
                UpdateStatus("Failed to connect to market data");
                _logger.Warn("Failed to connect to market data");
            }
        }
    }

    private async void DisconnectDataFeed_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMarketDataProvider != null)
        {
            await _currentMarketDataProvider.DisconnectAsync();
            MarketDataStatus.Fill = System.Windows.Media.Brushes.Red;
            MarketDataStatusText.Text = "Disconnected";
            StatusBarDataIndicator.Fill = System.Windows.Media.Brushes.Red;
            StatusBarDataText.Text = "Disconnected";
            ConnectionIndicator.Fill = System.Windows.Media.Brushes.Red;
            UpdateStatus("Market data disconnected");
            _logger.Info("Market data disconnected");
        }
    }

    private void RefreshAllData_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatus("Data refresh will be implemented when charts and watchlists are added");
        _logger.Warn("RefreshAllData invoked before implementation");
    }

    private void DataSettings_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Data Settings dialog will be implemented in Component #2 (Configuration System)", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    #endregion

    #region Trading Menu Events
    private void PaperTradingMode_Click(object sender, RoutedEventArgs e)
    {
        if (_isPaperTradingMode)
        {
            _logger.Debug("PaperTradingMode_Click ignored - already in paper trading mode");
            return;
        }
        _isPaperTradingMode = true;
        TradingModeIndicator.Fill = System.Windows.Media.Brushes.Green;
        TradingModeText.Text = "Paper Trading";
        TradingStatusText.Text = "Paper Trading Ready";
        StatusBarTradingText.Text = "Paper";
        StatusBarTradingIndicator.Fill = System.Windows.Media.Brushes.Green;
        UpdateStatus("Switched to Paper Trading mode (Safe!)");
        _logger.Info("Switched to Paper Trading mode");
    }

    private void LiveTradingMode_Click(object sender, RoutedEventArgs e)
    {
        if (!_isPaperTradingMode)
        {
            _logger.Debug("LiveTradingMode_Click ignored - already in live trading mode");
            return;
        }
        var result = System.Windows.MessageBox.Show(
            "⚠️ WARNING: You are switching to LIVE TRADING mode!\n\n" +
            "This means REAL MONEY will be used for trades.\n" +
            "Are you absolutely sure you want to continue?",
            "Live Trading Warning", 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _logger.Critical("User enabled LIVE trading mode");
            _isPaperTradingMode = false;
            TradingModeIndicator.Fill = System.Windows.Media.Brushes.Orange;
            TradingModeText.Text = "Live Trading";
            TradingStatusText.Text = "Live Trading Active";
            StatusBarTradingText.Text = "LIVE";
            StatusBarTradingIndicator.Fill = System.Windows.Media.Brushes.Orange;
            UpdateStatus("⚠️ LIVE TRADING MODE ACTIVE - Real money at risk!");
            _logger.Info("Switched to Live Trading mode");
        }
        else
        {
            _logger.Info("User cancelled LIVE trading mode switch");
            // Reset toggle if user cancelled
            if (sender is MenuItem menuItem) menuItem.IsChecked = false;
            if (TradingModeToggle != null) TradingModeToggle.IsChecked = true;
        }
    }

    private void ConnectBroker_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Broker connection will be implemented with trading provider integration", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DisconnectBroker_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Broker disconnection will be implemented with trading provider integration", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void AccountSummary_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Account Summary will be implemented in Component #27 (Portfolio Integration)", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Portfolio_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Portfolio view will be implemented in Component #27 (Portfolio Integration)", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OrderHistory_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Order History will be implemented in Component #27 (Portfolio Integration)", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void TradingSettings_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Trading Settings will be implemented in Component #2 (Configuration System)", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    #endregion

    #region Analysis Menu Events
    private void ManageWatchlists_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var watchlistWindow = new ApexV2.Analysis.Watchlist.WatchlistWindow(
                ServiceLocator.GetWatchlistService(),
                ServiceLocator.GetWatchlistAnalysisService(),
                ServiceLocator.GetLogger());
            watchlistWindow.Show();
            UpdateStatus("Opened watchlist manager");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error opening watchlist manager: {ex.Message}");
            UpdateStatus($"Error opening watchlist manager: {ex.Message}");
        }
    }

    private void AddToWatchlist_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var watchlistWindow = new ApexV2.Analysis.Watchlist.WatchlistWindow(
                ServiceLocator.GetWatchlistService(),
                ServiceLocator.GetWatchlistAnalysisService(),
                ServiceLocator.GetLogger());
            watchlistWindow.Show();
            UpdateStatus("Opened watchlist manager for adding symbols");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error opening watchlist for adding symbols: {ex.Message}");
            UpdateStatus($"Error opening watchlist: {ex.Message}");
        }
    }

    private void AddIndicator_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Technical indicators will be implemented starting with Component #19 (Indicator Engine Core)", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CustomIndicators_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Custom indicators will be implemented in Component #21 (Custom Indicator Builder)", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void IndicatorLibrary_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Indicator library will be implemented in Component #22 (Indicator Library Manager)", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Scanner_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Stock scanner will be implemented in Component #24 (Scanner Engine)", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Alerts_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Alert system will be implemented in Component #25 (Alert System)", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PatternRecognition_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Pattern recognition will be implemented in Component #26 (Pattern Recognition)", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    #endregion

    #region Tools Menu Events
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var settingsWindow = new Windows.SettingsWindow(_settingsService);
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.Error($"Error opening settings window: {ex.Message}");
            System.Windows.MessageBox.Show($"Error opening settings: {ex.Message}", 
                           "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Preferences_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // For now, redirect to Settings (we can create a separate Preferences dialog later)
            var settingsWindow = new Windows.SettingsWindow(_settingsService);
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.Error($"Error opening preferences window: {ex.Message}");
            System.Windows.MessageBox.Show($"Error opening preferences: {ex.Message}", 
                           "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PluginManager_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_pluginManager == null)
            {
                MessageBox.Show("Plugin system is not initialized. Please restart the application.", 
                    "Plugin Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var pluginManagerWindow = new ApexV2.Windows.PluginManagerWindow(_pluginManager)
            {
                Owner = this
            };
            
            pluginManagerWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.Error("Error opening Plugin Manager", ex);
            MessageBox.Show($"Error opening Plugin Manager: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApiManager_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_apiManager == null)
            {
                MessageBox.Show("API system is not initialized. Please restart the application.", 
                    "API Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var apiManagerWindow = new ApexV2.Extensions.API.ApiManagerWindow()
            {
                Owner = this
            };
            
            apiManagerWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.Error("Error opening API Manager", ex);
            MessageBox.Show($"Error opening API Manager: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BackupDatabase_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "SQLite Database|*.db|All Files|*.*",
            DefaultExt = "db",
            FileName = $"apex_v2_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db"
        };

        if (dialog.ShowDialog() == true)
        {
            var success = await _databaseService.BackupDatabaseAsync(dialog.FileName);
            System.Windows.MessageBox.Show(success ? "Database backed up successfully!" : "Database backup failed!",
                           "Backup Result", MessageBoxButton.OK, 
                           success ? MessageBoxImage.Information : MessageBoxImage.Error);
            _logger.Info($"Database backup attempted: {(success ? "Success" : "Failure")}");
        }
    }

    private async void RestoreDatabase_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "⚠️ WARNING: This will replace your current database!\n\n" +
            "All current data will be lost. Continue?",
            "Restore Database Warning", 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "SQLite Database|*.db|All Files|*.*",
                DefaultExt = "db"
            };

            if (dialog.ShowDialog() == true)
            {
                var success = await _databaseService.RestoreDatabaseAsync(dialog.FileName);
                System.Windows.MessageBox.Show(success ? "Database restored successfully!" : "Database restore failed!",
                               "Restore Result", MessageBoxButton.OK, 
                               success ? MessageBoxImage.Information : MessageBoxImage.Error);
                _logger.Info($"Database restore attempted: {(success ? "Success" : "Failure")}");
            }
        }
    }

    private void CleanOldData_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Data cleanup will be implemented with market data caching", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PerformanceMonitor_Click(object sender, RoutedEventArgs e)
    {
        var dbInfo = _databaseService.GetDatabaseInfo();
        var message = $"Database Information:\n\n" +
                     $"Path: {dbInfo.Path}\n" +
                     $"Size: {dbInfo.SizeFormatted}\n" +
                     $"Created: {dbInfo.Created?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"}\n" +
                     $"Modified: {dbInfo.LastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown"}";
        
        System.Windows.MessageBox.Show(message, "Performance Monitor", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LogViewer_Click(object sender, RoutedEventArgs e)
    {
        var win = new ApexV2.Windows.LogViewerWindow { Owner = this };
        win.Show();
        _logger.Info("Opened Log Viewer window");
    }
    #endregion

    #region Help Menu Events
    private void UserManual_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("User manual will be available when the application is complete", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void KeyboardShortcuts_Click(object sender, RoutedEventArgs e)
    {
        var shortcuts = "Keyboard Shortcuts:\n\n" +
                       "Ctrl+N - New Workspace\n" +
                       "Ctrl+O - Open Workspace\n" +
                       "Ctrl+S - Save Workspace\n" +
                       "Ctrl+T - Add Page\n" +
                       "F2 - Rename Current Page\n" +
                       "Ctrl+W - Close Current Page\n" +
                       "F5 - Refresh Data\n" +
                       "F11 - Full Screen\n" +
                       "Alt+F4 - Exit";
        
        System.Windows.MessageBox.Show(shortcuts, "Keyboard Shortcuts", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void VideoTutorials_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Video tutorials will be created when the application is complete", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ReportBug_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Bug reporting system will be implemented in Component #33 (Update System)", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void FeatureRequest_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Feature request system will be implemented in Component #33 (Update System)", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Update checking will be implemented in Component #33 (Update System)", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var aboutMessage = "APEX V2 - Professional Trading Analysis Platform\n\n" +
                          "Version: 2.0.0 Alpha\n" +
                          "Build: Component #1 (Application Shell)\n\n" +
                          "A revolutionary trading analysis platform with:\n" +
                          "• Real-time market data from multiple providers\n" +
                          "• Paper trading and live trading integration\n" +
                          "• Infinite workspace customization\n" +
                          "• Advanced technical analysis tools\n\n" +
                          "Built with C# + WPF + SQLite\n" +
                          "Designed for professional traders";
        
        System.Windows.MessageBox.Show(aboutMessage, "About APEX V2", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    #endregion

    #region Toolbar Events
    private void QuickSymbolSearch_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SearchSymbol_Click(sender, e);
        }
    }

    private void SearchSymbol_Click(object sender, RoutedEventArgs e)
    {
        var symbol = QuickSymbolSearch.Text?.Trim().ToUpper();
        if (string.IsNullOrEmpty(symbol))
        {
            System.Windows.MessageBox.Show("Please enter a symbol to search", "Symbol Required", 
                           MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        try
        {
            // Load fundamentals for the symbol
            _ = LoadFundamentalsForSymbolAsync(symbol);
            
            // Create a new chart window for the symbol
            CreateNewChartForSymbol(symbol);
            
            StatusText.Text = $"Loaded chart for {symbol}";
        }
        catch (Exception ex)
        {
            App.LogManager.GetLogger("MainWindow").Error($"Failed to load symbol {symbol}", ex);
            System.Windows.MessageBox.Show($"Failed to load symbol {symbol}: {ex.Message}", 
                           "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        
        QuickSymbolSearch.Clear();
    }
    
    private void CreateNewChartForSymbol(string symbol)
    {
        try
        {
            var chartWindow = new ApexV2.Charts.Windows.ChartWindow(symbol.ToUpper());
            chartWindow.Show();
            
            App.LogManager.GetLogger("MainWindow").Info($"Created new chart window for symbol: {symbol}");
        }
        catch (Exception ex)
        {
            App.LogManager.GetLogger("MainWindow").Error($"Failed to create chart for {symbol}", ex);
            // Fallback - at least show a message that we tried
            System.Windows.MessageBox.Show($"Chart created for {symbol} - Chart functionality is being enhanced", 
                           "Chart Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void QuickStart_Click(object sender, RoutedEventArgs e)
    {
        var quickStartMessage = "APEX V2 Quick Start:\n\n" +
                               "1. Connect to Market Data (Data menu)\n" +
                               "2. Choose Paper Trading or Live Trading\n" +
                               "3. Create or load a workspace layout\n" +
                               "4. Add symbols to your watchlist\n" +
                               "5. Start analyzing with charts and indicators\n\n" +
                               "Note: This is Component #1 (Application Shell)\n" +
                               "More features will be added as we build the remaining 32 components!";
        
        System.Windows.MessageBox.Show(quickStartMessage, "Quick Start Guide", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    #endregion

    private void UserProfile_Click(object sender, RoutedEventArgs e)
    {
        OpenUserProfileWindow();
    }

    private void CurrentUserDisplay_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenUserProfileWindow();
    }

    private void OpenUserProfileWindow()
    {
        if (_currentUser == null) return;
        var dbOptions = _databaseService.GetDbContextOptions();
        var profileRepo = new ApexV2.Core.Profiles.UserProfileRepository(dbOptions);
        var userRepo = new ApexV2.Core.Authentication.UserRepository(dbOptions);
        var profileService = new ApexV2.Core.Profiles.ProfileService(profileRepo, userRepo, App.LogManager);
        var win = new ApexV2.Windows.UserProfileWindow(profileService, _currentUser, App.LogManager.GetLogger("UserProfileWindow"))
        {
            Owner = this
        };
        win.ShowDialog();
        if (win.IsVisible == false && _currentUserProfile != null && FindName("CurrentUserDisplay") is System.Windows.Controls.TextBlock t)
        {
            // Refresh profile display after window closes
            _ = RefreshProfileDisplayAsync();
        }
    }

    private async Task RefreshProfileDisplayAsync()
    {
        if (_currentUser == null) return;
        var dbOptions = _databaseService.GetDbContextOptions();
        var profileRepo = new ApexV2.Core.Profiles.UserProfileRepository(dbOptions);
        var userRepo = new ApexV2.Core.Authentication.UserRepository(dbOptions);
        var profileService = new ApexV2.Core.Profiles.ProfileService(profileRepo, userRepo, App.LogManager);
        _currentUserProfile = await profileService.EnsureProfileAsync(_currentUser.Id);
        if (FindName("CurrentUserDisplay") is System.Windows.Controls.TextBlock t)
            t.Text = $"User: {_currentUserProfile.DisplayName}";
    }

    private async Task LoadFundamentalsForSymbolAsync(string symbol)
    {
        if (_fundamentalEngine == null) return;
        try
        {
            var snap = await _fundamentalEngine.GetSnapshotAsync(symbol, FundamentalDataScope.Core, forceRefresh: true);
            if (snap != null)
            {
                Dispatcher.Invoke(() => ApplyFundamentalsToUi(snap));
            }
            else
            {
                _logger.Warn($"No fundamentals returned for {symbol}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("LoadFundamentalsForSymbolAsync failed", ex);
        }
    }

    protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            if (_marketDataEngine != null)
            {
                await _marketDataEngine.StopAsync();
            }
        }
        catch { }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _fundamentalEngine?.Dispose(); // added dispose
        base.OnClosed(e);
    }

    // ===== Added Helper / Event Methods (Layout + File Menu) =====
    private MenuItem? FindMenuItemByHeaderPath(string[] headers)
    {
        if (headers.Length == 0) return null;
        // Attempt to locate the root menu (first Menu in window)
        var rootMenu = this.Content is DependencyObject dep ?
            LogicalTreeHelper.GetChildren(dep).OfType<Menu>().FirstOrDefault() : null;
        if (rootMenu == null)
        {
            // fallback: search entire logical tree
            rootMenu = LogicalTreeHelper.GetChildren(this).OfType<Menu>().FirstOrDefault();
        }
        if (rootMenu == null) return null;
        ItemsControl? current = rootMenu;
        foreach (var header in headers)
        {
            if (current == null) return null;
            MenuItem? next = null;
            foreach (var item in current.Items.OfType<object>())
            {
                if (item is MenuItem mi && mi.Header is string hs && string.Equals(hs, header, StringComparison.OrdinalIgnoreCase))
                {
                    next = mi; break;
                }
            }
            current = next;
        }
        return current as MenuItem;
    }

    private async Task RefreshLayoutsMenuAsync()
    {
        if (_layoutService == null) return;
        if (_layoutsDynamicMenu == null)
        {
            _layoutsDynamicMenu = FindMenuItemByHeaderPath(new[] { "_View", "_Layouts" });
            if (_layoutsDynamicMenu == null) return;
        }
        _layoutsDynamicMenu.Items.Clear();
        var layouts = await _layoutService.ListAsync();
        foreach (var l in layouts)
        {
            var item = new MenuItem { Header = l.Name + (l.IsDefault ? " (Default)" : string.Empty) };
            item.Click += async (_, __) => await OpenLayoutByNameAsync(l.Name);
            _layoutsDynamicMenu.Items.Add(item);
        }
        if (layouts.Count > 0)
        {
            _layoutsDynamicMenu.Items.Add(new Separator());
            foreach (var l in layouts)
            {
                var setDefault = new MenuItem { Header = $"Set '{l.Name}' as Default" };
                setDefault.Click += async (_, __) =>
                {
                    if (await _layoutService.SetDefaultAsync(l.Name))
                    {
                        await RefreshLayoutsMenuAsync();
                        UpdateStatus($"Set '{l.Name}' as default layout");
                    }
                };
                _layoutsDynamicMenu.Items.Add(setDefault);
            }
        }
        // Add basic actions
        _layoutsDynamicMenu.Items.Add(new Separator());
        var addPanel = new MenuItem { Header = "Add Panel (Chart)" };
        addPanel.Click += (_, __) => { WorkspaceHost.AddPanel("chart"); _layoutDirty = true; UpdateDirtyIndicator(); };
        _layoutsDynamicMenu.Items.Add(addPanel);
    }

    private bool PromptSaveIfDirty()
    {
        if (!_layoutDirty || WorkspaceHost.Layout == null) return true;
        var r = System.Windows.MessageBox.Show($"Save changes to layout '{WorkspaceHost.Layout.Name}'?", "Unsaved Layout", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (r == MessageBoxResult.Cancel) return false;
        if (r == MessageBoxResult.Yes)
        {
            _ = SaveCurrentLayoutAsync();
        }
        return true;
    }

    private async Task OpenLayoutByNameAsync(string name)
    {
        if (_layoutService == null) return;
        var layout = await _layoutService.LoadByNameAsync(name);
        if (layout == null)
        {
            System.Windows.MessageBox.Show($"Layout '{name}' not found.", "Open Layout", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        WorkspaceHost.SetLayout(layout);
        _layoutDirty = false;
        UpdateDirtyIndicator();
        await SetUserSettingAsync("LastLayoutName", layout.Name);
        UpdateStatus($"Opened layout '{layout.Name}'");
    }

    private async Task RemoveNavigationPageAsync(string name)
    {
        if (_navigationService == null) return;
        if (_navigationService.Pages.Count <= 1)
        {
            MessageBox.Show("At least one page must remain.", "Remove Page", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        await _navigationService.RemovePageAsync(name);
        SyncNavigationTabs();
        if (_navigationService.CurrentPage?.CachedLayout != null)
        {
            WorkspaceHost.SetLayout(_navigationService.CurrentPage.CachedLayout);
            _layoutDirty = false; UpdateDirtyIndicator();
        }
    }

    private void RenameCurrentPage()
    {
        if (_navigationService == null || _navigationService.CurrentPage == null) return;
        var old = _navigationService.CurrentPage.Name;
        var input = PromptForString("Rename Page", $"Enter new name for '{old}':");
        if (string.IsNullOrWhiteSpace(input)) return;
        if (!_navigationService.RenamePage(old, input.Trim()))
        {
            MessageBox.Show("Rename failed (duplicate or invalid name)", "Rename Page", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        SyncNavigationTabs();
    }

    // ===== File Menu handlers referenced in XAML =====
    private void ImportData_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Data import not yet implemented.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportData_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Data export not yet implemented.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        if (!PromptSaveIfDirty()) return;
        Close();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (_navigationService == null) return;
        
        // Chart shortcut: Ctrl+Shift+T
        if (e.Key == Key.T && Keyboard.Modifiers.HasFlag(ModifierKeys.Control | ModifierKeys.Shift))
        {
            NewChart_Click(this, new RoutedEventArgs()); 
            e.Handled = true; 
            return;
        }
        
        if (e.Key == Key.T && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            AddPageCommand(); e.Handled = true; return;
        }
        if (e.Key == Key.F2)
        {
            RenameCurrentPage(); e.Handled = true; return;
        }
        if (e.Key == Key.W && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (_navigationService.CurrentPage != null)
            {
                _ = RemoveNavigationPageAsync(_navigationService.CurrentPage.Name);
                e.Handled = true; return;
            }
        }
        if (e.Key >= Key.D1 && e.Key <= Key.D9 && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            int idx = (int)e.Key - (int)Key.D1; // 0-based index
            if (idx < _navigationService.Pages.Count)
            {
                var target = _navigationService.Pages[idx];
                _ = _navigationService.SetCurrentPageAsync(target.Name);
                e.Handled = true; return;
            }
        }
    }

    private void NavigationTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_navigationService == null) return;
        if (sender is TabControl tc && tc.SelectedItem is TabItem ti && ti.Tag is string name)
        {
            _ = _navigationService.SetCurrentPageAsync(name);
        }
    }

    #region Chart Event Handlers

    private void NewChart_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Prompt for symbol
            var symbol = PromptForSymbol("Enter symbol for new chart:");
            if (!string.IsNullOrWhiteSpace(symbol))
            {
                OpenNewChartWindow(symbol);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Error opening new chart", ex);
            MessageBox.Show($"Error opening chart: {ex.Message}", "Chart Error", 
                           MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ChartTemplates_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // TODO [REVIEWED]: Implement chart templates
            _logger.Info("Chart templates requested");
            MessageBox.Show("Chart templates feature coming soon!", "Chart Templates", 
                           MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.Error("Error accessing chart templates", ex);
        }
    }

    private void SaveChartLayout_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // TODO [REVIEWED]: Implement chart layout saving
            _logger.Info("Save chart layout requested");
            MessageBox.Show("Save chart layout feature coming soon!", "Save Layout", 
                           MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.Error("Error saving chart layout", ex);
        }
    }

    private void OpenNewChartWindow(string symbol)
    {
        try
        {
            var chartWindow = new ApexV2.Charts.Windows.ChartWindow(symbol.ToUpper());
            chartWindow.Show();
            
            _logger.Info($"Opened new chart window for symbol: {symbol}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error creating chart window for {symbol}", ex);
            MessageBox.Show($"Error opening chart for {symbol}: {ex.Message}", "Chart Error", 
                           MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string? PromptForSymbol(string prompt)
    {
        // Simple input dialog - in production app, use a proper dialog
        var inputDialog = new InputDialog(prompt, "AAPL");
        return inputDialog.ShowDialog() == true ? inputDialog.InputText : null;
    }

    #endregion

    #region Validation Event Handlers

    private async void ValidationIndicator_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (_validationManager == null)
            {
                MessageBox.Show("Validation system not initialized.", "Validation", 
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show validation options
            var result = MessageBox.Show(
                "Choose validation action:\n\n" +
                "YES - Run validation now\n" +
                "NO - View last validation report\n" +
                "CANCEL - Open validation settings",
                "Validation System",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    UpdateStatus("Running validation...");
                    await _validationManager.RunValidationAsync();
                    break;
                    
                case MessageBoxResult.No:
                    ShowValidationReport();
                    break;
                    
                case MessageBoxResult.Cancel:
                    ShowValidationSettings();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Error in validation indicator click", ex);
            MessageBox.Show($"Error: {ex.Message}", "Validation Error", 
                           MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowValidationReport()
    {
        try
        {
            if (_validationManager?.LastReport == null)
            {
                MessageBox.Show("No validation report available yet.", "Validation Report", 
                               MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var report = _validationManager.LastReport;
            var summary = $"Validation Report\n" +
                         $"Generated: {report.EndTime:yyyy-MM-dd HH:mm:ss}\n" +
                         $"Duration: {report.Duration.TotalSeconds:F1} seconds\n\n" +
                         $"Build Status: {(report.BuildSucceeded ? "✅ Success" : "❌ Failed")}\n" +
                         $"Issues Found: {report.IssuesFound}\n\n" +
                         $"Code Quality Issues: {report.CodeQualityIssues}\n" +
                         $"Performance Issues: {report.PerformanceAnalyticsIssues}\n" +
                         $"XAML Issues: {report.XamlIssues}\n" +
                         $"Test Coverage Issues: {report.TestCoverageIssues}\n" +
                         $"Runtime Issues: {report.RuntimeIssues}";

            if (report.Issues.Any())
            {
                summary += "\n\nTop Issues:\n";
                foreach (var issue in report.Issues.Take(5))
                {
                    var severityIcon = issue.Severity switch
                    {
                        ApexV2.Validation.Severity.Critical => "🔴",
                        ApexV2.Validation.Severity.High => "🟠",
                        ApexV2.Validation.Severity.Medium => "🟡",
                        ApexV2.Validation.Severity.Low => "🔵",
                        _ => "⚪"
                    };
                    summary += $"{severityIcon} {issue.Category}: {issue.Description}\n";
                }
            }

            MessageBox.Show(summary, "Validation Report", MessageBoxButton.OK, MessageBoxImage.Information);

            // Try to open the HTML report if it exists
            try
            {
                var reportPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "validation_report.html");
                if (System.IO.File.Exists(reportPath))
                {
                    var result = MessageBox.Show("Would you like to open the detailed HTML report?", 
                                                "Open Detailed Report", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = reportPath,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error opening validation report", ex);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Error showing validation report", ex);
            MessageBox.Show($"Error displaying report: {ex.Message}", "Report Error", 
                           MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowValidationSettings()
    {
        try
        {
            if (_validationManager == null)
            {
                MessageBox.Show("Validation system not initialized.", "Validation Settings", 
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var currentStatus = _validationManager.AutoFixEnabled ? "enabled" : "disabled";
            var summary = _validationManager.GetValidationSummary();
            
            var message = $"Validation Settings\n\n" +
                         $"Auto-fix currently: {currentStatus}\n\n" +
                         $"{summary}\n\n" +
                         $"Would you like to toggle auto-fix?";

            var result = MessageBox.Show(message, "Validation Settings", 
                                        MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                _validationManager.AutoFixEnabled = !_validationManager.AutoFixEnabled;
                var newStatus = _validationManager.AutoFixEnabled ? "enabled" : "disabled";
                MessageBox.Show($"Auto-fix is now {newStatus}.", "Settings Updated", 
                               MessageBoxButton.OK, MessageBoxImage.Information);
                UpdateStatus($"Auto-fix {newStatus}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Error showing validation settings", ex);
            MessageBox.Show($"Error: {ex.Message}", "Settings Error", 
                           MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Tools Menu Event Handlers

    private async void RunValidation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_validationManager == null)
            {
                MessageBox.Show("Validation system not initialized.", "Validation", 
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UpdateStatus("Running validation...");
            await _validationManager.RunValidationAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Error running validation", ex);
            MessageBox.Show($"Error running validation: {ex.Message}", "Validation Error", 
                           MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ViewValidationReport_Click(object sender, RoutedEventArgs e)
    {
        ShowValidationReport();
    }

    private void ToggleAutoFix_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_validationManager == null)
            {
                MessageBox.Show("Validation system not initialized.", "Auto-Fix", 
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _validationManager.AutoFixEnabled = !_validationManager.AutoFixEnabled;
            var status = _validationManager.AutoFixEnabled ? "enabled" : "disabled";
            
            MessageBox.Show($"Auto-fix is now {status}.", "Auto-Fix Settings", 
                           MessageBoxButton.OK, MessageBoxImage.Information);
            UpdateStatus($"Auto-fix {status}");
        }
        catch (Exception ex)
        {
            _logger.Error("Error toggling auto-fix", ex);
            MessageBox.Show($"Error: {ex.Message}", "Auto-Fix Error", 
                           MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ValidationSettings_Click(object sender, RoutedEventArgs e)
    {
        ShowValidationSettings();
    }

    private async void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        // Validation shortcut: Ctrl+Shift+V
        if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control | ModifierKeys.Shift))
        {
            await RunValidationAsync();
            e.Handled = true;
            return;
        }
    }

    private async Task RunValidationAsync()
    {
        try
        {
            if (_validationManager == null)
            {
                UpdateStatus("Validation system not initialized");
                return;
            }

            UpdateStatus("Running validation...");
            await _validationManager.RunValidationAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Error running validation via shortcut", ex);
            UpdateStatus("Validation failed");
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            // Dispose validation manager
            _validationManager?.Dispose();
            
            // Stop clock timer
            _clockTimer?.Stop();
            
            // Stop market data engine
            _marketDataEngine?.StopAsync();
            
            _logger.Info("MainWindow cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.Error("Error during MainWindow cleanup", ex);
        }
    }

    #endregion
}

/// <summary>
/// Extension methods for UI helpers
/// </summary>
public static class UIExtensions
{
    /// <summary>
    /// Finds an ancestor of the specified type in the visual tree
    /// </summary>
    public static T? FindAncestor<T>(this DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        if (parent == null) return null;
        
        if (parent is T parentT)
            return parentT;
        
        return FindAncestor<T>(parent);
    }
}