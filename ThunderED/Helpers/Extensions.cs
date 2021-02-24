using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Json.Internal;
using ThunderED.Json.ZKill;
using ThunderED.Thd;

namespace ThunderED
{
    public static class Extensions
    {
        public static async Task UpdateData(this ThdAuthUser user, JsonClasses.CharacterData characterData, JsonClasses.CorporationData rCorp = null, JsonClasses.AllianceData rAlliance = null, string permissions = null, bool forceUpdate = false)
        {
            rCorp ??= await APIHelper.ESIAPI.GetCorporationData(LogCat.AuthCheck.ToString(), characterData.corporation_id, forceUpdate);
            user.DataView.CharacterName = characterData.name;
            user.DataView.CorporationId = characterData.corporation_id;
            user.DataView.CorporationName = rCorp?.name;
            user.DataView.CorporationTicker = rCorp?.ticker;
            user.DataView.AllianceId = characterData.alliance_id ?? 0;
            user.DataView.AllianceName = null;
            user.DataView.AllianceTicker = null;
            if (user.DataView.AllianceId > 0)
            {
                rAlliance ??= await APIHelper.ESIAPI.GetAllianceData(LogCat.AuthCheck.ToString(), characterData.alliance_id, forceUpdate);
                user.DataView.AllianceName = rAlliance?.name;
                user.DataView.AllianceTicker = rAlliance?.ticker;
            }
            if (permissions != null)
                user.DataView.Permissions = permissions;

            user.MiscData.BirthDate = characterData.birthday;
            user.MiscData.SecurityStatus = characterData.security_status;
        }

        public static async Task<ThdAuthUser> CreateAlt(this ThdAuthUser user, long characterId, WebAuthGroup @group, string groupName, long mainCharId)
        {
            var authUser = new ThdAuthUser
            {
                CharacterId = characterId,
                DiscordId = 0,
                //Tokens = new List<ThdToken> { new ThdToken() { Token = refreshToken, CharacterId = characterId, Type = TokenEnum.General}},
                GroupName = groupName,
                AuthState = 2,
                CreateDate = DateTime.Now,
                MainCharacterId = mainCharId
            };
            var characterData = await APIHelper.ESIAPI.GetCharacterData(LogCat.AuthWeb.ToString(), characterId);
            await authUser.UpdateData(characterData, null, null, group.ESICustomAuthRoles.Count > 0 ? string.Join(',', group.ESICustomAuthRoles) : null);
            return authUser;
        }

