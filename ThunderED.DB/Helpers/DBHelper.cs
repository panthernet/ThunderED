using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Radzen;
using ThunderED.Classes;
using ThunderED.Classes.Enums;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Thd;

namespace ThunderED
{
    public static class DbHelper
    {
        public static async Task<object[]> GetUsersList(UserStatusEnum type, LoadDataArgs args)
        {
            await using var db = new ThunderedDbContext();
            IQueryable<ThdAuthUser> q;
            switch (type)
            {
                case UserStatusEnum.Awaiting:
                    q = db.Users.OrderBy(a => a.Data).AsNoTracking().Where(a => a.AuthState < 2);
                    break;
                case UserStatusEnum.Authed:
                    q = db.Users.OrderBy(a => a.Data).AsNoTracking().Where(a => a.AuthState == 2);
                    break;
                case UserStatusEnum.Dumped:
                    q = db.Users.OrderBy(a => a.Data).AsNoTracking().Where(a => a.AuthState == 3);
                    break;
                case UserStatusEnum.Spying:
                    q = db.Users.OrderBy(a => a.Data).AsNoTracking().Where(a => a.AuthState == 4);
                    break;
                case UserStatusEnum.Alts:
                    q = db.Users.OrderBy(a => a.Data).AsNoTracking().Where(a => a.MainCharacterId > 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "User type not found");
            }

            if (!string.IsNullOrEmpty(args.Filter))
            {
                var index = args.Filter.IndexOf("Contains(") + 10;
                var end = args.Filter.IndexOf('"', index + 1);
                var value = args.Filter.Substring(index, end - index).ToLower();
                q = q.Where(a => a.Data.ToLower().Contains(value));
            }

            if (!string.IsNullOrEmpty(args.OrderBy))
            {
                q = q.OrderBy(args.OrderBy);
            }

            if (args.Skip.HasValue)
            {
                q = q.Skip(args.Skip.Value);
            }

            if (args.Top.HasValue)
            {
                q = q.Take(args.Top.Value);
            }

            var list = await q.ToListAsync();
            list?.ForEach(a => a.UnpackData());

            return new object[] {list, db.Users.Count()};
        }

        public static async Task<List<ulong>> GetUserDiscordIdsForAuthCheck(int count = 100)
        {
            await using var db = new ThunderedDbContext();
            var compareDate = DateTime.Now.AddHours(-1);
            return await db.Users.Where(a => !a.LastCheck.HasValue || a.LastCheck.Value <= compareDate)
                .OrderBy(a => a.LastCheck)
                .Take(count).Select(a => a.DiscordId ?? 0).ToListAsync();
        }

        public static async Task<List<long>> GetUserIdsForAuthCheck(int count = 100)
        {
            await using var db = new ThunderedDbContext();
            var compareDate = DateTime.Now.AddHours(-1);
            return await db.Users.Where(a => !a.LastCheck.HasValue || a.LastCheck.Value <= compareDate)
                .OrderBy(a => a.LastCheck)
                .Take(count).Select(a => a.CharacterId).ToListAsync();
        }


        public static async Task<List<long>> GetAltUserIds(long entryCharacterId)
        {
            await using var db = new ThunderedDbContext();
            return await db.Users.AsNoTracking().Where(a => a.MainCharacterId == entryCharacterId)
                .Select(a => a.CharacterId)
                .ToListAsync();
        }

        #region Tokens

        public static async Task<List<ThdToken>> GetTokensWithoutScopes()
        {
            await using var db = new ThunderedDbContext();
            return await db.Tokens.AsNoTracking().Where(a => a.Scopes == null)
                .ToListAsync();
        }


        public static async Task<List<ThdToken>> GetTokensByScope(string scope)
        {
            await using var db = new ThunderedDbContext();
            return await db.Tokens.AsNoTracking().Where(a => EF.Functions.Like(a.Scopes, $"%{scope}%")).ToListAsync();
        }

        public static async Task<List<ThdToken>> GetTokens(TokenEnum type)
        {
            await using var db = new ThunderedDbContext();
            return await db.Tokens.AsNoTracking().Where(a => a.Type == type).ToListAsync();
        }

        public static async Task<List<ThdToken>> GetAllTokens()
        {
            await using var db = new ThunderedDbContext();
            return await db.Tokens.AsNoTracking().ToListAsync();
        }

        public static async Task DeleteToken(long userId, TokenEnum type)
        {
            if (userId == 0) return;

            await using var db = new ThunderedDbContext();
            var t = await db.Tokens.Where(a => a.CharacterId == userId && a.Type == type).ToListAsync();
            t.ForEach(a => db.Tokens.Remove(a));
            await db.SaveChangesAsync();
        }

        public static async Task<ThdToken> UpdateToken(string token, long characterId, TokenEnum type,
            string scopes = null)
        {
            try
            {
                await using var db = new ThunderedDbContext();
                var entry = db.Tokens.FirstOrDefault(a => a.Type == type && a.CharacterId == characterId);
                if (entry == null)
                {
                    entry = new ThdToken
                    {
                        CharacterId = characterId,
                        Token = token,
                        Type = type,
                        Scopes = scopes
                    };
                    await db.Tokens.AddAsync(entry);
                }
                else
                {
                    entry.Token = token;
                    entry.Scopes = scopes;
                }

                await db.SaveChangesAsync();

                return entry;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex);
                throw;
            }
        }

        public static async Task<string> GetTokenString(long charId, TokenEnum type)
        {
            await using var db = new ThunderedDbContext();
            return db.Tokens.FirstOrDefault(a => a.CharacterId == charId && a.Type == type)?.Token;
        }


