using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Classes.Enums;
using ThunderED.Json;
using ThunderED.Json.Internal;
using ThunderED.Providers;

namespace ThunderED.Helpers
{
    public static partial class SQLHelper
    {
        public static IDatabasePovider Provider { get; set; }

        #region Query

        private static async Task<T> Query<T>(string table, string field, string whereField, object whereData)
        {
            return await Provider?.Query<T>(table, field, new Dictionary<string, object> {{whereField, whereData}});
        }

        private static async Task<T> Query<T>(string table, string field, Dictionary<string, object> where)
        {
            return await Provider?.Query<T>(table, field, where);
        }
    
        private static async Task<List<T>> QueryList<T>(string table, string field, Dictionary<string, object> where)
        {
            return (await SelectData(table, new[] {field}, where))?.Select(a=> a.FirstOrDefault()).Cast<T>().ToList();
        }

        private static async Task<List<object[]>> SelectData(string table, string[] fields, Dictionary<string, object> where = null)
        {
            return await Provider?.SelectData(table, fields, where);
        }

        private static async Task<List<object[]>> SelectData(string query)
        {
            return await Provider?.SelectData(query);
        }

        private static async Task<bool> IsEntryExists(string table, Dictionary<string, object> where)
        {
            return await Provider?.IsEntryExists(table, where);
        }

        #endregion
        
        #region Update

        private static async Task Update(string table, string setField, object setData, string whereField, object whereData)
        {
            await Provider?.Update(table, setField, setData, new Dictionary<string, object> {{whereField, whereData}});
        }

        private static async Task Update(string table, string setField, object setData, Dictionary<string, object> where)
        {
            await Provider?.Update(table, setField, setData, where);

        }

        private static async Task InsertOrUpdate(string table, Dictionary<string, object> values)
        {
            await Provider?.InsertOrUpdate(table, values);
        }

        private static async Task Insert(string table, Dictionary<string, object> values)
        {
            await Provider?.Insert(table, values);
        }
        #endregion

        #region Delete
        private static async Task<bool> Delete(string table, string whereField = null, object whereData = null)
        {
            if (whereField == null && whereData == null)
                return await Provider?.Delete(table, null);
            return await Provider?.Delete(table, new Dictionary<string, object> {{whereField, whereData}});
        }

        private static async Task Delete(string table, Dictionary<string, object> where)
        {
            await Provider?.Delete(table, where);
        }
        
        public static async Task DeleteWhereIn(string table, string field, List<long> list, bool not)
        {
            await Provider?.DeleteWhereIn(table, field, list, not);

        }
        #endregion


        #region AuthUsers

