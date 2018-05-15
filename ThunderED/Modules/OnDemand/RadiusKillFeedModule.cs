using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Newtonsoft.Json.Linq;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Json.ZKill;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules.OnDemand
{
    public class RadiusKillFeedModule: AppModuleBase
    {
        private readonly Dictionary<string, int> _lastPosted = new Dictionary<string, int>();
        public override LogCat Category => LogCat.RadiusKill;
        private readonly bool _enableCache;

        public RadiusKillFeedModule()
        {
            ZKillLiveFeedModule.Queryables.Add(ProcessKill);
            _enableCache = SettingsManager.GetBool("radiusKillFeedModule", "enableCache");
        }

        private enum RadiusMode
        {
            Range,
            Constellation,
            Region
        }

        private async Task ProcessKill(JsonZKill.ZKillboard kill)
        {
            try
            {
                foreach (var group in SettingsManager.GetSubList("radiusKillFeedModule", "groupsConfig"))
                {
                    if(!_lastPosted.ContainsKey(group.Key))
                        _lastPosted.Add(group.Key, 0);

                    if (_lastPosted[group.Key] == kill.package.killID) continue;
                    _lastPosted[group.Key] = kill.package.killID;

                    var killmailID = kill.package.killmail.killmail_id;
                    var value = kill.package.zkb.totalValue;
                    var systemId = kill.package.killmail.solar_system_id;
                    var radius = Convert.ToInt16(group["radius"]);
                    var radiusSystemId = Convert.ToInt32(group["radiusSystemId"]);
                    var radiusConstId = Convert.ToInt32(group["radiusConstellationId"]);
                    var radiusRegionId = Convert.ToInt32(group["radiusRegionId"]);
                    var radiusChannelId = Convert.ToUInt64(group["radiusChannel"]);
                    int radiusValue = Convert.ToInt32(group["minimumValue"]);
                    var rSystem = await APIHelper.ESIAPI.GetSystemData(Reason, systemId, false, !_enableCache);
                    var sysName = rSystem?.name ?? "J";
                    if (radiusSystemId == 0 && radiusConstId == 0 && radiusRegionId == 0)
                    {
                        await LogHelper.LogError("Radius feed must have systemId, constId or regionId defined!", Category);
                        continue;
                    }

                    if (radiusChannelId == 0)
                    {
                        await LogHelper.LogWarning($"Group {group.Key} has no 'radiusChannel' specified! Kills will be skipped.", Category);
                        continue;
                    }

                    var mode = radiusSystemId != 0 ? RadiusMode.Range : (radiusConstId != 0 ? RadiusMode.Constellation : RadiusMode.Region);

                    //validity check
                    if (radiusChannelId <= 0 || (sysName[0] == 'J' && int.TryParse(sysName.Substring(1), out int _) && radiusSystemId != systemId) || (radiusValue > 0 && value < radiusValue)) continue;

                    var routeLength = 0;
                    JsonClasses.ConstellationData rConst = null;
                    JsonClasses.RegionData rRegion= null;
                    if (radiusSystemId == systemId)
                    {
                        //right there
                        rConst = rSystem.constellation_id == 0 ? null : await APIHelper.ESIAPI.GetConstellationData(Reason, rSystem.constellation_id);
                        rRegion =  rConst?.region_id == null ||  rConst.region_id == 0 ? null : await APIHelper.ESIAPI.GetRegionData(Reason, rConst.region_id);
                    }
                    else
                    {
                        switch (mode)
                        {
                            case RadiusMode.Range:
                                var route = await APIHelper.ESIAPI.GetRawRoute(Reason, radiusSystemId, systemId);
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
                                rConst = await APIHelper.ESIAPI.GetConstellationData(Reason, rSystem.constellation_id);
                                rRegion = await APIHelper.ESIAPI.GetRegionData(Reason, rConst.region_id);
                                break;
                            case RadiusMode.Constellation:
                                if(rSystem.constellation_id != radiusConstId) continue;
                                rConst = await APIHelper.ESIAPI.GetConstellationData(Reason, rSystem.constellation_id);
                                rRegion = await APIHelper.ESIAPI.GetRegionData(Reason, rConst.region_id);
                                break;
                            case RadiusMode.Region:
                                rConst = await APIHelper.ESIAPI.GetConstellationData(Reason, rSystem.constellation_id);
                                if(rConst == null || rConst.region_id != radiusRegionId) continue;
                                rRegion = await APIHelper.ESIAPI.GetRegionData(Reason, rConst.region_id);
                                break;
                        }
                    }

                    var rSystemName = rSystem?.name ?? LM.Get("Unknown");

                    var victimCharacterID = kill.package.killmail.victim.character_id;
                    var victimCorpID = kill.package.killmail.victim.corporation_id;
                    var victimAllianceID = kill.package.killmail.victim.alliance_id;
                    var attackers = kill.package.killmail.attackers;
                    var finalBlowAttacker = attackers.FirstOrDefault(a => a.final_blow);
                    var finalBlowAttackerCorpId = finalBlowAttacker?.corporation_id;
                    var finalBlowAttackerAllyId = finalBlowAttacker?.alliance_id;
                    var shipID = kill.package.killmail.victim.ship_type_id;
                    var isNPCKill = kill.package.zkb.npc;
                    var killTime = kill.package.killmail.killmail_time.ToString("dd.MM.yyyy hh:mm");

                    var rVictimCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, victimCorpID, false, !_enableCache);
                    var rAttackerCorp = finalBlowAttackerCorpId.HasValue && finalBlowAttackerCorpId.Value > 0
                        ? await APIHelper.ESIAPI.GetCorporationData(Reason, finalBlowAttackerCorpId)
                        : null;
                    var rVictimAlliance = victimAllianceID != 0 ? await APIHelper.ESIAPI.GetAllianceData(Reason, victimAllianceID, false, !_enableCache) : null;
                    var rAttackerAlliance = finalBlowAttackerAllyId.HasValue && finalBlowAttackerAllyId.Value > 0
                        ? await APIHelper.ESIAPI.GetAllianceData(Reason, finalBlowAttackerAllyId)
                        : null;
                    var rShipType = await APIHelper.ESIAPI.GetTypeId(Reason, shipID);
                    var rVictimCharacter = await APIHelper.ESIAPI.GetCharacterData(Reason, victimCharacterID, false, !_enableCache);
                    var rAttackerCharacter = await APIHelper.ESIAPI.GetCharacterData(Reason, finalBlowAttacker?.character_id, false, !_enableCache);
                    var systemSecurityStatus = Math.Round(rSystem.security_status, 1).ToString("0.0");

                    var dic = new Dictionary<string, string>
                    {
                        {"{shipID}", shipID.ToString()},
                        {"{shipType}", rShipType?.name},
                        {"{iskValue}", value.ToString("n0")},
                        {"{systemName}", sysName},
                        {"{systemSec}", systemSecurityStatus},
                        {"{victimName}", rVictimCharacter?.name},
                        {"{victimCorpName}", rVictimCorp?.name},
                        {"{victimCorpTicker}", rVictimCorp?.ticker},
                        {"{victimAllyName}", rVictimAlliance?.name},
                        {"{victimAllyTicker}", rVictimAlliance == null ? null : $"<{rVictimAlliance.ticker}>"},
                        {"{attackerName}", rAttackerCharacter?.name},
                        {"{attackerCorpName}", rAttackerCorp?.name},
                        {"{attackerCorpTicker}", rAttackerCorp?.ticker},
                        {"{attackerAllyTicker}", rAttackerAlliance == null ? null : $"<{rAttackerAlliance.ticker}>"},
                        {"{attackerAllyName}", rAttackerAlliance?.name},
                        {"{attackersCount}", attackers?.Length.ToString()},
                        {"{kmId}", killmailID.ToString()},
                        {"{isNpcKill}", isNPCKill.ToString()},
                        {"{timestamp}", killTime},
                        {"{radiusSystem}", rSystemName.ToString()},
                        {"{radiusJumps}", routeLength.ToString()},
                        {"{isRangeMode}", (mode == RadiusMode.Range).ToString()},
                        {"{isConstMode}", (mode == RadiusMode.Constellation).ToString()},
                        {"{isRegionMode}", (mode == RadiusMode.Region).ToString()},
                        {"{constName}", rConst?.name},
                        {"{regionName}", rRegion?.name},

                    };

                    if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailRadius, dic, radiusChannelId, group.Key))
                    {
                        var jumpsText = routeLength > 0 ? $"{routeLength} {LM.Get("From")} {rSystemName}" : $"{LM.Get("InSmall")} {sysName} ({systemSecurityStatus})";
                        await APIHelper.DiscordAPI.SendEmbedKillMessage(radiusChannelId, new Color(0x989898), shipID, killmailID, rShipType.name, (long) value,
                            sysName,
                            systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name, rVictimCorp.name,
                            rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
                            rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length, jumpsText);
                    }

                    await LogHelper.LogInfo($"Posting  Radius Kill: {kill.package.killID}  Value: {value:n0} ISK", Category);

                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
        }
    }
}