        public static async Task<ThdToken> GetToken(long charId, TokenEnum type)
        {
            await using var db = new ThunderedDbContext();
            return db.Tokens.AsNoTracking().FirstOrDefault(a => a.CharacterId == charId && a.Type == type);
        }

        #endregion

        #region AuthUsers

        public static async Task<List<ThdAuthUser>> GetAuthUsers(UserStatusEnum type, bool includeToken = false,
            bool checkPermissions = false)
        {
            await using var db = new ThunderedDbContext();
            var req = db.Users.AsNoTracking().Where(a => a.AuthState == (int) type);
            if (includeToken)
                req = req.Include(a => a.Tokens);
            var list = await req.ToListAsync();
            list.ForEach(a => { a?.UnpackData(); });
            if (checkPermissions)
                list = list.Where(a => a.DataView.PermissionsList.Any()).ToList();
            return list;
        }

        public static async Task<List<long>> GetAuthUsersId(UserStatusEnum type)
        {
            await using var db = new ThunderedDbContext();
            return await db.Users.AsNoTracking().Where(a => a.AuthState == (int) type).Select(a => a.CharacterId)
                .ToListAsync();
        }



        public static async Task<List<ThdAuthUser>> GetAuthUsers(bool includeToken = false,
            bool checkPermissions = false)
        {
            await using var db = new ThunderedDbContext();
            var req = db.Users.AsNoTracking();
            if (includeToken)
                req = req.Include(a => a.Tokens);
            var list = await req.ToListAsync();
            list.ForEach(a => { a?.UnpackData(); });
            if (checkPermissions)
                list = list.Where(a => a.DataView.PermissionsList.Any()).ToList();
            return list;
        }

        public static async Task SaveAuthUser(ThdAuthUser user, string token = null)
        {
            await using var db = new ThunderedDbContext();
            user.PackData();
            db.Attach(user);
            if (db.Entry(user).State == EntityState.Unchanged)
                db.Entry(user).State = EntityState.Modified;
            if (!string.IsNullOrEmpty(token))
            {
                var old = db.Tokens.FirstOrDefault(
                    a => a.CharacterId == user.CharacterId && a.Type == TokenEnum.General);
                if (old != null)
                    db.Tokens.Remove(old);
                await db.Tokens.AddAsync(new ThdToken
                    {Type = TokenEnum.General, CharacterId = user.CharacterId, Token = token});
            }

            await db.SaveChangesAsync();
        }

        public static async Task<ThdAuthUser> GetAuthUserByDiscordId(ulong userId, bool includeToken = false)
        {
            await using var db = new ThunderedDbContext();
            var req = db.Users.AsNoTracking().Where(a => a.DiscordId == userId);
            if (includeToken)
                req = req.Include(a => a.Tokens);
            var result = req.FirstOrDefault();
            result?.UnpackData();
            return result;
        }

        public static async Task<List<ThdAuthUser>> GetOutdatedAwaitingAuthUsers()
        {
            await using var db = new ThunderedDbContext();
            var list = await db.Users.AsNoTracking().Where(a => string.IsNullOrEmpty(a.GroupName) || a.AuthState < 2)
                .ToListAsync();
            list.ForEach(a => a.UnpackData());
            return list;
        }

        public static async Task DeleteAuthUser(long characterId, bool deleteAlts = false)
        {
            await using var db = new ThunderedDbContext();
            var item = db.Users.FirstOrDefault(a => a.CharacterId == characterId);
            if (item != null)
            {
                db.Users.Remove(item);
                //var tokens = db.Tokens.Where(a => a.CharacterId == characterId && a.Type == TokenEnum.General);
                //db.Tokens.RemoveRange(tokens);

                var alts = await db.Users.Where(a => a.MainCharacterId == characterId).ToListAsync();
                if (alts.Any())
                    db.Users.RemoveRange(alts);
                await db.SaveChangesAsync();
            }
        }

        public static async Task<ThdAuthUser> GetAuthUserByRegCode(string regCode, bool includeToken = false)
        {
            await using var db = new ThunderedDbContext();
            var req = db.Users.AsNoTracking();
            if (includeToken)
                req = req.Include(a => a.Tokens);
            return req
                .FirstOrDefault(a => !string.IsNullOrEmpty(a.RegCode) && a.RegCode.Equals(regCode));
        }

        public static async Task<List<long>> DeleteAuthDataByDiscordId(ulong discordId)
        {
            await using var db = new ThunderedDbContext();
            var user = db.Users.FirstOrDefault(a => a.DiscordId == discordId);
            if (user != null)
            {
                db.Users.Remove(user);
                var users = await db.Users.Where(a => a.MainCharacterId > 0 && a.MainCharacterId == user.CharacterId)
                    .ToListAsync();
                if (users.Any())
                    db.Users.RemoveRange(users);
                await db.SaveChangesAsync();
                return users.Select(a => a.CharacterId).ToList();
            }

            return null;
        }

        public static async Task UpdateMainCharacter(long altId, long mainCharacterId)
        {
            await using var db = new ThunderedDbContext();
            var user = db.Users.FirstOrDefault(a => a.CharacterId == altId);
            if (user != null)
            {
                user.MainCharacterId = mainCharacterId;
                await db.SaveChangesAsync();
            }
        }

        public static async Task<ThdAuthUser> GetAuthUser(long characterId, bool includeToken = false)
        {
            await using var db = new ThunderedDbContext();
            var req = db.Users.AsNoTracking();
            if (includeToken)
                req = req.Include(a => a.Tokens);
            var user = req.FirstOrDefault(a => a.CharacterId == characterId);
            user?.UnpackData();
            return user;
        }

