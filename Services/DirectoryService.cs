using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExifEditor.Services;

public class DirectoryService {

    public List<string> GetImagePaths(string? dirPath) {
        if (dirPath is null || !Directory.Exists(dirPath)) {
            return new List<string>();
        }
        var filePaths = Directory.GetFiles(dirPath).ToList();
        filePaths.Sort();
        var result = new List<string>();
        foreach(var filePath in filePaths) {
            if (Path.GetExtension(filePath) == ".jpg" || Path.GetExtension(filePath) == ".png") {
                result.Add(filePath);
            }
        }
        return result;
    }
}