        public static async Task<bool> RefreshRadius(this KillDataEntry entry, string reason, JsonZKill.Killmail kill)
        {
            if (entry.killmailID > 0) return true;
            try
            {
                entry.killmailID = kill.killmail_id;
                entry.value = kill.zkb.totalValue;
                entry.systemId = kill.solar_system_id;
                entry.rSystem = await APIHelper.ESIAPI.GetSystemData(reason, entry.systemId);
                entry.isUnreachableSystem = entry.systemId == 31000005;
                if (entry.rSystem != null)
                {
                    entry.sysName = entry.rSystem.IsAbyss() ? "Abyss" : (entry.rSystem.IsThera() ? "Thera" : (entry.rSystem.IsWormhole() ? "J" : entry.rSystem.name));
                    entry.isUnreachableSystem = entry.rSystem.IsUnreachable();
                }
                else entry.sysName = "?";
                var rRegion = entry.rSystem != null ? await APIHelper.ESIAPI.GetRegionData(reason, entry.rSystem.DB_RegionId) : null;

                entry.victimCharacterID = kill.victim.character_id;
                entry.victimCorpID = kill.victim.corporation_id;
                entry.victimAllianceID = kill.victim.alliance_id;
                entry.attackers = kill.attackers;
                entry.finalBlowAttacker = entry.attackers.FirstOrDefault(a => a.final_blow);
                entry.finalBlowAttackerCharacterId = entry.finalBlowAttacker.character_id;
                entry.finalBlowAttackerCorpId = entry.finalBlowAttacker?.corporation_id ?? 0;
                entry.finalBlowAttackerAllyId = entry.finalBlowAttacker?.alliance_id ?? 0;
                entry.victimShipID = kill.victim.ship_type_id;
                entry.attackerShipID = entry.finalBlowAttacker.ship_type_id;
                entry.killTime = kill.killmail_time.ToString(SettingsManager.Settings.Config.ShortTimeFormat);

                entry.rVictimCorp = await APIHelper.ESIAPI.GetCorporationData(reason, entry.victimCorpID);
                entry.rAttackerCorp = entry.finalBlowAttackerCorpId > 0
                    ? await APIHelper.ESIAPI.GetCorporationData(reason, entry.finalBlowAttackerCorpId)
                    : null;
                entry.rVictimAlliance = entry.victimAllianceID != 0 ? await APIHelper.ESIAPI.GetAllianceData(reason, entry.victimAllianceID) : null;
                entry.rAttackerAlliance = entry.finalBlowAttackerAllyId > 0
                    ? await APIHelper.ESIAPI.GetAllianceData(reason, entry.finalBlowAttackerAllyId)
                    : null;
                entry.rVictimShipType = await APIHelper.ESIAPI.GetTypeId(reason, entry.victimShipID);
                entry.rAttackerShipType = await APIHelper.ESIAPI.GetTypeId(reason, entry.attackerShipID);
                entry.rVictimCharacter = await APIHelper.ESIAPI.GetCharacterData(reason, entry.victimCharacterID);
                entry.rAttackerCharacter = await APIHelper.ESIAPI.GetCharacterData(reason, entry.finalBlowAttacker?.character_id);
                entry.systemSecurityStatus = Math.Round(entry.rSystem.security_status, 1).ToString("0.0");

                entry.dic = new Dictionary<string, string>
                    {
                        {"{shipID}", entry.victimShipID.ToString()},
                        {"{shipType}", entry.rVictimShipType?.name},
                        {"{attackerShipID}", entry.attackerShipID.ToString()},
                        {"{attackershipType}", entry.rAttackerShipType?.name},
                        {"{iskValue}", entry.value.ToString("n0")},
                        {"{iskFittedValue}", kill?.zkb?.fittedValue.ToString("n0") ?? "0"},
                        {"{systemName}", entry.sysName},
                        {"{systemID}", entry.rSystem.system_id.ToString()},
                        {"{regionName}", rRegion?.name},
                        {"{regionID}", rRegion != null ? entry.rSystem?.DB_RegionId.ToString() : null},
                        {"{systemSec}", entry.systemSecurityStatus},
                        {"{victimName}", entry.rVictimCharacter?.name},
                        {"{victimID}", entry.rVictimCharacter?.character_id.ToString()},
                        {"{victimCorpName}", entry.rVictimCorp?.name},
                        {"{victimCorpID}", entry.rVictimCharacter?.corporation_id.ToString()},
                        {"{victimCorpTicker}", entry.rVictimCorp?.ticker},
                        {"{victimAllyName}", entry.rVictimAlliance?.name},
                        {"{victimAllyID}", entry.rVictimAlliance != null ? entry.rVictimCorp?.alliance_id.ToString() : null},
                        {"{victimAllyTicker}", entry.rVictimAlliance == null ? null : $"<{entry.rVictimAlliance.ticker}>"},
                        {"{victimAllyOrCorpName}", entry.rVictimAlliance?.name ?? entry.rVictimCorp?.name},
                        {"{victimAllyOrCorpTicker}", entry.rVictimAlliance?.ticker ?? entry.rVictimCorp?.ticker},
                        {"{attackerName}", entry.rAttackerCharacter?.name},
                        {"{attackerID}", entry.rAttackerCharacter?.character_id.ToString()},
                        {"{attackerCorpName}", entry.rAttackerCorp?.name},
                        {"{attackerCorpID}", entry.rAttackerCharacter?.corporation_id.ToString()},
                        {"{attackerCorpTicker}", entry.rAttackerCorp?.ticker},
                        {"{attackerAllyName}", entry.rAttackerAlliance?.name},
                        {"{attackerAllyID}", entry.rAttackerAlliance != null ? entry.rAttackerCorp?.alliance_id.ToString() : null},
                        {"{attackerAllyTicker}", entry.rAttackerAlliance == null ? null : $"<{entry.rAttackerAlliance.ticker}>"},
                        {"{attackerAllyOrCorpName}", entry.rAttackerAlliance?.name ?? entry.rAttackerCorp?.name},
                        {"{attackerAllyOrCorpTicker}", entry.rAttackerAlliance?.ticker ?? entry.rAttackerCorp?.ticker},
                        {"{attackersCount}", entry.attackers?.Length.ToString()},
                        {"{kmId}", entry.killmailID.ToString()},
                        {"{timestamp}", entry.killTime},
                        {"{isNpcKill}", entry.isNPCKill.ToString() ?? "false"},
                        {"{isSoloKill}", kill?.zkb?.solo.ToString() ?? "false"},
                        {"{isAwoxKill}", kill?.zkb?.awox.ToString() ?? "false"},
                    };


                return true;
            }
            catch (Exception ex)
            {
                entry.killmailID = 0;
                await LogHelper.LogEx("refresh ex", ex, LogCat.RadiusKill);
                return false;
            }
        }

