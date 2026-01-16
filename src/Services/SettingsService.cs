using System;
using System.IO;
using System.Text.Json;
using EasyScreenRecord.Models;

namespace EasyScreenRecord.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly string _filePath;
        public AppSettings CurrentSettings { get; private set; }

        public SettingsService()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EasyScreenRecord");
            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "settings.json");
            
            Load();
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    CurrentSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    CurrentSettings = new AppSettings();
                }
            }
            catch
            {
                CurrentSettings = new AppSettings();
            }
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(CurrentSettings, options);
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // Silently fail or log? For now, silent.
            }
        }
    }
}
