using System;
using System.IO;
using ThunderED;
using ThunderED.Helpers;

namespace THDWebServer.Classes
{
    public static class WebUiHelper
    {
        private static readonly string DefaultAssetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "Assets");
        private static readonly string CustomAssetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "CustomAssets");

        static WebUiHelper()
        {
            try
            {
                if (!Directory.Exists(CustomAssetPath))
                    Directory.CreateDirectory(CustomAssetPath);
            }
            catch (Exception ex)
            {
                LogHelper.LogEx(ex).GetAwaiter().GetResult();
            }
        }

        public static string GetAsset(string name)
        {
            var def = Path.Combine(DefaultAssetPath, name);
            var cus = Path.Combine(CustomAssetPath, name);
            var asset = File.Exists(cus) ? $"/CustomAssets/{name}" : (File.Exists(def) ? $"/Assets/{name}" : null);

            return asset;
        }

        public static string GetSvg(string name)
        {
            var def = Path.Combine(DefaultAssetPath, "Svg", name);
            var cus = Path.Combine(CustomAssetPath, "Svg", name);
            var asset = File.Exists(cus) ? $"/CustomAssets/Svg/{name}" : (File.Exists(def) ? $"/Assets/Svg/{name}" : null);

            return asset;
        }

        public static string GetCustomHtmlContent(string name)
        {
            var cus = Path.Combine(CustomAssetPath, "Html", name);
            var asset = File.Exists(cus) ? File.ReadAllText(cus) : null;
            return asset;
        }
    }
}
