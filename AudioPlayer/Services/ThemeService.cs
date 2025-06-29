using System;
using MaterialDesignThemes.Wpf;
using System.Windows.Media;

namespace AudioPlayer.Services;

public class ThemeService
{
    private readonly PaletteHelper paletteHelper;

    public event EventHandler<bool> ThemeChanged;

    public bool IsDarkTheme { get; private set; } = true;

    public ThemeService()
    {
        paletteHelper = new PaletteHelper();
        InitializeTheme();
    }

    private void InitializeTheme()
    {
        SetTheme(BaseTheme.Dark, 76, 175, 80, 129, 199, 132);
        IsDarkTheme = true;
    }

    public void ToggleTheme()
    {
        if (IsDarkTheme)
        {
            SetTheme(BaseTheme.Light,156,39,176,186,104,200);
        }
        else
        {
            SetTheme(BaseTheme.Dark, 76, 175, 80, 129, 199, 132);
        }
        
        IsDarkTheme = !IsDarkTheme;
        ThemeChanged?.Invoke(this, IsDarkTheme);
    }

    private void SetTheme(BaseTheme baseTheme,  byte primaryR,   byte primaryG, byte primaryB,
                          byte      secondaryR, byte secondaryG, byte secondaryB)
    {
        var theme = paletteHelper.GetTheme();

        theme.SetBaseTheme(baseTheme);
        theme.SetPrimaryColor(Color.FromRgb(primaryR,     primaryG,   primaryB));
        theme.SetSecondaryColor(Color.FromRgb(secondaryR, secondaryG, secondaryB));

        paletteHelper.SetTheme(theme);
    }
}