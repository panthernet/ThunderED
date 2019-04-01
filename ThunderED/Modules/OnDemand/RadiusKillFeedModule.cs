﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Newtonsoft.Json.Linq;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Json.ZKill;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules.OnDemand
{
    public class RadiusKillFeedModule: AppModuleBase
    {
        private class RasiusGroupParsed
        {
            public readonly List<long> SystemIds  = new List<long>();
            public readonly List<long> ConstellationIds  = new List<long>();
            public readonly List<long> RegionIds  = new List<long>();
        }

        private readonly Dictionary<string, long> _lastPosted = new Dictionary<string, long>();
        public sealed override LogCat Category => LogCat.RadiusKill;

        public RadiusKillFeedModule()
        {
            LogHelper.LogModule("Inititalizing RadiusKillFeed module...", Category).GetAwaiter().GetResult();
            ZKillLiveFeedModule.Queryables.Add(ProcessKill);            
        }

        private readonly Dictionary<string, RasiusGroupParsed> _groups = new Dictionary<string, RasiusGroupParsed>();

        public override async Task Initialize()
        {
            _groups.Clear();
            foreach (var groupPair in Settings.RadiusKillFeedModule.GroupsConfig)
            {
                try
                {
                    if (!groupPair.Value.RadiusEntities.Any()) continue;
                    var group = new RasiusGroupParsed();
                    foreach (var entity in groupPair.Value.RadiusEntities)
                    {
                        if(entity == null) continue;
                        if (long.TryParse(entity.ToString(), out var longNumber))
                        {
                            //got ID
                            var rSystem = await APIHelper.ESIAPI.GetSystemData(Reason, entity);
                            if(rSystem != null)
                                group.SystemIds.Add(longNumber);
                            else
                            {
                                var rConst = await APIHelper.ESIAPI.GetConstellationData(Reason, entity);
                                if(rConst != null)
                                    group.ConstellationIds.Add(longNumber);
                                else
                                {
                                    var rRegion = await APIHelper.ESIAPI.GetRegionData(Reason, entity);
                                    if(rRegion != null)
                                        group.RegionIds.Add(longNumber);
                                }
                            }
                        }
                        else
                        {
                            var result = await APIHelper.ESIAPI.SearchLocationEntity(Reason, entity.ToString());
                            if (result != null)
                            {
                                if (result.solar_system.Any())
                                    group.SystemIds.Add(result.solar_system.First());
                                if (result.region.Any())
                                    group.RegionIds.Add(result.region.First());
                                if (result.constellation.Any())
                                    group.ConstellationIds.Add(result.constellation.First());
                            }
                        }
                    }

                    _groups.Add(groupPair.Key, group);
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(Initialize), ex, Category);
                }
            }
        }

        private enum RadiusMode
        {
            Range,
            Constellation,
            Region
        }

        private async Task ProcessKill(JsonZKill.Killmail kill)
        {
            try
            {
                foreach (var groupPair in Settings.RadiusKillFeedModule.GroupsConfig)
                {
                    var groupName = groupPair.Key;
                    var group = groupPair.Value;
                    if(!_lastPosted.ContainsKey(groupName))
                        _lastPosted.Add(groupName, 0);

                    if (_lastPosted[groupName] == kill.killmail_id) continue;
                    _lastPosted[groupName] = kill.killmail_id;

                    var isNPCKill = kill.zkb.npc;
                    if((!group.FeedPveKills && isNPCKill) || (!group.FeedPvpKills && !isNPCKill)) continue;
                    var groupData = _groups.ContainsKey(groupName) ? _groups[groupName] : null;
                    if(groupData == null) continue;

                    if (!group.RadiusChannels.Any())
                    {
                        await LogHelper.LogWarning($"Group {groupName} has no 'radiusChannel' specified! Kills will be skipped.", Category);
                        continue;
                    }

                    foreach (var radiusSystemId in groupData.SystemIds)
                    {
                        await ProcessLocation(radiusSystemId, RadiusMode.Range, kill, group, groupName);
                    }
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
                await LogHelper.LogWarning($"Error processing kill ID {kill?.killmail_id} ! Msg: {ex.Message}", Category);
            }
        }

        private async Task ProcessLocation(long radiusId, RadiusMode mode, JsonZKill.Killmail kill, RadiusGroup @group, string groupName)
        {            
            var isUrlOnly = group.FeedUrlsOnly;
            var radius = group.Radius;
            var minimumValue = group.MinimumValue;

            if (radiusId <= 0)
            {
                await LogHelper.LogError("Radius feed must have systemId, constId or regionId defined!", Category);
                return;
            }

            var km = new KillDataEntry();
            await km.Refresh(Reason, kill);

            //validity check
            if (minimumValue > 0 && km.value < minimumValue) return;

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
                if (radius == 0 || km.isUnreachableSystem || (srcSystem?.IsUnreachable() ?? false)) //Thera WH Abyss
                    return;

                switch (mode)
                {
                    case RadiusMode.Range:

                        var route = await APIHelper.ESIAPI.GetRawRoute(Reason, radiusId, km.systemId);
                        if (string.IsNullOrEmpty(route)) return;
                        JArray data;
                        try
                        {
                            data = JArray.Parse(route);
                        }
                        catch (Exception ex)
                        {
                            await LogHelper.LogEx("Route parse: " + ex.Message, ex, Category);
                            return;
                        }

                        routeLength = data.Count - 1;
                        //not in range
                        if (routeLength > radius) return;

                        var rSystemName = radiusId > 0 ? srcSystem?.name ?? LM.Get("Unknown") : LM.Get("Unknown");
                        km.dic.Add("{radiusSystem}", rSystemName);
                        km.dic.Add("{radiusJumps}", routeLength.ToString());

                        break;
                    case RadiusMode.Constellation:
                        if (km.rSystem.constellation_id != radiusId) return;
                        break;
                    case RadiusMode.Region:
                        rConst = await APIHelper.ESIAPI.GetConstellationData(Reason, km.rSystem.constellation_id);
                        if (rConst == null || rConst.region_id != radiusId) return;
                        break;
                }
                rConst = rConst ?? await APIHelper.ESIAPI.GetConstellationData(Reason, km.rSystem.constellation_id);
                rRegion = await APIHelper.ESIAPI.GetRegionData(Reason, rConst.region_id);

            }

            //var rSystemName = rSystem?.name ?? LM.Get("Unknown");

            km.dic.Add("{isRangeMode}", (mode == RadiusMode.Range).ToString());
            km.dic.Add("{isConstMode}", (mode == RadiusMode.Constellation).ToString());
            km.dic.Add("{isRegionMode}", (mode == RadiusMode.Region).ToString());
            km.dic.Add("{constName}", rConst?.name);
            km.dic.Add("{regionName}", rRegion?.name);

            var template = isUrlOnly ? null : await TemplateHelper.GetTemplatedMessage(MessageTemplateType.KillMailRadius, km.dic);
            foreach (var channel in group.RadiusChannels)
            {
                if (isUrlOnly)
                    await APIHelper.DiscordAPI.SendMessageAsync(channel, kill.zkb.url);
                else
                {
                    if (template != null)
                        await APIHelper.DiscordAPI.SendMessageAsync(channel, group.ShowGroupName ? groupName : " ", template).ConfigureAwait(false);
                    else
                    {
                        var jumpsText = routeLength > 0 ? $"{routeLength} {LM.Get("From")} {srcSystem?.name}" : $"{LM.Get("InSmall")} {km.sysName} ({km.systemSecurityStatus})";
                        await APIHelper.DiscordAPI.SendEmbedKillMessage(channel, new Color(0x989898), km, jumpsText, group.ShowGroupName ? groupName : " ");
                    }
                }
            }

            await LogHelper.LogInfo($"Posting  Radius Kill: {kill.killmail_id}  Value: {km.value:n0} ISK", Category);
        }
    }
}
