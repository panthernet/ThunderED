using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;

namespace ThunderED.Modules
{
    public class IncursionNotifyModule: AppModuleBase
    {
        public override LogCat Category => LogCat.Incursions;

        private DateTime _runAt = DateTime.UtcNow.Date + TimeSpan.FromHours(11) + TimeSpan.FromMinutes(5);
        private bool _isChecked;

        public override async Task Run(object prm)
        {
            if(IsRunning) return;
            IsRunning = true;
            try
            {
                if (_runAt.Date != DateTime.UtcNow.Date)
                {
                    _isChecked = false;
                    _runAt = DateTime.UtcNow.Date + TimeSpan.FromHours(11) + TimeSpan.FromMinutes(5);
                }

                if (_runAt < DateTime.UtcNow && !_isChecked)
                {
                    if (!await APIHelper.ESIAPI.IsServerOnline(Reason))
                    {
                        await Task.Delay(1000);
                        return;
                    }

                    var channel = APIHelper.DiscordAPI.GetChannel(Settings.IncursionNotificationModule.DiscordChannelId);
                    if (channel == null)
                    {
                        await LogHelper.LogError(
                            "IncursionNotificationModule is not configured properly! Make sure you have correct channel specified and bot have correct access rights!");
                        return;
                    }
                    await LogHelper.LogModule("Running Incursions module check...", Category);

                    var incursions = await APIHelper.ESIAPI.GetIncursions(Reason);
                    if (incursions == null) return;
                    _isChecked = true;

                    foreach (var incursion in incursions)
                    {
                        if (Settings.IncursionNotificationModule.Constellations.Count == 0 && Settings.IncursionNotificationModule.Regions.Count == 0)
                        {
                            await ReportIncursion(incursion, null, channel);
                            continue;
                        }

                        var result = false;
                        JsonClasses.ConstellationData c = null;
                        if (Settings.IncursionNotificationModule.Constellations.Count > 0)
                            result = Settings.IncursionNotificationModule.Constellations.Contains(incursion.constellation_id);

                        if (!result)
                        {
                            if (Settings.IncursionNotificationModule.Regions.Count > 0)
                            {
                                c = await APIHelper.ESIAPI.GetConstellationData(Reason, incursion.constellation_id);
                                if (Settings.IncursionNotificationModule.Regions.Contains(c.region_id))
                                    result = true;
                            }
                        }

                        if (result)
                            await ReportIncursion(incursion, c, channel);
                    }

                    await SQLHelper.DeleteWhereIn("incursions", "constId", incursions.Select(a => a.constellation_id).ToList(), not:true);
                }
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

        private async Task ReportIncursion(JsonClasses.IncursionData incursion, JsonClasses.ConstellationData c, IMessageChannel channel)
        {
            var result = await SQLHelper.IsIncurionExists(incursion.constellation_id);
            //skip existing incursion report
            var isUpdate = result && Settings.IncursionNotificationModule.ReportIncursionStatusAfterDT;
            if(!isUpdate && result)
                return;

            if (!isUpdate)
                await SQLHelper.AddIncursion(incursion.constellation_id);

            c = c ?? await APIHelper.ESIAPI.GetConstellationData(Reason, incursion.constellation_id);
            var r = await APIHelper.ESIAPI.GetRegionData(Reason, c.region_id);

            var sb = new StringBuilder();
            foreach (var system in incursion.infested_solar_systems)
            {
                sb.Append((await APIHelper.ESIAPI.GetSystemData(Reason, system)).name);
                sb.Append(" | ");
            }
            sb.Remove(sb.Length - 3, 2);

            var x = new EmbedBuilder().WithTitle(isUpdate ? LM.Get("incursionUpdateHeader", c.name, r.name) : LM.Get("incursionNewHeader", c.name, r.name))                
                .WithColor(isUpdate ? new Color(0x000045) : new Color(0xdd5353))
                .WithThumbnailUrl(Settings.Resources.ImgIncursion)
                .AddField(LM.Get("incursionInfestedSystems"), sb.ToString())
                .AddInlineField(LM.Get("incursionInfluence"), (incursion.influence).ToString("P"))
                .AddInlineField(LM.Get("incursionBoss"), incursion.has_boss ? LM.Get("Alive") : LM.Get("Defeated"))
                .WithCurrentTimestamp()
                .Build();
            await APIHelper.DiscordAPI.SendMessageAsync(channel, Settings.IncursionNotificationModule.DefaultMention ?? "", x);
        }
    }

}
