using System;
using ReactiveUI;

namespace ExifEditor.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly AppSettings appSettings;

    public MainWindowViewModel() {
        appSettings = SettingsService.LoadSettings();
    }

    public string DirPath
    {
        get 
        {
            return appSettings.DirPath;
        }
        set {
            appSettings.DirPath = value;
            SettingsService.SaveSettings(appSettings);
            this.RaisePropertyChanged(nameof(DirPath));
        }
    }    
}
