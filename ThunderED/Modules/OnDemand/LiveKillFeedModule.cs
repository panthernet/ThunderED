using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json.ZKill;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules.OnDemand
{
    public class LiveKillFeedModule: AppModuleBase
    {
        private int _lastPosted;
        public override LogCat Category => LogCat.KillFeed;
        private readonly bool _enableCache;

        public LiveKillFeedModule()
        {
            LogHelper.LogModule("Inititalizing LiveKillFeed module...", Category).GetAwaiter().GetResult();
            ZKillLiveFeedModule.Queryables.Add(ProcessKill);
            _enableCache = Settings.LiveKillFeedModule.EnableCache;
        }

        private async Task ProcessKill(JsonZKill.ZKillboard kill)
        {
            try
            {
                if (_lastPosted == kill.package.killID) return;

                var bigKillGlobalValue = Settings.LiveKillFeedModule.BigKill;
                var bigKillGlobalChan = Settings.LiveKillFeedModule.BigKillChannel;

                var killmailID = kill.package.killmail.killmail_id;
                var killTime = kill.package.killmail.killmail_time.ToString(SettingsManager.Settings.Config.ShortTimeFormat);
                var shipID = kill.package.killmail.victim.ship_type_id;
                var value = kill.package.zkb.totalValue;
                var victimCharacterID = kill.package.killmail.victim.character_id;
                var victimCorpID = kill.package.killmail.victim.corporation_id;
                var victimAllianceID = kill.package.killmail.victim.alliance_id;
                var attackers = kill.package.killmail.attackers;
                var finalBlowAttacker = attackers.FirstOrDefault(a => a.final_blow);
                var finalBlowAttackerCorpId = finalBlowAttacker?.corporation_id;
                var finalBlowAttackerAllyId = finalBlowAttacker?.alliance_id;
                var isNPCKill = kill.package.zkb.npc;

                // if(victimCorpID != 98370861) return;

                var systemId = kill.package.killmail.solar_system_id;
                var npckill = kill.package.zkb.npc;

                var postedGlobalBigKill = false;

                var rSystem = await APIHelper.ESIAPI.GetSystemData(Reason, systemId, false, !_enableCache);
                if (rSystem == null)
                {
                    //ESI fail - check back later
                    return;
                }

                var rVictimCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, victimCorpID, false, !_enableCache);

                var rAttackerCorp = finalBlowAttackerCorpId.HasValue && finalBlowAttackerCorpId.Value > 0
                    ? await APIHelper.ESIAPI.GetCorporationData(Reason, finalBlowAttackerCorpId, false, !_enableCache)
                    : null;
                if (rAttackerCorp == null)
                    isNPCKill = true;
                var rVictimAlliance = victimAllianceID != 0 ? await APIHelper.ESIAPI.GetAllianceData(Reason, victimAllianceID, false, !_enableCache) : null;
                var rAttackerAlliance = finalBlowAttackerAllyId.HasValue && finalBlowAttackerAllyId.Value > 0
                    ? await APIHelper.ESIAPI.GetAllianceData(Reason, finalBlowAttackerAllyId)
                    : null;
                var sysName = rSystem.name == rSystem.system_id.ToString() ? "Abyss" : rSystem.name;
                var rShipType = await APIHelper.ESIAPI.GetTypeId(Reason, shipID);
                var rVictimCharacter = await APIHelper.ESIAPI.GetCharacterData(Reason, victimCharacterID, false, !_enableCache);
                var rAttackerCharacter = await APIHelper.ESIAPI.GetCharacterData(Reason, finalBlowAttacker?.character_id, false, !_enableCache);
                var systemSecurityStatus = Math.Round(rSystem.security_status, 1).ToString("0.0");

                // ulong lastChannel = 0;

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
                    {"{attackersCount}", attackers.Length.ToString()},
                    {"{kmId}", killmailID.ToString()},
                    {"{isNpcKill}", isNPCKill.ToString()},
                    {"{timestamp}", killTime},
                    {"{isLoss}", "false"}
                };

                foreach (var groupPair in Settings.LiveKillFeedModule.GroupsConfig)
                {
                    var group = groupPair.Value;
                    var minimumValue = @group.MinimumValue;
                    var minimumLossValue = @group.MinimumLossValue;
                    var allianceIdList = @group.AllianceID;
                    var corpIdList = @group.CorpID;
                    var bigKillValue = @group.BigKillValue;
                    var c = @group.DiscordChannel;
                    var sendBigToGeneral = @group.BigKillSendToGeneralToo;
                    var bigKillChannel = @group.BigKillChannel;
                    var discordGroupName = groupPair.Key;

                    if ((!group.FeedPveKills && isNPCKill) || (!group.FeedPvpKills && !isNPCKill)) continue;

                    if (c == 0)
                    {
                        await LogHelper.LogWarning($"Group {groupPair.Key} has no 'discordChannel' specified! Kills will be skipped.", Category);
                        continue;
                    }

                    if (bigKillGlobalChan != 0 && bigKillGlobalValue != 0 && value >= bigKillGlobalValue && !postedGlobalBigKill)
                    {
                        postedGlobalBigKill = true;

                        if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailBig, dic, bigKillGlobalChan, discordGroupName))
                        {
                            await APIHelper.DiscordAPI.SendEmbedKillMessage(bigKillGlobalChan, new Color(0xFA2FF4), shipID, killmailID, rShipType.name, (long) value,
                                sysName, systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name, rVictimCorp.name,
                                rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
                                rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length, null);
                        }

                        await LogHelper.LogInfo($"Posting Global Big Kill: {kill.package.killID}  Value: {value:n0} ISK", Category);
                    }

                    if (!allianceIdList.Any() && !corpIdList.Any())
                    {
                        if (value >= minimumValue)
                        {
                            if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailGeneral, dic, c, discordGroupName))
                            {
                                await APIHelper.DiscordAPI.SendEmbedKillMessage(c, new Color(0x00FF00), shipID, killmailID, rShipType.name, (long) value, sysName,
                                    systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name, rVictimCorp.name,
                                    rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
                                    rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length, null);
                            }

                            await LogHelper.LogInfo($"Posting Global Kills: {kill.package.killID}  Value: {value:n0} ISK", Category);
                        }
                    }
                    else
                    {
                        //ally & corp 

                        //Losses
                        //Big
                        if (bigKillChannel != 0 && bigKillValue != 0 && value >= bigKillValue)
                        {
                            if (victimAllianceID != 0 && allianceIdList.Contains(victimAllianceID) || corpIdList.Contains(victimCorpID))
                            {
                                dic["{isLoss}"] = "true";
                                if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailBig, dic, bigKillChannel, discordGroupName))
                                {
                                    await APIHelper.DiscordAPI.SendEmbedKillMessage(bigKillChannel, new Color(0xD00000), shipID, killmailID, rShipType.name, (long) value,
                                        sysName, systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name, rVictimCorp.name,
                                        rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
                                        rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length, null,
                                        groupPair.Value.ShowGroupName ? discordGroupName : " ");
                                    if (sendBigToGeneral && c != bigKillChannel)
                                        if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailBig, dic, c, discordGroupName))
                                            await APIHelper.DiscordAPI.SendEmbedKillMessage(c, new Color(0xD00000), shipID, killmailID, rShipType.name, (long) value,
                                                sysName,
                                                systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name, rVictimCorp.name,
                                                rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
                                                rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length, null,
                                                groupPair.Value.ShowGroupName ? discordGroupName : " ");
                                }

                                await LogHelper.LogInfo($"Posting     Big Loss: {kill.package.killID}  Value: {value:n0} ISK", Category);
                                continue;
                            }
                        }

                        //Common
                        if (minimumLossValue == 0 || minimumLossValue <= value)
                        {
                            if (victimAllianceID != 0 && allianceIdList.Contains(victimAllianceID) || corpIdList.Contains(victimCorpID))
                            {
                                dic["{isLoss}"] = "true";
                                if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailGeneral, dic, c, discordGroupName))
                                {
                                    await APIHelper.DiscordAPI.SendEmbedKillMessage(c, new Color(0xFF0000), shipID, killmailID, rShipType?.name, (long) value, sysName,
                                        systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType?.name : rVictimCharacter?.name, rVictimCorp?.name,
                                        rVictimAlliance == null ? "" : $"[{rVictimAlliance?.ticker}]", isNPCKill, rAttackerCharacter?.name, rAttackerCorp?.name,
                                        rAttackerAlliance == null ? null : $"[{rAttackerAlliance?.ticker}]", attackers.Length, null,
                                        groupPair.Value.ShowGroupName ? discordGroupName : " ");
                                }

                                await LogHelper.LogInfo($"Posting         Loss: {kill.package.killID}  Value: {value:n0} ISK", Category);

                                continue;
                            }
                        }

                        //Kills
                        foreach (var attacker in attackers.ToList())
                        {
                            if (bigKillChannel != 0 && bigKillValue != 0 && value >= bigKillValue && !npckill)
                            {
                                if ((attacker.alliance_id != 0 && allianceIdList.Contains(attacker.alliance_id)) ||
                                    (!allianceIdList.Any() && corpIdList.Contains(attacker.corporation_id)))
                                {
                                    dic["{isLoss}"] = "false";
                                    if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailBig, dic, bigKillChannel, discordGroupName))
                                    {
                                        await APIHelper.DiscordAPI.SendEmbedKillMessage(bigKillChannel, new Color(0x00D000), shipID, killmailID, rShipType.name,
                                            (long) value, sysName, systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name,
                                            rVictimCorp.name,
                                            rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
                                            rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length, null,
                                            groupPair.Value.ShowGroupName ? discordGroupName : " ");
                                        if (sendBigToGeneral && c != bigKillChannel)
                                        {
                                            if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailBig, dic, c, discordGroupName))
                                                await APIHelper.DiscordAPI.SendEmbedKillMessage(c, new Color(0x00D000), shipID, killmailID, rShipType.name, (long) value,
                                                    sysName, systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name,
                                                    rVictimCorp.name,
                                                    rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
                                                    rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length, null,
                                                    groupPair.Value.ShowGroupName ? discordGroupName : " ");
                                        }

                                        await LogHelper.LogInfo($"Posting     Big Kill: {kill.package.killID}  Value: {value:#,##0} ISK", Category);
                                    }

                                    break;
                                }
                            }
                            else if (!npckill && attacker.alliance_id != 0 && allianceIdList.Any() && allianceIdList.Contains(attacker.alliance_id) ||
                                     !npckill && !allianceIdList.Any() && corpIdList.Contains(attacker.corporation_id))
                            {
                                dic["{isLoss}"] = "false";
                                if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailGeneral, dic, c, discordGroupName))
                                {
                                    await APIHelper.DiscordAPI.SendEmbedKillMessage(c, new Color(0x00FF00), shipID, killmailID, rShipType.name, (long) value, sysName,
                                        systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name, rVictimCorp.name,
                                        rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
                                        rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length, null,
                                        groupPair.Value.ShowGroupName ? discordGroupName : " ");
                                }

                                await LogHelper.LogInfo($"Posting         Kill: {kill.package.killID}  Value: {value:#,##0} ISK", Category);
                                break;
                            }
                        }
                    }
                }

                _lastPosted = killmailID;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
                await LogHelper.LogWarning($"Error processing kill ID {kill?.package?.killID} ! Msg: {ex.Message}", Category);
            }
        }
    }
}
