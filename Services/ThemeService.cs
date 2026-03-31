using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace ExifEditor.Services;

public enum AppTheme
{
    OceanBlue,
    VioletCyan
}

public class ThemeService
{
    private static readonly Dictionary<AppTheme, Uri> ThemeUris = new()
    {
        { AppTheme.OceanBlue, new Uri("avares://ExifEditor/Themes/OceanBlueTheme.axaml") },
        { AppTheme.VioletCyan, new Uri("avares://ExifEditor/Themes/VioletCyanTheme.axaml") }
    };

    private ResourceInclude? _currentThemeResource;

    public AppTheme CurrentTheme { get; private set; } = AppTheme.OceanBlue;

    public void ApplyTheme(AppTheme theme)
    {
        var app = Application.Current;
        if (app == null) return;

        if (_currentThemeResource != null)
        {
            app.Resources.MergedDictionaries.Remove(_currentThemeResource);
        }

        _currentThemeResource = new ResourceInclude(ThemeUris[theme])
        {
            Source = ThemeUris[theme]
        };
        app.Resources.MergedDictionaries.Add(_currentThemeResource);

        CurrentTheme = theme;
    }

    public void ToggleTheme()
    {
        var next = CurrentTheme == AppTheme.OceanBlue ? AppTheme.VioletCyan : AppTheme.OceanBlue;
        ApplyTheme(next);
    }

    public static string GetDisplayName(AppTheme theme) => theme switch
    {
        AppTheme.OceanBlue => "Ocean Blue",
        AppTheme.VioletCyan => "Violet Cyan",
        _ => theme.ToString()
    };
}
