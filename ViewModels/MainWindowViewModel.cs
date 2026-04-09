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
using Avalonia.Threading;
using ExifEditor.Services;

namespace ExifEditor.ViewModels;


public class MainWindowViewModel : ViewModelBase
{
    private readonly AppSettings _appSettings;

    private readonly ServiceFactory _serviceFactory;
    private readonly ThemeService _themeService;

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
    
    public List<string> RecentScanned => _appSettings.RecentScanned ??= new List<string>();

    public void AddRecentScanned(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var list = RecentScanned;
        list.Remove(value);
        list.Insert(0, value);
        if (list.Count > 20) list.RemoveRange(20, list.Count - 20);
        SettingsService.SaveSettings(_appSettings);
    }

    public ICommand ExitApplicationCommand {get; set;}
    public ICommand GeneratePDFReportCommand {get; set;}
    public ICommand SelectDirectoryCommand {get; set;}
    public ICommand ShowAboutCommand {get; set;}
    public ICommand SwitchToOceanBlueCommand {get; set;}
    public ICommand SwitchToVioletCyanCommand {get; set;}

    public bool IsLoading {
        get => _isLoading;
        set {
            this.RaiseAndSetIfChanged(ref _isLoading, value);
            this.RaisePropertyChanged(nameof(IsLoadingIndeterminate));
        }
    }

    public int LoadingProgress {
        get => _loadingProgress;
        set => this.RaiseAndSetIfChanged(ref _loadingProgress, value);
    }

    public int LoadingTotal {
        get => _loadingTotal;
        set {
            this.RaiseAndSetIfChanged(ref _loadingTotal, value);
            this.RaisePropertyChanged(nameof(IsLoadingIndeterminate));
        }
    }

    public bool IsLoadingIndeterminate => _isLoading && _loadingTotal == 0;

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

    public MainWindowViewModel(PdfGeneratorService pdfGenerator, DirectoryService directory, ServiceFactory serviceFactory, ThemeService themeService) {
        _pdfGenerator = pdfGenerator;
        _directory = directory;
        _serviceFactory = serviceFactory;
        _themeService = themeService;
        _appSettings = SettingsService.LoadSettings();
        SelectDirectoryCommand = ReactiveCommand.CreateFromTask(async () => await SelectDirectoryAsync());
        ShowAboutCommand = ReactiveCommand.CreateFromTask(async () => await ShowAbout());
        ExitApplicationCommand = ReactiveCommand.CreateFromTask(async () => await ExitApplication());
        GeneratePDFReportCommand = ReactiveCommand.CreateFromTask(async () => await GeneratePDFReportAsync());
        SwitchToOceanBlueCommand = ReactiveCommand.Create(() => SwitchTheme(AppTheme.OceanBlue));
        SwitchToVioletCyanCommand = ReactiveCommand.Create(() => SwitchTheme(AppTheme.VioletCyan));
    }

    #region Methods

    public Task InitializeAsync() {
        return LoadImagesAsync(_appSettings.DirPath, _appSettings.SelectedFilePath);
    }

    private async Task GeneratePDFReportAsync() {

        await _pdfGenerator.GenerateReportAsync(_appSettings.DirPath);
    }

    private async Task LoadImagesAsync(string? dirPath, string? selectedFilePath) {
        _loadingCts?.Cancel();
        _loadingCts = new CancellationTokenSource();
        var token = _loadingCts.Token;

        IsLoading = true;
        LoadingProgress = 0;
        LoadingTotal = 0;

        Images.Clear();
        _selectedImage = null;
        this.RaisePropertyChanged(nameof(SelectedImage));

        // Yield so the window can render the empty frame + spinner immediately.
        await Task.Yield();

        var imagePaths = await Task.Run(() => {
            if (dirPath is not null && Directory.Exists(dirPath)) {
                return _directory.GetImagePaths(dirPath);
            }
            return new List<string>();
        }, token).ConfigureAwait(true);

        if (token.IsCancellationRequested) return;

        LoadingTotal = imagePaths.Count;

        if (imagePaths.Count == 0) {
            IsLoading = false;
            return;
        }

        const int batchSize = 10;
        ImageViewModel? pendingSelection = null;

        for (int i = 0; i < imagePaths.Count; i += batchSize) {
            if (token.IsCancellationRequested) return;

            // Yield before each batch so the UI thread can repaint and stay responsive.
            await Task.Yield();

            var end = Math.Min(i + batchSize, imagePaths.Count);
            for (int j = i; j < end; j++) {
                var image = _serviceFactory.CreateImageViewModel(this, imagePaths[j]);
                Images.Add(image);
                if (imagePaths[j] == selectedFilePath && pendingSelection == null) {
                    pendingSelection = image;
                }
            }
            LoadingProgress = end;
        }

        if (token.IsCancellationRequested) return;

        var finalSelection = pendingSelection ?? (Images.Count > 0 ? Images[0] : null);
        IsLoading = false;

        if (finalSelection != null) {
            // Defer the EXIF read (triggered by the SelectedImage setter) so the
            // last loading frame can paint before we hit the disk again.
            Dispatcher.UIThread.Post(() => {
                if (token.IsCancellationRequested) return;
                SelectedImage = finalSelection;
            }, DispatcherPriority.Background);
        }
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
                    if (path == null) return;
                    this.DirPath = path;
                    await LoadImagesAsync(path, null);
                }
            }
        }
    }

    private async Task ShowAbout() {
        var assembly = Assembly.GetExecutingAssembly();

        var authorsAttribute = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
        var author = authorsAttribute?.Company ?? "unknown";
        
        var versionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var informationalVersion = versionAttribute?.InformationalVersion;
        var version = informationalVersion?.Split('+', 2)[0] ?? assembly.GetName().Version?.ToString() ?? "unknown";

        var releaseYear = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attr => attr.Key == "ReleaseYear")
            ?.Value ?? "unknown";

        var commit = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attr => attr.Key is "RepositoryCommit" or "SourceRevisionId" or "CommitHash")
            ?.Value;

        if (string.IsNullOrWhiteSpace(commit) && !string.IsNullOrWhiteSpace(informationalVersion)) {
            var match = Regex.Match(informationalVersion, @"(?<![0-9a-fA-F])[0-9a-fA-F]{7,40}(?![0-9a-fA-F])");
            if (match.Success) {
                commit = match.Value;
            }
        }

        var shortCommit = !string.IsNullOrWhiteSpace(commit)
            ? commit[..Math.Min(7, commit.Length)]
            : "unknown";

        var aboutMessage = string.Join(Environment.NewLine, new[]
        {
            "ExifEditor",
            $"Version: {version}",
            $"Release year: {releaseYear}",
            $"Git commit: {shortCommit}",
            $"Author: {author}"
        });
        
        var box = MessageBoxManager
            .GetMessageBoxStandard("ExifEditor", aboutMessage, ButtonEnum.Ok);
        var result = await box.ShowAsync();
    }

    private async Task ExitApplication() {
        Environment.Exit(0);
    }

    private void SwitchTheme(AppTheme theme) {
        _themeService.ApplyTheme(theme);
        _appSettings.Theme = theme.ToString();
        SettingsService.SaveSettings(_appSettings);
    }

    #endregion
}
