using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace AudioPlayer.Services;

public class DragDropService
{
    private PlayListService playlistService;
    private readonly string[] supportedFormats = { ".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac" };
    
    public event EventHandler<string> NotificationRequested;
    
    public DragDropService(PlayListService playlistService)
    {
        this.playlistService = playlistService;
    }
    
    public bool HasAudioFiles(string[] files)
    {
        return files.Any(file =>
        {
            string extension = Path.GetExtension(file).ToLower();
            return supportedFormats.Contains(extension);
        });
    }
    
    public void AddAudioFiles(string[] files)
    {
        var audioFiles = new List<string>();
        
        foreach (string file in files)
        {
            if (File.Exists(file))
            {
                string extension = Path.GetExtension(file).ToLower();
                if (supportedFormats.Contains(extension))
                {
                    audioFiles.Add(file);
                }
            }
            else if (Directory.Exists(file))
            {
                AddAudioFilesFromDirectory(file, audioFiles);
            }
        }
        
        if (audioFiles.Count > 0)
        {
            playlistService.AddTracks(audioFiles);
            NotificationRequested?.Invoke(this, $"Добавлено треков: {audioFiles.Count}");
        }
    }
    
    private void AddAudioFilesFromDirectory(string directoryPath, List<string> audioFiles)
    {
        try
        {
            foreach (string extension in supportedFormats)
            {
                string[] files = Directory.GetFiles(directoryPath, $"*{extension}", SearchOption.AllDirectories);
                audioFiles.AddRange(files);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при добавлении файлов из папки: {ex.Message}", 
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}