        public static async Task SetAuthUserLastCheck(ulong characterDiscordId, DateTime lastCheck)
        {
            await using var db = new ThunderedDbContext();
            var user = db.Users.FirstOrDefault(a => a.DiscordId == characterDiscordId);
            if (user != null)
            {
                user.LastCheck = lastCheck;
                await db.SaveChangesAsync();
            }
        }

        public static async Task<bool> IsAuthUsersGroupNameInDB(string groupName)
        {
            await using var db = new ThunderedDbContext();
            return await db.Users.AnyAsync(a =>
                !string.IsNullOrEmpty(a.GroupName) &&
                a.GroupName.Equals(groupName));
        }

        public static async Task RenameAuthGroup(string fromGroup, string toGroup)
        {
            await using var db = new ThunderedDbContext();
            var users = db.Users.Where(a =>
                !string.IsNullOrEmpty(a.GroupName) &&
                a.GroupName.Equals(fromGroup));
            await users.ForEachAsync(a => a.GroupName = toGroup);
            await db.SaveChangesAsync();

        }

        public static async Task ResetAuthUsersLastCheck()
        {
            await using var db = new ThunderedDbContext();
            await db.Users.ForEachAsync(a => a.LastCheck = null);
            await db.SaveChangesAsync();
        }

        public static async Task SaveAuthUsers(List<ThdAuthUser> users)
        {
            await using var db = new ThunderedDbContext();
            db.AttachRange(users);
            foreach (var entity in users)
            {
                if (db.Entry(entity).State == EntityState.Unchanged)
                    db.Entry(entity).State = EntityState.Modified;
            }

            await db.SaveChangesAsync();
        }

        public static async Task<List<ThdAuthUser>> GetRegisteredUsersWithSkills()
        {
            await using var db = new ThunderedDbContext();
            var ids = await db.Tokens.Where(a => EF.Functions.Like(a.Scopes, $"%{SettingsManager.GetCharSkillsScope()}%"))
                .Select(a => a.CharacterId).ToListAsync();

            return await db.Users.Where(a => ids.Contains(a.CharacterId)).ToListAsync();
        }

        public static async Task<List<FitTargetGroupEntry>> GetRegisteredUserCorpsAndAlliancesWithSkills()
        {
            await using var db = new ThunderedDbContext();
            var ids = await db.Tokens.AsNoTracking().Where(a => EF.Functions.Like(a.Scopes, $"%{SettingsManager.GetCharSkillsScope()}%"))
                .Select(a => a.CharacterId).ToListAsync();

            var users = await db.Users.AsNoTracking().Where(a => ids.Contains(a.CharacterId)).ToListAsync();
            var result = new List<FitTargetGroupEntry>();
            var tmpList = new List<string>();
            users.ForEach(a =>
            {
                a.UnpackData();

                if (!string.IsNullOrEmpty(a.DataView.AllianceName) && !tmpList.Contains(a.DataView.AllianceName) && a.AllianceId != 0)
                {
                    result.Add(new FitTargetGroupEntry { Id = a.AllianceId ?? 0, Name = a.DataView.AllianceName, IsAlliance = true });
                    tmpList.Add(a.DataView.AllianceName);
                }

                if (!tmpList.Contains(a.DataView.CorporationName) && a.CorporationId != 0)
                {
                    result.Add(new FitTargetGroupEntry { Id = a.CorporationId, Name = a.DataView.CorporationName, IsAlliance = false });
                    tmpList.Add(a.DataView.CorporationName);
                }

            });
            return result;
        }

        public static async Task<List<TestClass>> GetUserInfoByTargetGroup(FitTargetGroupEntry group)
        {
            await using var db = new ThunderedDbContext();
            List<TestClass> list;

            if (group.IsAlliance)
                list = (await db.Users.AsNoTracking().Where(a => a.AllianceId != null && a.AllianceId == group.Id)
                        .Select(a => new {a.CharacterId, a.CharacterName}).ToListAsync())
                    .Select(a => new TestClass {Id = a.CharacterId, Name = a.CharacterName}).ToList();
            else 
                list =  (await db.Users.AsNoTracking().Where(a => a.CorporationId == group.Id)
                    .Select(a => new {a.CharacterId, a.CharacterName}).ToListAsync())
                .Select(a => new TestClass {Id = a.CharacterId, Name = a.CharacterName}).ToList();

            if (list.FirstOrDefault(a => a.Id == 341748641) != null) //todo remove
                await LogHelper.LogWarning("FOUND CPT!", LogCat.Database);

            var tokenIds = await db.Tokens.AsNoTracking().Where(a => a.Type == TokenEnum.General && EF.Functions.Like(a.Scopes, $"%{SettingsManager.GetCharSkillsScope()}%"))
                .Select(a => a.CharacterId).Distinct().ToListAsync();
            if (tokenIds.Contains(341748641)) //todo remove
                await LogHelper.LogWarning("FOUND CPT 2!", LogCat.Database);
            list.RemoveAll(a => !tokenIds.Contains(a.Id));

            return list;
        }

        #endregion

        #region Fits

        public static async Task SaveOrUpdateFit(ThdFit fit)
        {
            await using var db = new ThunderedDbContext();
            if (fit.Id == 0)
            {
                await db.Fits.AddAsync(fit);
            }
            else
            {
                db.Attach(fit);
                if (db.Entry(fit).State == EntityState.Unchanged)
                    db.Entry(fit).State = EntityState.Modified;
            }

            await db.SaveChangesAsync();
        }

