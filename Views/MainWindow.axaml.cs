using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ExifLibrary;

namespace ExifEditor.Views;

public partial class MainWindow : Window
{   
    public static MainWindow? mainWindow;

    private readonly AppSettings appSettings;
    public MainWindow()
    {
        InitializeComponent();
        appSettings = SettingsService.LoadSettings();
        DirPath.Text = appSettings.DirPath;
        mainWindow = this;
    }

    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        string filePath = Path.Combine(appSettings.DirPath, "img.jpg");

        var file = ImageFile.FromFile(filePath);
        var description = (string)file.Properties.Get(ExifTag.ImageDescription).Value;
        outputText.Text = description;
    }

    private async void OnSelectDirectoryClick(object sender, RoutedEventArgs e)
    {
        var result = await new OpenFolderDialog()
        {
            Title = "Select folder",
            Directory = appSettings.DirPath,
        }.ShowAsync(mainWindow);

        if (!string.IsNullOrEmpty(result)) {
            DirPath.Text = result;
            appSettings.DirPath = result;
            SettingsService.SaveSettings(appSettings);
        }
    }
}