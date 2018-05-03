using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Newtonsoft.Json.Linq;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json.ZKill;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules.OnDemand
{
    public class RadiusKillFeedModule: AppModuleBase
    {
        private readonly Dictionary<string, int> _lastPosted = new Dictionary<string, int>();
        public override LogCat Category => LogCat.RadiusKill;

        public RadiusKillFeedModule()
        {
            ZKillLiveFeedModule.Queryables.Add(ProcessKill);
        }

        private async Task ProcessKill(JsonZKill.ZKillboard kill)
        {
            try
            {
                foreach (var group in SettingsManager.GetSubList("radiusKillFeedModule", "groupsConfig"))
                {
                    if(!_lastPosted.ContainsKey(group.Key))
                        _lastPosted.Add(group.Key, 0);

                    if (_lastPosted[group.Key] == kill.package.killID) return;
                    _lastPosted[group.Key] = kill.package.killID;

                    var killmailID = kill.package.killmail.killmail_id;
                    var value = kill.package.zkb.totalValue;
                    var systemId = kill.package.killmail.solar_system_id;
                    var radius = Convert.ToInt16(group["radius"]);
                    var radiusSystemId = Convert.ToUInt64(group["radiusSystemId"]);
                    var radiusChannelId = Convert.ToUInt64(group["radiusChannel"]);
                    int radiusValue = Convert.ToInt32(group["minimumValue"]);
                    var rSystem = await APIHelper.ESIAPI.GetSystemData(Reason, systemId);
                    var sysName = rSystem?.name ?? "J";

                    //validity check
                    if (radiusChannelId <= 0 ||
                        (sysName[0] == 'J' && int.TryParse(sysName.Substring(1), out int _) && (sysName[0] != 'J' || !int.TryParse(sysName.Substring(1), out _) || radius != 0)) ||
                        radiusSystemId == 0 || (radiusValue > 0 && value < radiusValue)) continue;

                    var data = JArray.Parse(await APIHelper.ESIAPI.GetRawRoute(Reason, radiusSystemId, systemId));
                    var routeLength = data.Count - 1;
                    //not in range
                    if (routeLength > radius) continue;

                    var rSystemName = (await APIHelper.ESIAPI.GetSystemData(Reason, radiusSystemId))?.name ?? LM.Get("Unknown");

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

                    var rVictimCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, victimCorpID);
                    var rAttackerCorp = finalBlowAttackerCorpId.HasValue && finalBlowAttackerCorpId.Value > 0
                        ? await APIHelper.ESIAPI.GetCorporationData(Reason, finalBlowAttackerCorpId)
                        : null;
                    var rVictimAlliance = victimAllianceID != 0 ? await APIHelper.ESIAPI.GetAllianceData(Reason, victimAllianceID) : null;
                    var rAttackerAlliance = finalBlowAttackerAllyId.HasValue && finalBlowAttackerAllyId.Value > 0
                        ? await APIHelper.ESIAPI.GetAllianceData(Reason, finalBlowAttackerAllyId)
                        : null;
                    var rShipType = await APIHelper.ESIAPI.GetTypeId(Reason, shipID);
                    var rVictimCharacter = await APIHelper.ESIAPI.GetCharacterData(Reason, victimCharacterID);
                    var rAttackerCharacter = await APIHelper.ESIAPI.GetCharacterData(Reason, finalBlowAttacker?.character_id);
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
                        {"{radiusJumps}", routeLength.ToString()}
                    };

                    if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailRadius, dic, radiusChannelId, group.Key))
                    {
                        var jumpsText = data.Count > 1 ? $"{routeLength} {LM.Get("From")} {rSystemName}" : $"{LM.Get("InSmall")} {sysName} ({systemSecurityStatus})";
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
