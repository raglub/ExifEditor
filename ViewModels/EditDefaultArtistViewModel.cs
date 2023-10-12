using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ExifEditor.Views;
using ExifLibrary;
using ReactiveUI;
using Avalonia.Controls.ApplicationLifetimes;

namespace ExifEditor.ViewModels;
public class EditDefaultArtistViewModel : ViewModelBase
{
    private string? _artist;
    private bool _isModified;
    public MainWindowViewModel _mainWindow;

    public static Window? CurrentWindow {get; set;}

    public ICommand? SaveFormCommand {get; set;}

    public EditDefaultArtistViewModel(MainWindowViewModel window) {
        _mainWindow = window;
        _isModified = false;
        _artist = window.DefaultArtist;
        SaveFormCommand = ReactiveCommand.Create(() => SaveForm());
    }

    public string? Artist {
        get => _artist;
        set {
            IsModified = true;
            this.RaiseAndSetIfChanged(ref _artist, value);
        }
    }

    public bool IsModified {
        get => _isModified;
        set {
            this.RaiseAndSetIfChanged(ref _isModified, value);
        }
    }

    public void SaveForm() {
        if (CurrentWindow is object) {
            CurrentWindow.Close();
        }
    }
}