       /* public static async Task RenameAuthGroup(string @from, string to)
        {
            await Update("auth_users", "groupName", to, "groupName", from);
        }

        public static async Task<List<long>> DeleteAuthDataByDiscordId(ulong discordId)
        {
            var user = await GetAuthUserByDiscordId(discordId);
            await Delete("auth_users", "discordID", discordId);
            if (user != null && user.CharacterId > 0)
            {
                var data = await SelectData("auth_users", new[] {"characterID"}, new Dictionary<string, object> {{"main_character_id", user.CharacterId}});
                return data?.Select(a=> Convert.ToInt64(a[0])).ToList();
            }

            return null;
        }

        
        public static async Task UpdateMainCharacter(long altId, long mainId)
        {
            await Update("auth_users", "main_character_id", mainId, "characterID", altId);
        }

        public static async Task DeleteAuthDataByCharId(long characterID, bool deleteAlts = false)
        {
            await Delete("auth_users", "characterID", characterID);
            if(deleteAlts)
                await Delete("auth_users", "main_character_id", characterID);
        }

        private static async Task<List<AuthUserEntity>> GetAuthUsers(Dictionary<string,object> where)
        {
            var res = await SelectData("auth_users", new[] {"*"}, where);

            return res?.Select(ParseAuthUser).ToList();
        }

        public static async Task<List<AuthUserEntity>> GetAuthUsers()
        {
            return await GetAuthUsers(null);
        }

        public static async Task<List<AuthUserEntity>> GetAuthUsers(int authState)
        {
            return await GetAuthUsers(new Dictionary<string, object>
            {
                {"authState", authState}
            });
        }

        public static async Task<List<AuthUserEntity>> GetOutdatedAwaitingAuthUsers()
        {
            var res = await SelectData("auth_users", new[] {"*"});

            return res?.Select(ParseAuthUser).Where(a=> a.IsPending || string.IsNullOrEmpty(a.GroupName)).ToList();
        }

        public static async Task<List<AuthUserEntity>> GetAuthUsersWithPerms(Dictionary<string,object> where = null)
        {
            var res = await SelectData("auth_users", new[] {"*"}, where);

            return res?.Select(ParseAuthUser).Where(a=> !string.IsNullOrEmpty(a.Data.Permissions)).ToList();
        }

        public static async Task<List<AuthUserEntity>> GetAuthUsersWithPerms(int state)
        {
            var res = await SelectData("auth_users", new[] {"*"}, new Dictionary<string, object>{{"authState", state}});

            return res?.Select(ParseAuthUser).Where(a=> !string.IsNullOrEmpty(a.Data.Permissions)).ToList();
        }



        public static async Task<List<ulong>> GetAuthUserIdsToCheck(int count = 100)
        {
            if (Provider == null) return new List<ulong>();

            var list = await DbHelper.GetUserDiscordIdsForAuthCheck(count);

            return list; 
            //(await Provider.SelectData("auth_users", new[] { "discordID" })).Select(a => Convert.ToUInt64(a[0])).ToList();
        }

        public static async Task SetAuthUserLastCheck(ulong id, DateTime date)
        {
            await Update("auth_users", "last_check", date, "discordID", id);
        }

        public static async Task ResetAuthUsersLastCheck()
        {
            await Provider?.Update("auth_users", "last_check", null);
        }

        private static AuthUserEntity ParseAuthUser(object[] item)
        {
            return new AuthUserEntity
            {
                Id = Convert.ToInt64(item[0]),
                CharacterId = Convert.ToInt64(item[1]),
                DiscordId = Convert.ToUInt64(item[2]),
                GroupName = (string) item[3],
                RefreshToken = (string) item[4],
                AuthState = Convert.ToInt32(item[5]),
                Data = JsonConvert.DeserializeObject<AuthUserData>((string) item[6]),
                RegCode = (string) item[7],
                CreateDate = Convert.ToDateTime(item[8]),
                DumpDate = item[9] == null ? null : (DateTime?)Convert.ToDateTime(item[9]),
                MainCharacterId = item[10] == null ? null : (long?)Convert.ToInt64(item[10]),
                LastCheck = item[11] == null? null : (DateTime?)Convert.ToDateTime(item[11]),
                Ip = (string)item[12],
            };
        }

        public static async Task SaveAuthUser(AuthUserEntity user, bool insertOnly = false)
        {
            var dic = new Dictionary<string, object>();
            if(user.Id > 0)
                dic.Add("Id", user.Id);
            dic.Add("characterID", user.CharacterId);
            dic.Add("discordID", user.DiscordId);
            dic.Add("groupName", user.GroupName);
            dic.Add("refreshToken", user.RefreshToken);
            dic.Add("authState", user.AuthState);
            dic.Add("reg_code", user.RegCode);
            dic.Add("reg_date", user.CreateDate);
            dic.Add("dump_date", user.DumpDate);
            dic.Add("data", JsonConvert.SerializeObject(user.Data));
            dic.Add("main_character_id", user.MainCharacterId);
            dic.Add("last_check", user.LastCheck);
            dic.Add("ip", user.Ip);
            if (insertOnly)
                await Insert("auth_users", dic);
            else await InsertOrUpdate("auth_users", dic);
        }


        public static async Task<AuthUserEntity> GetAuthUserByDiscordId(ulong discordId, bool order = false)
        {
            var res = await SelectData("auth_users", new[] {"*"}, new Dictionary<string, object> {{"discordID", discordId}});

            return res?.Select(ParseAuthUser).FirstOrDefault();
        }

        public static async Task<AuthUserEntity> GetAuthUserByCharacterId(long id, bool order = false)
        {
            var res = await SelectData("auth_users", new[] {"*"}, new Dictionary<string, object> {{"characterID", id}});

            return res?.Select(ParseAuthUser).FirstOrDefault();
        }
        
        public static async Task<bool> IsAuthUsersGroupNameInDB(string @from)
        {
            var result = await SelectData("auth_users", new[] {"*"}, new Dictionary<string, object> {{"groupName", from}});
            return result != null && result.Count > 0;
        }


        public static async Task<AuthUserEntity> GetAuthUserByRegCode(string code)
        {
            var characterId = await Query<long>("auth_users", "characterID", "reg_code", code);
            return await GetAuthUserByCharacterId(characterId);
        }

        public static async Task<ulong> GetAuthUserDiscordId(long characterId)
        {
            return await Query<ulong>("auth_users", "discordID", "characterID", characterId);
        }

        public static async Task<List<AuthUserEntity>> GetAuthUserAlts(long mainCharacterId)
        {
            var res = await SelectData("auth_users", new[] {"*"}, new Dictionary<string, object>{{"authState", (int)UserStatusEnum.Authed}, {"main_character_id", mainCharacterId}});

            return res?.Select(ParseAuthUser).ToList();
        }

        */
        #endregion

