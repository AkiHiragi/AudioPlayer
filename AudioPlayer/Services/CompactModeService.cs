using System;
using System.Windows;

namespace AudioPlayer.Services;

public class CompactModeService
{
    private Window      mainWindow;
    private double      originalWidth;
    private double      originalHeight;
    private WindowState originalWindowState;
    private ResizeMode  originalResizeMode;

    public bool IsCompactMode { get; private set; } = false;

    public event EventHandler<bool> CompactModeChanged;

    public CompactModeService(Window window)
    {
        mainWindow = window;
        SaveOriginalSize();
    }

    private void SaveOriginalSize()
    {
        originalWidth       = mainWindow.Width;
        originalHeight      = mainWindow.Height;
        originalWindowState = mainWindow.WindowState;
        originalResizeMode  = mainWindow.ResizeMode;
    }

    public void ToggleCompactMode()
    {
        if (IsCompactMode)
            ExitCompactMode();
        else
            EnterCompactMode();
    }

    private void EnterCompactMode()
    {
        if (IsCompactMode) return;

        SaveOriginalSize();

        mainWindow.Width       = 600;
        mainWindow.Height      = 90;
        mainWindow.WindowState = WindowState.Normal;
        mainWindow.ResizeMode  = ResizeMode.NoResize;

        IsCompactMode = true;
        CompactModeChanged?.Invoke(this, true);
    }

    private void ExitCompactMode()
    {
        if (!IsCompactMode) return;

        mainWindow.Width       = originalWidth;
        mainWindow.Height      = originalHeight;
        mainWindow.WindowState = originalWindowState;
        mainWindow.ResizeMode  = originalResizeMode;

        IsCompactMode = false;
        CompactModeChanged?.Invoke(this, false);
    }
}