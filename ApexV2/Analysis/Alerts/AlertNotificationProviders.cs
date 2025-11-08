using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using ApexV2.Charts.Export;

namespace ApexV2.Analysis.Alerts
{
    /// <summary>
    /// In-application notification provider for alerts
    /// </summary>
    public class InAppNotificationProvider : IAlertNotificationProvider
    {
        private readonly IChartLogger _logger;
        private readonly List<AlertNotification> _notifications = new();
        private readonly int _maxNotifications = 100;

        public string ProviderName => "In-App Notifications";
        public bool IsEnabled { get; set; } = true;

        public InAppNotificationProvider(IChartLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public event EventHandler<AlertNotificationEventArgs>? NotificationReceived;

        public async Task SendNotificationAsync(AlertBase alert, string message, AlertContext context)
        {
            if (!IsEnabled) return;

            await Task.Run(() =>
            {
                var notification = new AlertNotification
                {
                    Id = Guid.NewGuid(),
                    Alert = alert,
                    Message = message,
                    Context = context,
                    Timestamp = DateTime.Now,
                    IsRead = false,
                    Priority = alert.Priority
                };

                // Add to collection (thread-safe)
                lock (_notifications)
                {
                    _notifications.Insert(0, notification); // Most recent first
                    
                    // Trim to max size
                    if (_notifications.Count > _maxNotifications)
                    {
                        _notifications.RemoveAt(_notifications.Count - 1);
                    }
                }

                _logger.Info($"In-app notification: {message}");
                
                // Fire event on UI thread
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    NotificationReceived?.Invoke(this, new AlertNotificationEventArgs(notification));
                });
            });
        }

        public List<AlertNotification> GetNotifications()
        {
            lock (_notifications)
            {
                return new List<AlertNotification>(_notifications);
            }
        }

        public List<AlertNotification> GetUnreadNotifications()
        {
            lock (_notifications)
            {
                return _notifications.FindAll(n => !n.IsRead);
            }
        }

        public void MarkAsRead(Guid notificationId)
        {
            lock (_notifications)
            {
                var notification = _notifications.Find(n => n.Id == notificationId);
                if (notification != null)
                {
                    notification.IsRead = true;
                }
            }
        }

        public void MarkAllAsRead()
        {
            lock (_notifications)
            {
                _notifications.ForEach(n => n.IsRead = true);
            }
        }

        public void ClearNotifications()
        {
            lock (_notifications)
            {
                _notifications.Clear();
            }
        }

