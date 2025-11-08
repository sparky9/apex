using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;

namespace ApexV2.UI.Layout;

public partial class PanelHostControl : System.Windows.Controls.UserControl
{
    private System.Windows.Point _dragStart;
    private bool _dragging;
    private FrameworkElement? _activeElement;
    private bool _resizing;
    private System.Windows.Point _resizeStart;
    private double _origWidth;
    private double _origHeight;

    private LayoutPanelModel? _activePanelModel;
    public LayoutPanelModel? ActivePanel => _activePanelModel;

    public event EventHandler? LayoutChanged; // fire on move/resize
    private System.Timers.Timer? _debounceTimer;
    public event EventHandler? LayoutChangedDebounced; // fires after inactivity
    public int AutoSaveDebounceMs { get; set; } = 800;

    public int GridSize { get; set; } = 10; // snap size

    public PanelHostControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += (_, __) => ClampAllPanelsToBounds();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Ensure existing layout panels are rendered (in case layout set before load)
        if (Layout != null && PART_Canvas.Children.Count == 0)
        {
            RenderPanels();
        }
        ClampAllPanelsToBounds();
    }

    public WorkspaceLayoutModel? Layout { get; private set; }

    public void SetLayout(WorkspaceLayoutModel layout)
    {
        Layout = layout;
        RenderPanels();
    }

    public LayoutPanelModel AddPanel(string type, double? x = null, double? y = null, double width = 400, double height = 300)
    {
        if (Layout == null)
        {
            Layout = new WorkspaceLayoutModel();
        }
        // simple placement strategy: cascade
        var offset = Layout.Panels.Count * 30;
        var model = new LayoutPanelModel
        {
            Type = type,
            X = x ?? (50 + offset) % Math.Max(ActualWidth - 450, 800),
            Y = y ?? (50 + offset) % Math.Max(ActualHeight - 350, 600),
            Width = width,
            Height = height
        };
        model.ZIndex = (Layout.Panels.Count == 0) ? 0 : Layout.Panels.Max(p => p.ZIndex) + 1;
        Layout.Panels.Add(model);
        var element = CreatePanelElement(model);
        PART_Canvas.Children.Add(element);
        Canvas.SetZIndex(element, model.ZIndex);
        LayoutChanged?.Invoke(this, EventArgs.Empty);
        QueueDebouncedChange();
        return model;
    }

    public bool RemovePanel(string id)
    {
        if (Layout == null) return false;
        var model = Layout.Panels.FirstOrDefault(p => p.Id == id);
        if (model == null) return false;
        var ui = PART_Canvas.Children.OfType<FrameworkElement>().FirstOrDefault(fe => fe.Tag == model);
        if (ui != null) PART_Canvas.Children.Remove(ui);
        Layout.Panels.Remove(model);
        LayoutChanged?.Invoke(this, EventArgs.Empty);
        QueueDebouncedChange();
        return true;
    }

    private void BringToFront(LayoutPanelModel panel, FrameworkElement element)
    {
        if (Layout == null) return;
        var max = Layout.Panels.Count == 0 ? 0 : Layout.Panels.Max(p => p.ZIndex);
        panel.ZIndex = max + 1;
        Canvas.SetZIndex(element, panel.ZIndex);
    }

    private FrameworkElement CreatePanelElement(LayoutPanelModel p)
    {
        var border = new Border
        {
            Background = (System.Windows.Media.Brush)FindResource("Brush.PanelBackground"),
            BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Border"),
            BorderThickness = new Thickness(1),
            Width = p.Width,
            Height = p.Height,
            Tag = p,
            Child = new Grid
            {
                Children =
                {
                    new TextBlock { Text = p.Type + " panel", Margin = new Thickness(4), Foreground = (System.Windows.Media.Brush)FindResource("Brush.TextPrimary") },
                    new Thumb { Width=10, Height=10, HorizontalAlignment=System.Windows.HorizontalAlignment.Right, VerticalAlignment=System.Windows.VerticalAlignment.Bottom, Cursor=System.Windows.Input.Cursors.SizeNWSE, Tag="RESIZE" }
                }
            }
        };
        Canvas.SetLeft(border, p.X);
        Canvas.SetTop(border, p.Y);
        var ctx = new ContextMenu();
        var closeItem = new MenuItem { Header = "Close Panel" };
        closeItem.Click += (_, __) => { RemovePanel(p.Id); };
        ctx.Items.Add(closeItem);
        border.ContextMenu = ctx;
        border.MouseLeftButtonDown += Panel_MouseLeftButtonDown;
        border.MouseMove += Panel_MouseMove;
        border.MouseLeftButtonUp += Panel_MouseLeftButtonUp;
        if (border.Child is Grid g)
        {
            foreach (var child in g.Children)
            {
                if (child is Thumb thumb && Equals(thumb.Tag, "RESIZE"))
                {
                    thumb.DragStarted += Resize_DragStarted;
                    thumb.DragDelta += Resize_DragDelta;
                    thumb.DragCompleted += Resize_DragCompleted;
                }
            }
        }
        return border;
    }

    private void RenderPanels()
    {
        if (Layout == null) return;
        PART_Canvas.Children.Clear();
        foreach (var p in Layout.Panels)
        {
            var border = CreatePanelElement(p);
            PART_Canvas.Children.Add(border);
            Canvas.SetZIndex(border, p.ZIndex);
        }
    }

    private double Snap(double v) => GridSize <= 1 ? v : Math.Round(v / GridSize) * GridSize;

    private void Panel_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_resizing || !_dragging || _activeElement == null) return;
        var pos = e.GetPosition(PART_Canvas);
        var dx = pos.X - _dragStart.X;
        var dy = pos.Y - _dragStart.Y;
        var panel = (LayoutPanelModel?)_activeElement.Tag;
        if (panel != null)
        {
            panel.X = Snap(Math.Max(0, Canvas.GetLeft(_activeElement) + dx));
            panel.Y = Snap(Math.Max(0, Canvas.GetTop(_activeElement) + dy));
            Canvas.SetLeft(_activeElement, panel.X);
            Canvas.SetTop(_activeElement, panel.Y);
            LayoutChanged?.Invoke(this, EventArgs.Empty);
            QueueDebouncedChange();
        }
        _dragStart = pos;
    }

    // Drag handlers
    private void Panel_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _activeElement = sender as FrameworkElement;
        var panel = (LayoutPanelModel?)_activeElement?.Tag;
        if (panel != null)
        {
            _activePanelModel = panel;
            BringToFront(panel, _activeElement!);
        }
        _dragStart = e.GetPosition(PART_Canvas);
        _dragging = true;
        _activeElement?.CaptureMouse();
    }

    private void Panel_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_activeElement != null)
            _activeElement.ReleaseMouseCapture();
        _dragging = false;
        _activeElement = null;
    }

    private void Resize_DragStarted(object sender, DragStartedEventArgs e)
    {
        _resizing = true;
        if (sender is FrameworkElement fe)
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(fe);
            // Thumb inside Grid inside Border -> climb until Border or null
            while (parent != null && parent is not Border)
            {
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            if (parent is Border b)
            {
                _activeElement = b;
                _resizeStart = System.Windows.Input.Mouse.GetPosition(PART_Canvas);
                _origWidth = b.Width;
                _origHeight = b.Height;
            }
        }
    }

    private void Resize_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_resizing || _activeElement == null) return;
        var panel = (LayoutPanelModel?)_activeElement.Tag;
        if (panel == null) return;
        var pos = System.Windows.Input.Mouse.GetPosition(PART_Canvas);
        var dw = pos.X - _resizeStart.X;
        var dh = pos.Y - _resizeStart.Y;
        panel.Width = Snap(Math.Max(150, _origWidth + dw));
        panel.Height = Snap(Math.Max(100, _origHeight + dh));
        _activeElement.Width = panel.Width;
        _activeElement.Height = panel.Height;
        ClampPanelToBounds(panel);
        LayoutChanged?.Invoke(this, EventArgs.Empty);
        QueueDebouncedChange();
    }

    private void Resize_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _resizing = false;
        _activeElement = null;
    }

    private void ClampAllPanelsToBounds()
    {
        if (Layout == null) return;
        foreach (var p in Layout.Panels) ClampPanelToBounds(p);
        foreach (var child in PART_Canvas.Children.OfType<FrameworkElement>())
        {
            var p = (LayoutPanelModel?)child.Tag; if (p == null) continue;
            Canvas.SetLeft(child, p.X);
            Canvas.SetTop(child, p.Y);
            child.Width = p.Width; child.Height = p.Height;
        }
    }

    private void ClampPanelToBounds(LayoutPanelModel p)
    {
        double maxW = Math.Max(50, ActualWidth - 10);
        double maxH = Math.Max(50, ActualHeight - 10);
        if (p.X + p.Width > maxW) p.X = Math.Max(0, maxW - p.Width);
        if (p.Y + p.Height > maxH) p.Y = Math.Max(0, maxH - p.Height);
        if (p.Width > maxW) p.Width = maxW;
        if (p.Height > maxH) p.Height = maxH;
    }

    private void QueueDebouncedChange()
    {
        _debounceTimer?.Stop();
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Timers.Timer(AutoSaveDebounceMs) { AutoReset = false };
        _debounceTimer.Elapsed += (_, __) => Dispatcher.Invoke(() => LayoutChangedDebounced?.Invoke(this, EventArgs.Empty));
        _debounceTimer.Start();
    }
}
