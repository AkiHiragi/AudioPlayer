using System.IO;
using System.Text.Json;

namespace AudioPlayer {
    public static class PlaylistManager {

        private static readonly string PlaylistsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AudioPlayer", "Playlists");

        private static readonly string AutoSaveFile = Path.Combine(PlaylistsFolder, "autosave.json");

        static PlaylistManager() {
            if (!Directory.Exists(PlaylistsFolder)) {
                Directory.CreateDirectory(PlaylistsFolder);
            }
        }

        public static void SavePlaylist(PlaylistData playlist, string fileName = null) {
            try {
                playlist.LastModified = DateTime.Now;

                string filePath = fileName ?? AutoSaveFile;
                if (fileName != null && !fileName.EndsWith(".json")) {
                    fileName += ".json";
                    filePath = Path.Combine(PlaylistsFolder, fileName);
                }

                var options = new JsonSerializerOptions {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string jsonString = JsonSerializer.Serialize(playlist, options);
                File.WriteAllText(filePath, jsonString);
            }
            catch (Exception ex) {
                throw new Exception($"Ошибка сохранения плейлиста: {ex.Message}");
            }
        }

        public static PlaylistData LoadPlaylist(string fileName = null) {
            try {
                string filePath = fileName ?? AutoSaveFile;
                if (fileName != null && !Path.IsPathRooted(fileName)) {
                    if (!fileName.EndsWith(".json"))
                        fileName += ".json";
                    filePath = Path.Combine(PlaylistsFolder, fileName);
                }

                if (!File.Exists(filePath))
                    return null;

                string jsonString = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<PlaylistData>(jsonString);
            }
            catch (Exception ex) {
                throw new Exception($"Ошибка загрузки плейлиста: {ex.Message}");
            }
        }

        public static List<string> GetSavedPlaylists() {
            try {
                var playlists = new List<string>();
                if (Directory.Exists(PlaylistsFolder)) {
                    var files = Directory.GetFiles(PlaylistsFolder, "*.json");
                    foreach (var file in files) {
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        if (fileName != "autosave") {
                            playlists.Add(fileName);
                        }
                    }
                }
                return playlists;
            }
            catch {
                return new List<string>();
            }
        }

        public static void DeletePlaylist(string fileName) {
            try {
                if (!fileName.EndsWith(".json"))
                    fileName += ".json";

                string filePath = Path.Combine(PlaylistsFolder, fileName);
                if (File.Exists(filePath)) {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex) {
                throw new Exception($"Ошибка удаления плейлиста: {ex.Message}");
            }
        }

        public static bool AutoSaveExists() {
            return File.Exists(AutoSaveFile);
        }
    }
}