        #region System

        public static async Task<bool> ClearTable(string table)
        {
            return await Delete(table);
        }

        private static bool EnsureDBExists()
        {
            return Provider?.EnsureDBExists().GetAwaiter().GetResult() ?? false;
        }

        private static async Task<bool> RunScript(string file)
        {
            return await Provider?.RunScript(file);
        }

        public static async Task RunCommand(string query2, bool silent = false)
        {
            await Provider?.RunCommand(query2, silent);
        }

        public static async Task<string> LoadProvider()
        {
            try
            {
                var prov = DbSettingsManager.Settings.Database.DatabaseProvider;
                switch (prov)
                {
                    case "sqlite":
                        Provider = new SqliteDatabaseProvider();
                        break;
                    case "mysql":
                        Provider = new MysqlDatabaseProvider();
                        break;
                    default:
                        await LogHelper.LogInfo("Using default SQLite provider!");
                        Provider = new SqliteDatabaseProvider();
                        break;
                    //  return $"[CRITICAL] Unknown database provider {prov}!";

                }

                if (!EnsureDBExists())
                {
                    return "[CRITICAL] Failed to check DB integrity or create new instance!";
                }

                //upgrade database
                if (!await Upgrade())
                {
                    return "[CRITICAL] Failed to upgrade DB to latest version!";
                }

                return null;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(nameof(LoadProvider), ex);
                return "Unexpected error while loading DB provider!";
            }
        }
        #endregion

        #region Cache

        
        public static async Task DeleteCache(object type, object value)
        {
            await Delete("cache", new Dictionary<string, object>
            {
                {"type", type},
                {"id", value}
            });
        }

        public static async Task DeleteCache(string type = null)
        {
            if (string.IsNullOrEmpty(type))
                await Delete("cache");
            else await Delete("cache", "type", type);
        }

        public static async Task<string> GetCacheDataNextNotificationCheck()
        {
            return await Query<string>("cache_data", "data", "name", "nextNotificationCheck");
        }
        public static async Task SetCacheDataNextNotificationCheck(int interval)
        {
            await Update("cache_data", "data", DateTime.Now.AddMinutes(interval).ToString(CultureInfo.InvariantCulture), "name", "nextNotificationCheck");
        }

        public static async Task SetCacheLastAccess(object id, string type)
        {
            await Update("cache", "lastAccess", DateTime.Now, new Dictionary<string, object>
            {
                {"id", id},
                {"type", type}
            });
        }

        public static async Task<string> GetCacheDataFleetUpLastChecked()
        {
            return await Query<string>("cache_data", "data", "name", "fleetUpLastChecked");
        }
        public static async Task SetCacheDataFleetUpLastChecked(DateTime? dt)
        {
            await Update("cache_data", "data", dt.Value.ToString(CultureInfo.InvariantCulture), "name", "fleetUpLastChecked");
        }

        public static async Task<string> GetCacheDataFleetUpLastPosted()
        {
            return await Query<string>("cache_data", "data", "name", "fleetUpLastPostedOperation");
        }

        public static async Task SetCacheDataFleetUpLastPosted(long id)
        {
            await SQLHelper.Update("cache_data", "data", id.ToString(), "name", "fleetUpLastPostedOperation");
        }

