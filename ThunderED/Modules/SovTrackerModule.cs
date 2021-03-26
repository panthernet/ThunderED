using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using ThunderED.Helpers;
using ThunderED.Json;

namespace ThunderED.Modules
{
    public class SovTrackerModule : AppModuleBase
    {
        public override LogCat Category => LogCat.SovTracker;
        private int _checkInterval;
        private DateTime _lastCheckTime = DateTime.MinValue;

        private const long TCU_TYPEID = 32226;
        private const long IHUB_TYPEID = 32458;

        private readonly Dictionary<string, Dictionary<string, List<long>>> _userStorage = new Dictionary<string, Dictionary<string, List<long>>>();

        public override async Task Initialize()
        {
            await LogHelper.LogModule("Initializing Sov Tracker module...", Category);
            _checkInterval = Settings.SovTrackerModule.CheckIntervalInMinutes;

            var data = Settings.SovTrackerModule.GetEnabledGroups().ToDictionary(pair => pair.Key, pair => pair.Value.HolderAllianceEntities);
            await ParseMixedDataArray(data, MixedParseModeEnum.Member, _userStorage);

            data = Settings.SovTrackerModule.GetEnabledGroups().ToDictionary(pair => pair.Key, pair => pair.Value.LocationEntities);
            await ParseMixedDataArray(data, MixedParseModeEnum.Location);

        }

