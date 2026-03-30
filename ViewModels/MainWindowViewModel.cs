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
using System.Threading;
using System.Threading.Tasks;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System.Reflection;
using ExifEditor.Services;

namespace ExifEditor.ViewModels;


public class MainWindowViewModel : ViewModelBase
{
    private readonly AppSettings _appSettings;

    private readonly ServiceFactory _serviceFactory;

    private readonly DirectoryService _directory;
    private readonly PdfGeneratorService _pdfGenerator;
    private ImageViewModel? _selectedImage;
    private bool _isLoading;
    private int _loadingProgress;
    private int _loadingTotal;
    private CancellationTokenSource? _loadingCts;

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

    public bool IsLoading {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public int LoadingProgress {
        get => _loadingProgress;
        set => this.RaiseAndSetIfChanged(ref _loadingProgress, value);
    }

    public int LoadingTotal {
        get => _loadingTotal;
        set => this.RaiseAndSetIfChanged(ref _loadingTotal, value);
    }

    public ImageViewModel? SelectedImage
    {
        get {
            return _selectedImage;
        }
        set {
            _selectedImage = value;
            _selectedImage?.EnsureMetadataLoaded();
            _appSettings.SelectedFilePath = value?.FilePath;
            SettingsService.SaveSettings(_appSettings);
            this.RaisePropertyChanged(nameof(SelectedImage));
        }
    }
    #endregion

    public MainWindowViewModel(PdfGeneratorService pdfGenerator, DirectoryService directory, ServiceFactory serviceFactory) {
        _pdfGenerator = pdfGenerator;
        _directory = directory;
        _serviceFactory = serviceFactory;
        _appSettings = SettingsService.LoadSettings();
        _ = LoadImagesAsync(_appSettings.DirPath, _appSettings.SelectedFilePath);
        SelectDirectoryCommand = ReactiveCommand.CreateFromTask(async () => await SelectDirectoryAsync());
        ShowAboutCommand = ReactiveCommand.CreateFromTask(async () => await ShowAbout());
        ExitApplicationCommand = ReactiveCommand.CreateFromTask(async () => await ExitApplication());
        GeneratePDFReportCommand = ReactiveCommand.CreateFromTask(async () => await GeneratePDFReportAsync());
    }

    #region Methods

    private async Task GeneratePDFReportAsync() {

        await _pdfGenerator.GenerateReportAsync(_appSettings.DirPath);
    }

    private async Task LoadImagesAsync(string? dirPath, string? selectedFilePath) {
        _loadingCts?.Cancel();
        _loadingCts = new CancellationTokenSource();
        var token = _loadingCts.Token;

        IsLoading = true;
        LoadingProgress = 0;

        List<string> imagePaths;
        if (dirPath is not null && Directory.Exists(dirPath)) {
            imagePaths = _directory.GetImagePaths(dirPath);
        } else {
            imagePaths = new List<string>();
        }

        if (token.IsCancellationRequested) return;

        LoadingTotal = imagePaths.Count;
        Images.Clear();
        SelectedImage = null;

        if (imagePaths.Count == 0) {
            IsLoading = false;
            return;
        }

        const int batchSize = 10;

        for (int i = 0; i < imagePaths.Count; i += batchSize) {
            if (token.IsCancellationRequested) return;

            var end = Math.Min(i + batchSize, imagePaths.Count);
            for (int j = i; j < end; j++) {
                var image = _serviceFactory.CreateImageViewModel(this, imagePaths[j]);
                Images.Add(image);
                if (imagePaths[j] == selectedFilePath && SelectedImage == null) {
                    SelectedImage = image;
                }
            }
            LoadingProgress = end;

            if (end < imagePaths.Count) {
                await Task.Delay(10);
            }
        }

        if (token.IsCancellationRequested) return;

        if (Images.Count > 0 && SelectedImage == null) {
            SelectedImage = Images[0];
        }
        IsLoading = false;
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
