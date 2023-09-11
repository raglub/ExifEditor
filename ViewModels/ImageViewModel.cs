using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ExifLibrary;

namespace ExifEditor.ViewModels;
public class ImageViewModel : ViewModelBase
{
    public string? _description;

    public string? _author;
    public string? FileName { get; set; }

    public string? FilePath { get; set; }

    public Bitmap? ImageBitmap { 
        get
        {
            return new Bitmap(FilePath);
        }
    }

    public string? Description
    {
        get 
        {
            try {
                var file = ImageFile.FromFile(FilePath);
                var description = (string)file.Properties.Get(ExifTag.ImageDescription).Value;
                _description = description;
                
                return _description;
            } catch (Exception ex) {

                return "";
            }
        }
    }

    public string? Artist
    {
        get 
        {
            var file = ImageFile.FromFile(FilePath);
            var result = (string)file.Properties.Get(ExifTag.Artist).Value;
            _author = result;
            return _author;
        }
    }
}