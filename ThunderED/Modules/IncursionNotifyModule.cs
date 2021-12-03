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

        private DateTime? _lastTimersCheck;

        public override async Task Initialize()
        {
            await LogHelper.LogModule("Initializing Incursions module...", Category);
            await ParseMixedDataArray(new Dictionary<string, List<object>>{{"default", Settings.IncursionNotificationModule.LocationEntities}}, MixedParseModeEnum.Location);
        }

        public override async Task Run(object prm)
        {
            if(IsRunning || !APIHelper.IsDiscordAvailable) return;
            if (TickManager.IsNoConnection || TickManager.IsESIUnreachable) return;
            IsRunning = true;
            try
            {
                if (_lastTimersCheck != null && (DateTime.Now - _lastTimersCheck.Value).TotalMinutes <= 2) return;
                _lastTimersCheck = DateTime.Now;

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

                var regionIds = GetParsedRegions("default") ?? new List<long>();
                var constIds = GetParsedConstellations("default") ?? new List<long>();
                var systemIds = GetParsedSolarSystems("default") ?? new List<long>();

                var sysIds = constIds.SelectMany(a =>
                    (SQLHelper.GetSystemsByConstellation(a).GetAwaiter().GetResult())
                    ?.Select(b => b.system_id)).ToList();
                sysIds.AddRange(regionIds.SelectMany(a =>
                    (SQLHelper.GetSystemsByRegion(a).GetAwaiter().GetResult())
                    ?.Select(b => b.system_id)));
                sysIds.AddRange(systemIds);
                sysIds = sysIds.Distinct().ToList();

                foreach (var incursion in incursions)
                {
                    if (constIds.Count == 0 && regionIds.Count == 0 && systemIds.Count == 0)
                    {
                        await ReportIncursion(incursion, null, channel);
                        continue;
                    }

                    var result = incursion.infested_solar_systems.Intersect(sysIds).Any();
                    if (result)
                        await ReportIncursion(incursion, null, channel);
                }

                await SQLHelper.DeleteWhereIn("incursions", "constId",
                    incursions.Select(a => a.constellation_id).ToList(), not: true);

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
            var result = await DbHelper.IsIncursionExists(incursion.constellation_id);
            //skip existing incursion report
            if(result)
                return;
            await DbHelper.AddIncursion(incursion.constellation_id);

            c ??= await APIHelper.ESIAPI.GetConstellationData(Reason, incursion.constellation_id);
            var r = await APIHelper.ESIAPI.GetRegionData(Reason, c.region_id);

            var sb = new StringBuilder();
            foreach (var system in incursion.infested_solar_systems)
            {
                sb.Append((await APIHelper.ESIAPI.GetSystemData(Reason, system)).name);
                sb.Append(" | ");
            }
            sb.Remove(sb.Length - 3, 2);
            //LM.Get("incursionUpdateHeader", c.name, r.name)
            //new Color(0x000045)
            var x = new EmbedBuilder().WithTitle(LM.Get("incursionNewHeader", c.name, r.name))                
                .WithColor(new Color(0xdd5353))
                .WithThumbnailUrl(Settings.Resources.ImgIncursion)
                .AddField(LM.Get("incursionInfestedSystems"), sb.ToString())
                .AddField(LM.Get("incursionInfluence"), (incursion.influence).ToString("P"), true)
                .AddField(LM.Get("incursionBoss"), incursion.has_boss ? LM.Get("Alive") : LM.Get("Defeated"), true)
                .WithCurrentTimestamp()
                .Build();
            await APIHelper.DiscordAPI.SendMessageAsync(channel, Settings.IncursionNotificationModule.DefaultMention ?? "", x);
        }
    }

}
