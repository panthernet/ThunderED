using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Json.ZKill;

namespace ThunderED.Classes.Entities
{
    internal partial class KillDataEntry
    {
        public long killmailID;
        public string killTime;
        public long victimShipID;
        public long attackerShipID;
        public float value;
        public long victimCharacterID;
        public long victimCorpID;
        public long victimAllianceID;
        public JsonZKill.Attacker[] attackers;
        public JsonZKill.Attacker finalBlowAttacker;
        public long finalBlowAttackerCharacterId;
        public long finalBlowAttackerCorpId;
        public long finalBlowAttackerAllyId;
        public bool isNPCKill;
        public long systemId;
        public JsonClasses.SystemName rSystem;
        public JsonClasses.CorporationData rVictimCorp;
        public JsonClasses.CorporationData rAttackerCorp;
        public JsonClasses.AllianceData rVictimAlliance;
        public JsonClasses.AllianceData rAttackerAlliance;
        public string sysName;
        public JsonClasses.Type_id rVictimShipType;
        public JsonClasses.Type_id rAttackerShipType;
        public JsonClasses.CharacterData rVictimCharacter;
        public JsonClasses.CharacterData rAttackerCharacter;
        public string systemSecurityStatus;
        public Dictionary<string, string> dic;
        public bool isUnreachableSystem;

        public async Task<bool> RefreshRadius(string reason, JsonZKill.Killmail kill)
        {
            if (killmailID > 0) return true;
            try
            {
                killmailID = kill.killmail_id;
                value = kill.zkb.totalValue;
                systemId = kill.solar_system_id;
                rSystem = await APIHelper.ESIAPI.GetSystemData(reason, systemId);
                isUnreachableSystem = systemId == 31000005;
                if (rSystem != null)
                {
                    sysName = rSystem.IsAbyss() ? "Abyss" : (rSystem.IsThera() ? "Thera" : (rSystem.IsWormhole() ? "J" : rSystem.name));
                    isUnreachableSystem = rSystem.IsUnreachable();
                }
                else sysName = "?";
                var rRegion = rSystem != null ? await APIHelper.ESIAPI.GetRegionData(reason, rSystem.DB_RegionId) : null;

                victimCharacterID = kill.victim.character_id;
                victimCorpID = kill.victim.corporation_id;
                victimAllianceID = kill.victim.alliance_id;
                attackers = kill.attackers;
                finalBlowAttacker = attackers.FirstOrDefault(a => a.final_blow);
                finalBlowAttackerCharacterId = finalBlowAttacker.character_id;
                finalBlowAttackerCorpId = finalBlowAttacker?.corporation_id ?? 0;
                finalBlowAttackerAllyId = finalBlowAttacker?.alliance_id ?? 0;
                victimShipID = kill.victim.ship_type_id;
                attackerShipID = finalBlowAttacker.ship_type_id;
                killTime = kill.killmail_time.ToString(SettingsManager.Settings.Config.ShortTimeFormat);

                rVictimCorp = await APIHelper.ESIAPI.GetCorporationData(reason, victimCorpID);
                rAttackerCorp = finalBlowAttackerCorpId > 0
                    ? await APIHelper.ESIAPI.GetCorporationData(reason, finalBlowAttackerCorpId)
                    : null;
                rVictimAlliance = victimAllianceID != 0 ? await APIHelper.ESIAPI.GetAllianceData(reason, victimAllianceID) : null;
                rAttackerAlliance = finalBlowAttackerAllyId > 0
                    ? await APIHelper.ESIAPI.GetAllianceData(reason, finalBlowAttackerAllyId)
                    : null;
                rVictimShipType = await APIHelper.ESIAPI.GetTypeId(reason, victimShipID);
                rAttackerShipType = await APIHelper.ESIAPI.GetTypeId(reason, attackerShipID);
                rVictimCharacter = await APIHelper.ESIAPI.GetCharacterData(reason, victimCharacterID);
                rAttackerCharacter = await APIHelper.ESIAPI.GetCharacterData(reason, finalBlowAttacker?.character_id);
                systemSecurityStatus = Math.Round(rSystem.security_status, 1).ToString("0.0");

                dic = new Dictionary<string, string>
                    {
                        {"{shipID}", victimShipID.ToString()},
                        {"{shipType}", rVictimShipType?.name},
                        {"{attackerShipID}", attackerShipID.ToString()},
                        {"{attackershipType}", rAttackerShipType?.name},
                        {"{iskValue}", value.ToString("n0")},
                        {"{iskFittedValue}", kill?.zkb?.fittedValue.ToString("n0") ?? "0"},
                        {"{systemName}", sysName},
                        {"{systemID}", rSystem.system_id.ToString()},
                        {"{regionName}", rRegion?.name},
                        {"{regionID}", rRegion != null ? rSystem?.DB_RegionId.ToString() : null},
                        {"{systemSec}", systemSecurityStatus},
                        {"{victimName}", rVictimCharacter?.name},
                        {"{victimID}", rVictimCharacter?.character_id.ToString()},
                        {"{victimCorpName}", rVictimCorp?.name},
                        {"{victimCorpID}", rVictimCharacter?.corporation_id.ToString()},
                        {"{victimCorpTicker}", rVictimCorp?.ticker},
                        {"{victimAllyName}", rVictimAlliance?.name},
                        {"{victimAllyID}", rVictimAlliance != null ? rVictimCorp?.alliance_id.ToString() : null},
                        {"{victimAllyTicker}", rVictimAlliance == null ? null : $"<{rVictimAlliance.ticker}>"},
                        {"{victimAllyOrCorpName}", rVictimAlliance?.name ?? rVictimCorp?.name},
                        {"{victimAllyOrCorpTicker}", rVictimAlliance?.ticker ?? rVictimCorp?.ticker},
                        {"{attackerName}", rAttackerCharacter?.name},
                        {"{attackerID}", rAttackerCharacter?.character_id.ToString()},
                        {"{attackerCorpName}", rAttackerCorp?.name},
                        {"{attackerCorpID}", rAttackerCharacter?.corporation_id.ToString()},
                        {"{attackerCorpTicker}", rAttackerCorp?.ticker},
                        {"{attackerAllyName}", rAttackerAlliance?.name},
                        {"{attackerAllyID}", rAttackerAlliance != null ? rAttackerCorp?.alliance_id.ToString() : null},
                        {"{attackerAllyTicker}", rAttackerAlliance == null ? null : $"<{rAttackerAlliance.ticker}>"},
                        {"{attackerAllyOrCorpName}", rAttackerAlliance?.name ?? rAttackerCorp?.name},
                        {"{attackerAllyOrCorpTicker}", rAttackerAlliance?.ticker ?? rAttackerCorp?.ticker},
                        {"{attackersCount}", attackers?.Length.ToString()},
                        {"{kmId}", killmailID.ToString()},
                        {"{timestamp}", killTime},
                        {"{isNpcKill}", isNPCKill.ToString() ?? "false"},
                        {"{isSoloKill}", kill?.zkb?.solo.ToString() ?? "false"},
                        {"{isAwoxKill}", kill?.zkb?.awox.ToString() ?? "false"},
                    };


                return true;
            }
            catch (Exception ex)
            {
                killmailID = 0;
                await LogHelper.LogEx("refresh ex", ex, LogCat.RadiusKill);
                return false;
            }
        }

