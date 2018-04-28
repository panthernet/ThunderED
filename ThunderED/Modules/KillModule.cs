using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Newtonsoft.Json.Linq;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public class KillModule: AppModuleBase
    {
        internal volatile bool IsKillfeedRunning;
        private int _lastPosted;
        public override LogCat Category => LogCat.KillFeed;

        public override async Task Run(object prm)
        {
            await KillFeed();
        }

        private async Task KillFeed()
        {
            try
            {
                if (!IsKillfeedRunning)
                {
                    IsKillfeedRunning = true;
                    var kill = await APIHelper.ZKillAPI.GetRedisqResponce();

                    if (kill?.package != null && _lastPosted != kill.package.killID)
                    {
                        var bigKillGlobal = SettingsManager.GetLong("killFeed","bigKill");
                        var bigKillGlobalChan = SettingsManager.GetULong("killFeed","bigKillChannel");
                        
                        var killmailID = kill.package.killmail.killmail_id;
                        var killTime = kill.package.killmail.killmail_time.ToString("dd.MM.yyyy hh:mm");
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

                        var systemId = kill.package.killmail.solar_system_id;
                        var radius = SettingsManager.GetShort("killFeed","radius");
                        var radiusSystem = SettingsManager.Get("killFeed","radiusSystem");
                        var radiusChannelId = SettingsManager.GetULong("killFeed","radiusChannel");
                        var npckill = kill.package.zkb.npc;

                        var postedRadius = false;
                        var postedGlobalBigKill = false;

                        var rSystem = await APIHelper.ESIAPI.GetSystemData(Reason, systemId);
                        if (rSystem == null)
                        {
                            //ESI fail - check back later
                            return;
                        }

                        var rVictimCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, victimCorpID);
                        var rAttackerCorp = finalBlowAttackerCorpId.HasValue && finalBlowAttackerCorpId.Value > 0 ? await APIHelper.ESIAPI.GetCorporationData(Reason, finalBlowAttackerCorpId) : null;
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

                        foreach (var i in SettingsManager.GetSubList("killFeed","groupsConfig"))
                        {
                            var minimumValue = Convert.ToInt64(i["minimumValue"]);
                            var minimumLossValue = Convert.ToInt64(i["minimumLossValue"]);
                            var allianceID = Convert.ToInt32(i["allianceID"]);
                            var corpID = Convert.ToInt32(i["corpID"]);
                            var bigKillValue = Convert.ToInt64(i["bigKillValue"]);
                            var c = Convert.ToUInt64(i["discordChannel"]);
                            var sendBigToGeneral = Convert.ToBoolean(i["bigKillSendToGeneralToo"]);
                            var bigKillChannel = Convert.ToUInt64(i["bigKillChannel"]);
                            var discordGroupName = i["name"];

                            if (radiusChannelId > 0 && (!(sysName[0] == 'J' && int.TryParse(sysName.Substring(1), out int _)) ||
                                sysName[0] == 'J' && int.TryParse(sysName.Substring(1), out _) && radius == 0) &&
                                !string.IsNullOrWhiteSpace(radiusSystem))
                            {
                                var httpresult = await APIHelper.ESIAPI.GetRadiusSystems(Reason, radiusSystem);
                                var firstSystemID = httpresult.solar_system[0].ToString();
                                var data = JArray.Parse(await APIHelper.ESIAPI.GetRawRoute(Reason, firstSystemID, systemId));

                                var gg = data.Count - 1;
                                if (gg < radius && !postedRadius)
                                {
                                    postedRadius = true;
                                    var jumpsText = data.Count > 1 ? $"{gg} {LM.Get("From")} {radiusSystem}" : $"{LM.Get("InSmall")} {sysName} ({systemSecurityStatus})";

                                    dic.Add("{radiusSystem}", radiusSystem);
                                    dic.Add("{radiusJumps}", gg.ToString());

                                    if (!await PostTemplatedMessage(MessageTemplateType.KillMailRadius, dic, radiusChannelId, discordGroupName))
                                    {
                                        await APIHelper.DiscordAPI.SendEmbedKillMessage(radiusChannelId, new Color(0x989898), shipID, killmailID, rShipType.name, (long) value,
                                            sysName,
                                            systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name, rVictimCorp.name,
                                            rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
                                            rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length, jumpsText);
                                    }

                                    await LogHelper.LogInfo($"Posting  Radius Kill: {kill.package.killID}  Value: {value:n0} ISK", Category);

                                }
                            }

                            if (bigKillGlobal != 0 && value >= bigKillGlobal && !postedGlobalBigKill)
                            {
                                postedGlobalBigKill = true;

                                if (!await PostTemplatedMessage(MessageTemplateType.KillMailBig, dic, bigKillGlobalChan, discordGroupName))
                                {
                                    await APIHelper.DiscordAPI.SendEmbedKillMessage(bigKillGlobalChan, new Color(0xFA2FF4), shipID, killmailID, rShipType.name, (long) value,
                                        sysName, systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name, rVictimCorp.name,
                                        rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
                                        rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length, null);
                                }

                                await LogHelper.LogInfo($"Posting Global Big Kill: {kill.package.killID}  Value: {value:n0} ISK", Category);

                            }
                            if (allianceID == 0 && corpID == 0)
                            {
                                if (value >= minimumValue)
                                {
                                    if (!await PostTemplatedMessage(MessageTemplateType.KillMailGeneral, dic, c, discordGroupName))
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
                                if (bigKillValue != 0 && value >= bigKillValue)
                                {
                                    if (victimAllianceID == allianceID || victimCorpID == corpID)
                                    {
                                        dic.Add("{isLoss}", "true");
                                        if (!await PostTemplatedMessage(MessageTemplateType.KillMailBig, dic, bigKillChannel, discordGroupName))
                                        {
                                            await APIHelper.DiscordAPI.SendEmbedKillMessage(bigKillChannel, new Color(0xD00000), shipID, killmailID, rShipType.name, (long) value,
                                                sysName, systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name, rVictimCorp.name,
                                                rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
                                                rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length, null, discordGroupName);
                                            if (sendBigToGeneral && c != bigKillChannel)
                                                if (!await PostTemplatedMessage(MessageTemplateType.KillMailBig, dic, c, discordGroupName))
                                                    await APIHelper.DiscordAPI.SendEmbedKillMessage(c, new Color(0xD00000), shipID, killmailID, rShipType.name, (long) value,
                                                        sysName,
                                                        systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name, rVictimCorp.name,
                                                        rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
                                                        rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length, null, discordGroupName);
                                        }

                                        await LogHelper.LogInfo($"Posting     Big Loss: {kill.package.killID}  Value: {value:n0} ISK", Category);
                                        continue;
                                    }
                                }
                                //Common
                                if (minimumLossValue == 0 || minimumLossValue <= value)
                                {
                                    if (victimAllianceID != 0 && victimAllianceID == allianceID || victimCorpID == corpID)
                                    {
                                        dic.Add("{isLoss}", "true");
                                        if (!await PostTemplatedMessage(MessageTemplateType.KillMailGeneral, dic, c, discordGroupName))
                                        {
                                            await APIHelper.DiscordAPI.SendEmbedKillMessage(c, new Color(0xFF0000), shipID, killmailID, rShipType?.name, (long) value, sysName,
                                                systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType?.name : rVictimCharacter?.name, rVictimCorp?.name,
                                                rVictimAlliance == null ? "" : $"[{rVictimAlliance?.ticker}]", isNPCKill, rAttackerCharacter?.name, rAttackerCorp?.name,
                                                rAttackerAlliance == null ? null : $"[{rAttackerAlliance?.ticker}]", attackers.Length, null, discordGroupName);
                                        }
                                        await LogHelper.LogInfo($"Posting         Loss: {kill.package.killID}  Value: {value:n0} ISK", Category);

                                        continue;
                                    }
                                }

                                //Kills
                                foreach (var attacker in attackers.ToList())
                                {
                                    if (bigKillValue != 0 && value >= bigKillValue && !npckill)
                                    {
                                        if ((attacker.alliance_id != 0 && attacker.alliance_id == allianceID) || (allianceID == 0 && attacker.corporation_id == corpID))
                                        {
                                            if (!await PostTemplatedMessage(MessageTemplateType.KillMailBig, dic, bigKillChannel, discordGroupName))
                                            {
                                                await APIHelper.DiscordAPI.SendEmbedKillMessage(bigKillChannel, new Color(0x00D000), shipID, killmailID, rShipType.name,
                                                    (long) value, sysName, systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name,
                                                    rVictimCorp.name,
                                                    rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
                                                    rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length, null, discordGroupName);
                                                if (sendBigToGeneral && c != bigKillChannel)
                                                {
                                                    if (!await PostTemplatedMessage(MessageTemplateType.KillMailBig, dic, c, discordGroupName))
                                                        await APIHelper.DiscordAPI.SendEmbedKillMessage(c, new Color(0x00D000), shipID, killmailID, rShipType.name, (long) value,
                                                            sysName, systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name,
                                                            rVictimCorp.name,
                                                            rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
                                                            rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length, null, discordGroupName);
                                                }

                                                await LogHelper.LogInfo($"Posting     Big Kill: {kill.package.killID}  Value: {value:#,##0} ISK", Category);
                                            }

                                            break;
                                        }
                                    }
                                    else if (!npckill && attacker.alliance_id != 0 && allianceID != 0 && attacker.alliance_id == allianceID || !npckill && allianceID == 0 && attacker.corporation_id == corpID)
                                    {
                                        if(!await PostTemplatedMessage(MessageTemplateType.KillMailGeneral, dic, c, discordGroupName))
                                        {
                                            await APIHelper.DiscordAPI.SendEmbedKillMessage(c, new Color(0x00FF00), shipID, killmailID, rShipType.name, (long) value, sysName,
                                                systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name, rVictimCorp.name,
                                                rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
                                                rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length, null, discordGroupName);
                                        }

                                        await LogHelper.LogInfo($"Posting         Kill: {kill.package.killID}  Value: {value:#,##0} ISK", Category);
                                        break;
                                    }
                                }
                            }
                        }
                        _lastPosted = killmailID;
                    }
                    else if (kill?.package != null && _lastPosted != 0 && _lastPosted == kill.package.killID)
                    {
                        await LogHelper.LogInfo($"Skipping kill: {kill.package.killID} as its been posted recently", Category);
                    }
                    await Task.Delay(100);
                    IsKillfeedRunning = false;
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
                IsKillfeedRunning = false;
            }
        }

        private async Task<bool> PostTemplatedMessage(MessageTemplateType type, Dictionary<string, string> dic, ulong channelId, string message)
        {
            var templateFile = GetTemplate(type);
            if (string.IsNullOrEmpty(templateFile)) return false;
            var embed = await TemplateHelper.CompileTemplate(type, templateFile, dic);
            if (embed == null) return false;
            var guildID = SettingsManager.GetULong("config", "discordGuildId");
            var discordGuild = APIHelper.DiscordAPI.Client.Guilds.FirstOrDefault(x => x.Id == guildID);
            var channel = discordGuild?.GetTextChannel(channelId);
            if (channel != null) await channel.SendMessageAsync(message, false, embed).ConfigureAwait(false);
            return true;
        }

        private string GetTemplate(MessageTemplateType type)
        {
            string typeFile;
            switch (type)
            {
                case MessageTemplateType.KillMailBig:
                    typeFile = Path.Combine(SettingsManager.RootDirectory, "Templates/Messages", "Template.killMailBig.txt");
                    break;
                case MessageTemplateType.KillMailGeneral:
                    typeFile = Path.Combine(SettingsManager.RootDirectory, "Templates/Messages", "Template.killMailGeneral.txt");
                    break;
                case MessageTemplateType.KillMailRadius:
                    typeFile = Path.Combine(SettingsManager.RootDirectory, "Templates/Messages", "Template.killMailRadius.txt");
                    break;
                default:
                    return null;
            }

            if (!string.IsNullOrEmpty(typeFile) && File.Exists(typeFile))
                return typeFile;
            return null;
        }

    }
}
