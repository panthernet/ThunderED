using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Json.ZKill;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules.OnDemand
{
    public partial class LiveKillFeedModule: AppModuleBase
    {
        private static readonly ConcurrentDictionary<string, long> LastPostedDictionary = new ConcurrentDictionary<string, long>();

        protected readonly Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>> ParsedVictimsLists = new Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>>();
        protected readonly Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>> ParsedAttackersLists = new Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>>();
        protected readonly Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>> ParsedExcludeVictimsLists = new Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>>();
        protected readonly Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>> ParsedExcludeAttackersLists = new Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>>();
        protected readonly Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>> ParsedLocationLists = new Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>>();
        protected readonly Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>> ParsedExcludeLocationLists = new Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>>();
        protected readonly Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>> ParsedShipsLists = new Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>>();
        protected readonly Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>> ParsedExcludeShipsLists = new Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>>();

        public override LogCat Category => LogCat.KillFeed;

        public LiveKillFeedModule()
        {
            LogHelper.LogModule("Initializing LiveKillFeed module...", Category).GetAwaiter().GetResult();
            ZKillLiveFeedModule.Queryables.Add(ProcessKill);
        }

        public override async Task Initialize()
        {
            //check for group name dupes
            var dupes = SettingsManager.Settings.LiveKillFeedModule.Groups.Keys.GetDupes();
            if(dupes.Any())
                await LogHelper.LogWarning($"Module has groups with identical names: {string.Join(',', dupes)}\n Please set unique group names to avoid inconsistency during KM checks.", Category);

            foreach (var (key, value) in Settings.LiveKillFeedModule.Groups)
            {
                dupes = value.Filters.Keys.GetDupes();
                if(dupes.Any())
                    await LogHelper.LogWarning($"Group {key} has filters with identical names: {string.Join(',', dupes)}\n Please set unique filter names to avoid inconsistency during KM checks.", Category);
            }

            //check for Discord channels
            foreach (var (key, value) in Settings.LiveKillFeedModule.Groups)
            {
                if(!value.DiscordChannels.Any() && value.Filters.Values.Any(a=> !a.DiscordChannels.Any()))
                    await LogHelper.LogWarning($"Module group {key} has no Discord channels specified or has filters without channels!", Category);
            }

            //check filters
            var groupNames = Settings.LiveKillFeedModule.Groups.Where(a => !a.Value.Filters.Any()).Select(a => a.Key);
            if(groupNames.Any())
                await LogHelper.LogWarning($"Groups {string.Join(',', groupNames)} has no filters!", Category);

            groupNames = Settings.LiveKillFeedModule.Groups.Where(a => !a.Value.FeedPvpKills && !a.Value.FeedPveKills || !a.Value.FeedAwoxKills && !a.Value.FeedNotAwoxKills || !a.Value.FeedSoloKills && !a.Value.FeedGroupKills).Select(a => a.Key);
            if(groupNames.Any())
                await LogHelper.LogWarning($"Groups {string.Join(',', groupNames)} has mutually exclusive Feed params!", Category);

            //check templates
            foreach (var templateFile in Settings.LiveKillFeedModule.Groups.Where(a=> !string.IsNullOrWhiteSpace(a.Value.MessageTemplateFileName)).Select(a=> a.Value.MessageTemplateFileName))
            {
                if(!File.Exists(Path.Combine(SettingsManager.DataDirectory, "Templates", "Messages", templateFile)))
                    await LogHelper.LogWarning($"Specified template file {templateFile} not found!", Category);
            }

            //parse data
            foreach (var (key, value) in Settings.LiveKillFeedModule.Groups)
            {
                var aGroupDic = new Dictionary<string, Dictionary<string, List<long>>>();
                var exaGroupDic = new Dictionary<string, Dictionary<string, List<long>>>();
                var vGroupDic = new Dictionary<string, Dictionary<string, List<long>>>();
                var exvGroupDic = new Dictionary<string, Dictionary<string, List<long>>>();
                var lGroupDic = new Dictionary<string, Dictionary<string, List<long>>>();
                var exlGroupDic = new Dictionary<string, Dictionary<string, List<long>>>();
                var sGroupDic = new Dictionary<string, Dictionary<string, List<long>>>();
                var exsGroupDic = new Dictionary<string, Dictionary<string, List<long>>>();
                foreach (var (fKey, fValue) in value.Filters)
                {
                    var aData = await ParseMemberDataArray(fValue.AttackerEntities.Where(a=> !a.ToString().StartsWith("-")).ToList());
                    aGroupDic.Add(fKey, aData);
                    var vData = await ParseMemberDataArray(fValue.VictimEntities.Where(a=> !a.ToString().StartsWith("-")).ToList());
                    vGroupDic.Add(fKey, vData);
                    var lData = await ParseLocationDataArray(fValue.LocationEntities.Where(a=> !a.ToString().StartsWith("-")).ToList());
                    lGroupDic.Add(fKey, lData);
                    var sData = await ParseTypeDataArray(fValue.ShipEntities.Where(a=> !a.ToString().StartsWith("-")).ToList());
                    sGroupDic.Add(fKey, sData);
                    //excluded
                    var exaData = await ParseMemberDataArray(fValue.AttackerEntities.Where(a=> a.ToString().StartsWith("-")).Select(a=>
                    {
                        var str = a.ToString();
                        return (object)str.Substring(1, str.Length-1);
                    }).ToList());
                    exaGroupDic.Add(fKey, exaData);
                    var exvData = await ParseMemberDataArray(fValue.VictimEntities.Where(a=> a.ToString().StartsWith("-")).Select(a=>
                    {
                        var str = a.ToString();
                        return (object)str.Substring(1, str.Length-1);
                    }).ToList());
                    exvGroupDic.Add(fKey, exvData);
                    var exlData = await ParseLocationDataArray(fValue.LocationEntities.Where(a=> a.ToString().StartsWith("-")).Select(a=>
                    {
                        var str = a.ToString();
                        return (object)str.Substring(1, str.Length-1);
                    }).ToList());
                    exlGroupDic.Add(fKey, exlData);
                    var exsData = await ParseTypeDataArray(fValue.ShipEntities.Where(a=> a.ToString().StartsWith("-")).Select(a=>
                    {
                        var str = a.ToString();
                        return (object)str.Substring(1, str.Length-1);
                    }).ToList());
                    exsGroupDic.Add(fKey, exsData);
                }
                ParsedAttackersLists.Add(key, aGroupDic);
                ParsedExcludeAttackersLists.Add(key, exaGroupDic);
                ParsedVictimsLists.Add(key, vGroupDic);
                ParsedExcludeVictimsLists.Add(key, exvGroupDic);
                ParsedLocationLists.Add(key, lGroupDic);
                ParsedExcludeLocationLists.Add(key, exlGroupDic);
                ParsedShipsLists.Add(key, sGroupDic);
                ParsedExcludeShipsLists.Add(key, exsGroupDic);
            }

        }

        private async Task ProcessKill(JsonZKill.Killmail kill)
        {
            try
            {
              // kill = JsonConvert.DeserializeObject<JsonZKill.Killmail>(File.ReadAllText("testkm.txt"));

                var hasBeenPosted = false;
                foreach (var (groupName, group) in Settings.LiveKillFeedModule.Groups)
                {
                    if (Settings.ZKBSettingsModule.AvoidDupesAcrossAllFeeds && ZKillLiveFeedModule.IsInSharedPool(kill.killmail_id))
                        return;

                    if (hasBeenPosted && Settings.LiveKillFeedModule.StopOnFirstGroupMatch) break;

                    if(UpdateLastPosted(groupName, kill.killmail_id)) continue;

                    var isPveKill = kill.zkb.npc;
                    var isPvpKill = !kill.zkb.npc;

                    if (!@group.FeedPveKills && isPveKill || !@group.FeedPvpKills && isPvpKill) continue;
                    if(!group.FeedAwoxKills && kill.zkb.awox) continue;
                    if(!group.FeedNotAwoxKills && !kill.zkb.awox) continue;
                    if(!group.FeedSoloKills && kill.zkb.solo) continue;
                    if(!group.FeedGroupKills && !kill.zkb.solo) continue;

                    foreach (var (filterName, filter) in group.Filters)
                    {
                        var isInclusive = filter.Inclusive;
                        var isLoss = false;
                        var isPassed = false;
                        var isFirstMatchOnly = !filter.AllMustMatch;
                        var isCertifiedToFeed = false;

                        #region Person checks
                        //exclusions
                        var exList = GetTier2CharacterIds(ParsedExcludeAttackersLists, groupName, filterName);
                        if(exList.ContainsAnyFromList(kill.attackers.Select(a=> a.character_id).Distinct())) continue;
                        exList = GetTier2CorporationIds(ParsedExcludeAttackersLists, groupName, filterName);
                        if(exList.ContainsAnyFromList(kill.attackers.Select(a=> a.corporation_id).Distinct())) continue;
                        exList = GetTier2AllianceIds(ParsedExcludeAttackersLists, groupName, filterName);
                        if(exList.ContainsAnyFromList(kill.attackers.Where(a=> a.alliance_id > 0).Select(a=> a.alliance_id).Distinct())) continue;

                        exList = GetTier2CharacterIds(ParsedExcludeVictimsLists, groupName, filterName);
                        if(exList.Contains(kill.victim.character_id)) continue;
                        exList = GetTier2CorporationIds(ParsedExcludeVictimsLists, groupName, filterName);
                        if(exList.Contains(kill.victim.corporation_id)) continue;
                        if (kill.victim.alliance_id > 0)
                        {
                            exList = GetTier2AllianceIds(ParsedExcludeVictimsLists, groupName, filterName);
                            if (exList.Contains(kill.victim.alliance_id)) continue;
                        }

                        exList = GetTier2SystemIds(ParsedExcludeLocationLists, groupName, filterName);
                        if(exList.Contains(kill.solar_system_id)) continue;

                        var rSystem = await APIHelper.ESIAPI.GetSystemData(Reason, kill.solar_system_id);

                        exList = GetTier2ConstellationIds(ParsedExcludeLocationLists, groupName, filterName);
                        if(rSystem != null && exList.Contains(rSystem.constellation_id)) continue;
                        exList = GetTier2RegionIds(ParsedExcludeLocationLists, groupName, filterName);
                        if(rSystem?.DB_RegionId != null && exList.Contains(rSystem.DB_RegionId.Value)) continue;

                        exList = GetTier2TypeIds(ParsedExcludeShipsLists, groupName, filterName);
                        if(exList.Contains(kill.victim.ship_type_id)) continue;

                        //character check
                        var fChars = GetTier2CharacterIds(ParsedAttackersLists, groupName, filterName);
                        if (fChars.Any())
                        {
                            var attackers = kill.attackers.Select(a => a.character_id);
                            if (isInclusive)
                            {
                                if (!fChars.ContainsAnyFromList(attackers))
                                {
                                    if(!isFirstMatchOnly)
                                        continue;
                                }
                                else
                                {
                                    isPassed = true;
                                    isCertifiedToFeed = isFirstMatchOnly;
                                }
                            }
                            if(!isInclusive && fChars.ContainsAnyFromList(attackers))
                                continue;
                        }

                        if (!isCertifiedToFeed)
                        {
                            fChars = GetTier2CharacterIds(ParsedVictimsLists, groupName, filterName);
                            if (fChars.Any())
                            {
                                if (isInclusive)
                                {
                                    if (!fChars.Contains(kill.victim.character_id))
                                    {
                                        if (!isFirstMatchOnly)
                                            continue;
                                    }
                                    else
                                    {
                                        isLoss = true;
                                        isPassed = true;
                                        isCertifiedToFeed = isFirstMatchOnly;
                                    }

                                }
                                if (!isInclusive && fChars.Contains(kill.victim.character_id))
                                    continue;
                            }
                        }


                        //corp check
                        if (!isPassed && !isCertifiedToFeed)
                        {
                            fChars = GetTier2CorporationIds(ParsedAttackersLists, groupName, filterName);
                            if (fChars.Any())
                            {
                                var attackers = kill.attackers.Select(a => a.corporation_id);
                                if (isInclusive)
                                {
                                    if (!fChars.ContainsAnyFromList(attackers))
                                    {
                                        if(!isFirstMatchOnly)
                                            continue;
                                    }
                                    else
                                    {
                                        isPassed = true;
                                        isCertifiedToFeed = isFirstMatchOnly;
                                    }
                                }
                                if (!isInclusive && fChars.ContainsAnyFromList(attackers))
                                    continue;
                            }

                            if (!isCertifiedToFeed)
                            {
                                fChars = GetTier2CorporationIds(ParsedVictimsLists, groupName, filterName);
                                if (fChars.Any())
                                {
                                    if (isInclusive)
                                    {
                                        if (!fChars.Contains(kill.victim.corporation_id))
                                        {
                                            if (!isFirstMatchOnly)
                                                continue;
                                        }
                                        else
                                        {
                                            isLoss = true;
                                            isPassed = true;
                                            isCertifiedToFeed = isFirstMatchOnly;
                                        }

                                    }
                                    if (!isInclusive && fChars.Contains(kill.victim.corporation_id))
                                        continue;
                                }
                            }
                        }

                        //alliance check
                        if (!isPassed && !isCertifiedToFeed)
                        {
                            fChars = GetTier2AllianceIds(ParsedAttackersLists, groupName, filterName);
                            if (fChars.Any())
                            {
                                var attackers = kill.attackers.Where(a=> a.alliance_id > 0).Select(a => a.alliance_id);
                                if (isInclusive)
                                {
                                    if (!fChars.ContainsAnyFromList(attackers))
                                    {
                                        if(!isFirstMatchOnly)
                                            continue;
                                    }
                                    else
                                    {
                                        isPassed = true;
                                        isCertifiedToFeed = isFirstMatchOnly;
                                    }
                                }

                                if (!isInclusive && fChars.ContainsAnyFromList(attackers))
                                    continue;
                            }

                            if (!isCertifiedToFeed)
                            {
                                fChars = GetTier2AllianceIds(ParsedVictimsLists, groupName, filterName);
                                if (fChars.Any())
                                {
                                    if (isInclusive)
                                    {
                                        if (!isFirstMatchOnly && kill.victim.alliance_id == 0) continue;
                                        if (kill.victim.alliance_id > 0)
                                        {
                                            if (!fChars.Contains(kill.victim.alliance_id))
                                            {
                                                if (!isFirstMatchOnly)
                                                    continue;
                                            }
                                            else
                                            {
                                                isLoss = true;
                                                isPassed = true;
                                                isCertifiedToFeed = isFirstMatchOnly;
                                            }
                                        }
                                    }

                                    if (!isInclusive && fChars.Contains(kill.victim.alliance_id))
                                        continue;
                                }
                            }
                        }

                        isPassed = false;
                        #endregion

                        //value checks
                        if (!isCertifiedToFeed)
                        {
                            if (isLoss && filter.MinimumLossValue >= kill.zkb.totalValue) continue;
                            if (filter.MaximumLossValue > 0 && isLoss && filter.MaximumLossValue <= kill.zkb.totalValue) continue;
                            if (!isLoss && filter.MinimumKillValue >= kill.zkb.totalValue) continue;
                            if (filter.MaximumKillValue > 0 && !isLoss && filter.MaximumKillValue <= kill.zkb.totalValue) continue;

                            if (isFirstMatchOnly && (filter.MinimumKillValue > 0 || filter.MinimumLossValue > 0 || filter.MaximumLossValue > 0 || filter.MaximumKillValue > 0))
                                isCertifiedToFeed = true;
                        }

                        #region Location checks (except system radius)

                        if (!isCertifiedToFeed)
                        {
                            var check = CheckLocation(rSystem, kill, isInclusive, groupName, filterName);
                            //if have some location criteria
                            if (check != null)
                            {
                                if (check == false)
                                {
                                    if (filter.Radius == 0 && !isFirstMatchOnly) continue;
                                }
                                else if (isInclusive && isFirstMatchOnly)
                                    isCertifiedToFeed = true;
                            }
                        }

                        #endregion

                        #region Type checks

                        var types = GetTier2TypeIds(ParsedShipsLists, groupName, filterName);
                        if (types.Any() && !isCertifiedToFeed)
                        {
                            if (isInclusive && !types.Contains(kill.victim.ship_type_id))
                            {
                                if(!isFirstMatchOnly) continue;
                            }
                            if(!isInclusive && types.Contains(kill.victim.ship_type_id)) continue;
                            if (isInclusive && isFirstMatchOnly)
                                isCertifiedToFeed = true;
                        }
                        #endregion

                        //haven't hit any criteria for 1-hit mode
                        if(!isCertifiedToFeed && isFirstMatchOnly) continue;

                        var discordChannels = filter.DiscordChannels.Any() ? filter.DiscordChannels : group.DiscordChannels;

                        if (filter.Radius > 0)
                        {
                            #region Process system radius check

                            //var msgType = MessageTemplateType.KillMailRadius;
                            var isDone = false;
                            foreach (var radiusSystemId in GetTier2SystemIds(ParsedLocationLists, groupName, filterName))
                            {
                                if (await ProcessLocation(radiusSystemId, kill, group, filter, groupName))
                                {
                                    isDone = true;
                                    hasBeenPosted = true;
                                    if (Settings.ZKBSettingsModule.AvoidDupesAcrossAllFeeds)
                                        ZKillLiveFeedModule.UpdateSharedIdPool(kill.killmail_id);
                                    await LogHelper.LogInfo($"Posting     {(isLoss ? "RLoss" : "RKill")}: {kill.killmail_id}  Value: {kill.zkb.totalValue:n0} ISK", Category);

                                    break;
                                }
                            }

                            if( isDone && group.StopOnFirstFilterMatch) break; //goto next group

                            #endregion
                        }
                        else
                        {
                            if (group.FeedUrlsOnly)
                            {
                                foreach (var channel in discordChannels)
                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, kill.zkb.url);
                                await LogHelper.LogInfo($"U.Posted     {(isLoss ? "Loss" : "Kill")}: {kill.killmail_id}  Value: {kill.zkb.totalValue:n0} ISK", Category);
                            }
                            else
                            {
                                var hasTemplate = !string.IsNullOrWhiteSpace(group.MessageTemplateFileName);
                                var msgColor = isLoss ? new Color(0xD00000) : new Color(0x00FF00);
                               // var msgType = !hasTemplate ? MessageTemplateType.KillMailGeneral : MessageTemplateType.Custom;
                                var km = new KillDataEntry();

                                if (await km.Refresh(Reason, kill))
                                {
                                    km.dic["{isLoss}"] = isLoss ? "true" : "false";
                                    if (hasTemplate)
                                    {
                                        hasBeenPosted = await TemplateHelper.PostTemplatedMessage(group.MessageTemplateFileName, km.dic, discordChannels,
                                            group.ShowGroupName ? groupName : " ");
                                        if(hasBeenPosted)
                                            await LogHelper.LogInfo($"T.Posted     {(isLoss ? "Loss" : "Kill")}: {kill.killmail_id}  Value: {kill.zkb.totalValue:n0} ISK", Category);
                                    }
                                    else
                                    {
                                        await SendEmbedKillMessage(discordChannels, msgColor, km, group.ShowGroupName ? groupName : " ");
                                        hasBeenPosted = true;
                                        await LogHelper.LogInfo($"N.Posted     {(isLoss ? "Loss" : "Kill")}: {kill.killmail_id}  Value: {kill.zkb.totalValue:n0} ISK", Category);
                                    }
                                        
                                }
                            }

                            if (Settings.ZKBSettingsModule.AvoidDupesAcrossAllFeeds)
                                ZKillLiveFeedModule.UpdateSharedIdPool(kill.killmail_id);

                            if(group.StopOnFirstFilterMatch) break; //goto next group
                        }

                        continue; //goto next filter
                    }
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
                await LogHelper.LogWarning($"Error processing kill ID {kill?.killmail_id} ! Msg: {ex.Message}", Category);
            }
        }

        private async Task<bool> ProcessLocation(long radiusId, JsonZKill.Killmail kill, KillFeedGroup @group, KillMailFilter filter, string groupName)
        {
            var mode = RadiusMode.Range;
            var isUrlOnly = group.FeedUrlsOnly;
            var radius = filter.Radius;

            if (radiusId <= 0)
            {
                await LogHelper.LogError("Radius feed must have systemId!", Category);
                return false;
            }

            var km = new KillDataEntry();
            await km.Refresh(Reason, kill);

            var routeLength = 0;
            JsonClasses.ConstellationData rConst = null;
            JsonClasses.RegionData rRegion;
            var srcSystem = mode == RadiusMode.Range ? await APIHelper.ESIAPI.GetSystemData(Reason, radiusId) : null;

            if (radiusId == km.systemId)
            {
                //right there
                rConst = km.rSystem.constellation_id == 0 ? null : await APIHelper.ESIAPI.GetConstellationData(Reason, km.rSystem.constellation_id);
                rRegion = rConst?.region_id == null ||  rConst.region_id == 0 ? null : await APIHelper.ESIAPI.GetRegionData(Reason, rConst.region_id);
            }
            else
            {
                switch (mode)
                {
                    case RadiusMode.Range:

                        if (radius == 0 || km.isUnreachableSystem || (srcSystem?.IsUnreachable() ?? true)) //Thera WH Abyss
                            return false;

                        var route = await APIHelper.ESIAPI.GetRawRoute(Reason, radiusId, km.systemId);
                        if (string.IsNullOrEmpty(route)) return false;
                        JArray data;
                        try
                        {
                            data = JArray.Parse(route);
                        }
                        catch (Exception ex)
                        {
                            await LogHelper.LogEx("Route parse: " + ex.Message, ex, Category);
                            return false;
                        }

                        routeLength = data.Count - 1;
                        //not in range
                        if (routeLength > radius) return false;

                        var rSystemName = radiusId > 0 ? srcSystem?.name ?? LM.Get("Unknown") : LM.Get("Unknown");
                        km.dic.Add("{radiusSystem}", rSystemName);
                        km.dic.Add("{radiusJumps}", routeLength.ToString());

                        break;
                    case RadiusMode.Constellation:
                        if (km.rSystem.constellation_id != radiusId) return false;
                        break;
                    case RadiusMode.Region:
                        if (km.rSystem.DB_RegionId > 0)
                        {
                            if (km.rSystem.DB_RegionId != radiusId) return false;
                        }
                        else
                        {
                            rConst = await APIHelper.ESIAPI.GetConstellationData(Reason, km.rSystem.constellation_id);
                            if (rConst == null || rConst.region_id != radiusId) return false;
                        }

                        break;
                }
                rConst = rConst ?? await APIHelper.ESIAPI.GetConstellationData(Reason, km.rSystem.constellation_id);
                rRegion = await APIHelper.ESIAPI.GetRegionData(Reason, rConst.region_id);
            }

            //var rSystemName = rSystem?.name ?? LM.Get("Unknown");

            km.dic.Add("{isRangeMode}", (mode == RadiusMode.Range).ToString());
            km.dic.Add("{isConstMode}", (mode == RadiusMode.Constellation).ToString());
            km.dic.Add("{isRegionMode}", (mode == RadiusMode.Region).ToString());
            km.dic.AddOrUpdateEx("{constName}", rConst?.name);
            km.dic.AddOrUpdateEx("{regionName}", rRegion?.name);

            var channels = filter.DiscordChannels.Any() ? filter.DiscordChannels : group.DiscordChannels;

            if (!string.IsNullOrEmpty(group.MessageTemplateFileName))
                if (await TemplateHelper.PostTemplatedMessage(group.MessageTemplateFileName, km.dic, channels, group.ShowGroupName ? groupName : " "))
                    return true;
            foreach (var channel in channels)
            {
                if (isUrlOnly)
                    await APIHelper.DiscordAPI.SendMessageAsync(channel, kill.zkb.url);
                else
                {
                    var jumpsText = routeLength > 0 ? $"{routeLength} {LM.Get("From")} {srcSystem?.name}" : $"{LM.Get("InSmall")} {km.sysName} ({km.systemSecurityStatus})";
                    await SendEmbedKillMessage(new List<ulong> {channel}, new Color(0x989898), km, string.IsNullOrEmpty(jumpsText) ? "-" : jumpsText, group.ShowGroupName ? groupName : " ");
                }
            }

            return true;
        }

        private bool? CheckLocation(JsonClasses.SystemName rSystem, JsonZKill.Killmail kill, bool isInclusive, string groupName, string filterName)
        {
            var isPassed = false;
            if (rSystem == null)
            {
                LogHelper.LogError($"System not found: {kill.solar_system_id}!", Category).GetAwaiter().GetResult();
                return false;
            }

            var fCount = 0;
            //System
            var fLocs = GetTier2SystemIds(ParsedLocationLists, groupName, filterName);
            if (fLocs.Any())
            {
                if (isInclusive && !fLocs.Contains(kill.solar_system_id))
                    return false;
                if (!isInclusive && fLocs.Contains(kill.solar_system_id))
                    return false;
                if (isInclusive) isPassed = true;
            }
            else fCount++;

            //Constellation
            if (!isPassed)
            {
                fLocs = GetTier2ConstellationIds(ParsedLocationLists, groupName, filterName);
                if (fLocs.Any())
                {
                    if (isInclusive && !fLocs.Contains(rSystem.constellation_id))
                        return false;
                    if (!isInclusive && fLocs.Contains(rSystem.constellation_id))
                        return false;
                    if (isInclusive) isPassed = true;
                }
                else fCount++;
            }

            //Region
            if (!isPassed)
            {
                fLocs = GetTier2RegionIds(ParsedLocationLists, groupName, filterName);
                if (fLocs.Any() && rSystem.DB_RegionId.HasValue)
                {
                    if (isInclusive && !fLocs.Contains(rSystem.DB_RegionId.Value))
                        return false;
                    if (!isInclusive && fLocs.Contains(rSystem.DB_RegionId.Value))
                        return false;
                    if (isInclusive) isPassed = true;
                }
                else fCount++;
            }

            return fCount == 3 ? (bool?)null : true;
        }

        private bool UpdateLastPosted(string groupName, long id)
        {
            if (!LastPostedDictionary.ContainsKey(groupName))
                LastPostedDictionary.AddOrUpdateEx(groupName, 0);

            if (LastPostedDictionary[groupName] == id) return true;
            LastPostedDictionary[groupName] = id;
            return false;
        }

        #region Send message

        internal enum KillMailLinkTypes
        {
            character,
            corporation,
            alliance,
            ship,
            system
        }

        private static string GetKillMailLink(long id, KillMailLinkTypes killMailLinkTypes)
        {
            return $"https://zkillboard.com/{killMailLinkTypes}/{id}/";
        }

        private async Task SendEmbedKillMessage(List<ulong> channelIds, Color color, KillDataEntry km, string radiusMessage, string msg = "")
        {
            try
            {
                if (!channelIds.Any())
                {
                    await LogHelper.LogError($"No channels specified for KB feed! Check config.", Category);
                    return;
                }

                msg = msg ?? "";

                var victimName = $"{LM.Get("killFeedName", $"[{km.rVictimCharacter?.name}]({GetKillMailLink(km.victimCharacterID, KillMailLinkTypes.character)})")}";
                var victimCorp = $"{LM.Get("killFeedCorp", $"[{km.rVictimCorp?.name}]({GetKillMailLink(km.victimCorpID, KillMailLinkTypes.corporation)})")}";
                var victimAlliance = km.rVictimAlliance == null
                    ? ""
                    : $"{LM.Get("killFeedAlliance", $"[{km.rVictimAlliance?.name}]")}({GetKillMailLink(km.victimAllianceID, KillMailLinkTypes.alliance)})";
                var victimShip = $"{LM.Get("killFeedShip", $"[{km.rVictimShipType?.name}]({GetKillMailLink(km.victimShipID, KillMailLinkTypes.ship)})")}";


                string[] victimStringArray = new string[] {victimName, victimCorp, victimAlliance, victimShip};

                var attackerName = $"{LM.Get("killFeedName", $"[{km.rAttackerCharacter?.name}]({GetKillMailLink(km.finalBlowAttackerCharacterId, KillMailLinkTypes.character)})")}";
                var attackerCorp = $"{LM.Get("killFeedCorp", $"[{km.rAttackerCorp?.name}]({GetKillMailLink(km.finalBlowAttackerCorpId, KillMailLinkTypes.corporation)})")}";
                var attackerAlliance = km.rAttackerAlliance == null || km.finalBlowAttackerAllyId == 0
                    ? null
                    : $"{LM.Get("killFeedAlliance", $"[{km.rAttackerAlliance?.name}]({GetKillMailLink(km.finalBlowAttackerAllyId, KillMailLinkTypes.alliance)})")}";
                var attackerShip = $"{LM.Get("killFeedShip", $"[{km.rAttackerShipType?.name}]({GetKillMailLink(km.attackerShipID, KillMailLinkTypes.ship)})")}";

                string[] attackerStringArray = new string[] {attackerName, attackerCorp, attackerAlliance, attackerShip};


                var killFeedDetails = LM.Get("killFeedDetails", km.killTime, km.value.ToString("#,##0 ISk"));
                var killFeedDetailsSystem = LM.Get("killFeedDetailsSystem", $"[{km.sysName}]({GetKillMailLink(km.systemId, KillMailLinkTypes.system)})");

                string[] detailsStringArray = new string[] {killFeedDetails, killFeedDetailsSystem};


                var builder = new EmbedBuilder()
                    .WithColor(color)
                    .WithThumbnailUrl($"https://image.eveonline.com/Type/{km.victimShipID}_64.png")
                    .WithAuthor(author =>
                    {
                        author.WithName(LM.Get("killFeedHeader", km.rVictimShipType?.name, km.rSystem?.name))
                            .WithUrl($"https://zkillboard.com/kill/{km.killmailID}/");
                        if (km.isNPCKill) author.WithIconUrl("http://www.panthernet.org/uf/npc2.jpg");
                    })
                    .AddField(LM.Get("Victim"), string.Join("\n", victimStringArray.Where(c => !string.IsNullOrWhiteSpace(c))))
                    .AddField(LM.Get("Finalblow"), string.Join("\n", attackerStringArray.Where(c => !string.IsNullOrWhiteSpace(c))))
                    .AddField(LM.Get("Details"), string.Join("\n", detailsStringArray.Where(c => !string.IsNullOrWhiteSpace(c))));

                if (!string.IsNullOrEmpty(radiusMessage))
                    builder.AddField(LM.Get("radiusInfoHeader"), radiusMessage);

                var embed = builder.Build();
                foreach (var id in channelIds)
                {
                    var channel = APIHelper.DiscordAPI.GetChannel(id);
                    if (channel != null)
                    {
                        if(await APIHelper.DiscordAPI.SendMessageAsync(channel, msg, embed) == null)
                            await LogHelper.LogError($"Error sending KM to channel {id}!", Category);
                    }
                    else await LogHelper.LogWarning($"Channel {id} not found!", Category);
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(nameof(SendEmbedKillMessage), ex, Category);
            }
        }
        #endregion


        private enum RadiusMode
        {
            Range,
            Constellation,
            Region
        }
    }
}
