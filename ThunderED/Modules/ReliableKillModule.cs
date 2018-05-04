using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public class ReliableKillModule: AppModuleBase
    {
        internal volatile bool IsKillfeedRunning;
        private int _lastPosted;
        public override LogCat Category => LogCat.KillFeed;

        public override async Task Run(object prm)
        {
            await ReliableKillFeed();
        }

        private async Task ReliableKillFeed()
        {
            try
            {
                if (!IsKillfeedRunning)
                {
                    IsKillfeedRunning = true;

                    var interval = SettingsManager.GetInt("reliableKillFeed", "queryIntervalInSeconds");

                    foreach (var i in SettingsManager.GetSubList("reliableKillFeed", "groupsConfig"))
                    {
                        var minimumValue = Convert.ToInt64(i["minimumValue"]);
                        var minimumLossValue = Convert.ToInt64(i["minimumLossValue"]);
                        var allianceID = Convert.ToInt32(i["allianceID"]);
                        var corpID = Convert.ToInt32(i["corpID"]);
                        var bigKillValue = Convert.ToInt64(i["bigKillValue"]);
                        var c = Convert.ToUInt64(i["discordChannel"]);
                        var sendBigToGeneral = Convert.ToBoolean(i["bigKillSendToGeneralToo"]);
                        var bigKillChannel = Convert.ToUInt64(i["bigKillChannel"]);
                        var discordGroupName = i.Key;
                        var isAlliance = corpID == 0;
                        var isLossEnabled = Convert.ToBoolean(i["losses"]);

                        var kills = await APIHelper.ZKillAPI.GetZKillOnlyFeed(isAlliance, isAlliance ? allianceID : corpID);
                        kills.Reverse();
                        var where = new Dictionary<string, object>();
                        where.Add("type", isAlliance? "ally" : "corp");
                        where.Add("id", isAlliance? allianceID.ToString() : corpID.ToString());
                        var resultQ = await SQLiteHelper.SQLiteDataQuery("killFeedCache", "lastId", where);
                        _lastPosted = string.IsNullOrEmpty(resultQ) ? 0 : Convert.ToInt32(resultQ);
                        kills = _lastPosted == 0 ? kills.TakeLast(5).ToList() : kills.Where(a => a.killmail_id > _lastPosted).ToList();
                        
                        if(kills.Count == 0) continue;

                        foreach (var lightKill in kills)
                        {
                            var killmailID = lightKill.killmail_id;
                            var kill = await APIHelper.ZKillAPI.GetLightEntityKill(killmailID);
                            var killTime = kill.killmail_time.ToString("dd.MM.yyyy hh:mm");
                            var shipID = kill.victim.ship_type_id;
                            var value = kill.zkb.totalValue;
                            var victimCharacterID = kill.victim.character_id;
                            var victimCorpID = kill.victim.corporation_id;
                            var victimAllianceID = kill.victim.alliance_id;


                            var attackers = kill.attackers;
                            var finalBlowAttacker = attackers.FirstOrDefault(a => a.final_blow);
                            var finalBlowAttackerCorpId = finalBlowAttacker?.corporation_id;
                            var finalBlowAttackerAllyId = finalBlowAttacker?.alliance_id;
                            var isNPCKill = kill.zkb.npc;
                            var systemId = kill.solar_system_id;

                            var rSystem = await APIHelper.ESIAPI.GetSystemData(Reason, systemId);
                            if (rSystem == null)
                            {
                                //ESI fail - check back later
                                return;
                            }

                            var rVictimCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, victimCorpID);
                            var rAttackerCorp = finalBlowAttackerCorpId.HasValue && finalBlowAttackerCorpId.Value > 0
                                ? await APIHelper.ESIAPI.GetCorporationData(Reason, finalBlowAttackerCorpId)
                                : null;
                            if (rAttackerCorp == null)
                                isNPCKill = true;
                            var rVictimAlliance = victimAllianceID != 0 ? await APIHelper.ESIAPI.GetAllianceData(Reason, victimAllianceID) : null;
                            var rAttackerAlliance = finalBlowAttackerAllyId.HasValue && finalBlowAttackerAllyId.Value > 0
                                ? await APIHelper.ESIAPI.GetAllianceData(Reason, finalBlowAttackerAllyId)
                                : null;
                            var sysName = rSystem?.name;
                            var rShipType = await APIHelper.ESIAPI.GetTypeId(Reason, shipID);
                            var rVictimCharacter = await APIHelper.ESIAPI.GetCharacterData(Reason, victimCharacterID);
                            var rAttackerCharacter = await APIHelper.ESIAPI.GetCharacterData(Reason, finalBlowAttacker?.character_id);
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
                                {"{attackersCount}", attackers?.Length.ToString()},
                                {"{kmId}", killmailID.ToString()},
                                {"{isNpcKill}", isNPCKill.ToString()},
                                {"{timestamp}", killTime},

                            };

                            var isAttack = attackers.Any(a => a.alliance_id != 0 && a.alliance_id == allianceID || corpID != 0 && a.corporation_id == corpID);

                            if (bigKillValue != 0 && value >= bigKillValue && (victimAllianceID == allianceID || victimCorpID == corpID || isAttack))
                            {
                                dic.Add("{isLoss}", isAttack ? "false" : "true");
                                if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailBig, dic, bigKillChannel, discordGroupName))
                                {
                                    await APIHelper.DiscordAPI.SendEmbedKillMessage(bigKillChannel, isAttack ? new Color(0x00ff00) : new Color(0xff0000), shipID, killmailID, rShipType.name,
                                        (long) value,
                                        sysName, systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name, rVictimCorp.name,
                                        rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
                                        rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length, null, discordGroupName);
                                    if (sendBigToGeneral && c != bigKillChannel)
                                        if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailBig, dic, c, discordGroupName))
                                            await APIHelper.DiscordAPI.SendEmbedKillMessage(c, isAttack ? new Color(0x00ff00) : new Color(0xff0000), shipID, killmailID, rShipType.name, (long) value,
                                                sysName,
                                                systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name, rVictimCorp.name,
                                                rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
                                                rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length, null, discordGroupName);
                                }

                                await LogHelper.LogInfo($"Posting     Big {(isAttack? "Kill": "Loss")}: {kill.killmail_id}  Value: {value:n0} ISK", Category);
                            }
                            else if ((minimumLossValue == 0 || minimumLossValue <= value) && ((victimAllianceID != 0 && victimAllianceID == allianceID) || (corpID != 0 && victimCorpID == corpID)) && isLossEnabled)
                            {
                                var isBigKill = bigKillValue != 0 && value >= bigKillValue;
                                var channel = isBigKill ? (bigKillChannel == 0 ? c : bigKillChannel) : c;
                                var template = isBigKill ? MessageTemplateType.KillMailBig : MessageTemplateType.KillMailGeneral;
                                dic.Add("{isLoss}", "true");

                                if (!await TemplateHelper.PostTemplatedMessage(template, dic, channel, discordGroupName))
                                {
                                    await APIHelper.DiscordAPI.SendEmbedKillMessage(channel, new Color(0xFF0000), shipID, killmailID, rShipType?.name, (long) value, sysName,
                                        systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType?.name : rVictimCharacter?.name, rVictimCorp?.name,
                                        rVictimAlliance == null ? "" : $"[{rVictimAlliance?.ticker}]", isNPCKill, rAttackerCharacter?.name, rAttackerCorp?.name,
                                        rAttackerAlliance == null ? null : $"[{rAttackerAlliance?.ticker}]", attackers.Length, null, discordGroupName);
                                }

                                await LogHelper.LogInfo($"Posting         Loss: {kill.killmail_id}  Value: {value:n0} ISK", Category);
                            }
                            else if (isAttack && !isNPCKill && value > minimumValue)
                            {
                                var isBigKill = bigKillValue != 0 && value >= bigKillValue;
                                var channel = isBigKill ? (bigKillChannel == 0 ? c : bigKillChannel) : c;
                                var template = isBigKill ? MessageTemplateType.KillMailBig : MessageTemplateType.KillMailGeneral;
                                dic.Add("{isLoss}", "false");

                                if (!await TemplateHelper.PostTemplatedMessage(template, dic, channel, discordGroupName))
                                {
                                    await APIHelper.DiscordAPI.SendEmbedKillMessage(c, new Color(0x00FF00), shipID, killmailID, rShipType.name, (long) value, sysName,
                                        systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name, rVictimCorp.name,
                                        rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
                                        rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length, null, discordGroupName);
                                }

                                await LogHelper.LogInfo($"Posting         Kill: {kill.killmail_id}  Value: {value:#,##0} ISK", Category);
                            }
                                

                            _lastPosted = kill.killmail_id;
                            var dic2 = new Dictionary<string, object>
                            {
                                {"type", isAlliance ? "ally" : "corp"},
                                {"id", isAlliance ? allianceID.ToString() : corpID.ToString()},
                                {"lastId", _lastPosted.ToString()}
                            };
                            await SQLiteHelper.SQLiteDataInsertOrUpdate("killFeedCache", dic2);  
                        }
                    }

                    await Task.Delay(interval * 1000);
                    IsKillfeedRunning = false;
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
                IsKillfeedRunning = false;
            }
        }

    }
}
