using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExifEditor.ViewModels;

namespace ExifEditor.Services;

public class ServiceFactory {
    public ImageService CreateImageService(string imagePath)
    {
        return new ImageService(imagePath);
    }

    public ImageViewModel CreateImageViewModel(MainWindowViewModel mainWindowViewModel, string imagePath)
    {
        return new ImageViewModel(mainWindowViewModel, this) {
            FilePath = imagePath,
            FileName = Path.GetFileName(imagePath)
        };
    }
}