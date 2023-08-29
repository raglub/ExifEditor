using Avalonia.Controls;
using Avalonia.Interactivity;
using ExifLibrary;

namespace ExifEditor;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        string filePath = "path.jpg";

        var file = ImageFile.FromFile(filePath);
        var description = (string)file.Properties.Get(ExifTag.ImageDescription).Value;
        outputText.Text = description;
    }
}