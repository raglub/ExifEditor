using System.Collections.Generic;
using System.Text.Json.Serialization;

public class AppSettings
{
        public string? DirPath { get; set; }

        public string? SelectedFilePath {get; set;}

        public string? Theme {get; set;}

        public List<string>? RecentScanned {get; set;}
}

[JsonSerializable(typeof(AppSettings))]
internal partial class AppSettingsJsonContext : JsonSerializerContext
{
}