        public override async Task Run(object prm)
        {
            if (IsRunning || !APIHelper.IsDiscordAvailable) return;
            IsRunning = true;
            try
            {
                if((DateTime.Now - _lastCheckTime).TotalMinutes < _checkInterval) return;
                _lastCheckTime = DateTime.Now;
                await LogHelper.LogModule("Running Sov Tracker check...", Category);

                var data = await APIHelper.ESIAPI.GetSovStructuresData(Reason);
                foreach (var (groupName, group) in Settings.SovTrackerModule.GetEnabledGroups())
                {
                    // var t = Stopwatch.StartNew();

                    if (APIHelper.DiscordAPI.GetChannel(group.DiscordChannelId) == null)
                    {
                        await SendOneTimeWarning(groupName + "ch",
                            $"Group {groupName} has invalid Discord channel ID!");
                        continue;
                    }

                    var trackerData = await SQLHelper.GetSovIndexTrackerData(groupName);
                    var holderIds = GetParsedAlliances(groupName, _userStorage) ?? new List<long>();

                    if (!trackerData.Any())
                    {
                        var list = GetUpdatedList(data, group, groupName, holderIds);
                        if (!list.Any())
                            await SendOneTimeWarning(groupName, $"No systems found for Sov Tracker group {groupName}!");
                        else
                            await SQLHelper.SaveSovIndexTrackerData(groupName, list);
                        return;
                    }

                    var idList = trackerData.Select(a => a.solar_system_id).Distinct();

                    //expensive check for HolderAlliances
                    var workingSet = !holderIds.Any()
                        ? data.Where(a => idList.Contains(a.solar_system_id)).ToList()
                        : GetUpdatedList(data, group, groupName, holderIds);

                    //check ADM
                    if (group.TrackADMIndexChanges)
                        foreach (var d in workingSet.Where(a => a.structure_type_id == TCU_TYPEID))
                        {
                            if (group.WarningThresholdValue > 0 &&
                                d.vulnerability_occupancy_level < group.WarningThresholdValue)
                                await SendIndexWarningMessage(d, group);
                        }

                    //check sov
                    if (group.TrackIHUBHolderChanges || group.TrackTCUHolderChanges)
                        foreach (var d in workingSet)
                        {
                            if (group.TrackIHUBHolderChanges && d.structure_type_id == IHUB_TYPEID)
                            {
                                var old = trackerData.FirstOrDefault(a =>
                                    a.solar_system_id == d.solar_system_id && a.structure_type_id == IHUB_TYPEID);
                                if ((old?.alliance_id ?? 0) != (d?.alliance_id ?? 0))
                                    await SendHolderChangedMessage(d, old, group, false);
                            }

                            if (group.TrackTCUHolderChanges && d.structure_type_id == TCU_TYPEID)
                            {
                                var old = trackerData.FirstOrDefault(a =>
                                    a.solar_system_id == d.solar_system_id && a.structure_type_id == TCU_TYPEID);
                                if ((old?.alliance_id ?? 0) != (d?.alliance_id ?? 0))
                                    await SendHolderChangedMessage(d, old, group, true);
                            }
                        }

                    await SQLHelper.SaveSovIndexTrackerData(groupName, workingSet);
                    // t.Stop();
                    // Debug.WriteLine($"Sov check: {t.Elapsed.TotalSeconds}sec");
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

        private List<JsonClasses.SovStructureData> GetUpdatedList(List<JsonClasses.SovStructureData> data, SovTrackerGroup group, string groupName, List<long> holderIds)
        {
            var t2 = Stopwatch.StartNew();
            try
            {
                var list = data.ToList();
                var groupSystems = GetParsedSolarSystems(groupName) ?? new List<long>();
                var groupConstellations = GetParsedConstellations(groupName) ?? new List<long>();
                var groupRegions = GetParsedRegions(groupName) ?? new List<long>();
                if (groupSystems.Any())
                    list = list.Where(a => groupSystems.Contains(a.solar_system_id)).ToList();
                if (holderIds.Any())
                    list = list.Where(a => holderIds.Contains(a.alliance_id)).ToList();
                if (groupSystems.Any())
                    list = list.Where(a => groupSystems.Contains(a.solar_system_id)).ToList();
                var hasRegions = groupRegions.Any();
                var hasConsts = groupConstellations.Any();
                if (hasRegions || hasConsts)
                    list = list.Where(a =>
                    {
                        var system = APIHelper.ESIAPI.GetSystemData(Reason, a.solar_system_id).GetAwaiter().GetResult();
                        if (!system.DB_RegionId.HasValue) return false;
                        if (hasRegions && groupRegions.Contains(system.DB_RegionId.Value))
                            return true;

                        return hasConsts && @groupConstellations.Contains(system.constellation_id);
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
                msg = LM.Get("sovLostStructure", isTcu? "TCU": "IHUB");
            else
            {
                var oldHolder = old == null ? LM.Get("sovSystemUncontested") : LM.Get("sovSystemWasOwnedBy", oldOwner.name, oldOwner.ticker);
                var timers = data == null
                    ? null
                    : LM.Get("sovNextVulnerability", data.vulnerable_start_time.ToString(Settings.Config.ShortTimeFormat), data.vulnerable_end_time.ToString(Settings.Config.ShortTimeFormat));
                msg =  $"{LM.Get(isTcu?"sovNewHolder": "sovNewIhubHolder", owner.name, owner.ticker)} {oldHolder}{timers}";
            }
            var embed = new EmbedBuilder()
                .WithThumbnailUrl(Settings.Resources.ImgLowFWStand)
                .AddField(LM.Get("sovSystem"), system?.name ?? LM.Get("Unknown"), true)
                .AddField(LM.Get("sovMessage"), msg);
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
            var msg = LM.Get("sovLowIndexMessage", data.vulnerability_occupancy_level);
            var embed = new EmbedBuilder()
                .WithThumbnailUrl(Settings.Resources.ImgLowFWStand)
                .AddField(LM.Get("sovSystem"), system?.name ?? LM.Get("Unknown"), true)
                .AddField(LM.Get("sovHolder"), alliance?.name ?? LM.Get("Unknown"), true)
                .AddField(LM.Get("sovMessage"), msg);
            var ch = APIHelper.DiscordAPI.GetChannel(group.DiscordChannelId);
            var mention = string.Join(' ', group.DiscordMentions);
            if (string.IsNullOrEmpty(mention))
                mention = " ";
            await APIHelper.DiscordAPI.SendMessageAsync(ch, $"{mention}", embed.Build()).ConfigureAwait(false);
        }
    }
}
