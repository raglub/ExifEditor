using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ExifEditor.ViewModels;
public class ImageViewModel : ViewModelBase
{
    public string? FileName { get; set; }

    public string? Path { get; set; }

    public Bitmap? ImageBitmap { 
        get
        {
            return new Bitmap(Path);
        }
    }
}