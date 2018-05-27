using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Modules.Settings;

namespace ThunderED.Modules
{
    public class IncursionNotifyModule: AppModuleBase
    {
        public override LogCat Category => LogCat.Incursions;

        public IncursionNotifySettings Settings { get; }
        private DateTime _runAt = DateTime.Today + TimeSpan.FromHours(11);
        private bool _isChecked;

        public IncursionNotifyModule()
        {
            Settings = IncursionNotifySettings.Load(SettingsManager.FileSettingsPath);
        }

        public override async Task Run(object prm)
        {
            if (_runAt.Date != DateTime.UtcNow.Date)
            {
                _isChecked = false;
                _runAt = DateTime.Today + TimeSpan.FromHours(11);
            }

            if(IsRunning) return;
            IsRunning = true;
            try
            {
                if (_runAt < DateTime.UtcNow && !_isChecked)
                {
                    if (!await APIHelper.ESIAPI.IsServerOnline(Reason))
                    {
                        await Task.Delay(1000);
                        return;
                    }

                    var channel = APIHelper.DiscordAPI.GetChannel(Settings.Core.DiscordChannelId);
                    if (channel == null)
                    {
                        await LogHelper.LogError(
                            "IncursionNotificationModule is not configured properly! Make sure you have correct channel specified and bot have correct access rights!");
                        return;
                    }

                    var incursions = await APIHelper.ESIAPI.GetIncursions(Reason);
                    if (incursions == null) return;
                    _isChecked = true;

                    foreach (var incursion in incursions)
                    {
                        if (Settings.Core.Constellations.Count == 0 && Settings.Core.Regions.Count == 0)
                        {
                            await ReportIncursion(incursion, null, channel);
                            continue;
                        }

                        var result = false;
                        JsonClasses.ConstellationData c = null;
                        if (Settings.Core.Constellations.Count > 0)
                            result = Settings.Core.Constellations.Contains(incursion.constellation_id);

                        if (!result)
                        {
                            if (Settings.Core.Regions.Count > 0)
                            {
                                c = await APIHelper.ESIAPI.GetConstellationData(Reason, incursion.constellation_id);
                                if (Settings.Core.Regions.Contains(c.region_id))
                                    result = true;
                            }
                        }

                        if (result)
                            await ReportIncursion(incursion, c, channel);
                    }

                    await SQLHelper.SQLiteDataDeleteWhereIn("incursions", "constId", incursions.Select(a => a.constellation_id).ToList(), not:true);
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
            var result = await SQLHelper.SQLiteDataQuery<int>("incursions", "constId", "constId", incursion.constellation_id);
            //skip existing incursion report
            var isUpdate = result > 0 && Settings.Core.ReportIncursionStatusAfterDT;
            if(!isUpdate && result > 0)
                return;

            if (!isUpdate)
                await SQLHelper.SQLiteDataInsertOrUpdate("incursions", new Dictionary<string, object>
                {
                    { "constId", incursion.constellation_id }
                });

            c = c ?? await APIHelper.ESIAPI.GetConstellationData(Reason, incursion.constellation_id);
            var r = await APIHelper.ESIAPI.GetRegionData(Reason, c.region_id);

            var sb = new StringBuilder();
            foreach (var system in incursion.infested_solar_systems)
            {
                sb.Append((await APIHelper.ESIAPI.GetSystemData(Reason, system)).name);
                sb.Append(" | ");
            }
            sb.Remove(sb.Length - 3, 2);

            var x = new EmbedBuilder().WithTitle(isUpdate ? $"Incursion status update for {c.name} const of {r.name} region" : $"Incursion spotted in {c.name} const of {r.name} region!")                
                .WithColor(isUpdate ? new Color(0x000045) : new Color(0xdd5353))
                .WithThumbnailUrl(SettingsManager.Get("resources", "imgIncursion"))
                .AddField("Infested Systems", sb.ToString())
                .AddInlineField("Influence", (incursion.influence).ToString("P"))
                .AddInlineField("Boss", incursion.has_boss ? "Alive" : "Defeated")
                .WithCurrentTimestamp()
                .Build();
            await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", x);
        }
    }

}
