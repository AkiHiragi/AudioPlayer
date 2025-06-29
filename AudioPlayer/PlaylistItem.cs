using System;
using System.IO;
using System.Text.Json.Serialization;

namespace AudioPlayer {
    public class PlaylistItem {
        [JsonPropertyName("filePath")]
        public string FilePath { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("duration")]
        public string Duration { get; set; }

        [JsonPropertyName("addedDate")]
        public DateTime AddedDate { get; set; }

        public PlaylistItem() {
            AddedDate = DateTime.Now;
        }

        public PlaylistItem(string filePath) {
            FilePath = filePath;
            DisplayName = Path.GetFileName(filePath);
            AddedDate = DateTime.Now;
            Duration = "00:00";
        }
    }
}
