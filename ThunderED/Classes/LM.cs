using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED
{
    public static class LM
    {
        private static readonly Dictionary<string, string> Translations = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        public static string Locale { get; private set; }

        public static async Task Load()
        {
            try
            {
                var tr = SettingsManager.Settings.Config.Language ?? "en-US";
                Locale = tr.Split('-')[0].ToLower();

                var folder = string.IsNullOrEmpty(SettingsManager.Settings.Config.LanguageFilesFolder) ? Path.Combine(SettingsManager.RootDirectory, "Languages") : SettingsManager.Settings.Config.LanguageFilesFolder;
                if (!Directory.Exists(folder))
                {
                    await LogHelper.LogError("LM.Load - language dir not found!");
                    throw new FileNotFoundException("Languages folder and files are absent!");
                }

                var file = Path.Combine(folder, $"{tr}.json");
                if (!File.Exists(file))
                {
                    await LogHelper.LogError("LM.Load - language file not found!");
                    throw new FileNotFoundException("Language file not found!");
                }

                var data = JObject.Parse(await File.ReadAllTextAsync(file));
                Translations.Clear();
                foreach (var pair in data)
                {
                    Translations.Add(pair.Key, (string) pair.Value);
                }

                var path = SettingsManager.IsLinux ? Path.Combine(SettingsManager.RootDirectory, "Data", "custom_language.json") : Path.Combine(SettingsManager.RootDirectory, "custom_language.json");

                if (File.Exists(path))
                {
                    var d = JObject.Parse(await File.ReadAllTextAsync(path));
                    if (d.HasValues)
                    {
                        foreach (var (key, value) in d)
                        {
                            if (Translations.ContainsKey(key))
                                Translations.Remove(key);
                            Translations.Add(key, (string)value);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("LM.Load", ex, LogCat.Translation);
                throw;
            }
        }

        public static string Get(string key)
        {            
            if (string.IsNullOrEmpty(key) || !Translations.ContainsKey(key))
            {
                LogHelper.LogWarning($"Requested translation not found: {key}", LogCat.Translation, false).GetAwaiter().GetResult();
                return "-NO-TRANS-";
            }
            return Translations[key.ToLower()];
        }

        public static string Get(string key, params object[] prms)
        {            
            if (string.IsNullOrEmpty(key) || !Translations.ContainsKey(key))
            {
                LogHelper.LogWarning($"Requested translation not found: {key}", LogCat.Translation, false).GetAwaiter().GetResult();
                return "-NO-TRANS-";
            }

            try
            {
                return string.Format(Translations[key.ToLower()], prms);
            }
            catch
            {
                return "-FORMAT-ERR-";
            }
        }
    }
}
