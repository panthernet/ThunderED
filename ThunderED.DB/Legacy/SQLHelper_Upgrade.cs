using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using ThunderED.Classes;

namespace ThunderED.Helpers
{
    public static partial class SQLHelper
    {
        //"1.0.0","1.0.1","1.0.7", "1.0.8", "1.1.3", "1.1.4", "1.1.5", "1.1.6", "1.1.8", "1.2.2","1.2.6", "1.2.7", "1.2.8", "1.2.10", "1.2.14", "1.2.15", "1.2.16","1.2.19",
        private static readonly string[] MajorVersionUpdates = new[]
        {
            "1.3.1", "1.3.2", "1.3.4", "1.3.10", "1.3.16", "1.4.2", "1.4.5", "1.5.4", "2.0.1", "2.0.2", "2.0.3"
        };

        public static async Task<bool> Upgrade()
        {
            var version = await Query<string>("cache_data", "data", "name", "version") ?? await Query<string>("cacheData", "data", "name", "version");
            var isNew = string.IsNullOrEmpty(version) || SettingsManager.IsNew;

            var vDbVersion = isNew ? new Version(Program.VERSION) : new Version(version);

            try
            {
                var firstUpdate = new Version(MajorVersionUpdates[0]);
                if (vDbVersion < firstUpdate)
                {
                    await LogHelper.LogError("Your database version is below the required minimum for an upgrade. You have to do clean install without the ability to migrate your data. Consult GitHub WIKI or reach @panthernet#1659 on Discord group for assistance.");
                    return false;
                }

                foreach (var update in MajorVersionUpdates)
                {
                    var v = new Version(update);
                    if (vDbVersion >= v) continue;

                    switch (update)
                    {
                        #region OLD
                       /* case "1.0.1":
                            await RunCommand("DELETE FROM cacheData where name='version'");
                            await RunCommand("CREATE UNIQUE INDEX cacheData_name_uindex ON cacheData (name)");
                            await RunCommand("CREATE TABLE `killFeedCache` ( `type` text NOT NULL, `id` text NOT NULL, `lastId` TEXT)");
                            await RunCommand("CREATE UNIQUE INDEX killFeedCache_type_id_uindex ON killFeedCache (type, id)");
                            await RunCommand("delete from cache");
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.0.7":
                            await RunCommand("CREATE TABLE `timersAuth` ( `id` text UNIQUE PRIMARY KEY NOT NULL, `time` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP);");
                            await RunCommand(
                                "CREATE TABLE `timers` ( `id` INTEGER PRIMARY KEY NOT NULL, `timerType` int NOT NULL, `timerStage` int NOT NULL,`timerLocation` text NOT NULL, `timerOwner` text NOT NULL, `timerET` timestamp NOT NULL,`timerNotes` text, `timerChar` text NOT NULL, `announce` int NOT NULL DEFAULT 0);");
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.0.8":
                            await RunCommand("ALTER TABLE refreshTokens ADD mail TEXT NULL;");
                            await RunCommand("CREATE TABLE `mail` ( `id` text UNIQUE PRIMARY KEY NOT NULL, `mailId` int DEFAULT 0);");
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.1.3":
                            await RunCommand("CREATE TABLE `fleetup` ( `id` text UNIQUE PRIMARY KEY NOT NULL, `announce` int NOT NULL DEFAULT 0);");
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.1.4":
                            await RunCommand("DROP TABLE notificationsList;");
                            await RunCommand("DROP TABLE notifications;");
                            await RunCommand("CREATE TABLE `notificationsList` ( groupName TEXT NOT NULL, filterName TEXT NOT NULL,`id` int NOT NULL, `time` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP);");                            
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.1.5":
                            await RunCommand("CREATE TABLE `incursions` ( `constId` int UNIQUE PRIMARY KEY NOT NULL, `time` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP);");
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.1.6": 
                            await RunCommand("CREATE TABLE `nullCampaigns` ( `groupKey` text NOT NULL, `campaignId` INTEGER NOT NULL, `time` timestamp NOT NULL, `data` TEXT NOT NULL, `lastAnnounce` INTEGER NOT NULL DEFAULT 0);");
                            await RunCommand("CREATE INDEX nullCampaigns_groupKey_uindex ON nullCampaigns (groupKey);");
                            await RunCommand("CREATE UNIQUE INDEX nullCampaigns_groupKey_campaignId_uindex ON nullCampaigns (groupKey, campaignId);");

                            //https://www.fuzzwork.co.uk/dump/latest/
                            if(await RunScript(Path.Combine(SettingsManager.RootDirectory, "Content", "SQL", "1.1.6.sql")))
                                await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            else await LogHelper.LogError($"Upgrade to DB version {update} FAILED! Script not found!");
                            break;
                        case "1.1.8":
                            await RunCommand(
                                "CREATE TABLE `userTokens` ( `characterID` INT UNIQUE NOT NULL, `characterName` TEXT NOT NULL, `discordUserId` INT NOT NULL DEFAULT 0, `refreshToken` TEXT NOT NULL, `groupName` TEXT NOT NULL DEFAULT 'DEFAULT', `permissions` TEXT NOT NULL, `authState` INT NOT NULL DEFAULT 0);");
                            await LogHelper.LogWarning("Step 1 finished...");
                            await RunCommand("DELETE FROM `pendingUsers`;");
                            await RunCommand("CREATE UNIQUE INDEX ux_pendingUsers_characterID ON `pendingUsers`(`characterID`);;");
                            await LogHelper.LogWarning("Step 2 finished...");
                            await RunCommand("ALTER TABLE `pendingUsers` ADD COLUMN `discordID` INT NOT NULL DEFAULT 0;");
                            await LogHelper.LogWarning("Step 3 finished...");
                            await RunCommand("CREATE TABLE `hrmAuth` ( `id` text UNIQUE PRIMARY KEY NOT NULL, `time` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP, `code` TEXT NOT NULL);");
                            await LogHelper.LogWarning("Step 4 finished...");
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.2.2":
                            await BackupDatabase();
                            await RunCommand("CREATE TABLE invTypes(typeID INTEGER PRIMARY KEY NOT NULL,groupID INTEGER,typeName VARCHAR(100),description TEXT,mass FLOAT,volume FLOAT,capacity FLOAT,portionSize INTEGER,raceID INTEGER,basePrice DECIMAL(19,4),published BOOLEAN,marketGroupID INTEGER,iconID INTEGER,soundID INTEGER,graphicID INTEGER);");
                            await RunCommand("CREATE INDEX ix_invTypes_groupID ON invTypes (groupID);");
                            await RunCommand("CREATE TABLE mapConstellations(regionID INTEGER,constellationID INTEGER PRIMARY KEY NOT NULL,constellationName VARCHAR(100),x FLOAT,y FLOAT,z FLOAT,xMin FLOAT,xMax FLOAT,yMin FLOAT,yMax FLOAT,zMin FLOAT,zMax FLOAT,factionID INTEGER,radius FLOAT);");
                            await RunCommand("CREATE TABLE mapRegions(regionID INTEGER PRIMARY KEY NOT NULL,regionName VARCHAR(100),x FLOAT,y FLOAT,z FLOAT,xMin FLOAT,xMax FLOAT,yMin FLOAT,yMax FLOAT,zMin FLOAT,zMax FLOAT,factionID INTEGER,radius FLOAT);");
                            await RunCommand("CREATE TABLE invGroups(groupID INTEGER PRIMARY KEY NOT NULL,categoryID INTEGER,groupName VARCHAR(100),iconID INTEGER,useBasePrice BOOLEAN,anchored BOOLEAN,anchorable BOOLEAN,fittableNonSingleton BOOLEAN,published BOOLEAN);");
                            await RunCommand("CREATE INDEX ix_invGroups_categoryID ON invGroups (categoryID);");
                            await RunCommand("DELETE FROM `cache`;");

                            if (!await CopyTableDataFromDefault("invTypes", "invGroups", "mapConstellations", "mapRegions", "mapSolarSystems"))
                            {
                                await RestoreDatabase();
                                return false;
                            }

                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.2.6":
                            await BackupDatabase();
                            await RunCommand("ALTER TABLE `refreshTokens` ADD COLUMN `ctoken` TEXT;");
                            await RunCommand("CREATE TABLE contracts(`characterID` INTEGER PRIMARY KEY NOT NULL,`type` INTEGER NOT NULL,`data` TEXT NOT NULL);");
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.2.7":
                            await BackupDatabase();
                            await RunCommand("CREATE TABLE contracts(`characterID` INTEGER PRIMARY KEY NOT NULL,`type` INTEGER NOT NULL,`data` TEXT NOT NULL);");
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.2.8":
                            await BackupDatabase();
                            await RunCommand("DROP TABLE `contracts`;");
                            await RunCommand("CREATE TABLE contracts(`characterID` INTEGER PRIMARY KEY NOT NULL,`data` TEXT, `corpdata` TEXT);");
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.2.10":
                            await BackupDatabase();
                            await RunCommand("CREATE TABLE standAuth(`characterID` INTEGER PRIMARY KEY NOT NULL, `token` TEXT, `personalStands` TEXT, `corpStands` TEXT, `allianceStands` TEXT);");
                            break;
                        case "1.2.14":
                            await BackupDatabase();
                            await LogHelper.LogWarning("Upgrading DB! Please wait...");
                            var users = await GetAuthUsersEx();
                            var tokens = await UserTokensGetAllEntriesEx();
                            await RunCommand("DROP TABLE `authUsers`;");
                            await RunCommand("CREATE TABLE authUsers(`Id` INTEGER PRIMARY KEY NOT NULL, `characterID` INTEGER NOT NULL, `discordID` INTEGER, `groupName` TEXT, `refreshToken` TEXT, `authState` INTEGER NOT NULL DEFAULT 0, `data` TEXT);");
                            await RunCommand("CREATE INDEX ix_authUsers_characterID ON authUsers (characterID);");
                            await RunCommand("CREATE INDEX ix_authUsers_discordID ON authUsers (discordID);");
                            await users.ParallelForEachAsync(async user =>
                            {
                                var t = tokens.FirstOrDefault(a => a.CharacterId == user.CharacterId);
                                user.AuthState = t?.AuthState ?? (user.IsActive ? 2 : 0);
                                user.GroupName = user.Group;
                                user.DiscordId = user.DiscordId == 0 ? (t?.DiscordUserId ?? 0) : user.DiscordId;
                                user.RefreshToken = t?.RefreshToken;
                                user.Data.Permissions = t?.Permissions;
                                user.Data.CharacterName = user.EveName;

                                var cData = await APIHelper.ESIAPI.GetCharacterData("DB_UPGRADE", user.CharacterId);
                                if (cData != null)
                                {
                                    var corp = await APIHelper.ESIAPI.GetCorporationData("DB_UPGRADE", cData.corporation_id);
                                    user.Data.CorporationName = corp?.name;
                                    user.Data.CorporationTicker = corp?.ticker;
                                    user.Data.CorporationId = cData.corporation_id;
                                    var ally = cData.alliance_id.HasValue ? await APIHelper.ESIAPI.GetAllianceData("DB_UPGRADE", cData.alliance_id) : null;
                                    user.Data.AllianceName = ally?.name;
                                    user.Data.AllianceTicker = ally?.ticker;
                                    user.Data.AllianceId = cData.alliance_id ?? 0;
                                }
                            }, 10);

                            var cUsers = new ConcurrentBag<AuthUserEntity>(users);
                            var lTokens = tokens.Where(a => users.All(b => b.CharacterId != a.CharacterId));
                            await lTokens.ParallelForEachAsync(async token =>
                            {
                                var item = new AuthUserEntity
                                {
                                    CharacterId = token.CharacterId,
                                    DiscordId = token.DiscordUserId,
                                    GroupName = token.GroupName,
                                    AuthState = token.AuthState,
                                    RefreshToken = token.RefreshToken,
                                    Data = {CharacterName = token.CharacterName, Permissions = token.Permissions}
                                };
                                var cData = await APIHelper.ESIAPI.GetCharacterData("DB_UPGRADE", token.CharacterId);
                                if (cData != null)
                                {
                                    var corp = await APIHelper.ESIAPI.GetCorporationData("DB_UPGRADE", cData.corporation_id);
                                    item.Data.CorporationName = corp?.name;
                                    item.Data.CorporationId = cData.corporation_id;
                                    item.Data.CorporationTicker = corp?.ticker;
                                    var ally = cData.alliance_id.HasValue ? await APIHelper.ESIAPI.GetAllianceData("DB_UPGRADE", cData.alliance_id) : null;
                                    item.Data.AllianceName = ally?.name;
                                    item.Data.AllianceId = cData.alliance_id ?? 0;
                                    item.Data.AllianceTicker = ally?.ticker;
                                }
                                cUsers.Add(item);
                            }, 10);


                            var oUsers = cUsers.ToList();
                            oUsers.ToList().Select(a => a.DiscordId).Distinct().ToList().ForEach(item =>
                            {
                                if(item == 0) return;
                                var l = oUsers.Where(a => a.DiscordId == item).ToList();
                                if (l.Count > 1)
                                {
                                    var pending = l.Where(a => a.IsPending).ToList();
                                    if (pending.Count == l.Count)
                                    {
                                        l.Remove(pending[0]);
                                        oUsers.Remove(pending[0]);
                                        pending.RemoveAt(0);
                                    }

                                    pending.ForEach(d =>
                                    {
                                        l.Remove(d);
                                        oUsers.Remove(d);
                                    });
                                    if (l.Count > 1)
                                    {
                                        l.RemoveAt(0);
                                        l.ForEach(d => { oUsers.Remove(d); });                                        
                                    }
                                }

                            });

                            foreach (var a in oUsers)
                            {
                                a.Id = 0;
                                await SaveAuthUserEx(a, true);
                            }

                            await RunCommand("DROP TABLE `userTokens`;");
                            await LogHelper.LogWarning("Step 1 finished...");

                            //text fixes
                            await RunCommand("DROP TABLE `hrmAuth`;");
                            await RunCommand("CREATE TABLE `hrmAuth` ( `id` int UNIQUE PRIMARY KEY NOT NULL, `time` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP, `code` TEXT NOT NULL);");
                            await RunCommand("DROP TABLE `fleetup`;");
                            await RunCommand("CREATE TABLE `fleetup` ( `id` int UNIQUE PRIMARY KEY NOT NULL, `announce` int NOT NULL DEFAULT 0);");
                            await RunCommand("DROP TABLE `mail`;");
                            await RunCommand("CREATE TABLE `mail` ( `id` int UNIQUE PRIMARY KEY NOT NULL, `mailId` int DEFAULT 0);");
                            await RunCommand("DROP TABLE `timersAuth`;");
                            await RunCommand("CREATE TABLE `timersAuth` ( `id` int UNIQUE PRIMARY KEY NOT NULL, `time` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP);");
                            await LogHelper.LogWarning("Step 2 finished...");

                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;

                        case "1.2.15":
                            if (SettingsManager.Settings.Database.DatabaseProvider == "sqlite")
                            {
                                await RunCommand("drop table killFeedCache;");

                                await RunCommand("alter table authUsers rename to auth_users;");
                                await RunCommand("alter table cacheData rename to cache_data;");
                                await RunCommand("alter table hrmAuth rename to hrm_auth;");
                                await RunCommand("alter table invGroups rename to inv_groups;");
                                await RunCommand("alter table invTypes rename to inv_types;");
                                await RunCommand("alter table mapConstellations rename to map_constellations;");
                                await RunCommand("alter table mapRegions rename to map_regions;");
                                await RunCommand("alter table mapSolarSystems rename to map_solar_systems;");
                                await RunCommand("alter table notificationsList rename to notifications_list;");
                                await RunCommand("alter table nullCampaigns rename to null_campaigns;");
                                await RunCommand("alter table pendingUsers rename to pending_users;");
                                await RunCommand("alter table refreshTokens rename to refresh_tokens;");
                                await RunCommand("alter table standAuth rename to stand_auth;");
                                await RunCommand("alter table timersAuth rename to timers_auth;");
                            }
                            if (SettingsManager.Settings.Database.DatabaseProvider == "mysql")
                            {
                                await RunCommand("drop table killfeedcache;");

                                await RunCommand("alter table authusers rename to auth_users;");
                                await RunCommand("alter table cachedata rename to cache_data;");
                                await RunCommand("alter table hrmauth rename to hrm_auth;");
                                await RunCommand("alter table invgroups rename to inv_groups;");
                                await RunCommand("alter table invtypes rename to inv_types;");
                                await RunCommand("alter table mapconstellations rename to map_constellations;");
                                await RunCommand("alter table mapregions rename to map_regions;");
                                await RunCommand("alter table mapsolarsystems rename to map_solar_systems;");
                                await RunCommand("alter table notificationslist rename to notifications_list;");
                                await RunCommand("alter table nullcampaigns rename to null_campaigns;");
                                await RunCommand("alter table pendingusers rename to pending_users;");
                                await RunCommand("alter table refreshtokens rename to refresh_tokens;");
                                await RunCommand("alter table standauth rename to stand_auth;");
                                await RunCommand("alter table timersauth rename to timers_auth;");

                                if(!string.IsNullOrEmpty(SettingsManager.Settings.Database.DatabaseName))
                                    await RunCommand($"ALTER DATABASE `{SettingsManager.Settings.Database.DatabaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
                            }

                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.2.16":
                            await BackupDatabase();

                            var pUsers = await GetPendingUsersEx();

                            await RunCommand("drop table pending_users;");
                            await RunCommand("ALTER TABLE `auth_users` ADD COLUMN `reg_code` TEXT;");
                            await RunCommand("ALTER TABLE `auth_users` ADD COLUMN `reg_date` timestamp;");
                            
                            foreach (var user in pUsers.Where(a=> a.Active))
                            {
                                var dbentry = await GetAuthUserByCharacterId(user.CharacterId);
                                if (dbentry != null)
                                {
                                    dbentry.RegCode = user.AuthString;
                                    dbentry.CreateDate = user.CreateDate;
                                    await SaveAuthUser(dbentry);
                                }
                                else
                                {
                                    var au = new AuthUserEntity
                                    {
                                        CharacterId = user.CharacterId,
                                        DiscordId = 0,
                                        RegCode = user.AuthString,
                                        AuthState = 0,
                                        CreateDate = user.CreateDate,
                                        Data = new AuthUserData()
                                    };
                                    await au.UpdateData();
                                    await SaveAuthUser(au);
                                }
                            }

                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                            //MYSQL HAS BEEN ADDED HERE
                        case "1.2.19":
                            await Delete("notifications_list", "id", 999990000);
                            break;*/
                        #endregion
                        case "1.3.1":
                            await RunCommand("ALTER TABLE `auth_users` ADD COLUMN `dump_date` timestamp NULL;");
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.3.2":
                            if(SettingsManager.Settings.Database.DatabaseProvider == "sqlite")
                                await RunCommand("CREATE TABLE `sovIndexTracker` ( `groupName` TEXT UNIQUE NOT NULL, `data` TEXT NOT NULL);");
                            else
                                await RunCommand("CREATE TABLE `sovIndexTracker` ( `groupName` VARCHAR(100) UNIQUE NOT NULL, `data` TEXT NOT NULL);");
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.3.4":
                            await RunCommand("ALTER TABLE `auth_users` ADD COLUMN `main_character_id` bigint NULL;");
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.3.10":
                            await RunCommand("CREATE TABLE `web_editor_auth` ( `id` int UNIQUE PRIMARY KEY NOT NULL, `code` TEXT NOT NULL, `time` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP);");
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.3.16":
                            await RunCommand("ALTER TABLE `refresh_tokens` ADD COLUMN `indtoken` bigint NULL;");
                            await RunCommand("CREATE TABLE `industry_jobs` (`character_id` bigint UNIQUE NOT NULL, `personal_jobs` TEXT NULL, `corporate_jobs` TEXT NULL);");
                            await RunCommand("DELETE FROM `contracts`;");
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.4.2":
                            await RunCommand("ALTER TABLE `auth_users` ADD COLUMN `last_check` timestamp NULL;");
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.4.5":
                            await RunCommand("ALTER TABLE `auth_users` ADD COLUMN `ip` text NULL;");
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "1.5.4":
                            await RunCommand("create unique index timers_id_uindex on timers(id);");
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "2.0.1":
                            await BackupDatabase();
                            if (SettingsManager.Settings.Database.DatabaseProvider.Equals("sqlite",
                                StringComparison.OrdinalIgnoreCase))
                            {
                                await RunCommand(
                                    @"create table tokens (id integer not null constraint tokens_pk primary key autoincrement,	token text not null,	type int not null,	character_id integer not null);");
                                await RunCommand(@"create index tokens_character_id_index on tokens (character_id);");
                                await RunCommand(
                                    @"create unique index tokens_character_id_type_uindex on tokens (character_id, type);");
                                await RunCommand(@"create unique index tokens_id_uindex on tokens (id);");
                            }
                            else
                            {
                                await RunCommand(@"create table tokens(id int key auto_increment,	token text not null, type int not null,	character_id int not null);");
                                await RunCommand(@"create index tokens_character_id_index on tokens (character_id);");
                                await RunCommand(@"create unique index tokens_character_id_type_uindex on tokens (character_id, type);");
                                await RunCommand(@"create unique index tokens_id_uindex on tokens (id);");
                            }

                            await LogHelper.LogWarning("Step 1 finished...");

                            //notifications
                            var tokens = (await SelectData("select id,token from refresh_tokens"))
                                .Where(a => !string.IsNullOrEmpty((string) a[1]))
                                .ToDictionary(a => Convert.ToInt64(a[0]), a => (string) a[1]);
                            foreach (var (key, value) in tokens)
                            {
                                await DbHelper.UpdateToken(value, key, TokenEnum.Notification);
                            }
                            //contracts
                            tokens.Clear();
                            tokens = (await SelectData("select id,ctoken from refresh_tokens"))
                                .Where(a => !string.IsNullOrEmpty((string)a[1]))
                                .ToDictionary(a => Convert.ToInt64(a[0]), a => (string)a[1]);
                            foreach (var (key, value) in tokens)
                            {
                                await DbHelper.UpdateToken(value, key, TokenEnum.Contract);
                            }
                            //mail
                            tokens.Clear();
                            tokens = (await SelectData("select id,mail from refresh_tokens"))
                                .Where(a => !string.IsNullOrEmpty((string)a[1]))
                                .ToDictionary(a => Convert.ToInt64(a[0]), a => (string)a[1]);
                            foreach (var (key, value) in tokens)
                            {
                                await DbHelper.UpdateToken(value, key, TokenEnum.Mail);
                            }
                            //industry
                            tokens.Clear();
                            tokens = (await SelectData("select id,indtoken from refresh_tokens"))
                                .Where(a => !string.IsNullOrEmpty((string)a[1]))
                                .ToDictionary(a => Convert.ToInt64(a[0]), a => (string)a[1]);
                            foreach (var (key, value) in tokens)
                            {
                                await DbHelper.UpdateToken(value, key, TokenEnum.Industry);
                            }
                            //general
                            tokens.Clear();
                            var data = (await SelectData("select characterID,refreshToken from auth_users"))
                                .Where(a => !string.IsNullOrEmpty((string) a[1]));
                            foreach (var d in data)
                            {
                                var key = Convert.ToInt64(d[0]);
                                var value = (string) d[1];
                                await DbHelper.UpdateToken(value, key, TokenEnum.General);
                            }
                            await LogHelper.LogWarning("Step 2 finished...");
                            await RunCommand(@"drop table refresh_tokens;");
                            await LogHelper.LogWarning("Step 3 finished...");


                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "2.0.2":
                            await BackupDatabase();
                            if (SettingsManager.Settings.Database.DatabaseProvider.Equals("sqlite",
                                StringComparison.OrdinalIgnoreCase))
                            {
                                await RunCommand(
                                    @"create table mining_notifications(citadel_id int not null constraint mining_notifications_pk primary key, ore_composition text not null, operator text not null,date timestamp not null);");
                                await RunCommand("create unique index mining_notifications_citadel_id_uindex on mining_notifications(citadel_id);");
                            }
                            else
                            {
                                await RunCommand(
                                    @"create table mining_notifications(citadel_id int key, ore_composition text not null, operator text not null,date timestamp not null);");
                                await RunCommand("create unique index mining_notifications_citadel_id_uindex on mining_notifications(citadel_id);");

                            }
                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        case "2.0.3":
                            await BackupDatabase();
                            if (SettingsManager.Settings.Database.DatabaseProvider.Equals("sqlite",
                                StringComparison.OrdinalIgnoreCase))
                            {
                                await RunCommand(
                                    "create table mining_ledger(id integer not null constraint mining_ledger_pk primary key autoincrement, citadel_id integer not null, date timestamp,ore_json text);");
                            }
                            else
                            {
                                await RunCommand(
                                    "create table mining_ledger(bigint int not null key auto_increment,citadel_id bigint not null, date timestamp,ore_json text);");
                                await RunCommand(
                                    "ALTER TABLE `mining_notifications` CHANGE COLUMN `citadel_id` `citadel_id` BIGINT NOT NULL;");
                                await RunCommand(
                                    "ALTER TABLE `tokens` CHANGE COLUMN `id` `id` BIGINT NOT NULL;");
                            }

                            await LogHelper.LogWarning($"Upgrade to DB version {update} is complete!");
                            break;
                        default:
                            continue;
                    }
                }

                //update version in DB
                InsertOrUpdate("cache_data", new Dictionary<string, object>
                {
                    { "name", "version"},
                    { "data", Program.VERSION}
                }).GetAwaiter().GetResult();


                return true;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("Upgrade", ex, LogCat.Database);
                return false;
            }
            finally
            {
                SettingsManager.IsNew = false;
            }
        }

