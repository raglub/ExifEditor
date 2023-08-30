using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ExifEditor.ViewModels;
using ExifLibrary;

namespace ExifEditor.Views;

public partial class MainWindow : Window
{   
    public static MainWindow? mainWindow;

    public MainWindow()
    {
        InitializeComponent();
        mainWindow = this;
    }

    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        //string filePath = Path.Combine(appSettings.DirPath, "img.jpg");

        //var file = ImageFile.FromFile(filePath);
        //var description = (string)file.Properties.Get(ExifTag.ImageDescription).Value;
        //outputText.Text = description;
    }

    private async void OnSelectDirectoryClick(object sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as MainWindowViewModel;
        var result = await new OpenFolderDialog()
        {
            Title = "Select folder",
            Directory = viewModel.DirPath,
        }.ShowAsync(mainWindow);

        if (!string.IsNullOrEmpty(result)) {
            viewModel.DirPath = result;
        }
    }
}