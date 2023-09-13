using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ExifLibrary;
using ReactiveUI;

namespace ExifEditor.ViewModels;
public class ImageViewModel : ViewModelBase
{
    public ICommand? _saveCommand;

    public string? _filePath;
    public string? FileName { get; set; }

    private void SetValueOfTag(ImageFile file, ExifTag tag, string? value){
        if (string.IsNullOrEmpty(value)) {
            if (file.Properties.Contains(tag)){
                file.Properties.Remove(tag);
            }
        } 
        else 
        {
            file.Properties.Set(tag, value);
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
            });
            return _saveCommand;
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
                
                foreach(var property in file.Properties) {
                    if (property.Name == nameof(ExifTag.Artist) || property.Name == nameof(ExifTag.ImageDescription)) {
                        continue;
                    }
                    ImageProperties.Add($"{property.Name}: {property.Value}");
                }
            }
        }
    }

    public string? Artist {get; set;}

    public string? Description {get; set;}

    public Bitmap? ImageBitmap { 
        get
        {
            return new Bitmap(FilePath);
        }
    }   

    public ObservableCollection<string> ImageProperties {get; set;} = new ObservableCollection<string>();
}