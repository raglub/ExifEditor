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

    private enum DragMode { None, DragDot, DragLabel }
    private DragMode _currentDragMode = DragMode.None;
    private ImageTag? _draggedTag;
    private bool _isDragging;
    private Point _dragStartPoint;
    private Ellipse? _dragDot;
    private Control? _dragLabel;
    private Line? _dragLine;
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

        Opened += async (_, _) => await viewModel.InitializeAsync();

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
            var dotX = layout.offsetX + tag.AnchorX * layout.renderedWidth;
            var dotY = layout.offsetY + tag.AnchorY * layout.renderedHeight;
            var labelX = layout.offsetX + (tag.AnchorX + tag.EffectiveLabelOffsetX) * layout.renderedWidth;
            var labelY = layout.offsetY + (tag.AnchorY + tag.EffectiveLabelOffsetY) * layout.renderedHeight;

            var tagMarkerBrush = this.FindResource("TagMarkerBrush") as IBrush ?? new SolidColorBrush(Colors.Orange);
            var tagMarkerStrokeBrush = this.FindResource("TagMarkerStrokeBrush") as IBrush ?? new SolidColorBrush(Colors.White);
            var tagLabelBgBrush = this.FindResource("TagLabelBgBrush") as IBrush ?? new SolidColorBrush(Color.FromArgb(200, 0, 0, 0));

            // Scale sizes inversely with zoom so they stay readable
            var markerSize = 8.0 / _zoom;
            var hitAreaSize = 20.0 / _zoom;
            var fontSize = 10.0 / _zoom;
            var labelPadH = 4.0 / _zoom;
            var labelPadV = 2.0 / _zoom;

            // 1. Connector line — single white stroke with slight opacity so it
            //    stays readable on both light and dark backgrounds.
            var line = new Line
            {
                StartPoint = new Point(dotX, dotY),
                EndPoint = new Point(labelX, labelY), // updated below after label measure
                Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                StrokeThickness = 1.5 / _zoom,
                StrokeLineCap = PenLineCap.Round,
                IsHitTestVisible = false,
                IsVisible = _showAllTags
            };
            canvas.Children.Add(line);

            // 2. Dot marker
            var dot = new Ellipse
            {
                Width = markerSize,
                Height = markerSize,
                Fill = tagMarkerBrush,
                Stroke = tagMarkerStrokeBrush,
                StrokeThickness = 1.0 / _zoom,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dot, dotX - markerSize / 2);
            Canvas.SetTop(dot, dotY - markerSize / 2);
            canvas.Children.Add(dot);

            // 3. Label (directly positioned on canvas)
            var label = new Border
            {
                Background = tagLabelBgBrush,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1.5 / _zoom),
                CornerRadius = new CornerRadius(8.0 / _zoom),
                Padding = new Thickness(labelPadH + 2.0 / _zoom, labelPadV + 1.0 / _zoom),
                Child = new TextBlock
                {
                    Text = tag.Label,
                    Foreground = Brushes.White,
                    FontSize = fontSize
                },
                IsHitTestVisible = true,
                Cursor = new Cursor(StandardCursorType.Hand),
                IsVisible = _showAllTags
            };
            Canvas.SetLeft(label, labelX);
            Canvas.SetTop(label, labelY);
            canvas.Children.Add(label);

            // Measure label so we can pick the connector attachment point.
            label.Measure(Size.Infinity);
            var labelWidth = label.DesiredSize.Width;
            var labelHeight = label.DesiredSize.Height;
            var (initialEnd, initialLineNeeded) = ComputeConnectorEnd(
                dotX, dotY, labelX, labelY, labelWidth, labelHeight);
            line.EndPoint = initialEnd;
            if (!initialLineNeeded) line.IsVisible = false;

            // 4. Dot hit area (for dragging the dot)
            var dotHitArea = new Ellipse
            {
                Width = hitAreaSize,
                Height = hitAreaSize,
                Fill = Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.SizeAll)
            };
            Canvas.SetLeft(dotHitArea, dotX - hitAreaSize / 2);
            Canvas.SetTop(dotHitArea, dotY - hitAreaSize / 2);

            // Capture variables for closures
            var capturedTag = tag;
            var capturedDot = dot;
            var capturedLabel = label;
            var capturedLine = line;
            var capturedLineNeeded = initialLineNeeded;
            var capturedDotHitArea = dotHitArea;

            // --- Dot drag handlers ---
            dotHitArea.PointerPressed += (s, e) =>
            {
                if (!e.GetCurrentPoint(panel).Properties.IsLeftButtonPressed) return;
                _draggedTag = capturedTag;
                _currentDragMode = DragMode.DragDot;
                _isDragging = false;
                _dragStartPoint = ScreenToContent(e.GetPosition(panel));
                _dragDot = capturedDot;
                _dragLabel = capturedLabel;
                _dragLine = capturedLine;
                _dragHitArea = capturedDotHitArea;
                e.Pointer.Capture(dotHitArea);
                e.Handled = true;
            };

            dotHitArea.PointerMoved += (s, e) =>
            {
                if (_draggedTag != capturedTag || _currentDragMode != DragMode.DragDot) return;

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

                    var newDotPx = currentLayout.offsetX + relX * currentLayout.renderedWidth;
                    var newDotPy = currentLayout.offsetY + relY * currentLayout.renderedHeight;
                    var newLabelPx = currentLayout.offsetX + (relX + capturedTag.EffectiveLabelOffsetX) * currentLayout.renderedWidth;
                    var newLabelPy = currentLayout.offsetY + (relY + capturedTag.EffectiveLabelOffsetY) * currentLayout.renderedHeight;

                    Canvas.SetLeft(_dragDot!, newDotPx - markerSize / 2);
                    Canvas.SetTop(_dragDot!, newDotPy - markerSize / 2);
                    Canvas.SetLeft(_dragLabel!, newLabelPx);
                    Canvas.SetTop(_dragLabel!, newLabelPy);
                    Canvas.SetLeft(_dragHitArea!, newDotPx - hitAreaSize / 2);
                    Canvas.SetTop(_dragHitArea!, newDotPy - hitAreaSize / 2);
                    var dragLabelW = _dragLabel!.DesiredSize.Width;
                    var dragLabelH = _dragLabel!.DesiredSize.Height;
                    var (dragEnd, dragVisible) = ComputeConnectorEnd(
                        newDotPx, newDotPy, newLabelPx, newLabelPy, dragLabelW, dragLabelH);
                    _dragLine!.StartPoint = new Point(newDotPx, newDotPy);
                    _dragLine!.EndPoint = dragEnd;
                    _dragLine!.IsVisible = dragVisible;
                }
            };

            dotHitArea.PointerReleased += (s, e) =>
            {
                if (_draggedTag == capturedTag && _currentDragMode == DragMode.DragDot)
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
                    ResetDragState();
                }
                e.Pointer.Capture(null);
                e.Handled = true;
            };

            // --- Label drag handlers ---
            label.PointerPressed += (s, e) =>
            {
                if (!e.GetCurrentPoint(panel).Properties.IsLeftButtonPressed) return;
                _draggedTag = capturedTag;
                _currentDragMode = DragMode.DragLabel;
                _isDragging = false;
                _dragStartPoint = ScreenToContent(e.GetPosition(panel));
                _dragLabel = capturedLabel;
                _dragLine = capturedLine;
                e.Pointer.Capture(label);
                e.Handled = true;
            };

            label.PointerMoved += (s, e) =>
            {
                if (_draggedTag != capturedTag || _currentDragMode != DragMode.DragLabel) return;

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

                    Canvas.SetLeft(_dragLabel!, currentPoint.X);
                    Canvas.SetTop(_dragLabel!, currentPoint.Y);

                    var dragLabelW = _dragLabel!.DesiredSize.Width;
                    var dragLabelH = _dragLabel!.DesiredSize.Height;
                    var (dragEnd, dragVisible) = ComputeConnectorEnd(
                        dotX, dotY, currentPoint.X, currentPoint.Y, dragLabelW, dragLabelH);
                    _dragLine!.EndPoint = dragEnd;
                    _dragLine!.IsVisible = dragVisible;
                }
            };

            label.PointerReleased += (s, e) =>
            {
                if (_draggedTag == capturedTag && _currentDragMode == DragMode.DragLabel)
                {
                    if (_isDragging)
                    {
                        var currentPoint = ScreenToContent(e.GetPosition(panel));
                        var currentLayout = GetImageLayout();
                        if (currentLayout.isValid)
                        {
                            var labelRelX = (currentPoint.X - currentLayout.offsetX) / currentLayout.renderedWidth;
                            var labelRelY = (currentPoint.Y - currentLayout.offsetY) / currentLayout.renderedHeight;
                            var offsetX = labelRelX - capturedTag.AnchorX;
                            var offsetY = labelRelY - capturedTag.AnchorY;
                            viewModel.MoveLabelOffset(capturedTag, offsetX, offsetY);
                        }
                    }
                    ResetDragState();
                }
                e.Pointer.Capture(null);
                e.Handled = true;
            };

            // Show/hide on hover when "Show all tags" is unchecked
            dotHitArea.PointerEntered += (s, e) =>
            {
                if (!_showAllTags)
                {
                    capturedLabel.IsVisible = true;
                    capturedLine.IsVisible = capturedLineNeeded;
                }
            };
            dotHitArea.PointerExited += (s, e) =>
            {
                if (!_showAllTags && !_isDragging)
                {
                    capturedLabel.IsVisible = false;
                    capturedLine.IsVisible = false;
                }
            };

            canvas.Children.Add(dotHitArea);
        }
    }

    private static (Point endPoint, bool visible) ComputeConnectorEnd(
        double dotX, double dotY,
        double labelLeft, double labelTop, double labelWidth, double labelHeight)
    {
        var labelRight = labelLeft + labelWidth;
        var labelBottom = labelTop + labelHeight;
        var midX = labelLeft + labelWidth / 2;
        var midY = labelTop + labelHeight / 2;

        // Dot is inside the label bounds → no connector at all.
        if (dotX >= labelLeft && dotX <= labelRight && dotY >= labelTop && dotY <= labelBottom)
            return (new Point(midX, midY), false);

        // Label is fully to the right of the dot → attach to its left edge center.
        if (dotX < labelLeft)
            return (new Point(labelLeft, midY), true);

        // Label is fully to the left of the dot → attach to its right edge center.
        if (dotX > labelRight)
            return (new Point(labelRight, midY), true);

        // Dot is horizontally between the label edges → keep the connector
        // strictly vertical by anchoring it at the dot's X coordinate.
        if (dotY < labelTop)
            return (new Point(dotX, labelTop), true);

        return (new Point(dotX, labelBottom), true);
    }

    private void ResetDragState()
    {
        _draggedTag = null;
        _currentDragMode = DragMode.None;
        _isDragging = false;
        _dragDot = null;
        _dragLabel = null;
        _dragLine = null;
        _dragHitArea = null;
    }
}