        public static async Task<bool> Refresh(this KillDataEntry entry, string reason, JsonZKill.Killmail kill)
        {
            if (entry.killmailID > 0) return true;
            try
            {
                entry.killmailID = kill.killmail_id;
                entry.killTime = kill.killmail_time.ToString(SettingsManager.Settings.Config.ShortTimeFormat);
                entry.victimShipID = kill.victim.ship_type_id;
                entry.value = kill.zkb.totalValue;
                entry.victimCharacterID = kill.victim.character_id;
                entry.victimCorpID = kill.victim.corporation_id;
                entry.victimAllianceID = kill.victim.alliance_id;
                entry.attackers = kill.attackers;
                entry.finalBlowAttacker = entry.attackers.FirstOrDefault(a => a.final_blow);
                entry.finalBlowAttackerCharacterId = entry.finalBlowAttacker.character_id;
                entry.attackerShipID = entry.finalBlowAttacker?.ship_type_id ?? 0;
                entry.finalBlowAttackerCorpId = entry.finalBlowAttacker?.corporation_id ?? 0;
                entry.finalBlowAttackerAllyId = entry.finalBlowAttacker?.alliance_id ?? 0;
                entry.isNPCKill = kill.zkb.npc;
                entry.systemId = kill.solar_system_id;
                entry.rSystem = await APIHelper.ESIAPI.GetSystemData(reason, entry.systemId);
                if (entry.rSystem == null)
                {
                    //ESI fail - check back later
                    return false;
                }

                entry.rVictimCorp = await APIHelper.ESIAPI.GetCorporationData(reason, entry.victimCorpID);
                entry.rAttackerCorp = entry.finalBlowAttackerCorpId > 0
                    ? await APIHelper.ESIAPI.GetCorporationData(reason, entry.finalBlowAttackerCorpId)
                    : null;

                entry.rVictimAlliance = entry.victimAllianceID != 0 ? await APIHelper.ESIAPI.GetAllianceData(reason, entry.victimAllianceID) : null;
                entry.rAttackerAlliance = entry.finalBlowAttackerAllyId > 0
                    ? await APIHelper.ESIAPI.GetAllianceData(reason, entry.finalBlowAttackerAllyId)
                    : null;
                entry.sysName = entry.rSystem.name == entry.rSystem.system_id.ToString() ? "Abyss" : entry.rSystem.name;
                var rConst = entry.rSystem != null ? await APIHelper.ESIAPI.GetConstellationData(reason, entry.rSystem.constellation_id) : null;
                var rRegion = rConst != null ? await APIHelper.ESIAPI.GetRegionData(reason, rConst.region_id) : null;
                entry.rVictimShipType = await APIHelper.ESIAPI.GetTypeId(reason, entry.victimShipID);
                entry.rAttackerShipType = await APIHelper.ESIAPI.GetTypeId(reason, entry.attackerShipID);
                entry.rVictimCharacter = await APIHelper.ESIAPI.GetCharacterData(reason, entry.victimCharacterID);
                entry.rAttackerCharacter = await APIHelper.ESIAPI.GetCharacterData(reason, entry.finalBlowAttacker?.character_id);
                entry.systemSecurityStatus = Math.Round(entry.rSystem.security_status, 1).ToString("0.0");

                entry.dic = new Dictionary<string, string>
                    {
                        {"{shipID}", entry.victimShipID.ToString()},
                        {"{shipType}", entry.rVictimShipType?.name},
                        {"{attackerShipID}", entry.attackerShipID.ToString()},
                        {"{attackerShipType}", entry.rAttackerShipType?.name},
                        {"{iskValue}", entry.value.ToString("n0")},
                        {"{iskFittedValue}", kill?.zkb?.fittedValue.ToString("n0") ?? "0"},
                        {"{systemName}", entry.sysName},
                        {"{systemID}", entry.rSystem.system_id.ToString()},
                        {"{constName}", rConst?.name},
                        {"{constID}", rConst?.constellation_id.ToString()},
                        {"{regionName}", rRegion?.name},
                        {"{regionID}", rRegion != null ? rConst?.region_id.ToString() : null},
                        {"{systemSec}", entry.systemSecurityStatus},
                        {"{victimName}", entry.rVictimCharacter?.name},
                        {"{victimID}", entry.rVictimCharacter?.character_id.ToString()},
                        {"{victimCorpName}", entry.rVictimCorp?.name},
                        {"{victimCorpID}", entry.rVictimCharacter?.corporation_id.ToString()},
                        {"{victimCorpTicker}", entry.rVictimCorp?.ticker},
                        {"{victimAllyName}", entry.rVictimAlliance?.name},
                        {"{victimAllyID}",entry. rVictimAlliance != null ? entry.rVictimCorp?.alliance_id.ToString() : null},
                        {"{victimAllyTicker}", entry.rVictimAlliance == null ? null : $"<{entry.rVictimAlliance.ticker}>"},
                        {"{victimAllyOrCorpName}", entry.rVictimAlliance?.name ?? entry.rVictimCorp?.name},
                        {"{victimAllyOrCorpTicker}", entry.rVictimAlliance?.ticker ?? entry.rVictimCorp?.ticker},
                        {"{attackerName}", entry.rAttackerCharacter?.name},
                        {"{attackerID}", entry.rAttackerCharacter?.character_id.ToString()},
                        {"{attackerCorpName}", entry.rAttackerCorp?.name},
                        {"{attackerCorpID}", entry.rAttackerCharacter?.corporation_id.ToString()},
                        {"{attackerCorpTicker}", entry.rAttackerCorp?.ticker},
                        {"{attackerAllyName}", entry.rAttackerAlliance?.name},
                        {"{attackerAllyID}", entry.rAttackerAlliance != null ? entry.rAttackerCorp?.alliance_id.ToString() : null},
                        {"{attackerAllyTicker}", entry.rAttackerAlliance == null ? null : $"<{entry.rAttackerAlliance.ticker}>"},
                        {"{attackerAllyOrCorpName}", entry.rAttackerAlliance?.name ?? entry.rAttackerCorp?.name},
                        {"{attackerAllyOrCorpTicker}", entry.rAttackerAlliance?.ticker ?? entry.rAttackerCorp?.ticker},
                        {"{attackersCount}", entry.attackers.Length.ToString()},
                        {"{kmId}", entry.killmailID.ToString()},
                        {"{timestamp}", entry.killTime},
                        {"{isLoss}", "false"},
                        {"{isNpcKill}", entry.isNPCKill.ToString() ?? "false"},
                        {"{isSoloKill}", kill?.zkb?.solo.ToString() ?? "false"},
                        {"{isAwoxKill}", kill?.zkb?.awox.ToString() ?? "false"},
                    };

                return true;
            }
            catch (Exception ex)
            {
                entry.killmailID = 0;
                await LogHelper.LogEx("refresh ex", ex, LogCat.KillFeed);
                return false;
            }
        }

