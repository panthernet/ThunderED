using System;
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
        private readonly Dictionary<string, long> _lastPosted = new Dictionary<string, long>();
        public override LogCat Category => LogCat.RadiusKill;

        public RadiusKillFeedModule()
        {
            LogHelper.LogModule("Inititalizing RadiusKillFeed module...", Category).GetAwaiter().GetResult();
            ZKillLiveFeedModule.Queryables.Add(ProcessKill);
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
                    var group = groupPair.Value;
                    if(!_lastPosted.ContainsKey(groupPair.Key))
                        _lastPosted.Add(groupPair.Key, 0);

                    if (_lastPosted[groupPair.Key] == kill.killmail_id) continue;
                    _lastPosted[groupPair.Key] = kill.killmail_id;

                    var isNPCKill = kill.zkb.npc;
                    if((!group.FeedPveKills && isNPCKill) || (!group.FeedPvpKills && !isNPCKill)) continue;

                    var isUrlOnly = group.FeedUrlsOnly;

                    var radius = group.Radius;
                    var radiusSystemId = group.RadiusSystemId;
                    var radiusConstId = group.RadiusConstellationId;
                    var radiusRegionId = group.RadiusRegionId;
                    var radiusChannelId = group.RadiusChannel;
                    var radiusValue = group.MinimumValue;
                    if (radiusSystemId == 0 && radiusConstId == 0 && radiusRegionId == 0)
                    {
                        await LogHelper.LogError("Radius feed must have systemId, constId or regionId defined!", Category);
                        continue;
                    }
                    if (radiusChannelId == 0)
                    {
                        await LogHelper.LogWarning($"Group {groupPair.Key} has no 'radiusChannel' specified! Kills will be skipped.", Category);
                        continue;
                    }
                    var mode = radiusSystemId != 0 ? RadiusMode.Range : (radiusConstId != 0 ? RadiusMode.Constellation : RadiusMode.Region);


                    var km = new KillDataEntry();
                    await km.Refresh(Reason, kill);

                    //validity check
                    if (radiusChannelId <= 0 || (km.sysName[0] == 'J' && int.TryParse(km.sysName.Substring(1), out int _) && radiusSystemId != km.systemId) || (radiusValue > 0 && km.value < radiusValue)) continue;

                    var routeLength = 0;
                    JsonClasses.ConstellationData rConst = null;
                    JsonClasses.RegionData rRegion= null;
                    if (radiusSystemId == km.systemId)
                    {
                        //right there
                        rConst = km.rSystem.constellation_id == 0 ? null : await APIHelper.ESIAPI.GetConstellationData(Reason, km.rSystem.constellation_id);
                        rRegion =  rConst?.region_id == null ||  rConst.region_id == 0 ? null : await APIHelper.ESIAPI.GetRegionData(Reason, rConst.region_id);
                    }
                    else
                    {
                        if (km.isUnreachableSystem) //Thera WH Abyss
                            continue;

                        switch (mode)
                        {
                            case RadiusMode.Range:
                                var route = await APIHelper.ESIAPI.GetRawRoute(Reason, radiusSystemId, km.systemId);
                                if(string.IsNullOrEmpty(route)) continue;
                                JArray data;
                                try
                                {
                                    data = JArray.Parse(route);
                                }
                                catch (Exception ex)
                                {
                                    await LogHelper.LogEx("Route parse: " + ex.Message, ex, Category);
                                    continue;
                                }

                                routeLength = data.Count - 1;
                                //not in range
                                if (routeLength > radius) continue;
                                rConst = await APIHelper.ESIAPI.GetConstellationData(Reason, km.rSystem.constellation_id);
                                rRegion = await APIHelper.ESIAPI.GetRegionData(Reason, rConst.region_id);
                                break;
                            case RadiusMode.Constellation:
                                if(km.rSystem.constellation_id != radiusConstId) continue;
                                rConst = await APIHelper.ESIAPI.GetConstellationData(Reason, km.rSystem.constellation_id);
                                rRegion = await APIHelper.ESIAPI.GetRegionData(Reason, rConst.region_id);
                                break;
                            case RadiusMode.Region:
                                rConst = await APIHelper.ESIAPI.GetConstellationData(Reason, km.rSystem.constellation_id);
                                if(rConst == null || rConst.region_id != radiusRegionId) continue;
                                rRegion = await APIHelper.ESIAPI.GetRegionData(Reason, rConst.region_id);
                                break;
                        }
                    }

                    //var rSystemName = rSystem?.name ?? LM.Get("Unknown");
                    var rSystemName = radiusSystemId > 0 ? (await APIHelper.ESIAPI.GetSystemData(Reason, radiusSystemId))?.name ?? LM.Get("Unknown") : LM.Get("Unknown");

                    km.dic.Add("{radiusSystem}", rSystemName);
                    km.dic.Add("{radiusJumps}", routeLength.ToString());
                    km.dic.Add("{isRangeMode}", (mode == RadiusMode.Range).ToString());
                    km.dic.Add("{isConstMode}", (mode == RadiusMode.Constellation).ToString());
                    km.dic.Add("{isRegionMode}", (mode == RadiusMode.Region).ToString());
                    km.dic.Add("{constName}", rConst?.name);
                    km.dic.Add("{regionName}", rRegion?.name);


                    if (isUrlOnly)
                    {
                        await APIHelper.DiscordAPI.SendMessageAsync(radiusChannelId, kill.zkb.url);
                    } else if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailRadius, km.dic, radiusChannelId, groupPair.Key))
                    {
                        var jumpsText = routeLength > 0 ? $"{routeLength} {LM.Get("From")} {rSystemName}" : $"{LM.Get("InSmall")} {km.sysName} ({km.systemSecurityStatus})";
                        await APIHelper.DiscordAPI.SendEmbedKillMessage(radiusChannelId, new Color(0x989898), km, jumpsText,
                            groupPair.Value.ShowGroupName ? groupPair.Key : " ");
                    }

                    await LogHelper.LogInfo($"Posting  Radius Kill: {kill.killmail_id}  Value: {km.value:n0} ISK", Category);

                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
                await LogHelper.LogWarning($"Error processing kill ID {kill?.killmail_id} ! Msg: {ex.Message}", Category);
            }
        }
    }
}
