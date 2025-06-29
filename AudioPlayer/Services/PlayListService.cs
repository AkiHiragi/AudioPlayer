using System;
using System.Collections.Generic;
using System.Linq;
using AudioPlayer.Models;

namespace AudioPlayer.Services;

public class PlayListService
{
    private List<string> playlist;
    private int          currentTrackIndex;
    private PlaybackMode playbackMode;
    private bool         isShuffleEnabled;
    private List<int>    shuffleOrder;
    private int          shuffleIndex;
    private Random       random;

    //События
    public event EventHandler<string> CurrentTrackChanged;
    public event EventHandler         PlaylistChanged;
    public event EventHandler<int>    TrackIndexChanged;

    public List<string> Playlist          => playlist;
    public int          CurrentTrackIndex => currentTrackIndex;
    public int          Count             => playlist.Count;
    public PlaybackMode PlaybackMode      => playbackMode;
    public bool         IsShuffleEnabled  => isShuffleEnabled;

    public string CurrentTrack => currentTrackIndex >= 0 && currentTrackIndex < Count
                                      ? playlist[currentTrackIndex]
                                      : null;

    public PlayListService()
    {
        playlist          = new List<string>();
        shuffleOrder      = new List<int>();
        random            = new Random();
        currentTrackIndex = -1;
        playbackMode      = PlaybackMode.Normal;
    }

    public void AddTrack(string filePath)
    {
        if (!playlist.Contains(filePath))
        {
            playlist.Add(filePath);
            PlaylistChanged?.Invoke(this, EventArgs.Empty);

            if (currentTrackIndex == -1)
                currentTrackIndex = 0;
        }
    }

    public void AddTracks(IEnumerable<string> filePaths)
    {
        var added = false;
        foreach (var path in filePaths)
        {
            if (!playlist.Contains(path))
            {
                playlist.Add(path);
                added = true;
            }
        }

        if (added)
        {
            PlaylistChanged?.Invoke(this, EventArgs.Empty);
            if (currentTrackIndex == -1 && playlist.Count > 0)
                currentTrackIndex = 0;
        }
    }

    public void Clear()
    {
        playlist.Clear();
        shuffleOrder.Clear();
        currentTrackIndex = -1;
        PlaylistChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetCurrentTrack(int index)
    {
        if (index >= 0 && index < playlist.Count)
        {
            currentTrackIndex = index;
            CurrentTrackChanged?.Invoke(this, playlist[index]);
            TrackIndexChanged?.Invoke(this, index);
        }
    }

    public void RemoveTrack(int index)
    {
        if (index < 0 || index >= playlist.Count) return;

        playlist.RemoveAt(index);

        if (currentTrackIndex == index)
        {
            if (currentTrackIndex >= playlist.Count)
                currentTrackIndex = playlist.Count - 1;
        }
        else if (currentTrackIndex > index)
        {
            currentTrackIndex--;
        }

        if (isShuffleEnabled && shuffleOrder.Count > 0)
        {
            UpdateShuffleOrderAfterRemoval(index);
        }

        PlaylistChanged?.Invoke(this, EventArgs.Empty);

        if (playlist.Count == 0)
            currentTrackIndex = -1;
    }

    public void RemoveTrack(string filePath)
    {
        var index = playlist.IndexOf(filePath);
        if (index >= 0)
            RemoveTrack(index);
    }

    private void UpdateShuffleOrderAfterRemoval(int removedIndex)
    {
        for (var i = shuffleOrder.Count - 1; i >= 0; i--)
        {
            if (shuffleOrder[i] == removedIndex)
            {
                shuffleOrder.RemoveAt(i);
                if (i <= shuffleIndex)
                    shuffleIndex--;
            }
            else if (shuffleOrder[i] > removedIndex)
            {
                shuffleOrder[i]--;
            }
        }

        if (shuffleIndex < 0 && shuffleOrder.Count > 0)
            shuffleIndex = 0;
        else if (shuffleIndex >= shuffleOrder.Count)
            shuffleIndex = shuffleOrder.Count - 1;
    }

    public string GetNextTrack()
    {
        if (playlist.Count == 0) return null;

        if (isShuffleEnabled)
        {
            return GetNextShuffled();
        }

        if (currentTrackIndex < playlist.Count - 1)
        {
            currentTrackIndex++;
        }
        else if (playbackMode == PlaybackMode.RepeatAll)
        {
            currentTrackIndex = 0;
        }
        else
        {
            return null;
        }


        CurrentTrackChanged?.Invoke(this, playlist[currentTrackIndex]);
        TrackIndexChanged?.Invoke(this, currentTrackIndex);
        return playlist[currentTrackIndex];
    }

    public string GetPreviousTrack()
    {
        if (playlist.Count == 0) return null;

        if (isShuffleEnabled)
        {
            return GetPreviousShuffled();
        }

        if (currentTrackIndex > 0)
        {
            currentTrackIndex--;
        }
        else if (playbackMode == PlaybackMode.RepeatAll)
        {
            currentTrackIndex = playlist.Count - 1;
        }
        else
        {
            return null;
        }

        CurrentTrackChanged?.Invoke(this, playlist[currentTrackIndex]);
        TrackIndexChanged?.Invoke(this, currentTrackIndex);
        return playlist[currentTrackIndex];
    }

    public void SetShuffle(bool enabled)
    {
        isShuffleEnabled = enabled;
        if (enabled)
        {
            GenerateShuffleOrder();
        }
        else
        {
            shuffleOrder.Clear();
        }
    }

    private void GenerateShuffleOrder()
    {
        shuffleOrder = Enumerable.Range(0, playlist.Count).ToList();

        for (var i = shuffleOrder.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (shuffleOrder[i], shuffleOrder[j]) = (shuffleOrder[j], shuffleOrder[i]);
        }

        shuffleIndex = shuffleOrder.IndexOf(currentTrackIndex);
    }

    private string GetNextShuffled()
    {
        if (shuffleOrder.Count == 0) GenerateShuffleOrder();

        shuffleIndex++;
        if (shuffleIndex >= shuffleOrder.Count)
        {
            if (playbackMode == PlaybackMode.RepeatAll)
            {
                shuffleIndex = 0;
            }
            else
            {
                return null;
            }
        }

        currentTrackIndex = shuffleOrder[shuffleIndex];
        
        CurrentTrackChanged?.Invoke(this, playlist[currentTrackIndex]);
        TrackIndexChanged?.Invoke(this, currentTrackIndex);
        
        return playlist[currentTrackIndex];
    }

    private string GetPreviousShuffled()
    {
        if (shuffleOrder.Count == 0 || shuffleIndex <= 0) return null;

        shuffleIndex--;
        currentTrackIndex = shuffleOrder[shuffleIndex];
        
        CurrentTrackChanged?.Invoke(this, playlist[currentTrackIndex]);
        TrackIndexChanged?.Invoke(this, currentTrackIndex);
        
        return playlist[currentTrackIndex];
    }

    public void SetPlaybackMode(PlaybackMode mode) => playbackMode = mode;
}