        public static string GetStageName(this TimerItem entry)
        {
            switch (entry.timerStage)
            {
                case 1:
                    return LM.Get("timerHull");
                case 2:
                    return LM.Get("timerArmor");
                case 3:
                    return LM.Get("timerShield");
                case 4:
                    return LM.Get("timerOther");
                default:
                    return null;
            }
        }

        public static string GetRemains(this TimerItem entry, bool addWord = false)
        {
            if (!entry.Date.HasValue) return null;
            var dif = (entry.Date.Value - DateTime.UtcNow);
            return $"{(addWord ? $"{LM.Get("Remains")} " : null)}{LM.Get("timerRemains", dif.Days, dif.Hours, dif.Minutes)}";
        }

        public static string GetModeName(this TimerItem entry)
        {
            switch (entry.timerType)
            {
                case 1:
                    return LM.Get("timerOffensive");
                case 2:
                    return LM.Get("timerDefensive");
                default:
                    return null;
            }
        }

        public static async Task UpdateData(this ThdAuthUser user, bool forceUpdate = false)
        {
            var ch = await APIHelper.ESIAPI.GetCharacterData(LogCat.AuthCheck.ToString(), user.CharacterId, forceUpdate);
            if (ch == null) return;
            await UpdateData(user, ch, null, null, null, forceUpdate);
            user.PackData();
            user.MiscData.BirthDate = ch.birthday;
            user.MiscData.SecurityStatus = ch.security_status;
        }
        public static TimerItem FromWebTimerData(this WebTimerData entry, WebTimerData data, WebAuthUserData user)
        {
            var ti = new TimerItem
            {
                timerLocation = data.Location,
                timerType = data.Type,
                timerStage = data.Stage,
                timerOwner = data.Owner,
                timerET = ((int)(data.Date.Subtract(new DateTime(1970, 1, 1))).TotalSeconds).ToString(),
                timerNotes = data.Notes,
                timerChar = user.Name,
                Id = data.Id,
            };

            ti.Date = ti.GetDateTime();
            return ti;
        }