        private static async Task BackupDatabase(string bkFile = null)
        {
            if(SettingsManager.Settings.Database.DatabaseProvider != "sqlite") return;
            try
            {
                bkFile = bkFile ?? $"{SettingsManager.DatabaseFilePath}.bk";
                if (File.Exists(bkFile))
                    File.Delete(bkFile);
                using (var source = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
                using (var target = new SqliteConnection($"Data Source = {bkFile};"))
                {
                    await source.OpenAsync();
                    await target.OpenAsync();
                    source.BackupDatabase(target);
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("DbBackup", ex, LogCat.Database);

            }
        }

        private static async Task RestoreDatabase()
        {
            if(SettingsManager.Settings.Database.DatabaseProvider != "sqlite") return;
            try
            {
                var bkFile = $"{SettingsManager.DatabaseFilePath}.bk";
                if (!File.Exists(bkFile))
                    return;
                File.Copy(bkFile, SettingsManager.DatabaseFilePath, true);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("DbRestore", ex, LogCat.Database);
            }
        }

        private static async Task<bool> CopyTableDataFromDefault(params string[] tables)
        {
            try
            {
                if (SettingsManager.Settings.Database.DatabaseProvider == "sqlite")
                {
                    using (var source = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
                    {
                        await source.OpenAsync();

                        using (var attach = new SqliteCommand("ATTACH DATABASE 'edb.def.db' AS Y;", source))
                        {
                            attach.ExecuteNonQuery();
                        }

                        foreach (var table in tables)
                        {
                            using (var command = new SqliteCommand($"DELETE FROM '{table}';", source))
                                command.ExecuteNonQuery();

                            using (var command = new SqliteCommand($"INSERT INTO '{table}' SELECT * FROM Y.'{table}';", source))
                                command.ExecuteNonQuery();
                        }

                        using (var attach = new SqliteCommand("DETACH DATABASE Y;", source))
                        {
                            attach.ExecuteNonQuery();
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("Upgrade CopyTableDataFromDefault", ex, LogCat.Database);
                return false;
            }
        }
    }
}
