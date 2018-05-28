using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public abstract class AppModuleBase
    {
        public string Reason => Category.ToString();
        public bool LogToConsole = true;
        public abstract LogCat Category { get; }

        public bool IsRunning { get; set; }

        public virtual Task Run(object prm)
        {
            return Task.CompletedTask;
        }

        public ThunderSettings Settings => SettingsManager.Settings;
    }
}