        public static async Task PopulateNames(this JsonClasses.SkillsData entry)
        {
            foreach (var skill in entry.skills)
            {
                var t = await SQLHelper.GetTypeId(skill.skill_id);
                if (t == null) continue;
                skill.DB_Name = t.name;
                skill.DB_Description = t.description;
                skill.DB_Group = t.group_id;
                var g = await SQLHelper.GetInvGroup(skill.DB_Group);
                if (g == null) continue;
                skill.DB_GroupName = g.groupName;
            }
        }

        public static LogSeverity ToSeverity(this Discord.LogSeverity severity)
        {
            switch (severity)
            {
                case Discord.LogSeverity.Info:
                    return LogSeverity.Info;
                case Discord.LogSeverity.Debug:
                    return LogSeverity.Debug;
                case Discord.LogSeverity.Warning:
                    return LogSeverity.Warning;
                case Discord.LogSeverity.Critical:
                    return LogSeverity.Critical;
                case Discord.LogSeverity.Error:
                    return LogSeverity.Error;
                case Discord.LogSeverity.Verbose:
                    return LogSeverity.Verbose;
                default:
                    return LogSeverity.Info;
            }
        }

        public static string ToFormattedString(this TimeSpan ts, string separator = ", ")
        {
            if (ts.TotalMilliseconds < 1) { return "No time"; }

            return string.Join(separator, new string[]
            {
                ts.Days > 0 ? $"{ts.Days}{LM.Get("dateD")} " : null,
                ts.Hours > 0 ? $"{ts.Hours}{LM.Get("dateH")} " : null,
                ts.Minutes > 0 ? $"{ts.Minutes}{LM.Get("dateM")}" : null
                //ts.Seconds > 0 ? ts.Seconds + (ts.Seconds > 1 ? " seconds" : " second") : null,
                //ts.Milliseconds > 0 ? ts.Milliseconds + (ts.Milliseconds > 1 ? " milliseconds" : " millisecond") : null,
            }.Where(t => t != null));
        }
    }
}
