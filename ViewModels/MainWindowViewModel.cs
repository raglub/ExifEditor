using System;
using System.Windows.Input;
using ReactiveUI;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ExifEditor.ViewModels;
using ExifLibrary;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.IO;

namespace ExifEditor.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly AppSettings appSettings;

    private string? _exifDescription;
    public ICommand _selectDirectoryCommand;
    public ICommand _showExifCommand;

    public MainWindowViewModel() {
        appSettings = SettingsService.LoadSettings();
    }

    public ICommand SelectDirectoryCommand {
        get {
            _selectDirectoryCommand = _selectDirectoryCommand ?? ReactiveCommand.CreateFromTask(async () =>
            {
                if (Avalonia.Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var result = await new OpenFolderDialog()
                    {
                        Title = "Select folder",
                        Directory = this.DirPath,
                    }.ShowAsync(desktop.MainWindow);
                    if (!string.IsNullOrEmpty(result)) {
                        this.DirPath = result;
                    }
                }
            });
            return _selectDirectoryCommand;
        }
    }

    public ICommand ShowExifCommand {
        get {
            _showExifCommand = _showExifCommand ?? ReactiveCommand.CreateFromTask(async () =>
            {
                string filePath = Path.Combine(DirPath, "img.jpg");
                var file = ImageFile.FromFile(filePath);
                var description = (string)file.Properties.Get(ExifTag.ImageDescription).Value;
                ExifDescription = description;
            });
            return _showExifCommand;
        }
    }

    public string DirPath
    {
        get
        {
            return appSettings?.DirPath ?? "";
        }
        set {
            appSettings.DirPath = value;
            SettingsService.SaveSettings(appSettings);
            this.RaisePropertyChanged(nameof(DirPath));
        }
    }

    public string? ExifDescription
    {
        get 
        {
            return _exifDescription;
        }
        set 
        {
            this.RaiseAndSetIfChanged(ref _exifDescription, value);
        }

    }    
}
