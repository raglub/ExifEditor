using System;
using System.Collections.Specialized;
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

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

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
                SubscribeToTagChanges();
                UpdateTagMarkers();
            }
        };

        var imageTagPanel = this.FindControl<Panel>("ImageTagPanel");
        if (imageTagPanel != null)
        {
            imageTagPanel.SizeChanged += (s, e) => UpdateTagMarkers();
        }
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
        if (_isDragging) return;

        var panel = sender as Panel;
        if (panel == null) return;

        var layout = GetImageLayout();
        if (!layout.isValid)
        {
            panel.Cursor = Cursor.Default;
            return;
        }

        var point = e.GetPosition(panel);
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

        if (!e.GetCurrentPoint(panel).Properties.IsLeftButtonPressed) return;

        var layout = GetImageLayout();
        if (!layout.isValid) return;

        var point = e.GetPosition(panel);
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

            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = tagMarkerBrush,
                Stroke = tagMarkerStrokeBrush,
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dot, x - 4);
            Canvas.SetTop(dot, y - 4);
            canvas.Children.Add(dot);

            var label = new Border
            {
                Background = tagLabelBgBrush,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 2),
                Child = new TextBlock
                {
                    Text = tag.Label,
                    Foreground = Brushes.White,
                    FontSize = 10
                },
                IsVisible = _showAllTags,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, x + 6);
            Canvas.SetTop(label, y - 10);
            canvas.Children.Add(label);

            var capturedTag = tag;
            var capturedDot = dot;
            var capturedLabel = label;

            var hitArea = new Ellipse
            {
                Width = 20,
                Height = 20,
                Fill = Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.SizeAll)
            };
            Canvas.SetLeft(hitArea, x - 10);
            Canvas.SetTop(hitArea, y - 10);

            hitArea.PointerPressed += (s, e) =>
            {
                if (!e.GetCurrentPoint(panel).Properties.IsLeftButtonPressed) return;
                _draggedTag = capturedTag;
                _isDragging = false;
                _dragStartPoint = e.GetPosition(panel);
                _dragDot = capturedDot;
                _dragLabel = capturedLabel;
                _dragHitArea = hitArea;
                e.Pointer.Capture(hitArea);
                e.Handled = true;
            };

            hitArea.PointerMoved += (s, e) =>
            {
                if (_draggedTag != capturedTag) return;

                var currentPoint = e.GetPosition(panel);
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

                    Canvas.SetLeft(_dragDot!, px - 4);
                    Canvas.SetTop(_dragDot!, py - 4);
                    Canvas.SetLeft(_dragLabel!, px + 6);
                    Canvas.SetTop(_dragLabel!, py - 10);
                    Canvas.SetLeft(_dragHitArea!, px - 10);
                    Canvas.SetTop(_dragHitArea!, py - 10);
                }
            };

            hitArea.PointerReleased += (s, e) =>
            {
                if (_draggedTag == capturedTag)
                {
                    if (_isDragging)
                    {
                        var currentPoint = e.GetPosition(panel);
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
