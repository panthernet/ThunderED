using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;

namespace ThunderED.Modules
{
    public class SovTrackerModule : AppModuleBase
    {
        public override LogCat Category => LogCat.SovIndexTracker;
        private readonly int _checkInterval;
        private DateTime _lastCheckTime = DateTime.MinValue;

        private const long TCU_TYPEID = 32226;
        private const long IHUB_TYPEID = 32458;


        public SovTrackerModule()
        {
            _checkInterval = Settings.SovTrackerModule.CheckIntervalInMinutes;
        }

        public override async Task Run(object prm)
        {
            if (IsRunning) return;
            IsRunning = true;
            try
            {
                if((DateTime.Now - _lastCheckTime).TotalMinutes < _checkInterval) return;
                _lastCheckTime = DateTime.Now;
                await LogHelper.LogModule("Running Sov Tracker check...", Category);

                var data = await APIHelper.ESIAPI.GetSovStructuresData(Reason);
                foreach (var pair in Settings.SovTrackerModule.Groups)
                {
                    var t = Stopwatch.StartNew();
                    var group = pair.Value;
                    var groupName = pair.Key;

                    if (APIHelper.DiscordAPI.GetChannel(group.DiscordChannelId) == null)
                    {
                        await SendOneTimeWarning(groupName + "ch", $"Group {groupName} has invalid Discord channel ID!");
                        continue;
                    }

                    var trackerData = await SQLHelper.GetSovIndexTrackerData(groupName);

                    if (!trackerData.Any())
                    {
                        var list = GetUpdatedList(data, group);
                        if (!list.Any())
                            await SendOneTimeWarning(groupName, $"No systems found for Sov Index Tracker group {group}!");
                        else
                            await SQLHelper.SaveSovIndexTrackerData(groupName, list);
                        return;
                    }

                    var idList = trackerData.Select(a => a.solar_system_id).Distinct();
                    //expensive check for HolderAlliances
                    var workingSet = !group.HolderAlliances.Any() ? data.Where(a => idList.Contains(a.solar_system_id)).ToList() : GetUpdatedList(data, group);

                    //check ADM
                    foreach (var d in workingSet.Where(a=> a.structure_type_id == TCU_TYPEID))
                    {
                        if (group.WarningThresholdValue > 0 && d.vulnerability_occupancy_level < group.WarningThresholdValue)
                            await SendIndexWarningMessage(d, group);
                    }

                    //check sov
                    foreach (var d in workingSet)
                    {
                        if (group.TrackIHUBHolderChanges && d.structure_type_id == IHUB_TYPEID)
                        {
                            var old = trackerData.FirstOrDefault(a => a.solar_system_id == d.solar_system_id && a.structure_type_id == IHUB_TYPEID);
                            if ((old?.alliance_id ?? 0) != (d?.alliance_id ?? 0))
                                await SendHolderChangedMessage(d, old, group, false);
                        }
                        if (group.TrackTCUHolderChanges && d.structure_type_id == TCU_TYPEID)
                        {
                            var old = trackerData.FirstOrDefault(a => a.solar_system_id == d.solar_system_id && a.structure_type_id == TCU_TYPEID);
                            if ((old?.alliance_id ?? 0) != (d?.alliance_id ?? 0))
                                await SendHolderChangedMessage(d, old, group, true);
                        }
                    }

                    await SQLHelper.SaveSovIndexTrackerData(groupName, workingSet);
                    t.Stop();
                    Debug.WriteLine($"Sov check: {t.Elapsed.TotalSeconds}sec");
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

        private List<JsonClasses.SovStructureData> GetUpdatedList(List<JsonClasses.SovStructureData> data, SovTrackerGroup group)
        {
            var t2 = Stopwatch.StartNew();
            try
            {
                var list = data.ToList();
                if (group.Systems.Any())
                    list = list.Where(a => group.Systems.Contains(a.solar_system_id)).ToList();
                if (group.HolderAlliances.Any())
                    list = list.Where(a => group.HolderAlliances.Contains(a.alliance_id)).ToList();
                if (group.Systems.Any())
                    list = list.Where(a => group.Systems.Contains(a.solar_system_id)).ToList();
                var hasRegions = group.Regions.Any();
                var hasConsts = group.Constellations.Any();
                if (hasRegions || hasConsts)
                    list = list.Where(a =>
                    {
                        var system = APIHelper.ESIAPI.GetSystemData(Reason, a.solar_system_id).GetAwaiter().GetResult();
                        if (!system.DB_RegionId.HasValue) return false;
                        if (hasRegions && group.Regions.Contains(system.DB_RegionId.Value))
                            return true;

                        return hasConsts && @group.Constellations.Contains(system.constellation_id);
                    }).ToList();
                return list;
            }
            finally
            {
                t2.Stop();
                Debug.WriteLine($"Sov Upd: {t2.Elapsed.TotalSeconds}sec");
            }
        }

        private async Task SendHolderChangedMessage(JsonClasses.SovStructureData data, JsonClasses.SovStructureData old, SovTrackerGroup @group, bool isTcu)
        {
            var system = await APIHelper.ESIAPI.GetSystemData(Reason, data?.solar_system_id ?? old.solar_system_id);
            var owner = data != null ? await APIHelper.ESIAPI.GetAllianceData(Reason, data.alliance_id) : null;
            var oldOwner = old != null ? await APIHelper.ESIAPI.GetAllianceData(Reason, old.alliance_id) : null;

            string msg;
            if (owner == null)
                msg = $"System has lost its {(isTcu? "TCU": "IHUB")}!";
            else
            {
                var oldHolder = old == null ? "It was previously uncontested." : $"It was previously owned by {oldOwner.name}[{oldOwner.ticker}].";
                var timers = data == null
                    ? null
                    : $"\n\nNext vulnerabilty window is from {data.vulnerable_start_time.ToString(Settings.Config.ShortTimeFormat)} to {data.vulnerable_end_time.ToString(Settings.Config.ShortTimeFormat)}";
                msg = $"{owner.name}[{owner.ticker}] is the new sov holder in this system. {oldHolder}{timers}";
            }
            var embed = new EmbedBuilder()
                .WithThumbnailUrl(Settings.Resources.ImgLowFWStand)
                .AddField("System", system?.name ?? LM.Get("Unknown"), true)
                .AddField("Message", msg);
            var ch = APIHelper.DiscordAPI.GetChannel(group.DiscordChannelId);
            var mention = string.Join(' ', group.DiscordMentions);
            if (string.IsNullOrEmpty(mention))
                mention = " ";
            await APIHelper.DiscordAPI.SendMessageAsync(ch, $"{mention}", embed.Build()).ConfigureAwait(false);

        }

        private async Task SendIndexWarningMessage(JsonClasses.SovStructureData data, SovTrackerGroup group)
        {
            var system = await APIHelper.ESIAPI.GetSystemData(Reason, data.solar_system_id);
            var alliance = await APIHelper.ESIAPI.GetAllianceData(Reason, data.alliance_id);
            var msg = $"ADM index has dropped to very low value of {data.vulnerability_occupancy_level}. Get to work buddies!";
            var embed = new EmbedBuilder()
                .WithThumbnailUrl(Settings.Resources.ImgLowFWStand)
                .AddField("System", system?.name ?? LM.Get("Unknown"), true)
                .AddField("Holder", alliance?.name ?? LM.Get("Unknown"), true)
                .AddField("Message", msg);
            var ch = APIHelper.DiscordAPI.GetChannel(group.DiscordChannelId);
            var mention = string.Join(' ', group.DiscordMentions);
            if (string.IsNullOrEmpty(mention))
                mention = " ";
            await APIHelper.DiscordAPI.SendMessageAsync(ch, $"{mention}", embed.Build()).ConfigureAwait(false);
        }
    }
}
