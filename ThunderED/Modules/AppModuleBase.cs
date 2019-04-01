using System.Collections.Concurrent;
using System.Collections.Generic;
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

        /// <summary>
        /// List of IDs to control one-time warnings during the single bot session
        /// </summary>
        private readonly List<object> _oneTimeWarnings = new List<object>();

        public bool IsRunning { get; set; }

        public virtual Task Run(object prm)
        {
            return Task.CompletedTask;
        }

        protected async Task SendOneTimeWarning(object id, string message)
        {
            if(_oneTimeWarnings.Contains(id)) return;

            await LogHelper.LogWarning(message, Category);
            _oneTimeWarnings.Add(id);
        }

        public virtual async Task Initialize()
        {

        }

        public ThunderSettings Settings => SettingsManager.Settings;
    }
}