using System;
using System.IO;
using ThunderED.Classes;

namespace ThunderED.Modules.Sub
{
    public sealed partial class WebServerModule
    {
        public static string GetHtmlResourceDefault(bool defaultCss)
        {
            var dop = defaultCss ? "\n<link rel=\"stylesheet\" href=\"/Content/scripts/default.css\">" : "<link rel=\"stylesheet\" href=\"/Content/scripts/jumbo.css\">";
            return GetHtmlResource("default_package.txt") + dop;
        }

        public static string GetHtmlResourceConfirmation()
        {
            return GetHtmlResource("confirmation_package.txt");
        }

        public static string GetHtmlResourceDatetime()
        {
            return GetHtmlResource("datetime_package.txt");
        }

        public static string GetHtmlResourceBootpage()
        {
            return GetHtmlResource("bootpage_package.txt");
        }

        private static string GetHtmlResource(string filename)
        {
            var file = Path.Combine(SettingsManager.RootDirectory, "Templates", "Resources", filename);
            if (!File.Exists(file))
                throw new FileNotFoundException("Resource file not found!", file);
            var result = File.ReadAllText(file);
            if(string.IsNullOrEmpty(result))
                throw new Exception($"Resource {file} file is empty!");
            return result.Replace("{locale}", LM.Locale);
        }
    }
}
