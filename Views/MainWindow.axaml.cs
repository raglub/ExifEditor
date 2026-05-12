using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ExifEditor.Models;
using ExifEditor.Services;
using ExifEditor.ViewModels;

namespace ExifEditor.Views;

public partial class MainWindow : Window
{
    private INotifyCollectionChanged? _subscribedTags;
    private ImageViewModel? _subscribedImage;

    private enum TagDisplayMode { Names, Numbers, Hidden }
    private TagDisplayMode _tagMode = TagDisplayMode.Names;

    private enum DragMode { None, DragDot, DragLabel }
    private DragMode _currentDragMode = DragMode.None;
    private ImageTag? _draggedTag;
    private bool _isDragging;
    private Point _dragStartPoint;
    private Control? _dragDot;
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

    public MainWindow() : this(null!) {}

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Opened += (_, _) =>
        {
            // Defer loading until after the first paint so the window appears immediately.
            Avalonia.Threading.Dispatcher.UIThread.Post(
                async () => await viewModel.InitializeAsync(),
                Avalonia.Threading.DispatcherPriority.Background);
        };

        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "icon.png");
        if (File.Exists(iconPath))
            Icon = new WindowIcon(iconPath);

        var namesRadio = this.FindControl<RadioButton>("TagModeNames");
        var numbersRadio = this.FindControl<RadioButton>("TagModeNumbers");
        var hiddenRadio = this.FindControl<RadioButton>("TagModeHidden");
        if (namesRadio != null && numbersRadio != null && hiddenRadio != null)
        {
            namesRadio.IsCheckedChanged += (s, e) =>
            {
                if (namesRadio.IsChecked == true) { _tagMode = TagDisplayMode.Names; UpdateTagMarkers(); }
            };
            numbersRadio.IsCheckedChanged += (s, e) =>
            {
                if (numbersRadio.IsChecked == true) { _tagMode = TagDisplayMode.Numbers; UpdateTagMarkers(); }
            };
            hiddenRadio.IsCheckedChanged += (s, e) =>
            {
                if (hiddenRadio.IsChecked == true) { _tagMode = TagDisplayMode.Hidden; UpdateTagMarkers(); }
            };
        }

        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.SelectedImage))
            {
                ResetZoom();
                SubscribeToTagChanges();
                SubscribeToImageChanges();
                UpdateTagMarkers();
            }
        };

        var imageTagPanel = this.FindControl<Panel>("ImageTagPanel");
        if (imageTagPanel != null)
        {
            imageTagPanel.SizeChanged += (s, e) => UpdateTagMarkers();
        }

        var imagesList = this.FindControl<ListBox>("ImagesList");
        if (imagesList != null)
        {
            imagesList.SelectionChanged += (s, e) => CenterSelectedItem(imagesList);
        }

        SetupZoomTransform();
    }

    private void CenterSelectedItem(ListBox list)
    {
        if (list.SelectedItem == null) return;
        list.ScrollIntoView(list.SelectedItem);

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (list.ContainerFromItem(list.SelectedItem) is not Control container) return;
            if (list.Scroll is not ScrollViewer scroll) return;

            var pt = container.TranslatePoint(new Point(0, container.Bounds.Height / 2), scroll);
            if (pt is null) return;

            var delta = pt.Value.Y - scroll.Viewport.Height / 2;
            scroll.Offset = new Vector(scroll.Offset.X, scroll.Offset.Y + delta);
        }, Avalonia.Threading.DispatcherPriority.Background);
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

    private void SubscribeToImageChanges()
    {
        if (_subscribedImage != null)
            _subscribedImage.PropertyChanged -= OnSelectedImagePropertyChanged;

        _subscribedImage = (DataContext as MainWindowViewModel)?.SelectedImage;

        if (_subscribedImage != null)
            _subscribedImage.PropertyChanged += OnSelectedImagePropertyChanged;
    }

    private void OnSelectedImagePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImageViewModel.LargerThumbnail))
        {
            UpdateTagMarkers();
        }
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

        var mainVm = DataContext as MainWindowViewModel;
        var dialog = new AddTagWindow(null, mainVm?.KnownTags);
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
        var legend = this.FindControl<TextBlock>("TagLegend");
        if (canvas == null || panel == null || image == null) return;

        canvas.Children.Clear();
        if (legend != null) { legend.Text = null; legend.IsVisible = false; }

        var viewModel = (DataContext as MainWindowViewModel)?.SelectedImage;
        if (viewModel == null) return;

        var layout = GetImageLayout();
        if (!layout.isValid) return;

        // Build per-tag number map when in Numbers mode: top-to-bottom, left-to-right.
        Dictionary<ImageTag, int>? numberMap = null;
        if (_tagMode == TagDisplayMode.Numbers && viewModel.Tags.Count > 0)
        {
            numberMap = viewModel.Tags
                .OrderBy(t => t.AnchorY).ThenBy(t => t.AnchorX)
                .Select((t, i) => (t, i))
                .ToDictionary(x => x.t, x => x.i + 1);

            if (legend != null)
            {
                legend.Text = string.Join("; ", numberMap
                    .OrderBy(kv => kv.Value)
                    .Select(kv => $"{kv.Value} — {kv.Key.Label}"));
                legend.IsVisible = true;
            }
        }

        var labelsVisible = _tagMode != TagDisplayMode.Hidden;

        foreach (var tag in viewModel.Tags)
        {
            var dotX = layout.offsetX + tag.AnchorX * layout.renderedWidth;
            var dotY = layout.offsetY + tag.AnchorY * layout.renderedHeight;
            var labelX = layout.offsetX + (tag.AnchorX + tag.EffectiveLabelOffsetX) * layout.renderedWidth;
            var labelY = layout.offsetY + (tag.AnchorY + tag.EffectiveLabelOffsetY) * layout.renderedHeight;

            var tagMarkerBrush = this.FindResource("TagMarkerBrush") as IBrush ?? new SolidColorBrush(Colors.Orange);
            var tagMarkerStrokeBrush = this.FindResource("TagMarkerStrokeBrush") as IBrush ?? new SolidColorBrush(Colors.White);
            var tagLabelBgBrush = this.FindResource("TagLabelBgBrush") as IBrush ?? new SolidColorBrush(Color.FromArgb(200, 0, 0, 0));

            var inNumbersMode = numberMap != null;

            // Scale sizes inversely with zoom so they stay readable
            var markerSize = (inNumbersMode ? 20.0 : 8.0) / _zoom;
            var hitAreaSize = Math.Max(20.0 / _zoom, markerSize);
            var fontSize = 10.0 / _zoom;
            var labelPadH = 4.0 / _zoom;
            var labelPadV = 2.0 / _zoom;

            var displayText = inNumbersMode && numberMap!.TryGetValue(tag, out var n)
                ? n.ToString()
                : tag.Label;

            // 1. Connector line — hidden in Numbers mode.
            var line = new Line
            {
                StartPoint = new Point(dotX, dotY),
                EndPoint = new Point(labelX, labelY),
                Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                StrokeThickness = 1.5 / _zoom,
                StrokeLineCap = PenLineCap.Round,
                IsHitTestVisible = false,
                IsVisible = labelsVisible && !inNumbersMode
            };
            canvas.Children.Add(line);

            // 2. Dot / number bubble marker
            Control dot;
            if (inNumbersMode)
            {
                dot = new Border
                {
                    Width = markerSize,
                    Height = markerSize,
                    Background = tagMarkerBrush,
                    BorderBrush = tagMarkerStrokeBrush,
                    BorderThickness = new Thickness(1.5 / _zoom),
                    CornerRadius = new CornerRadius(markerSize / 2),
                    IsHitTestVisible = false,
                    Child = new TextBlock
                    {
                        Text = displayText,
                        Foreground = Brushes.White,
                        FontSize = fontSize,
                        FontWeight = FontWeight.Bold,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    }
                };
            }
            else
            {
                dot = new Ellipse
                {
                    Width = markerSize,
                    Height = markerSize,
                    Fill = tagMarkerBrush,
                    Stroke = tagMarkerStrokeBrush,
                    StrokeThickness = 1.0 / _zoom,
                    IsHitTestVisible = false
                };
            }
            Canvas.SetLeft(dot, dotX - markerSize / 2);
            Canvas.SetTop(dot, dotY - markerSize / 2);
            canvas.Children.Add(dot);
            var labelText = new TextBlock
            {
                Text = tag.Label,
                Foreground = Brushes.White,
                FontSize = fontSize,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var iconButtonBg = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            var iconButtonHoverBg = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255));
            var iconPadH = 5.0 / _zoom;
            var iconPadV = 1.0 / _zoom;
            var iconCornerRadius = new CornerRadius(6.0 / _zoom);
            var iconMargin = new Thickness(5.0 / _zoom, 0, 0, 0);
            var iconCursor = new Cursor(StandardCursorType.Hand);

            var editIcon = new Border
            {
                Background = iconButtonBg,
                CornerRadius = iconCornerRadius,
                Padding = new Thickness(iconPadH, iconPadV),
                Margin = iconMargin,
                Cursor = iconCursor,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                IsVisible = false,
                Child = new TextBlock
                {
                    Text = "✎",
                    Foreground = Brushes.White,
                    FontSize = fontSize,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                }
            };

            var deleteIcon = new Border
            {
                Background = iconButtonBg,
                CornerRadius = iconCornerRadius,
                Padding = new Thickness(iconPadH, iconPadV),
                Margin = iconMargin,
                Cursor = iconCursor,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                IsVisible = false,
                Child = new TextBlock
                {
                    Text = "✕",
                    Foreground = Brushes.White,
                    FontSize = fontSize,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                }
            };

            ToolTip.SetTip(editIcon, LocalizationService.Current.TooltipEditTag);
            ToolTip.SetTip(deleteIcon, LocalizationService.Current.TooltipDeleteTag);

            editIcon.PointerEntered += (s, e) => editIcon.Background = iconButtonHoverBg;
            editIcon.PointerExited += (s, e) => editIcon.Background = iconButtonBg;
            deleteIcon.PointerEntered += (s, e) => deleteIcon.Background = iconButtonHoverBg;
            deleteIcon.PointerExited += (s, e) => deleteIcon.Background = iconButtonBg;

            var labelStack = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Children = { labelText }
            };
            if (!inNumbersMode)
            {
                labelStack.Children.Add(editIcon);
                labelStack.Children.Add(deleteIcon);
            }

            var label = new Border
            {
                Background = tagLabelBgBrush,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1.5 / _zoom),
                CornerRadius = new CornerRadius(8.0 / _zoom),
                Padding = new Thickness(labelPadH + 2.0 / _zoom, labelPadV + 1.0 / _zoom),
                Child = labelStack,
                IsHitTestVisible = !inNumbersMode,
                Cursor = new Cursor(StandardCursorType.SizeAll),
                IsVisible = labelsVisible && !inNumbersMode
            };

            // Show edit/delete icons only while hovering the label.
            label.PointerEntered += (s, e) =>
            {
                if (inNumbersMode) return;
                editIcon.IsVisible = true;
                deleteIcon.IsVisible = true;
            };
            label.PointerExited += (s, e) =>
            {
                if (inNumbersMode) return;
                editIcon.IsVisible = false;
                deleteIcon.IsVisible = false;
            };

            // In Numbers mode add a floating icon strip next to the bubble.
            Border? numberIconsPanel = null;
            if (inNumbersMode)
            {
                var iconsStack = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 2.0 / _zoom
                };
                editIcon.IsVisible = true;
                deleteIcon.IsVisible = true;
                editIcon.Margin = new Thickness(0);
                deleteIcon.Margin = new Thickness(2.0 / _zoom, 0, 0, 0);
                editIcon.Width = markerSize;
                editIcon.Height = markerSize;
                deleteIcon.Width = markerSize;
                deleteIcon.Height = markerSize;
                editIcon.CornerRadius = new CornerRadius(markerSize / 2);
                deleteIcon.CornerRadius = new CornerRadius(markerSize / 2);
                editIcon.Padding = new Thickness(0);
                deleteIcon.Padding = new Thickness(0);
                if (editIcon.Child is TextBlock editTb)
                {
                    editTb.FontSize = fontSize * 1.4;
                    editTb.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
                }
                if (deleteIcon.Child is TextBlock delTb)
                {
                    delTb.FontSize = fontSize * 1.4;
                    delTb.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
                }
                iconsStack.Children.Add(editIcon);
                iconsStack.Children.Add(deleteIcon);

                numberIconsPanel = new Border
                {
                    Background = tagLabelBgBrush,
                    CornerRadius = new CornerRadius(6.0 / _zoom),
                    Padding = new Thickness(3.0 / _zoom, 2.0 / _zoom),
                    Child = iconsStack,
                    IsVisible = false,
                    IsHitTestVisible = true
                };
                Canvas.SetLeft(numberIconsPanel, dotX + hitAreaSize / 2);
                Canvas.SetTop(numberIconsPanel, dotY - markerSize / 2);
                canvas.Children.Add(numberIconsPanel);
            }

            var capturedTagForIcons = tag;
            editIcon.PointerPressed += async (s, e) =>
            {
                e.Handled = true;
                e.Pointer.Capture(null);
                await viewModel.EditTag(capturedTagForIcons);
            };
            deleteIcon.PointerPressed += async (s, e) =>
            {
                e.Handled = true;
                e.Pointer.Capture(null);
                var dialog = new ConfirmWindow(LocalizationService.Current.ConfirmDeleteTag(capturedTagForIcons.Label));
                await dialog.ShowDialog(this);
                if (dialog.Confirmed)
                {
                    viewModel.RemoveTag(capturedTagForIcons);
                }
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
            if (inNumbersMode)
            {
                ToolTip.SetTip(dotHitArea, tag.Label);
            }
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
                    {
                        _isDragging = true;
                        dotHitArea.Cursor = new Cursor(StandardCursorType.DragMove);
                    }
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
                dotHitArea.Cursor = new Cursor(StandardCursorType.SizeAll);
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
                    {
                        _isDragging = true;
                        label.Cursor = new Cursor(StandardCursorType.DragMove);
                    }
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
                label.Cursor = new Cursor(StandardCursorType.SizeAll);
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

            // Show on hover when labels are globally hidden.
            // Or, in Numbers mode, show the floating icon strip on hover.
            bool overDot = false, overIcons = false;
            void RefreshNumbersHover()
            {
                if (numberIconsPanel != null)
                    numberIconsPanel.IsVisible = overDot || overIcons;
            }

            dotHitArea.PointerEntered += (s, e) =>
            {
                overDot = true;
                if (_tagMode == TagDisplayMode.Hidden)
                {
                    capturedLabel.IsVisible = true;
                    capturedLine.IsVisible = capturedLineNeeded;
                }
                RefreshNumbersHover();
            };
            dotHitArea.PointerExited += (s, e) =>
            {
                overDot = false;
                if (_tagMode == TagDisplayMode.Hidden && !_isDragging)
                {
                    capturedLabel.IsVisible = false;
                    capturedLine.IsVisible = false;
                }
                RefreshNumbersHover();
            };
            if (numberIconsPanel != null)
            {
                numberIconsPanel.PointerEntered += (s, e) => { overIcons = true; RefreshNumbersHover(); };
                numberIconsPanel.PointerExited += (s, e) => { overIcons = false; RefreshNumbersHover(); };
            }

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
