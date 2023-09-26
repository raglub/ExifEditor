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
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
    public ICommand? _useSavedArtistCommand;
    public string? _title;
    public MainWindowViewModel? _mainWindow;

    public ImageViewModel(MainWindowViewModel window) {
        _mainWindow = window;
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

    public ICommand UseSavedArtistCommand {
        get {;
            _useSavedArtistCommand = _useSavedArtistCommand ?? ReactiveCommand.CreateFromTask(async () =>
            {
                Artist = _mainWindow?.SavedArtist;
            });
            return _useSavedArtistCommand;
        }
    }

    public string? FilePath {
        get {
            return _filePath;
        } 
        set {
            _filePath = value;
            if (!string.IsNullOrEmpty(_filePath)) {
                var file = ImageFile.FromFile(FilePath);
                
                if (file.Properties.Contains(ExifTag.ImageDescription)) {
                    Description = (string)file.Properties.Get(ExifTag.ImageDescription).Value;
                    this.RaisePropertyChanged(nameof(Description));
                }
            
                if (file.Properties.Contains(ExifTag.Artist)) {
                    Artist = (string)file.Properties.Get(ExifTag.Artist).Value;
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
            return new Bitmap(FilePath);
        }
    }
       
    public ObservableCollection<string> ImageProperties {get; set;} = new ObservableCollection<string>();
}