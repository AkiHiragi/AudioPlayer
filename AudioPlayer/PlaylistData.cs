using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AudioPlayer {
    public class PlaylistData {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("createdDate")]
        public DateTime CreatedDate { get; set; }

        [JsonPropertyName("lastModified")]
        public DateTime LastModified { get; set; }

        [JsonPropertyName("currentTrackIndex")]
        public int CurrentTrackIndex { get; set; }

        [JsonPropertyName("items")]
        public List<PlaylistItem> Items { get; set; }

        [JsonPropertyName("playbackMode")]
        public string PlaybackMode { get; set; }

        [JsonPropertyName("isShuffledEnabled")]
        public bool IsShuffleEnabled { get; set; }

        [JsonPropertyName("volume")]
        public double Volume { get; set; }

        public PlaylistData() {
            Items = new List<PlaylistItem>();
            CreatedDate = DateTime.Now;
            LastModified = DateTime.Now;
            CurrentTrackIndex = -1;
            PlaybackMode = "Normal";
            IsShuffleEnabled = false;
            Volume = 50.0;
        }
    }
}
