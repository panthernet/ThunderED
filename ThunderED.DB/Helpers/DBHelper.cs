using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Radzen;
using ThunderED.Classes.Entities;
using ThunderED.Classes.Enums;
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
                .Take(count).Select(a => a.DiscordId).ToListAsync();
        }

        public static async Task<List<long>> GetUserIdsForAuthCheck(int count = 100)
        {
            await using var db = new ThunderedDbContext();
            var compareDate = DateTime.Now.AddHours(-1);
            return await db.Users.Where(a => !a.LastCheck.HasValue || a.LastCheck.Value <= compareDate).OrderBy(a => a.LastCheck)
                .Take(count).Select(a => a.CharacterId).ToListAsync();
        }

        public static async Task UpdateToken(string token, long characterId, TokenEnum type)
        {
            throw new NotImplementedException();
        }
    }
}