        public static async Task<T> SelectCache<T>(object whereValue, int maxDays)
            where T: class
        {
            return await Provider?.SelectCache<T>(whereValue, maxDays);
        }

        public static async Task UpdateCache<T>(T data, object id, int days = 1) 
            where T : class
        {
            await Provider?.UpdateCache(data, id, days);
        }

        public static async Task PurgeCache()
        {
            await Provider?.PurgeCache();
        }

        #endregion

        #region Timers
        public static async Task DeleteTimer(long id)
        {
            await Delete("timers", "id", id);
        }

        public static async Task SetTimerAnnounce(long id, int value)
        {
            await Update("timers", "announce", value, "id", id);
        }

        public static async Task<string> GetTimersAuthTime(long characterId)
        {
            return await Query<string>("timers_auth", "time", "id", characterId);
        }

        public static async Task<List<TimerItem>> SelectTimers()
        {
            var list = (await SelectData("timers", new[] {"*"})).Select(item => new TimerItem
            {
                Id = Convert.ToInt64(item[0]),
                timerType = Convert.ToInt32(item[1]),
                timerStage = Convert.ToInt32(item[2]),
                timerLocation = (string)item[3],
                timerOwner = (string)item[4],
                timerET = (string)item[5],
                timerNotes = (string)item[6],
                timerChar = (string)item[7],
                announce = Convert.ToInt32(item[8])
            }).ToList();
            list.ForEach(a=> a.Date = a.GetDateTime());
            return list;
        }

        public static async Task UpdateTimersAuth(long charId)
        {
            await InsertOrUpdate("timers_auth", new Dictionary<string, object> {{"id", charId}, {"time", DateTime.Now}});
        }

        public static async Task UpdateTimer(TimerItem entry)
        {
            await InsertOrUpdate("timers", entry.GetDictionary());
        }

        #endregion

        #region Notifications
        
        public static async Task<long> GetLastNotification(string group, string filter)
        {
            return await Query<long>("notifications_list", "id", new Dictionary<string, object>
            {
                {"groupName", group},
                {"filterName", filter}
            });
        }

        public static async Task SetLastNotification(string group, string filter, long id, bool insert = false)
        {
            if (insert)
            {
                await InsertOrUpdate("notifications_list", new Dictionary<string, object>
                {
                    {"id", id},
                    {"groupName", group},
                    {"filterName", filter},
                });
            }else 
                await Update("notifications_list", "id", id, new Dictionary<string, object>
                {
                    {"groupName", group},
                    {"filterName", filter},
                });
        }

        public static async Task CleanupNotificationsList()
        {
            await Provider?.CleanupNotificationsList();
        }

        #endregion

        #region StaticData

        public static async Task<List<JsonClasses.SystemName>> GetSystemsByConstellation(long constellationId)
        {
            return (await SelectData("map_solar_systems", new[] {"solarSystemID", "constellationID", "regionID", "solarSystemName", "security"}, new Dictionary<string, object>
            {
                {"constellationID", constellationId}
            })).Select(item => new JsonClasses.SystemName
            {
                system_id = Convert.ToInt64(item[0]),
                constellation_id = Convert.ToInt64(item[1]),
                DB_RegionId = Convert.ToInt64(item[2]),
                name = Convert.ToString(item[3]),
                security_status = (float)Convert.ToDouble(item[4]),
            }).ToList();
        }

        public static async Task<List<JsonClasses.SystemName>> GetSystemsByRegion(long regionId)
        {
            return (await SelectData("map_solar_systems", new[] {"solarSystemID", "constellationID", "regionID", "solarSystemName", "security"}, new Dictionary<string, object>
            {
                {"regionID", regionId}
            })).Select(item => new JsonClasses.SystemName
            {
                system_id = Convert.ToInt64(item[0]),
                constellation_id = Convert.ToInt64(item[1]),
                DB_RegionId = Convert.ToInt64(item[2]),
                name = Convert.ToString(item[3]),
                security_status = (float)Convert.ToDouble(item[4]),
            }).ToList();
        }

