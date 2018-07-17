using System;
using System.Threading.Tasks;
using Discord;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules.OnDemand
{
    public class ContinousCheckModule: AppModuleBase
    {
        public override LogCat Category => LogCat.AutoPost;

        private DateTime _checkOneSec = DateTime.MinValue;

        private bool? _IsTQOnline;
        private bool _isTQOnlineRunning;

        public override async Task Run(object prm)
        {
            try
            {
                var now = DateTime.Now;
                if (!_IsTQOnline.HasValue)
                    _IsTQOnline = !TickManager.IsNoConnection;

                if ((now - _checkOneSec).TotalSeconds >= 1)
                {
                    _checkOneSec = now;
                    //onesec ops
                    if (Settings.ContinousCheckModule.EnableTQStatusPost && _IsTQOnline != !TickManager.IsNoConnection && !_isTQOnlineRunning)
                    {
                        try
                        {
                            _isTQOnlineRunning = true;
                            if (APIHelper.DiscordAPI.IsAvailable)
                            {
                                var msg = _IsTQOnline.Value ? $"{LM.Get("autopost_tq")} {LM.Get("Offline")}" : $"{LM.Get("autopost_tq")} {LM.Get("Online")}";
                                var color = _IsTQOnline.Value ? new Discord.Color(0xFF0000) : new Discord.Color(0x00FF00);
                                foreach (var channelId in Settings.ContinousCheckModule.TQStatusPostChannels)
                                {
                                    try
                                    {
                                        var embed = new EmbedBuilder().WithTitle(msg).WithColor(color);
                                        await APIHelper.DiscordAPI.SendMessageAsync(channelId, Settings.ContinousCheckModule.TQStatusPostMention, embed.Build());
                                    }
                                    catch (Exception ex)
                                    {
                                        await LogHelper.LogEx("Autopost - TQStatus", ex, Category);
                                    }
                                }

                                _IsTQOnline = !TickManager.IsNoConnection;
                            }
                        }
                        finally
                        {
                            _isTQOnlineRunning = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("Autopost", ex, Category);
            }
        }
    }
}