        public static async Task<List<ThdFit>> GetFits(string groupName)
        {
            await using var db = new ThunderedDbContext();
            return await db.Fits.AsNoTracking().Where(a => EF.Functions.Like(a.GroupName, groupName)).ToListAsync();
        }

        public static async Task DeleteFit(long id)
        {
            await using var db = new ThunderedDbContext();
            var fit = await db.Fits.FirstOrDefaultAsync(a => a.Id == id);
            if(fit == null)
                return;
            db.Fits.Remove(fit);
            await db.SaveChangesAsync();
        }

        #endregion

        #region Mining

        public static async Task<ThdMiningNotification> GetMiningNotification(long citadelId, DateTime extractionDate)
        {
            await using var db = new ThunderedDbContext();

            return await db.MiningNotifications.AsNoTracking()
                .FirstOrDefaultAsync(a => a.CitadelId == citadelId && a.Date <= extractionDate);
        }

        public static async Task UpdateMiningNotification(ThdMiningNotification notify)
        {
            await using var db = new ThunderedDbContext();
            if (db.MiningNotifications.Any(a => a.CitadelId == notify.CitadelId))
            {
                db.Attach(notify);
                db.Entry(notify).State = EntityState.Modified;
            }
            else
            {
                await db.MiningNotifications.AddAsync(notify);
                db.Entry(notify).State = EntityState.Added;
            }

            await db.SaveChangesAsync();
        }

        public static async Task UpdateMiningLedger(ThdMiningLedger entry)
        {
            await using var db = new ThunderedDbContext();
            if (entry.Id > 0)
            {
                db.Attach(entry);
                db.Entry(entry).State = EntityState.Modified;
            }
            else
            {
                await db.MiningLedgers.AddAsync(entry);
                db.Entry(entry).State = EntityState.Added;
            }

            await db.SaveChangesAsync();
        }

        public static async Task<ThdMiningLedger> GetMiningLedger(long citadelId, bool lastComplete)
        {
            await using var db = new ThunderedDbContext();

            ThdMiningLedger result;
            if (lastComplete)
                result = await db.MiningLedgers.AsNoTracking().Where(a => a.CitadelId == citadelId)
                    .OrderByDescending(a => a.Date).FirstOrDefaultAsync();
            else
                result = await db.MiningLedgers.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.CitadelId == citadelId && a.Date == null);
            result?.Unpack();

