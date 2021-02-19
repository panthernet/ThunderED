using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ThunderED.Helpers;

namespace THDWebServer.Classes
{
    public class WebConfig
    {
        public static WebConfig Instance { get; set; }

        public ConfigModule Config { get; set; } = new ConfigModule();
        public ApiModule Api { get; set; } = new ApiModule();

        public static async Task<bool> Load(string filename)
        {
            try
            {
                if(!File.Exists(filename))
                    return false;
                Instance = JsonConvert.DeserializeObject<WebConfig>(await File.ReadAllTextAsync(filename));
                return Instance != null;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex);
                return false;
            }
        }
    }

    public class ApiModule
    {
        public bool IsEnabled { get; set; } = false;
    }

    public class ConfigModule
    {
    }
}
