using System;
using System.IO;
using System.Xml.Serialization;
using MEPBuriedDepthCalculator.Models;

namespace MEPBuriedDepthCalculator.Services
{
    public class UserSettings
    {
        public SelectionMode LastSelectionMode { get; set; } = SelectionMode.CurrentSelection;
        public string LastSelectedLinkName { get; set; } = string.Empty;
        public long LastSelectedLinkInstanceId { get; set; } = -1;
    }

    public static class SettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Hatco", "MEPBuriedDepthCalculator", "settings.xml");

        public static UserSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return new UserSettings();

                var serializer = new XmlSerializer(typeof(UserSettings));
                using (var reader = new StreamReader(SettingsPath))
                {
                    return (UserSettings)serializer.Deserialize(reader);
                }
            }
            catch
            {
                return new UserSettings();
            }
        }

        public static void Save(UserSettings settings)
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var serializer = new XmlSerializer(typeof(UserSettings));
                using (var writer = new StreamWriter(SettingsPath))
                {
                    serializer.Serialize(writer, settings);
                }
            }
            catch
            {
                // Ignore save errors
            }
        }
    }
}
