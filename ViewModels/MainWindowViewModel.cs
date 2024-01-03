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
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System.Reflection;

namespace ExifEditor.ViewModels;


public class MainWindowViewModel : ViewModelBase
{
    private readonly AppSettings _appSettings;
    private readonly PdfGeneratorService _pdfGenerator;
    private ImageViewModel? _selectedImage;

    #region Properties

    public string DirPath
    {
        get
        {
            return _appSettings?.DirPath ?? "";
        }
        set {
            _appSettings.DirPath = value;
            SettingsService.SaveSettings(_appSettings);
            this.RaisePropertyChanged(nameof(DirPath));
        }
    }

    public ObservableCollection<ImageViewModel> Images { get;} = new ObservableCollection<ImageViewModel>();
    
    public string? DefaultArtist
    {
        get
        {
            return _appSettings?.DefaultArtist ?? "";
        }
        set {
            _appSettings.DefaultArtist = value;
            SettingsService.SaveSettings(_appSettings);
            this.RaisePropertyChanged(nameof(DefaultArtist));
        }
    }

    public ICommand ExitApplicationCommand {get; set;}
    public ICommand GeneratePDFReportCommand {get; set;}    
    public ICommand SelectDirectoryCommand {get; set;}
    public ICommand ShowAboutCommand {get; set;}

    public ImageViewModel? SelectedImage
    {
        get {

            return _selectedImage;
        }
        set {
            _selectedImage = value;
            _appSettings.SelectedFilePath = value?.FilePath;
            SettingsService.SaveSettings(_appSettings);
            this.RaisePropertyChanged(nameof(SelectedImage));
        }
    }
    #endregion

    public MainWindowViewModel(PdfGeneratorService pdfGenerator) {
        _pdfGenerator = pdfGenerator;
        _appSettings = SettingsService.LoadSettings();
        Task.Run(async () => await LoadImagesAsync(_appSettings.DirPath, _appSettings.SelectedFilePath));
        SelectDirectoryCommand = ReactiveCommand.CreateFromTask(async () => await SelectDirectoryAsync());
        ShowAboutCommand = ReactiveCommand.CreateFromTask(async () => await ShowAbout());
        ExitApplicationCommand = ReactiveCommand.CreateFromTask(async () => await ExitApplication());
        GeneratePDFReportCommand = ReactiveCommand.CreateFromTask(async () => await GeneratePDFReportAsync());
    }

    #region Methods

    private async Task GeneratePDFReportAsync() {

        await _pdfGenerator.GenerateReportAsync();
    }

    private async Task LoadImagesAsync(string? dirPath, string? selectedFilePath) {
        await Task.Run(() => {    
            Images.Clear();
            if (dirPath is object && Directory.Exists(dirPath)) {
                var filePaths = Directory.GetFiles(dirPath).ToList();
                filePaths.Sort();
                foreach(var filePath in filePaths) {
                    if (Path.GetExtension(filePath) == ".jpg" || Path.GetExtension(filePath) == ".png") {
                        var image = new ImageViewModel(this) {
                            FilePath = filePath,
                            FileName = Path.GetFileName(filePath)
                        };
                        Images.Add(image);
                        if (filePath == selectedFilePath) {

                            SelectedImage = image;
                        }
                    }
                }
                if (Images.Count > 0 && SelectedImage == null) {
                    SelectedImage = Images[0];
                }
            }
        });
    }

    private async Task SelectDirectoryAsync() {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is not null) {
                var result = await desktop.MainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions{
                    Title = "Select folder",
                    AllowMultiple = false,
                    SuggestedStartLocation = await desktop.MainWindow.StorageProvider.TryGetFolderFromPathAsync(this.DirPath)
                });
                if (result.Any()) {
                    var path = result.FirstOrDefault()?.Path.AbsolutePath;
                    this.DirPath = path;
                    await LoadImagesAsync(path, null);
                }
            }
        }
    }

    private async Task ShowAbout() {
        var assembly = Assembly.GetExecutingAssembly();

        var authorsAttribute = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
        var author = authorsAttribute?.Company;
        
        var versionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var version = versionAttribute?.InformationalVersion;
        
        var box = MessageBoxManager
            .GetMessageBoxStandard("ExifEditor", $"ExifEditor \nVersion: {version} \nAuthor: {author}", ButtonEnum.Ok);
        var result = await box.ShowAsync();
    }

    private async Task ExitApplication() {
        Environment.Exit(0);
    }

    #endregion    
}
