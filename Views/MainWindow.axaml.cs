using System;
using System.Collections.Specialized;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ExifEditor.Models;
using ExifEditor.ViewModels;

namespace ExifEditor.Views;

public partial class MainWindow : Window
{
    private INotifyCollectionChanged? _subscribedTags;
    private bool _showAllTags = true;

    private ImageTag? _draggedTag;
    private bool _isDragging;
    private Point _dragStartPoint;
    private Ellipse? _dragDot;
    private Border? _dragLabel;
    private Ellipse? _dragHitArea;

    // Zoom & pan state
    private double _zoom = 1.0;
    private double _panX = 0;
    private double _panY = 0;
    private readonly ScaleTransform _scaleTransform = new(1, 1);
    private readonly TranslateTransform _translateTransform = new(0, 0);
    private bool _isPanning;
    private Point _panStart;
    private double _panStartX;
    private double _panStartY;

    private const double MinZoom = 1.0;
    private const double MaxZoom = 5.0;
    private const double ZoomStep = 1.15;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "icon.png");
        if (File.Exists(iconPath))
            Icon = new WindowIcon(iconPath);

        var showAllTagsCheckBox = this.FindControl<CheckBox>("ShowAllTagsCheckBox");
        if (showAllTagsCheckBox != null)
        {
            showAllTagsCheckBox.Click += (s, e) =>
            {
                _showAllTags = showAllTagsCheckBox.IsChecked ?? true;
                UpdateTagMarkers();
            };
        }

        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.SelectedImage))
            {
                ResetZoom();
                SubscribeToTagChanges();
                UpdateTagMarkers();
            }
        };

        var imageTagPanel = this.FindControl<Panel>("ImageTagPanel");
        if (imageTagPanel != null)
        {
            imageTagPanel.SizeChanged += (s, e) => UpdateTagMarkers();
        }

        SetupZoomTransform();
    }

    private void SetupZoomTransform()
    {
        var zoomableContent = this.FindControl<Panel>("ZoomableContent");
        if (zoomableContent == null) return;

        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(_scaleTransform);
        transformGroup.Children.Add(_translateTransform);
        zoomableContent.RenderTransform = transformGroup;
        zoomableContent.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
    }

    private void ResetZoom()
    {
        _zoom = 1.0;
        _panX = 0;
        _panY = 0;
        ApplyZoomTransform();
    }

    private void ApplyZoomTransform()
    {
        _scaleTransform.ScaleX = _zoom;
        _scaleTransform.ScaleY = _zoom;
        _translateTransform.X = _panX;
        _translateTransform.Y = _panY;

        var indicator = this.FindControl<Border>("ZoomIndicator");
        var zoomText = this.FindControl<TextBlock>("ZoomText");
        if (indicator != null && zoomText != null)
        {
            indicator.IsVisible = _zoom > 1.01;
            zoomText.Text = $"{_zoom:F0}x";
        }
    }

    /// <summary>
    /// Converts a point in the outer panel coordinate space
    /// to the inner (unzoomed) content coordinate space.
    /// </summary>
    private Point ScreenToContent(Point screenPoint)
    {
        var x = (screenPoint.X - _panX) / _zoom;
        var y = (screenPoint.Y - _panY) / _zoom;
        return new Point(x, y);
    }

    private void ClampPan()
    {
        var panel = this.FindControl<Panel>("ImageTagPanel");
        if (panel == null) return;

        var w = panel.Bounds.Width;
        var h = panel.Bounds.Height;

        var maxPanX = w * (_zoom - 1);
        var maxPanY = h * (_zoom - 1);

        _panX = Math.Clamp(_panX, -maxPanX, 0);
        _panY = Math.Clamp(_panY, -maxPanY, 0);
    }

    private void OnImagePointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var panel = sender as Panel;
        if (panel == null) return;

        var cursorPos = e.GetPosition(panel);
        var oldZoom = _zoom;

        if (e.Delta.Y > 0)
            _zoom = Math.Min(_zoom * ZoomStep, MaxZoom);
        else
            _zoom = Math.Max(_zoom / ZoomStep, MinZoom);

        // Zoom toward cursor position
        var zoomRatio = _zoom / oldZoom;
        _panX = cursorPos.X - zoomRatio * (cursorPos.X - _panX);
        _panY = cursorPos.Y - zoomRatio * (cursorPos.Y - _panY);

        ClampPan();
        ApplyZoomTransform();
        UpdateTagMarkers();
        e.Handled = true;
    }

    private void SubscribeToTagChanges()
    {
        if (_subscribedTags != null)
            _subscribedTags.CollectionChanged -= OnTagsCollectionChanged;

        var selectedImage = (DataContext as MainWindowViewModel)?.SelectedImage;
        _subscribedTags = selectedImage?.Tags;

        if (_subscribedTags != null)
            _subscribedTags.CollectionChanged += OnTagsCollectionChanged;
    }

    private void OnTagsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateTagMarkers();
    }

    private (double offsetX, double offsetY, double renderedWidth, double renderedHeight, bool isValid) GetImageLayout()
    {
        var panel = this.FindControl<Panel>("ImageTagPanel");
        var image = this.FindControl<Image>("PreviewImage");
        if (panel == null || image == null) return (0, 0, 0, 0, false);

        var bitmap = image.Source as Bitmap;
        if (bitmap == null) return (0, 0, 0, 0, false);

        var controlWidth = panel.Bounds.Width;
        var controlHeight = panel.Bounds.Height;
        if (controlWidth <= 0 || controlHeight <= 0) return (0, 0, 0, 0, false);

        var imageAspect = (double)bitmap.PixelSize.Width / bitmap.PixelSize.Height;
        var controlAspect = controlWidth / controlHeight;

        double renderedWidth, renderedHeight;
        if (imageAspect > controlAspect)
        {
            renderedWidth = controlWidth;
            renderedHeight = controlWidth / imageAspect;
        }
        else
        {
            renderedHeight = controlHeight;
            renderedWidth = controlHeight * imageAspect;
        }

        var offsetX = (controlWidth - renderedWidth) / 2;
        var offsetY = (controlHeight - renderedHeight) / 2;

        return (offsetX, offsetY, renderedWidth, renderedHeight, true);
    }

    private void OnImagePointerMoved(object? sender, PointerEventArgs e)
    {
        var panel = sender as Panel;
        if (panel == null) return;

        // Handle panning
        if (_isPanning)
        {
            var currentPoint = e.GetPosition(panel);
            _panX = _panStartX + (currentPoint.X - _panStart.X);
            _panY = _panStartY + (currentPoint.Y - _panStart.Y);
            ClampPan();
            ApplyZoomTransform();
            UpdateTagMarkers();
            return;
        }

        if (_isDragging) return;

        var layout = GetImageLayout();
        if (!layout.isValid)
        {
            panel.Cursor = Cursor.Default;
            return;
        }

        var point = ScreenToContent(e.GetPosition(panel));
        var relX = (point.X - layout.offsetX) / layout.renderedWidth;
        var relY = (point.Y - layout.offsetY) / layout.renderedHeight;

        panel.Cursor = (relX >= 0 && relX <= 1 && relY >= 0 && relY <= 1)
            ? new Cursor(StandardCursorType.Cross)
            : Cursor.Default;
    }

    private async void OnImagePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var panel = sender as Panel;
        var image = this.FindControl<Image>("PreviewImage");
        var viewModel = (DataContext as MainWindowViewModel)?.SelectedImage;
        if (panel == null || image == null || viewModel == null) return;

        var props = e.GetCurrentPoint(panel).Properties;

        // Right-click: start panning
        if (props.IsRightButtonPressed && _zoom > 1.01)
        {
            _isPanning = true;
            _panStart = e.GetPosition(panel);
            _panStartX = _panX;
            _panStartY = _panY;
            e.Pointer.Capture(panel);
            e.Handled = true;
            return;
        }

        if (!props.IsLeftButtonPressed) return;

        var layout = GetImageLayout();
        if (!layout.isValid) return;

        var point = ScreenToContent(e.GetPosition(panel));
        var relX = (point.X - layout.offsetX) / layout.renderedWidth;
        var relY = (point.Y - layout.offsetY) / layout.renderedHeight;

        if (relX < 0 || relX > 1 || relY < 0 || relY > 1) return;

        // Release pointer capture before showing dialog to prevent
        // the panel from "eating" the first click after dialog closes
        e.Pointer.Capture(null);

        var dialog = new AddTagWindow();
        await dialog.ShowDialog(this);

        if (!string.IsNullOrWhiteSpace(dialog.TagLabel))
        {
            viewModel.AddTag(Math.Round(relX, 3), Math.Round(relY, 3), dialog.TagLabel);
        }
    }

    private void OnImagePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void UpdateTagMarkers()
    {
        var canvas = this.FindControl<Canvas>("TagCanvas");
        var panel = this.FindControl<Panel>("ImageTagPanel");
        var image = this.FindControl<Image>("PreviewImage");
        if (canvas == null || panel == null || image == null) return;

        canvas.Children.Clear();

        var viewModel = (DataContext as MainWindowViewModel)?.SelectedImage;
        if (viewModel == null) return;

        var layout = GetImageLayout();
        if (!layout.isValid) return;

        foreach (var tag in viewModel.Tags)
        {
            var x = layout.offsetX + tag.X * layout.renderedWidth;
            var y = layout.offsetY + tag.Y * layout.renderedHeight;

            var tagMarkerBrush = this.FindResource("TagMarkerBrush") as IBrush ?? new SolidColorBrush(Colors.Orange);
            var tagMarkerStrokeBrush = this.FindResource("TagMarkerStrokeBrush") as IBrush ?? new SolidColorBrush(Colors.White);
            var tagLabelBgBrush = this.FindResource("TagLabelBgBrush") as IBrush ?? new SolidColorBrush(Color.FromArgb(200, 0, 0, 0));

            // Scale marker sizes inversely with zoom so they stay readable
            var markerSize = 8.0 / _zoom;
            var hitAreaSize = 20.0 / _zoom;
            var fontSize = 10.0 / _zoom;
            var labelPadH = 4.0 / _zoom;
            var labelPadV = 2.0 / _zoom;

            var dot = new Ellipse
            {
                Width = markerSize,
                Height = markerSize,
                Fill = tagMarkerBrush,
                Stroke = tagMarkerStrokeBrush,
                StrokeThickness = 1.0 / _zoom,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dot, x - markerSize / 2);
            Canvas.SetTop(dot, y - markerSize / 2);
            canvas.Children.Add(dot);

            var label = new Border
            {
                Background = tagLabelBgBrush,
                CornerRadius = new CornerRadius(4.0 / _zoom),
                Padding = new Thickness(labelPadH, labelPadV),
                Child = new TextBlock
                {
                    Text = tag.Label,
                    Foreground = Brushes.White,
                    FontSize = fontSize
                },
                IsVisible = _showAllTags,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, x + 6.0 / _zoom);
            Canvas.SetTop(label, y - 10.0 / _zoom);
            canvas.Children.Add(label);

            var capturedTag = tag;
            var capturedDot = dot;
            var capturedLabel = label;

            var hitArea = new Ellipse
            {
                Width = hitAreaSize,
                Height = hitAreaSize,
                Fill = Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.SizeAll)
            };
            Canvas.SetLeft(hitArea, x - hitAreaSize / 2);
            Canvas.SetTop(hitArea, y - hitAreaSize / 2);

            hitArea.PointerPressed += (s, e) =>
            {
                if (!e.GetCurrentPoint(panel).Properties.IsLeftButtonPressed) return;
                _draggedTag = capturedTag;
                _isDragging = false;
                _dragStartPoint = ScreenToContent(e.GetPosition(panel));
                _dragDot = capturedDot;
                _dragLabel = capturedLabel;
                _dragHitArea = hitArea;
                e.Pointer.Capture(hitArea);
                e.Handled = true;
            };

            hitArea.PointerMoved += (s, e) =>
            {
                if (_draggedTag != capturedTag) return;

                var currentPoint = ScreenToContent(e.GetPosition(panel));
                if (!_isDragging)
                {
                    var dx = currentPoint.X - _dragStartPoint.X;
                    var dy = currentPoint.Y - _dragStartPoint.Y;
                    if (Math.Sqrt(dx * dx + dy * dy) > 3)
                        _isDragging = true;
                }

                if (_isDragging)
                {
                    var currentLayout = GetImageLayout();
                    if (!currentLayout.isValid) return;

                    var relX = Math.Clamp((currentPoint.X - currentLayout.offsetX) / currentLayout.renderedWidth, 0, 1);
                    var relY = Math.Clamp((currentPoint.Y - currentLayout.offsetY) / currentLayout.renderedHeight, 0, 1);

                    var px = currentLayout.offsetX + relX * currentLayout.renderedWidth;
                    var py = currentLayout.offsetY + relY * currentLayout.renderedHeight;

                    Canvas.SetLeft(_dragDot!, px - markerSize / 2);
                    Canvas.SetTop(_dragDot!, py - markerSize / 2);
                    Canvas.SetLeft(_dragLabel!, px + 6.0 / _zoom);
                    Canvas.SetTop(_dragLabel!, py - 10.0 / _zoom);
                    Canvas.SetLeft(_dragHitArea!, px - hitAreaSize / 2);
                    Canvas.SetTop(_dragHitArea!, py - hitAreaSize / 2);
                }
            };

            hitArea.PointerReleased += (s, e) =>
            {
                if (_draggedTag == capturedTag)
                {
                    if (_isDragging)
                    {
                        var currentPoint = ScreenToContent(e.GetPosition(panel));
                        var currentLayout = GetImageLayout();
                        if (currentLayout.isValid)
                        {
                            var relX = Math.Clamp((currentPoint.X - currentLayout.offsetX) / currentLayout.renderedWidth, 0, 1);
                            var relY = Math.Clamp((currentPoint.Y - currentLayout.offsetY) / currentLayout.renderedHeight, 0, 1);
                            viewModel.MoveTag(capturedTag, Math.Round(relX, 3), Math.Round(relY, 3));
                        }
                    }
                    _draggedTag = null;
                    _isDragging = false;
                    _dragDot = null;
                    _dragLabel = null;
                    _dragHitArea = null;
                }
                e.Pointer.Capture(null);
                e.Handled = true;
            };

            hitArea.PointerEntered += (s, e) => { if (!_showAllTags) label.IsVisible = true; };
            hitArea.PointerExited += (s, e) => { if (!_showAllTags && !_isDragging) label.IsVisible = false; };
            canvas.Children.Add(hitArea);
        }
    }
}