        public static async Task<JsonClasses.SystemName> GetSystemById(long id)
        {
            return (await SelectData("map_solar_systems", new[] {"solarSystemID", "constellationID", "regionID", "solarSystemName", "security"}, new Dictionary<string, object>
            {
                {"solarSystemID", id}
            })).Select(item => new JsonClasses.SystemName
            {
                system_id = Convert.ToInt64(item[0]),
                constellation_id = Convert.ToInt64(item[1]),
                DB_RegionId = Convert.ToInt64(item[2]),
                name = Convert.ToString(item[3]),
                security_status = (float)Convert.ToDouble(item[4]),
            }).FirstOrDefault();
        }

        public static async Task<JsonClasses.RegionData> GetRegionById(long id)
        {
            return (await SelectData("map_regions", new[] {"regionID", "regionName"}, new Dictionary<string, object>
            {
                {"regionID", id}
            })).Select(item => new JsonClasses.RegionData
            {
                DB_id = Convert.ToInt64(item[0]),
                name = Convert.ToString(item[1]),
            }).FirstOrDefault();
        }

        public static async Task<JsonClasses.ConstellationData> GetConstellationById(long id)
        {
            return (await SelectData("map_constellations", new[] {"regionID", "constellationID","constellationName"}, new Dictionary<string, object>
            {
                {"constellationID", id}
            })).Select(item => new JsonClasses.ConstellationData
            {
                region_id = Convert.ToInt64(item[0]),
                constellation_id = Convert.ToInt64(item[1]),
                name = Convert.ToString(item[2]),
            }).FirstOrDefault();
        }

        
        public static async Task<JsonClasses.Type_id> GetTypeId(long id)
        {
            return (await SelectData("inv_types", new[] {"typeID", "groupID","typeName", "description", "mass", "volume"}, new Dictionary<string, object>
            {
                {"typeID", id}
            })).Select(item => new JsonClasses.Type_id
            {
                type_id = Convert.ToInt64(item[0]),
                group_id = Convert.ToInt64(item[1]),
                name = Convert.ToString(item[2]),
                description = Convert.ToString(item[3]),
                mass = (float)Convert.ToDouble(item[4]),
                volume = (float)Convert.ToDouble(item[5])
            }).FirstOrDefault();
        }


        public static async Task<JsonClasses.invGroup> GetInvGroup(long id)
        {
            return (await SelectData("inv_groups", new[] {"groupID", "categoryID","groupName"}, new Dictionary<string, object>
            {
                {"groupID", id}
            })).Select(item => new JsonClasses.invGroup
            {
                groupId = Convert.ToInt64(item[0]),
                categoryId = Convert.ToInt64(item[1]),
                groupName = Convert.ToString(item[2]),
            }).FirstOrDefault();
        }

        #endregion

        #region NullCampaigns

        public static async Task<List<JsonClasses.NullCampaignItem>> GetNullCampaigns(string group)
        {
            return (await SelectData("null_campaigns", new[] {"data", "lastAnnounce" }, new Dictionary<string, object>{{"groupKey", group}}))
                .Select(item =>
                {                    
                    var i = new JsonClasses.NullCampaignItem().FromJson((string) item[0]);
                    i.LastAnnounce = Convert.ToInt64(item[1]);
                    return i;
                }).ToList();
        }

        public static async Task UpdateNullCampaignAnnounce(string group, long campaignId, int announce)
        {
            await Update("null_campaigns", "lastAnnounce", announce, new Dictionary<string, object>
            {
                {"groupKey", group},
                {"campaignId", campaignId}
            });
        }

        public static async Task UpdateNullCampaign(string groupName, long id, DateTimeOffset startTime, string data)
        {
            await InsertOrUpdate("null_campaigns", new Dictionary<string, object>
            {
                {"groupKey",groupName},
                {"campaignId",id},
                {"time",startTime},
                {"data", data}
            });
        }

        public static async Task DeleteNullCampaign(string groupName, long id)
        {
            await Delete("null_campaigns", new Dictionary<string, object> {{"groupKey", groupName}, {"campaignId", id}});
        }

        public static async Task<List<long>> GetNullsecCampaignIdList(string groupName)
        {
            return (await SelectData("null_campaigns", new [] {"campaignId"}, new Dictionary<string, object> {{"groupKey", groupName}})).Select(a=> Convert.ToInt64(a[0])).ToList();
        }

