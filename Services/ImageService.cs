using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExifLibrary;

namespace ExifEditor.Services;

public class ImageService {
    private readonly string? _imagePath;

    private ImageFile? _imageFile;

    private ImageFile? ImageFile { 
        get {
            if (_imageFile is not object) {
                if (string.IsNullOrEmpty(_imagePath))
                    return null;
                _imageFile = ImageFile.FromFile(_imagePath);
            }
            return _imageFile;
        }
    }

    public ImageService(string imagePath) {
        _imagePath = imagePath;
    }

    public bool IsArtist() {
        return ImageFile is object && ImageFile.Properties.Contains(ExifTag.Artist);
    }

    public bool IsDescription() {
        return ImageFile is object && ImageFile.Properties.Contains(ExifTag.ImageDescription);
    }

    public string? GetDescription() {
        if (IsDescription()) {
            return (string)ImageFile!.Properties.Get(ExifTag.ImageDescription).Value;
        }       
        return null;
    }

    public string? GetArtist() {
        if (IsArtist()) {
            return (string)ImageFile!.Properties.Get(ExifTag.Artist).Value;
        }       
        return null;
    }

}