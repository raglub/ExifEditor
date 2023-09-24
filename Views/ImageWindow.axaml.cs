using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ExifEditor.ViewModels;
using ExifLibrary;

namespace ExifEditor.Views;

public partial class ImageWindow : Window
{
    public ImageWindow()
    {
        InitializeComponent();
    }
}