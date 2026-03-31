using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ExifEditor.ViewModels;

namespace ExifEditor.Views;

public partial class MainWindow : Window
{
    private INotifyCollectionChanged? _subscribedTags;
    private bool _showAllTags = true;

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

    private async void OnImagePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var panel = sender as Panel;
        var image = this.FindControl<Image>("PreviewImage");
        var viewModel = (DataContext as MainWindowViewModel)?.SelectedImage;
        if (panel == null || image == null || viewModel == null) return;

        if (!e.GetCurrentPoint(panel).Properties.IsLeftButtonPressed) return;

        var bitmap = image.Source as Bitmap;
        if (bitmap == null) return;

        var point = e.GetPosition(panel);
        var controlWidth = panel.Bounds.Width;
        var controlHeight = panel.Bounds.Height;

        if (controlWidth <= 0 || controlHeight <= 0) return;

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

        var relX = (point.X - offsetX) / renderedWidth;
        var relY = (point.Y - offsetY) / renderedHeight;

        if (relX < 0 || relX > 1 || relY < 0 || relY > 1) return;

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

        var bitmap = image.Source as Bitmap;
        if (bitmap == null) return;

        var controlWidth = panel.Bounds.Width;
        var controlHeight = panel.Bounds.Height;

        if (controlWidth <= 0 || controlHeight <= 0) return;

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

        foreach (var tag in viewModel.Tags)
        {
            var x = offsetX + tag.X * renderedWidth;
            var y = offsetY + tag.Y * renderedHeight;

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

            var hitArea = new Ellipse
            {
                Width = 20,
                Height = 20,
                Fill = Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            Canvas.SetLeft(hitArea, x - 10);
            Canvas.SetTop(hitArea, y - 10);
            hitArea.PointerPressed += (s, e) => e.Handled = true;
            hitArea.PointerEntered += (s, e) => { if (!_showAllTags) label.IsVisible = true; };
            hitArea.PointerExited += (s, e) => { if (!_showAllTags) label.IsVisible = false; };
            canvas.Children.Add(hitArea);
        }
    }
}