        public static async Task<bool> IsNullsecCampaignExists(string groupName, long id)
        {
            return await IsEntryExists("null_campaigns", new Dictionary<string, object> {{"groupKey", groupName}, {"campaignId", id}});
        }
        #endregion

        #region WebEditor

        public static async Task<WebEditorAuthEntry> GetWebEditorAuthEntry(long characterId)
        {    
            return (await SelectData("web_editor_auth", new [] {"*"}, new Dictionary<string, object> {{"id", characterId}}))?.Select(a => new WebEditorAuthEntry
                {
                    Id = Convert.ToInt64(a[0]),
                    Code = (string)a[1],
                    Time = Convert.ToDateTime(a[2])
                }
            ).FirstOrDefault();
        }

        public static async Task<WebEditorAuthEntry> GetWebEditorAuthEntry(string code)
        {    
            return (await SelectData("web_editor_auth", new [] {"*"}, new Dictionary<string, object> {{"code", code}}))?.Select(a => new WebEditorAuthEntry
                {
                    Id = Convert.ToInt64(a[0]),
                    Code = (string)a[1],
                    Time = Convert.ToDateTime(a[2])
                }
            ).FirstOrDefault();
        }

        public static async Task SaveWebEditorAuthEntry(WebEditorAuthEntry entry)
        {
            var dic = new Dictionary<string, object> {{"id", entry.Id}, {"code", entry.Code}, {"time", entry.Time}};
            await InsertOrUpdate("web_editor_auth", dic);
        }

        public static async Task DeleteWebEditorEntry(long weId)
        {
            await Delete("web_editor_auth", "id", weId);
        }

        #endregion

        #region Contracts
        public static async Task<List<JsonClasses.Contract>> LoadContracts(long characterID, bool isCorp)
        {
            var data = (string)(await SelectData("contracts", new [] {isCorp ? "corpdata" : "data"}, new Dictionary<string, object> {{"characterID", characterID}}))?.FirstOrDefault()?.FirstOrDefault();
            return string.IsNullOrEmpty(data) ? null : JsonConvert.DeserializeObject<List<JsonClasses.Contract>>(data).OrderByDescending(a=> a.contract_id).ToList();
        }

        public static async Task SaveContracts(long characterID, List<JsonClasses.Contract> data, bool isCorp)
        {
            var result = JsonConvert.SerializeObject(data);

            var d = (string)(await SelectData("contracts", new [] {isCorp ? "data" : "corpdata"}, new Dictionary<string, object> {{"characterID", characterID}}))?.FirstOrDefault()?.FirstOrDefault();

            await InsertOrUpdate("contracts", new Dictionary<string, object>
            {
                {"characterID", characterID},
                {isCorp ? "corpdata" : "data", string.IsNullOrEmpty(result) ? null : result},
                {isCorp ? "data" : "corpdata", string.IsNullOrEmpty(d) ? null : d}
            });
        }
        #endregion
       
        #region AuthStandings
        public static async Task SaveAuthStands(AuthStandsEntity data)
        {
            await InsertOrUpdate("stand_auth", new Dictionary<string, object>
            {
                {"characterID", data.CharacterID},
                {"token", data.Token},
                {"personalStands", JsonConvert.SerializeObject(data.PersonalStands)},
                {"corpStands", JsonConvert.SerializeObject(data.CorpStands)},
                {"allianceStands", JsonConvert.SerializeObject(data.AllianceStands)},
            });
        }

        public static async Task<AuthStandsEntity> LoadAuthStands(long id)
        {
            var data = (await SelectData("stand_auth", new [] {"token", "personalStands", "corpStands", "allianceStands"}, new Dictionary<string, object> {{"characterID", id}}))?.FirstOrDefault();
            if (data == null)
                return null;
            return new AuthStandsEntity
            {
                CharacterID = id,
                Token = (string)data[0],
                PersonalStands = JsonConvert.DeserializeObject<List<JsonClasses.Contact>>((string)data[1]),
                CorpStands = JsonConvert.DeserializeObject<List<JsonClasses.Contact>>((string)data[2]),
                AllianceStands = JsonConvert.DeserializeObject<List<JsonClasses.Contact>>((string)data[3]),
            };
        }

