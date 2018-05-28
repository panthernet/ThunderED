using System;
using System.Threading.Tasks;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public class NullCampaignModule: AppModuleBase
    {
        public override LogCat Category => LogCat.NullCampaign;
        private DateTime _nextNotificationCheck = DateTime.FromFileTime(0);

        public override async Task Run(object prm)
        {
            if (IsRunning) return;
            try
            {
                IsRunning = true;
                if (DateTime.Now <= _nextNotificationCheck) return;
                _nextNotificationCheck = DateTime.Now.AddMinutes(Settings.NullCampaignModule.CheckIntervalInMinutes);

            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
            finally
            {
                IsRunning = false;
            }
        }
    }
}
