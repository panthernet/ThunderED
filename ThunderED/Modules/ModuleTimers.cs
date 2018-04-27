using System.Threading.Tasks;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    internal class ModuleTimers: AppModuleBase
    {
        public override LogCat Category => LogCat.Timers;

        public override async  Task Run(object prm)
        {
            await ProcessTimers();
        }

        private async Task ProcessTimers()
        {

        }
    }
}