        public async Task<bool> Refresh(string reason, JsonZKill.Killmail kill)
        {
            if (killmailID > 0) return true;
            try
            {
                killmailID = kill.killmail_id;
                killTime = kill.killmail_time.ToString(SettingsManager.Settings.Config.ShortTimeFormat);
                victimShipID = kill.victim.ship_type_id;
                value = kill.zkb.totalValue;
                victimCharacterID = kill.victim.character_id;
                victimCorpID = kill.victim.corporation_id;
                victimAllianceID = kill.victim.alliance_id;
                attackers = kill.attackers;
                finalBlowAttacker = attackers.FirstOrDefault(a => a.final_blow);
                finalBlowAttackerCharacterId = finalBlowAttacker.character_id;
                attackerShipID = finalBlowAttacker?.ship_type_id ?? 0;
                finalBlowAttackerCorpId = finalBlowAttacker?.corporation_id ?? 0;
                finalBlowAttackerAllyId = finalBlowAttacker?.alliance_id ?? 0;
                isNPCKill = kill.zkb.npc;
                systemId = kill.solar_system_id;
                rSystem = await APIHelper.ESIAPI.GetSystemData(reason, systemId);
                if (rSystem == null)
                {
                    //ESI fail - check back later
                    return false;
                }

                rVictimCorp = await APIHelper.ESIAPI.GetCorporationData(reason, victimCorpID);
                rAttackerCorp = finalBlowAttackerCorpId > 0
                    ? await APIHelper.ESIAPI.GetCorporationData(reason, finalBlowAttackerCorpId)
                    : null;

                rVictimAlliance = victimAllianceID != 0 ? await APIHelper.ESIAPI.GetAllianceData(reason, victimAllianceID) : null;
                rAttackerAlliance = finalBlowAttackerAllyId > 0
                    ? await APIHelper.ESIAPI.GetAllianceData(reason, finalBlowAttackerAllyId)
                    : null;
                sysName = rSystem.name == rSystem.system_id.ToString() ? "Abyss" : rSystem.name;
                var rConst = rSystem != null ? await APIHelper.ESIAPI.GetConstellationData(reason, rSystem.constellation_id) : null;
                var rRegion = rConst != null ? await APIHelper.ESIAPI.GetRegionData(reason, rConst.region_id) : null;
                rVictimShipType = await APIHelper.ESIAPI.GetTypeId(reason, victimShipID);
                rAttackerShipType = await APIHelper.ESIAPI.GetTypeId(reason, attackerShipID);
                rVictimCharacter = await APIHelper.ESIAPI.GetCharacterData(reason, victimCharacterID);
                rAttackerCharacter = await APIHelper.ESIAPI.GetCharacterData(reason, finalBlowAttacker?.character_id);
                systemSecurityStatus = Math.Round(rSystem.security_status, 1).ToString("0.0");

                dic = new Dictionary<string, string>
                    {
                        {"{shipID}", victimShipID.ToString()},
                        {"{shipType}", rVictimShipType?.name},
                        {"{attackerShipID}", attackerShipID.ToString()},
                        {"{attackerShipType}", rAttackerShipType?.name},
                        {"{iskValue}", value.ToString("n0")},
                        {"{iskFittedValue}", kill?.zkb?.fittedValue.ToString("n0") ?? "0"},
                        {"{systemName}", sysName},
                        {"{systemID}", rSystem.system_id.ToString()},
                        {"{constName}", rConst?.name},
                        {"{constID}", rConst?.constellation_id.ToString()},
                        {"{regionName}", rRegion?.name},
                        {"{regionID}", rRegion != null ? rConst?.region_id.ToString() : null},
                        {"{systemSec}", systemSecurityStatus},
                        {"{victimName}", rVictimCharacter?.name},
                        {"{victimID}", rVictimCharacter?.character_id.ToString()},
                        {"{victimCorpName}", rVictimCorp?.name},
                        {"{victimCorpID}", rVictimCharacter?.corporation_id.ToString()},
                        {"{victimCorpTicker}", rVictimCorp?.ticker},
                        {"{victimAllyName}", rVictimAlliance?.name},
                        {"{victimAllyID}", rVictimAlliance != null ? rVictimCorp?.alliance_id.ToString() : null},
                        {"{victimAllyTicker}", rVictimAlliance == null ? null : $"<{rVictimAlliance.ticker}>"},
                        {"{victimAllyOrCorpName}", rVictimAlliance?.name ?? rVictimCorp?.name},
                        {"{victimAllyOrCorpTicker}", rVictimAlliance?.ticker ?? rVictimCorp?.ticker},
                        {"{attackerName}", rAttackerCharacter?.name},
                        {"{attackerID}", rAttackerCharacter?.character_id.ToString()},
                        {"{attackerCorpName}", rAttackerCorp?.name},
                        {"{attackerCorpID}", rAttackerCharacter?.corporation_id.ToString()},
                        {"{attackerCorpTicker}", rAttackerCorp?.ticker},
                        {"{attackerAllyName}", rAttackerAlliance?.name},
                        {"{attackerAllyID}", rAttackerAlliance != null ? rAttackerCorp?.alliance_id.ToString() : null},
                        {"{attackerAllyTicker}", rAttackerAlliance == null ? null : $"<{rAttackerAlliance.ticker}>"},
                        {"{attackerAllyOrCorpName}", rAttackerAlliance?.name ?? rAttackerCorp?.name},
                        {"{attackerAllyOrCorpTicker}", rAttackerAlliance?.ticker ?? rAttackerCorp?.ticker},
                        {"{attackersCount}", attackers.Length.ToString()},
                        {"{kmId}", killmailID.ToString()},
                        {"{timestamp}", killTime},
                        {"{isLoss}", "false"},
                        {"{isNpcKill}", isNPCKill.ToString() ?? "false"},
                        {"{isSoloKill}", kill?.zkb?.solo.ToString() ?? "false"},
                        {"{isAwoxKill}", kill?.zkb?.awox.ToString() ?? "false"},
                    };

                return true;
            }
            catch (Exception ex)
            {
                killmailID = 0;
                await LogHelper.LogEx("refresh ex", ex, LogCat.KillFeed);
                return false;
            }
        }
    }
}
