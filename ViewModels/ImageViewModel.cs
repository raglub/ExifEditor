using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ExifEditor.Services;
using ExifEditor.Views;
using ExifLibrary;
using ReactiveUI;

namespace ExifEditor.ViewModels;
public class ImageViewModel : ViewModelBase
{
    private string? _artist;
    private Bitmap? _bitmap;
    private string? _description;
    private string? _filePath;
    private bool _isModified = false;
    private Bitmap? _largerThumbnail;
    private Bitmap? _originalBitmap;
    public ICommand? _saveCommand;
    public ICommand? _showFullImageCommand;
    public ICommand? UseDefaultArtistCommand {get; set;}
    public ICommand? EditDefaultArtistCommand {get; set;}
    public string? _title;
    public readonly MainWindowViewModel? _mainWindow;
    public readonly ServiceFactory _serviceFactory;

    public ImageViewModel(MainWindowViewModel window, ServiceFactory serviceFactory) {
        _mainWindow = window;
        _serviceFactory = serviceFactory;
        EditDefaultArtistCommand = ReactiveCommand.CreateFromTask(async () => await EditDefaultArtist());
        UseDefaultArtistCommand = ReactiveCommand.Create(() => UseDefaultArtist());
    }

    private void SetValueOfTag(ImageFile file, ExifTag tag, string? value){
        if (string.IsNullOrEmpty(value)) {
            if (file.Properties.Contains(tag)){
                file.Properties.Remove(tag);
            }
        } 
        else 
        {
            var realLength = Encoding.UTF8.GetBytes(value).Length;
            var extendedValue = value.PadRight(realLength, ' ');
            file.Properties.Set(tag, extendedValue);
        }
    }

    public ICommand SaveCommand {
        get {;
            _saveCommand = _saveCommand ?? ReactiveCommand.CreateFromTask(async () =>
            {
                var file = ImageFile.FromFile(FilePath);
                SetValueOfTag(file, ExifTag.ImageDescription, Description);
                SetValueOfTag(file, ExifTag.Artist, Artist);
                file.Save(FilePath);
                IsModified = false;
            });
            return _saveCommand;
        }
    }

    public ICommand ShowFullImageCommand {
        get {;
            _showFullImageCommand = _showFullImageCommand ?? ReactiveCommand.CreateFromTask(async () =>
            {
                var window = new ImageWindow();
                window.DataContext = this;
                window.Height = 700;
                window.Width = 1000;
                window.Show();
            });
            return _showFullImageCommand;
        }
    }

    public void UseDefaultArtist() {
        Artist = _mainWindow?.DefaultArtist;
    }

    public async Task EditDefaultArtist() {        
        var window = new EditDefaultArtistWindow();
        window.Width = 500;
        window.Height = 150;
        if (_mainWindow is object) {
            var viewModel = new EditDefaultArtistViewModel(_mainWindow);
            EditDefaultArtistViewModel.CurrentWindow = window;
            window.DataContext = viewModel;
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop?.MainWindow is object) {
                    await window.ShowDialog(desktop.MainWindow);
                    var artist = viewModel.Artist;
                    _mainWindow.DefaultArtist = artist;
                }
            }
        }
    }
    

    public string? FilePath {
        get {
            return _filePath;
        } 
        set {
            _filePath = value;
            if (!string.IsNullOrEmpty(_filePath)) {
                var imageService = _serviceFactory.CreateImageService(_filePath);
                var file = ImageFile.FromFile(FilePath);
                
                if (imageService.IsDescription()) {
                    Description = imageService.GetDescription();
                    this.RaisePropertyChanged(nameof(Description));
                }
            
                if (imageService.IsArtist()) {
                    Artist = imageService.GetArtist();
                    this.RaisePropertyChanged(nameof(Artist));
                }

                IsModified = false;
                foreach(var property in file.Properties) {
                    if (property.Name == nameof(ExifTag.Artist) || property.Name == nameof(ExifTag.ImageDescription)) {
                        continue;
                    }
                    ImageProperties.Add($"{property.Name}: {property.Value}");
                }
            }
        }
    }

    public string? Artist {
        get => _artist;
        set {
            IsModified = true;
            this.RaiseAndSetIfChanged(ref _artist, value);
        }
    }

    public string? Description {
        get => _description;
        set {
            IsModified = true;
            this.RaiseAndSetIfChanged(ref _description, value);
        }
    }

    public string? Title {
        get {
            var result = FileName;
            if (IsModified) {
                result += "*";
            }
            return result;
        }
    }

    public bool IsModified {
        get => _isModified;
        set {
            this.RaiseAndSetIfChanged(ref _isModified, value);
            this.RaisePropertyChanged(nameof(Title));
        }
    }

    public string? FileName { get; set; }

    public Bitmap? Thumbnail { 
        get
        {
            if (_bitmap == null && FilePath is object) {
                var file = File.OpenRead(FilePath);
                _bitmap = Bitmap.DecodeToWidth(file, 100);
            }
            return _bitmap;
        }
    }

    public Bitmap? LargerThumbnail { 
        get
        {
            if (_largerThumbnail == null && FilePath is object) {
                var file = File.OpenRead(FilePath);
                _largerThumbnail = Bitmap.DecodeToHeight(file, 300);
            }
            return _largerThumbnail;
        }
    }

    public Bitmap? OriginalBitmap { 
        get
        {
            if (_originalBitmap == null && FilePath is object) {
                var file = File.OpenRead(FilePath);
                _originalBitmap = new Bitmap(file);
            }
            return _originalBitmap;
        }
    }

    public Bitmap? ImageBitmap {
        get
        {   
            return FilePath is object ? new Bitmap(FilePath) : null;
        }
    }
       
    public ObservableCollection<string> ImageProperties {get; set;} = new ObservableCollection<string>();
}