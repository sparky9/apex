using System.Windows;
using System.Windows.Controls;
using ApexV2.Core.Logging;
using Microsoft.Win32;
using System.IO;

namespace ApexV2.Windows;

public partial class LogViewerWindow : Window
{
    private readonly InMemoryLogSink? _sink;

    public LogViewerWindow()
    {
        InitializeComponent();
        _sink = App.LogManager.GetSink<InMemoryLogSink>();
        RefreshList();
    }

    private LogLevel? SelectedLevel
    {
        get
        {
            if (LevelFilter.SelectedItem is ComboBoxItem item)
            {
                var text = item.Content?.ToString();
                if (text == "All") return null;
                if (Enum.TryParse<LogLevel>(text, true, out var lvl)) return lvl;
            }
            return null;
        }
    }

    private void RefreshList()
    {
        if (_sink == null) return;
        var level = SelectedLevel;
        var search = SearchBox.Text?.Trim() ?? string.Empty;
        var items = _sink.Events
            .Where(e => (level == null || e.Level >= level) &&
                        (string.IsNullOrEmpty(search) || e.Message.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(e => e.TimestampUtc)
            .ToList();
        LogList.ItemsSource = items;
        if (items.Count > 0) LogList.ScrollIntoView(items[^1]);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshList();

    private void CopySelected_Click(object sender, RoutedEventArgs e)
    {
        if (LogList.SelectedItems.Count == 0) return;
        var lines = LogList.SelectedItems.Cast<LogEvent>().Select(e => e.ToString());
        System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, lines));
    }

    private void ExportVisible_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Log Export|*.log|All Files|*.*",
            FileName = $"apex_visible_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log"
        };
        if (dialog.ShowDialog() == true && LogList.Items.Count > 0)
        {
            var lines = LogList.Items.Cast<LogEvent>().Select(e => e.ToString());
            System.IO.File.WriteAllLines(dialog.FileName, lines);
            System.Windows.MessageBox.Show("Export complete", "Logs", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshList();

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        RefreshList();
    }
}
