using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Radzen;
using ThunderED.Classes.Enums;
using ThunderED.Helpers;
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
                    q = db.Users.OrderBy(a=> a.Data).AsNoTracking().Where(a=> a.AuthState < 2);
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
                q = q.Where(args.Filter);
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

            var list =  await q.ToListAsync();
            list?.ForEach(a=> a.UnpackData());

            return new object[] {list, db.Users.Count()};
        }

        public static async Task<List<ulong>> GetUserDiscordIdsForAuthCheck(int count = 100)
        {
            await using var db = new ThunderedDbContext();
            var compareDate = DateTime.Now.AddHours(-1);
            return await db.Users.Where(a => !a.LastCheck.HasValue || a.LastCheck.Value <= compareDate).OrderBy(a => a.LastCheck)
                .Take(count).Select(a => a.DiscordId ?? 0).ToListAsync();
        }

        public static async Task<List<long>> GetUserIdsForAuthCheck(int count = 100)
        {
            await using var db = new ThunderedDbContext();
            var compareDate = DateTime.Now.AddHours(-1);
            return await db.Users.Where(a => !a.LastCheck.HasValue || a.LastCheck.Value <= compareDate).OrderBy(a => a.LastCheck)
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

        public static async Task<List<ThdToken>> GetTokens(TokenEnum type)
        {
            await using var db = new ThunderedDbContext();
            return await db.Tokens.AsNoTracking().Where(a => a.Type == type).ToListAsync();
        }

        public static async Task DeleteToken(long userId, TokenEnum type)
        {
            if (userId == 0) return;

            await using var db = new ThunderedDbContext();
            var t = await db.Tokens.Where(a => a.CharacterId == userId && a.Type == type).ToListAsync();
            t.ForEach(a => db.Tokens.Remove(a));
            await db.SaveChangesAsync();
        }

        public static async Task UpdateToken(string token, long characterId, TokenEnum type)
        {
            try
            {
                await using var db = new ThunderedDbContext();
                var entry = db.Tokens.FirstOrDefault(a => a.Type == type && a.CharacterId == characterId);
                if (entry == null)
                {
                    await db.Tokens.AddAsync(new ThdToken
                    {
                        CharacterId = characterId,
                        Token = token,
                        Type = type
                    });
                }
                else entry.Token = token;

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex);
                throw;
            }
        }

        public static async Task<string> GetToken(long charId, TokenEnum type)
        {
            await using var db = new ThunderedDbContext();
            return db.Tokens.FirstOrDefault(a => a.CharacterId == charId && a.Type == type)?.Token;
        }

        #endregion

        #region AuthUsers
        public static async Task<List<ThdAuthUser>> GetAuthUsers(UserStatusEnum type, bool includeToken = false, bool checkPermissions = false)
        {
            await using var db = new ThunderedDbContext();
            var req = db.Users.AsNoTracking().Where(a => a.AuthState == (int) type);
            if (includeToken)
                req = req.Include(a => a.Tokens);
            var list = await req.ToListAsync();
            list.ForEach(a=>
            {
                a?.UnpackData();
            });
            if (checkPermissions)
                list = list.Where(a => a.DataView.PermissionsList.Any()).ToList();
            return list;
        }

        public static async Task<List<ThdAuthUser>> GetAuthUsers(bool includeToken = false, bool checkPermissions = false)
        {
            await using var db = new ThunderedDbContext();
            var req = db.Users.AsNoTracking();
            if (includeToken)
                req = req.Include(a => a.Tokens);
            var list = await req.ToListAsync();
            list.ForEach(a =>
            {
                a?.UnpackData();
            });
            if (checkPermissions)
                list = list.Where(a => a.DataView.PermissionsList.Any()).ToList();
            return list;
        }

        public static async Task SaveAuthUser(ThdAuthUser user, string token = null)
        {
            await using var db = new ThunderedDbContext();
            user.PackData();
            db.Attach(user);
            if(db.Entry(user).State == EntityState.Unchanged)
                db.Entry(user).State = EntityState.Modified;
            if (!string.IsNullOrEmpty(token))
            {
                var old = db.Tokens.FirstOrDefault(a => a.CharacterId == user.CharacterId && a.Type == TokenEnum.General);
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
            var list = await db.Users.AsNoTracking().Where(a => string.IsNullOrEmpty(a.GroupName) || a.AuthState < 2).ToListAsync();
            list.ForEach(a=> a.UnpackData());
            return list;
        }

        public static async Task DeleteAuthUser(long characterId, bool deleteAlts = false)
        {
            await using var db = new ThunderedDbContext();
            var item = db.Users.FirstOrDefault(a => a.CharacterId == characterId);
            if (item != null)
            {
                db.Users.Remove(item);
                var tokens = db.Tokens.Where(a => a.CharacterId == characterId && a.Type == TokenEnum.General);
                db.Tokens.RemoveRange(tokens);

                var alts = await db.Users.Where(a => a.MainCharacterId == characterId).ToListAsync();
                if(alts.Any())
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
                var users = await db.Users.Where(a => a.MainCharacterId > 0 && a.MainCharacterId == user.CharacterId).ToListAsync();
                if(users.Any())
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
                result = await db.MiningLedgers.AsNoTracking().Where(a=> a.CitadelId == citadelId)
                    .OrderByDescending(a => a.Date).FirstOrDefaultAsync();
            else result = await db.MiningLedgers.AsNoTracking()
                .FirstOrDefaultAsync(a => a.CitadelId == citadelId && a.Date == null);
            result?.Unpack();

            return result;
        }

        #endregion

        #region Cache
        public static async Task<T> GetCache<T>(string cacheId, int minutes)
        {
            await using var db = new ThunderedDbContext();
            var now = DateTime.Now;
            var content =  (await db.Cache.AsNoTracking().FirstOrDefaultAsync(a => a.Id == cacheId && (now - a.LastUpdate).Minutes <= minutes))?.Content;
            return string.IsNullOrEmpty(content) ? (T)(object)null : JsonConvert.DeserializeObject<T>(content);
        }

        public static async Task UpdateCache(string cacheId, string content)
        {
            try
            {
                await using var db = new ThunderedDbContext();
                var entry = await db.Cache.FirstOrDefaultAsync(a => a.Id == cacheId);
                if (entry == null)
                    await db.Cache.AddAsync(new ThdCacheEntry {Id = cacheId, Content = content});
                else
                {
                    entry.Content = content;
                    entry.LastUpdate = DateTime.Now;
                    entry.LastAccess = DateTime.Now;
                }

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, LogCat.Database);
            }
        }
        #endregion
    }
}