        public static async Task DeleteAuthStands(long id)
        {
            await Delete("stand_auth", "characterID", id);
        }
        #endregion

        #region Incursions
        public static async Task<bool> IsIncurionExists(long id)
        {
            return await Query<long>("incursions", "constId", "constId", id) > 0;
        }

        public static async Task AddIncursion(long id)
        {
            await InsertOrUpdate("incursions", new Dictionary<string, object>
            {
                { "constId", id }
            });
        }
        #endregion

        #region Mail
        public static async Task<long> GetLastMailId(long charId)
        {
            return await Query<long>("mail", "mailId", "id", charId);
        }

        public static async Task UpdateMail(long charId, long mailId)
        {
            await InsertOrUpdate("mail", new Dictionary<string, object> {{"id", charId}, {"mailId", mailId}});
        }
        #endregion

        #region HRM
        public static async Task<long> GetHRAuthCharacterId(string authCode)
        {
            return await Query<long>("hrm_auth", "id", "code", authCode);
        }

        public static async Task<string> GetHRAuthTime(long characterId)
        {
            return await Query<string>("hrm_auth", "time", "id", characterId);
        }

        public static async Task SetHRAuthTime(string authCode)
        {
            await Update("hrm_auth", "time", DateTime.Now, "code", authCode);
        }

        public static async Task UpdateHrmAuth(long charId, string authCode)
        {
            await InsertOrUpdate("hrm_auth", new Dictionary<string, object> {{"id", charId}, {"time", DateTime.Now}, {"code", authCode}});
        }
        #endregion

        #region Fleetup
        public static async Task DeleteFleetupOp(long id)
        {
            await Delete("fleetup", "id", id);
        }

        public static async Task<long> GetFleetupAnnounce(long opId)
        {
            return await Query<long>("fleetup", "announce", "id", opId);
        }

        public static async Task AddFleetupOp(long id, long value)
        {
            await InsertOrUpdate("fleetup", new Dictionary<string, object>
            {
                { "id", id},
                { "announce", value}
            });
        }
        #endregion

        #region Sov Index Tracker
        public static async Task<List<JsonClasses.SovStructureData>> GetSovIndexTrackerData(string name)
        {
            var res = await SelectData("sovIndexTracker", new[] {"data"}, new Dictionary<string, object> {{"groupName", name}});
            if(res == null || !res.Any()) return  new List<JsonClasses.SovStructureData>();
            return JsonConvert.DeserializeObject<List<JsonClasses.SovStructureData>>((string)res[0][0]);
        }

        public static async Task SaveSovIndexTrackerData(string name, List<JsonClasses.SovStructureData> data)
        {
            await InsertOrUpdate("sovIndexTracker", new Dictionary<string, object>
            {
                {"groupName", name},
                {"data", JsonConvert.SerializeObject(data)}
            });
        }

        #endregion

        #region Industry Jobs
        public static async Task<List<JsonClasses.IndustryJob>> LoadIndustryJobs(long characterID, bool isCorp)
        {
            var data = (string)(await SelectData("industry_jobs", new [] {isCorp ? "corporate_jobs" : "personal_jobs"}, new Dictionary<string, object> {{"character_id", characterID}}))?.FirstOrDefault()?.FirstOrDefault();
            return string.IsNullOrEmpty(data) ? null : JsonConvert.DeserializeObject<List<JsonClasses.IndustryJob>>(data).OrderByDescending(a=> a.job_id).ToList();
        }

        public static async Task SaveIndustryJobs(long characterID, List<JsonClasses.IndustryJob> data, bool isCorp)
        {
            var result = JsonConvert.SerializeObject(data);

            var d = (string)(await SelectData("industry_jobs", new [] {isCorp ? "personal_jobs" : "corporate_jobs"}, new Dictionary<string, object> {{"character_id", characterID}}))?.FirstOrDefault()?.FirstOrDefault();

            await InsertOrUpdate("industry_jobs", new Dictionary<string, object>
            {
                {"character_id", characterID},
                {isCorp ? "corporate_jobs" : "personal_jobs", string.IsNullOrEmpty(result) ? null : result},
                {isCorp ? "personal_jobs" : "corporate_jobs", string.IsNullOrEmpty(d) ? null : d}
            });
        }
        #endregion

    }
}