            return result;
        }

        #endregion

        #region Cache

        private static string GetListItemTypeName(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition()
                == typeof(List<>))
            {
                return type.GetGenericArguments()[0].Name; // use this...
            }

            return type.Name;
        }

        public static async Task<T> GetCache<T>(string cacheId, int minutes)
        {
            await using var db = new ThunderedDbContext();
            var now = DateTime.Now;
            var typeName = GetListItemTypeName(typeof(T));

            var content = db.Cache.AsNoTracking().Where(a => a.Id == cacheId && a.Type == typeName).ToList()
                .FirstOrDefault(a => (now - a.LastUpdate).Minutes <= minutes)?.Content;

            return string.IsNullOrEmpty(content) ? (T) (object) null : JsonConvert.DeserializeObject<T>(content);
        }

        private static volatile bool _isCacheUpdating = false;
        public static async Task UpdateCache<T>(string cacheId, T content, int days = 1)
        {
            while (_isCacheUpdating)
                await Task.Delay(5);
            _isCacheUpdating = true;
            try
            {
                await using var db = new ThunderedDbContext();
                var typeName = GetListItemTypeName(typeof(T));

                var entry = await db.Cache.FirstOrDefaultAsync(a =>
                    a.Id == cacheId && a.Type == typeName);
                if (entry == null)
                    await db.Cache.AddAsync(new ThdCacheEntry
                        {Id = cacheId, Content = JsonConvert.SerializeObject(content), Days = days, Type = typeName, LastAccess = DateTime.Now, LastUpdate = DateTime.Now});
                else
                {
                    entry.Type = typeName;
                    entry.Content = JsonConvert.SerializeObject(content);
                    entry.LastUpdate = DateTime.Now;
                    entry.LastAccess = DateTime.Now;
                    entry.Days = days;
                }

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, LogCat.Database);
            }
            finally
            {
                _isCacheUpdating = false;
            }
        }

        public static async Task SetCacheLastAccess(string id, Type type)
        {
            await using var db = new ThunderedDbContext();
            var typeName = GetListItemTypeName(type);
            var entry = await db.Cache.FirstOrDefaultAsync(a => a.Id == id && a.Type == typeName);

            if (entry != null)
            {
                entry.LastAccess = DateTime.Now;
                await db.SaveChangesAsync();
            }
        }

        public static async Task DeleteCache(string id, string type)
        {
            await using var db = new ThunderedDbContext();

            var old = await db.Cache.FirstOrDefaultAsync(a => a.Id == id && a.Type == type);
            if (old != null)
            {
                db.Cache.Remove(old);
                await db.SaveChangesAsync();
            }
        }

        public static async Task DeleteCache(string type = null)
        {
            await using var db = new ThunderedDbContext();
            if (type == null)
            {
                await PurgeCache();
                return;
            }

            var old = await db.Cache.Where(a => a.Type == type).ToListAsync();
            if (old.Any())
            {
                db.Cache.RemoveRange(old);
                await db.SaveChangesAsync();
            }
        }

        public static async Task PurgeCache()
        {
            var dt1 = DateTime.Now.Subtract(TimeSpan.FromDays(1));
            var dt30 = DateTime.Now.Subtract(TimeSpan.FromDays(30));
            var dt180 = DateTime.Now.Subtract(TimeSpan.FromDays(180));

            await using var db = new ThunderedDbContext();
            var list = await db.Cache.Where(a => (a.Days == 1 && a.LastUpdate < dt1) || (a.Days == 30 && a.LastUpdate < dt30) || (a.Days == 180 && a.LastUpdate < dt180)).ToListAsync();
            if (list.Any())
            {
                db.Cache.RemoveRange(list);
                await db.SaveChangesAsync();
            }
        }

        #endregion

        #region NotificationList

        public static async Task<DateTime?> GetNotificationListEntryDate(string group, long id)
        {
            await using var db = new ThunderedDbContext();
            return (await db.NotificationsList.AsNoTracking().FirstOrDefaultAsync(a =>
                EF.Functions.Like(a.GroupName, group) && a.Id == id))?.Time;
        }

        public static async Task<ThdNotificationListEntry> GetNotificationListEntry(string group, long id)
        {
            await using var db = new ThunderedDbContext();
            return (await db.NotificationsList.AsNoTracking().FirstOrDefaultAsync(a =>
                EF.Functions.Like(a.GroupName, group) && a.Id == id));
        }

        public static async Task UpdateNotificationListEntry(string group, long id, string filter = null)
        {
            try
            {
                var fentry = filter == null ? id.ToString() : $"{filter}{id}";

                await using var db = new ThunderedDbContext();
                var item = filter == null ? await db.NotificationsList.FirstOrDefaultAsync(a =>
                    EF.Functions.Like(a.GroupName, group) && a.Id == id) : await db.NotificationsList.FirstOrDefaultAsync(a =>
                    EF.Functions.Like(a.GroupName, group) && EF.Functions.Like(a.FilterName, fentry) && a.Id == id);
                if (item != null)
                {
                    item.Time = DateTime.Now;
                    item.FilterName = filter;
                }
                else
                {
                    await db.NotificationsList.AddAsync(new ThdNotificationListEntry
                        {Id = id, GroupName = group, Time = DateTime.Now, FilterName = fentry});
                }

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, LogCat.Database);
            }
        }

        #endregion

        #region CacheData

        public static async Task<ThdCacheDataEntry> GetCacheDataEntry(string name)
        {
            await using var db = new ThunderedDbContext();
            return await db.CacheData.FirstOrDefaultAsync(a => EF.Functions.Like(a.Name, name));
        }

        public static async Task UpdateCacheDataEntry(string name, string data, ThunderedDbContext dbx = null)
        {
            ThunderedDbContext db = null;
            try
            {
                db = dbx ?? new ThunderedDbContext();
                var old = await db.CacheData.FirstOrDefaultAsync(a => EF.Functions.Like(a.Name, name));
                if (old != null)
                    db.CacheData.Remove(old);
                await db.CacheData.AddAsync(new ThdCacheDataEntry {Name = name, Data = data});

                await db.SaveChangesAsync();
            }
            finally
            {
                if (dbx == null && db != null)
                    await db.DisposeAsync();
            }
        }

        public static async Task<bool> IsCacheDataExist(string name)
        {
            await using var db = new ThunderedDbContext();
            return await db.CacheData.FirstOrDefaultAsync(a => EF.Functions.Like(a.Name, name)) !=null;
        }

        #endregion

        #region Contracts

        public static async Task<List<JsonClasses.Contract>> GetContracts(long characterID, bool isCorp)
        {
            await using var db = new ThunderedDbContext();
            if (isCorp)
                return (await db.Contracts.AsNoTracking().FirstOrDefaultAsync(a => a.CharacterId == characterID))
                    ?.CorpData;
            return (await db.Contracts.AsNoTracking().FirstOrDefaultAsync(a => a.CharacterId == characterID))
                ?.Data;
        }

        public static async Task SaveContracts(long characterId, List<JsonClasses.Contract> data, bool isCorp)
        {
            await using var db = new ThunderedDbContext();
            var old = await db.Contracts.FirstOrDefaultAsync(a => a.CharacterId == characterId);
            if (old != null)
            {
                if (isCorp)
                    old.CorpData = data;
                else old.Data = data;
            }
            else
            {
                await db.Contracts.AddAsync(new ThdContract
                    {CharacterId = characterId, CorpData = isCorp ? data : null, Data = isCorp ? null : data});
            }

            await db.SaveChangesAsync();
        }

        #endregion

        #region NullCampaign

        public static async Task<List<JsonClasses.NullCampaignItem>> GetNullCampaigns(string group)
        {
            await using var db = new ThunderedDbContext();
            var result = await db.NullCampaigns.AsNoTracking().Where(a => EF.Functions.Like(a.GroupKey, group))
                .ToListAsync();
            if (result != null)
                result.ForEach(a => a.Data.LastAnnounce = a.LastAnnounce);
            return result?.Select(a => a.Data).ToList();
        }

        public static async Task UpdateNullCampaignAnnounce(string group, long campaignId, int announce)
        {
            await using var db = new ThunderedDbContext();
            var result = await db.NullCampaigns.FirstOrDefaultAsync(a =>
                EF.Functions.Like(a.GroupKey, group) && a.CampaignId == campaignId);
            if (result != null)
            {
                result.LastAnnounce = announce;
                await db.SaveChangesAsync();
            }
        }

        public static async Task UpdateNullCampaign(string groupName, long id, DateTimeOffset startTime,
            JsonClasses.NullCampaignItem data)
        {
            await using var db = new ThunderedDbContext();
            var result =
                await db.NullCampaigns.FirstOrDefaultAsync(a =>
                    EF.Functions.Like(a.GroupKey, groupName) && a.CampaignId == id);
            if (result != null)
            {
                result.Time = startTime;
                result.Data = data;
            }
            else
            {
                await db.NullCampaigns.AddAsync(new ThdNullCampaign
                {
                    GroupKey = groupName,
                    CampaignId = id,
                    Time = startTime,
                    Data = data,
                });
            }

            await db.SaveChangesAsync();
        }

        public static async Task DeleteNullCampaign(string groupName, long id)
        {
            await using var db = new ThunderedDbContext();
            var result =
                await db.NullCampaigns.FirstOrDefaultAsync(a =>
                    EF.Functions.Like(a.GroupKey, groupName) && a.CampaignId == id);
            if (result != null)
            {
                db.NullCampaigns.Remove(result);
                await db.SaveChangesAsync();
            }
        }

        public static async Task<List<long>> GetNullsecCampaignIdList(string groupName)
        {
            await using var db = new ThunderedDbContext();
            return await db.NullCampaigns.AsNoTracking().Where(a => EF.Functions.Like(a.GroupKey, groupName))
                .Select(a => a.CampaignId).ToListAsync();
        }

        public static async Task<bool> IsNullsecCampaignExists(string groupName, long id)
        {
            await using var db = new ThunderedDbContext();
            return await db.NullCampaigns.AsNoTracking()
                .FirstOrDefaultAsync(a => EF.Functions.Like(a.GroupKey, groupName) && a.CampaignId == id) != null;
        }

        #endregion

        #region Incursions


        public static async Task CleanupIncursions(List<long> list)
        {
            await using var db = new ThunderedDbContext();
            var result = await db.Incursions.Where(a => !list.Contains(a.ConstId)).ToListAsync();
            if (result != null && result.Any())
            {
                db.Incursions.RemoveRange(result);
                await db.SaveChangesAsync();
            }
        }

        public static async Task<bool> IsIncursionExists(long id)
        {
            await using var db = new ThunderedDbContext();
            return await db.Incursions.AsNoTracking().FirstOrDefaultAsync(
                a => a.ConstId == id) != null;
        }

        public static async Task AddIncursion(long id)
        {
            await using var db = new ThunderedDbContext();
            await db.Incursions.AddAsync(new ThdIncursion {ConstId = id, Time = DateTime.Now});
            await db.SaveChangesAsync();
        }

        #endregion

        #region Mail

        public static async Task<long> GetLastMailId(long charId)
        {
            await using var db = new ThunderedDbContext();
            return (await db.Mails.AsNoTracking().FirstOrDefaultAsync(
                a => a.Id == charId))?.MailId ?? 0;
        }

        public static async Task UpdateMail(long charId, long mailId)
        {
            await using var db = new ThunderedDbContext();
            var old = await db.Mails.FirstOrDefaultAsync(
                a => a.Id == charId);
            if (old == null)
            {
                await db.Mails.AddAsync(new ThdMail {Id = charId, MailId = mailId});
            }
            else
            {
                old.MailId = mailId;
            }

            await db.SaveChangesAsync();
        }

        #endregion

        #region Sov Index Tracker

        public static async Task<List<JsonClasses.SovStructureData>> GetSovIndexTrackerData(string name)
        {
            await using var db = new ThunderedDbContext();
            return (await db.SovIndexTrackers.FirstOrDefaultAsync(a => EF.Functions.Like(a.GroupName, name)))?.Data;
        }

        public static async Task SaveSovIndexTrackerData(string name, List<JsonClasses.SovStructureData> data)
        {
            await using var db = new ThunderedDbContext();
            var old = await db.SovIndexTrackers.FirstOrDefaultAsync(
                a => EF.Functions.Like(a.GroupName, name));
            if (old == null)
            {
                await db.SovIndexTrackers.AddAsync(new ThdSovIndexTracker {GroupName = name, Data = data});
            }
            else
            {
                old.Data = data;
            }

            await db.SaveChangesAsync();
        }

        #endregion

        #region Industry Jobs

        public static async Task<List<JsonClasses.IndustryJob>> GetIndustryJobs(long characterId, bool isCorp)
        {
            await using var db = new ThunderedDbContext();
            if (isCorp)
                return (await db.IndustryJobs.AsNoTracking().FirstOrDefaultAsync(a => a.CharacterId == characterId))
                    ?.CorporateJobs.OrderByDescending(a => a.job_id).ToList();
            return (await db.IndustryJobs.AsNoTracking().FirstOrDefaultAsync(a => a.CharacterId == characterId))
                ?.PersonalJobs.OrderByDescending(a => a.job_id).ToList();
        }

        public static async Task SaveIndustryJobs(long characterId, List<JsonClasses.IndustryJob> data, bool isCorp)
        {
            await using var db = new ThunderedDbContext();
            var old = await db.IndustryJobs.FirstOrDefaultAsync(a => a.CharacterId == characterId);
            if (old != null)
            {
                if (isCorp)
                    old.CorporateJobs = data;
                else old.PersonalJobs = data;
            }
            else
            {
                await db.IndustryJobs.AddAsync(new ThdIndustryJob
                {
                    CharacterId = characterId, CorporateJobs = isCorp ? data : null, PersonalJobs = isCorp ? null : data
                });
            }

            await db.SaveChangesAsync();
        }

        #endregion

        #region Moon table

        public static async Task UpdateMoonTable(ThdMoonTableEntry entry)
        {
            await using var db = new ThunderedDbContext();
            var old = await db.MoonTable.FirstOrDefaultAsync(
                a => a.SystemId == entry.SystemId && entry.MoonId == a.MoonId && entry.OreId == a.OreId);
            if (old != null)
            {
                db.MoonTable.Remove(old);
            await db.SaveChangesAsync();
            }

            await db.MoonTable.AddAsync(entry);

            await db.SaveChangesAsync();
        }

        public static async Task<List<ThdMoonTableEntry>> UpdateMoonTable(List<ThdMoonTableEntry> list)
        {
            await using var db = new ThunderedDbContext();
            foreach (var entry in list)
            {
                var old = await db.MoonTable.FirstOrDefaultAsync(
                    a => a.SystemId == entry.SystemId && entry.MoonId == a.MoonId && entry.OreId == a.OreId);
                if (old != null)
                {
                    db.MoonTable.Remove(old);
                    await db.SaveChangesAsync();
                }

                await db.MoonTable.AddAsync(entry);
            }

            await db.SaveChangesAsync();
            return list;
        }

        public static async Task<List<ThdMoonTableEntry>> GetMoonTable(long systemId = 0)
        {
            await using var db = new ThunderedDbContext();
            return systemId != 0
                ? await db.MoonTable.AsNoTracking().Where(a => a.SystemId == systemId).ToListAsync()
                : await db.MoonTable.AsNoTracking().ToListAsync();
        }

        public static async Task<List<ThdMoonTableEntry>> GetMoonTableByRegion(long regionId)
        {
            await using var db = new ThunderedDbContext();
            return await db.MoonTable.AsNoTracking().Where(a => a.RegionId == regionId).ToListAsync();
        }

        public static async Task<List<long>> GetMoonTableRegions()
        {
            await using var db = new ThunderedDbContext();
            return await db.MoonTable.Select(a => a.RegionId).Distinct().ToListAsync();
        }

        #endregion

        #region ThdStorageConsole

        public static async Task<List<ThdStorageConsoleEntry>> GetStorageConsoleEntries()
        {
            await using var db = new ThunderedDbContext();
            return await db.StorageConsole.AsNoTracking().ToListAsync();
        }

        public static async Task<ThdStorageConsoleEntry> GetStorageConsoleEntry(string name)
        {
            await using var db = new ThunderedDbContext();
            return await db.StorageConsole.AsNoTracking().FirstOrDefaultAsync(a => EF.Functions.Like(a.Name, name));
        }


        public static async Task SetStorageConsoleEntry(string name, double value)
        {
            await using var db = new ThunderedDbContext();
            var old = await db.StorageConsole.FirstOrDefaultAsync(a => EF.Functions.Like(a.Name, name));
            if (old != null)
            {
                old.Value = value;
                db.Entry(old).State = EntityState.Modified;
            }
            else
            {
                await db.StorageConsole.AddAsync(new ThdStorageConsoleEntry {Name = name, Value = value});

            }

            await db.SaveChangesAsync();
        }

        public static async Task<bool> ModStorageConsoleEntry(string name, double value)
        {
            await using var db = new ThunderedDbContext();
            var old = await db.StorageConsole.FirstOrDefaultAsync(a => EF.Functions.Like(a.Name, name));
            if (old != null)
            {
                old.Value += value;
                db.Entry(old).State = EntityState.Modified;
                await db.SaveChangesAsync();
                return true;
            }

            return false;
        }

        public static async Task<bool> RemoveStorageConsoleEntry(string name)
        {
            await using var db = new ThunderedDbContext();
            var old = await db.StorageConsole.FirstOrDefaultAsync(a => EF.Functions.Like(a.Name, name));
            if (old != null)
            {
                db.StorageConsole.Remove(old);
                await db.SaveChangesAsync();
                return true;
            }

            return false;
        }

        #endregion

        #region inv_custom_scheme

        public static async Task<List<ThdInvCustomScheme>> GetCustomSchemeValues(List<long> ids)
        {
            await using var db = new ThunderedDbContext();

            return await db.CustomSchemes.AsNoTracking().Where(a => ids.Contains(a.Id)).ToListAsync();
        }

        #endregion

        #region Auth stands

        public static async Task<ThdStandsAuth> GetAuthStands(long id)
        {
            await using var db = new ThunderedDbContext();
            return await db.StandsAuth.AsNoTracking().FirstOrDefaultAsync(a => a.CharacterId == id);
        }

        public static async Task DeleteAuthStands(long id)
        {
            await using var db = new ThunderedDbContext();
            var item = await db.StandsAuth.FirstOrDefaultAsync(a => a.CharacterId == id);
            if (item != null)
            {
                db.Remove(item);
                await db.SaveChangesAsync();
            }
        }

        public static async Task UpdateAuthStands(ThdStandsAuth entry)
        {
            await using var db = new ThunderedDbContext();
            var old = await db.StandsAuth.FirstOrDefaultAsync(a => a.CharacterId == entry.CharacterId);
            if (old != null)
            {
                db.Attach(entry);
                db.Entry(entry).State = EntityState.Modified;
            }
            else
            {
                await db.AddAsync(entry);
            }

            await db.SaveChangesAsync();
        }

        public static async Task<List<ThdStandsAuth>> GetAllAuthStands()
        {
            await using var db = new ThunderedDbContext();
            return await db.StandsAuth.AsNoTracking().ToListAsync();
        }

        #endregion

        #region System, region, const

        public static async Task<List<ThdStarSystem>> GetSystemsByConstellation(long constellationId)
        {
            await using var db = new ThunderedDbContext();
            return await db.StarSystems.AsNoTracking().Where(a => a.ConstellationId == constellationId).ToListAsync();
        }

        public static async Task<List<ThdStarSystem>> GetSystemsByRegion(long regionId)
        {
            await using var db = new ThunderedDbContext();
            return await db.StarSystems.AsNoTracking().Where(a => a.RegionId == regionId).ToListAsync();
        }

        public static async Task<ThdStarSystem> GetSystemById(long id)
        {
            await using var db = new ThunderedDbContext();
            return await db.StarSystems.AsNoTracking().FirstOrDefaultAsync(a => a.SolarSystemId == id);
        }

        public static async Task<ThdStarSystem> SaveStarSystem(ThdStarSystem input)
        {
            await using var db = new ThunderedDbContext();
            await db.StarSystems.AddAsync(input);
            await db.SaveChangesAsync();
            return input;
        }

        public static async Task<ThdStarRegion> GetRegionById(long id)
        {
            await using var db = new ThunderedDbContext();
            return await db.StarRegions.AsNoTracking().FirstOrDefaultAsync(a => a.RegionId == id);

        }

        public static async Task<ThdStarConstellation> GetConstellationById(long id)
        {
            await using var db = new ThunderedDbContext();
            return await db.StarConstellations.AsNoTracking().FirstOrDefaultAsync(a => a.ConstellationId == id);
        }

        public static async Task<ThdStarRegion> SaveStarRegion(ThdStarRegion input)
        {
            await using var db = new ThunderedDbContext();
            await db.StarRegions.AddAsync(input);
            await db.SaveChangesAsync();
            return input;
        }

        public static async Task<ThdStarConstellation> SaveStarConstellation(ThdStarConstellation input)
        {
            await using var db = new ThunderedDbContext();
            await db.StarConstellations.AddAsync(input);
            await db.SaveChangesAsync();
            return input;
        }

        public static async Task<ThdType> GetTypeId(long id)
        {
            await using var db = new ThunderedDbContext();
            return await db.Types.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        }

        public static async Task<ThdGroup> GetInvGroup(long id)
        {
            await using var db = new ThunderedDbContext();
            return await db.Groups.AsNoTracking().FirstOrDefaultAsync(a => a.GroupId == id);
        }

        public static async Task SaveType(ThdType input)
        {
            await using var db = new ThunderedDbContext();
            await db.Types.AddAsync(input);
            await db.SaveChangesAsync();
        }

        public static async Task SaveGroup(ThdGroup input)
        {
            await using var db = new ThunderedDbContext();
            await db.Groups.AddAsync(input);
            await db.SaveChangesAsync();
        }

        #endregion

        #region Notifications

        public static async Task PurgeNotifications()
        {
            await using var db = new ThunderedDbContext();
            if (db.NotificationsList.Any())
            {
                db.NotificationsList.RemoveRange(db.NotificationsList);
                await db.SaveChangesAsync();
            }
        }

        public static async Task<long> GetLastNotification(string group, string filter)
        {
            await using var db = new ThunderedDbContext();
            return (await db.NotificationsList.AsNoTracking().FirstOrDefaultAsync(a =>
                EF.Functions.Like(a.GroupName, group) && EF.Functions.Like(a.FilterName, filter)))?.Id ?? 0;
        }

        public static async Task SetLastNotification(string group, string filter, long id)
        {
            await using var db = new ThunderedDbContext();
            var old = await db.NotificationsList.FirstOrDefaultAsync(a =>
                EF.Functions.Like(a.GroupName, group) && EF.Functions.Like(a.FilterName, filter));
            if (old != null)
            {
                //no key modif allowed so delete first
                db.NotificationsList.Remove(old);
                await db.SaveChangesAsync();
            }
            await db.NotificationsList.AddAsync(new ThdNotificationListEntry
            {
                GroupName = group,
                FilterName = filter,
                Id = id,
                Time = DateTime.Now
            });
            await db.SaveChangesAsync();
        }

        public static async Task CleanupNotificationsList()
        {
            await using var db = new ThunderedDbContext();
            var time = DateTime.Now.Subtract(TimeSpan.FromDays(30));
            var list = await db.NotificationsList.Where(a => a.Time <= time).ToListAsync();
            if (list.Any())
            {
                db.RemoveRange(list);
                await db.SaveChangesAsync();
            }
        }

        #endregion

        #region Timers
        public static async Task DeleteTimer(long id)
        {
            await using var db = new ThunderedDbContext();
            var old = await db.Timers.FirstOrDefaultAsync(a => a.Id == id);
            if (old != null)
            {
                db.Timers.Remove(old);
                await db.SaveChangesAsync();
            }
        }

        public static async Task SetTimerAnnounce(long id, int value)
        {
            await using var db = new ThunderedDbContext();
            var old = await db.Timers.FirstOrDefaultAsync(a => a.Id == id);
            if (old != null)
            {
                old.Announce = value;
                await db.SaveChangesAsync();
            }
        }

        public static async Task<List<ThdTimer>> SelectTimers()
        {
            await using var db = new ThunderedDbContext();
            return await db.Timers.AsNoTracking().ToListAsync();
        }

        public static async Task UpdateTimer(ThdTimer entry)
        {
            await using var db = new ThunderedDbContext();
            var old =  await db.Timers.AsNoTracking().FirstOrDefaultAsync(a => a.Id == entry.Id);
            if (old != null)
            {
                db.Attach(entry);
                if (db.Entry(entry).State == EntityState.Unchanged)
                    db.Entry(entry).State = EntityState.Modified;
            }
            else
            {
                await db.Timers.AddAsync(entry);
            }
            await db.SaveChangesAsync();
        }

        #endregion

        
    }
}
