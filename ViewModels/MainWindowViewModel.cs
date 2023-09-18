using System;
using System.Windows.Input;
using ReactiveUI;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ExifEditor.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ExifEditor.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly AppSettings appSettings;

    public ICommand? _selectDirectoryCommand;
    public ImageViewModel? _selectedImage;
    public ICommand? _showExifCommand;

    public ObservableCollection<ImageViewModel> Images { get;} = new ObservableCollection<ImageViewModel>();

    public MainWindowViewModel() {
        appSettings = SettingsService.LoadSettings();
        if (appSettings.DirPath is object) {
            var filePaths = Directory.GetFiles(appSettings.DirPath);
            foreach(var filePath in filePaths) {
                if (Path.GetExtension(filePath) == ".jpg" || Path.GetExtension(filePath) == ".png") {
                    Images.Add(new ImageViewModel {
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath)
                    });
                    if (SelectedImage == null) {
                        SelectedImage = Images[0];

                    }
                }
            }
        }
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

    #region Properties

    public ImageViewModel? SelectedImage
    {

        get {

            return _selectedImage;
        }
        set {
            _selectedImage = value;
            appSettings.SelectedFilePath = value?.FilePath;
            SettingsService.SaveSettings(appSettings);
            this.RaisePropertyChanged(nameof(SelectedImage));
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
    #endregion    
}
