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

                var folder = Path.Combine(SettingsManager.RootDirectory, "Languages");
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

                var data = JObject.Parse(File.ReadAllText(file));
                Translations.Clear();
                foreach (var pair in data)
                {
                    Translations.Add(pair.Key, (string) pair.Value);
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
    }
}