        public void ClearReadNotifications()
        {
            lock (_notifications)
            {
                _notifications.RemoveAll(n => n.IsRead);
            }
        }
    }

    /// <summary>
    /// Windows system notification provider using toast notifications
    /// </summary>
    public class SystemNotificationProvider : IAlertNotificationProvider
    {
        private readonly IChartLogger _logger;
        
        public string ProviderName => "System Notifications";
        public bool IsEnabled { get; set; } = true;

        public SystemNotificationProvider(IChartLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendNotificationAsync(AlertBase alert, string message, AlertContext context)
        {
            if (!IsEnabled) return;

            await Task.Run(() =>
            {
                try
                {
                    // For now, we'll use a simple MessageBox
                    // In a real implementation, you would use Windows Toast notifications
                    // or a third-party notification library
                    
                    var title = GetNotificationTitle(alert);
                    var icon = GetNotificationIcon(alert.Priority);
                    
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        if (alert.Priority >= AlertPriority.High)
                        {
                            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
                        }
                    });

                    _logger.Info($"System notification sent: {message}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to send system notification: {ex.Message}", ex);
                }
            });
        }

        private string GetNotificationTitle(AlertBase alert)
        {
            return alert.Priority switch
            {
                AlertPriority.Critical => "🚨 Critical Alert",
                AlertPriority.High => "⚠️ High Priority Alert",
                AlertPriority.Medium => "📊 Alert",
                AlertPriority.Low => "ℹ️ Alert",
                _ => "📊 Alert"
            };
        }

        private MessageBoxImage GetNotificationIcon(AlertPriority priority)
        {
            return priority switch
            {
                AlertPriority.Critical => MessageBoxImage.Error,
                AlertPriority.High => MessageBoxImage.Warning,
                AlertPriority.Medium => MessageBoxImage.Information,
                AlertPriority.Low => MessageBoxImage.Information,
                _ => MessageBoxImage.Information
            };
        }
    }

    /// <summary>
    /// Audio notification provider for alert sounds
    /// </summary>
    public class AudioNotificationProvider : IAlertNotificationProvider
    {
        private readonly IChartLogger _logger;
        private readonly Dictionary<AlertPriority, string> _soundFiles;
        
        public string ProviderName => "Audio Notifications";
        public bool IsEnabled { get; set; } = true;

        public AudioNotificationProvider(IChartLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Default system sounds - in a real implementation, these would be configurable
            _soundFiles = new Dictionary<AlertPriority, string>
            {
                { AlertPriority.Critical, "SystemHand" },
                { AlertPriority.High, "SystemExclamation" },
                { AlertPriority.Medium, "SystemAsterisk" },
                { AlertPriority.Low, "SystemAsterisk" }
            };
        }

        public async Task SendNotificationAsync(AlertBase alert, string message, AlertContext context)
        {
            if (!IsEnabled) return;

            await Task.Run(() =>
            {
                try
                {
                    // Play system sound based on priority
                    var soundName = _soundFiles.GetValueOrDefault(alert.Priority, "SystemAsterisk");
                    
                    // In a real implementation, you would use:
                    // SystemSounds.Hand.Play() for Critical
                    // SystemSounds.Exclamation.Play() for High
                    // SystemSounds.Asterisk.Play() for Medium/Low
                    
                    // For now, we'll just use a simple beep
                    Console.Beep();
                    
                    _logger.Info($"Audio notification played for {alert.Priority} priority alert");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to play audio notification: {ex.Message}", ex);
                }
            });
        }

        public void SetSoundFile(AlertPriority priority, string soundFile)
        {
            _soundFiles[priority] = soundFile;
        }
    }

    /// <summary>
    /// Email notification provider (placeholder for future implementation)
    /// </summary>
    public class EmailNotificationProvider : IAlertNotificationProvider
    {
        private readonly IChartLogger _logger;
        
        public string ProviderName => "Email Notifications";
        public bool IsEnabled { get; set; } = false; // Disabled by default

        public string SmtpServer { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        public string ToAddress { get; set; } = string.Empty;
        public bool UseSSL { get; set; } = true;

        public EmailNotificationProvider(IChartLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendNotificationAsync(AlertBase alert, string message, AlertContext context)
        {
            if (!IsEnabled || string.IsNullOrEmpty(ToAddress)) return;

            await Task.Run(() =>
            {
                try
                {
                    // Email implementation would go here
                    // Using SMTP client to send emails
                    // This is a placeholder for future implementation
                    
                    _logger.Info($"Email notification would be sent: {message}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to send email notification: {ex.Message}", ex);
                }
            });
        }
    }

    /// <summary>
    /// Log-based notification provider for debugging and audit purposes
    /// </summary>
    public class LogNotificationProvider : IAlertNotificationProvider
    {
        private readonly IChartLogger _logger;
        
        public string ProviderName => "Log Notifications";
        public bool IsEnabled { get; set; } = true;

        public LogNotificationProvider(IChartLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendNotificationAsync(AlertBase alert, string message, AlertContext context)
        {
            if (!IsEnabled) return;

            await Task.Run(() =>
            {
                var logLevel = alert.Priority switch
                {
                    AlertPriority.Critical => "CRITICAL",
                    AlertPriority.High => "WARNING",
                    AlertPriority.Medium => "INFO",
                    AlertPriority.Low => "DEBUG",
                    _ => "INFO"
                };

                var logMessage = $"[ALERT-{alert.AlertType}] {message} | Priority: {alert.Priority} | Symbol: {alert.Symbol} | Price: {context.CurrentPrice:C}";

                // Log based on priority
                switch (alert.Priority)
                {
                    case AlertPriority.Critical:
                        _logger.Critical(logMessage);
                        break;
                    case AlertPriority.High:
                        _logger.Error(logMessage);
                        break;
                    case AlertPriority.Medium:
                        _logger.Info(logMessage);
                        break;
                    case AlertPriority.Low:
                        _logger.Debug(logMessage);
                        break;
                }
            });
        }
    }

    /// <summary>
    /// Alert notification data model
    /// </summary>
    public class AlertNotification
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public AlertBase Alert { get; set; } = null!;
        public string Message { get; set; } = string.Empty;
        public AlertContext Context { get; set; } = null!;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;
        public AlertPriority Priority { get; set; } = AlertPriority.Medium;
    }

    /// <summary>
    /// Event args for notification events
    /// </summary>
    public class AlertNotificationEventArgs : EventArgs
    {
        public AlertNotification Notification { get; }

        public AlertNotificationEventArgs(AlertNotification notification)
        {
            Notification = notification ?? throw new ArgumentNullException(nameof(notification));
        }
    }
}
