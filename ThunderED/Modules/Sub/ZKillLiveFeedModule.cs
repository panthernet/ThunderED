using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json.ZKill;

namespace ThunderED.Modules.Sub
{
    public class ZKillLiveFeedModule: AppModuleBase, IDisposable
    {
        public override LogCat Category => LogCat.KillFeed;
        public static List<Func<JsonZKill.Killmail, Task>> Queryables = new List<Func<JsonZKill.Killmail, Task>>();
        private readonly List<long> _receivedIds = new List<long>();

        
        private static readonly ConcurrentQueue<long> SharedIdPool = new ConcurrentQueue<long>();
        /// <summary>
        /// Update pool with new ID
        /// </summary>
        /// <param name="id">ID</param>
        internal static void UpdateSharedIdPool(long id)
        {
            if (SharedIdPool.Contains(id)) return;
            SharedIdPool.Enqueue(id);
            if (SharedIdPool.Count > 30)
                SharedIdPool.TryDequeue(out _);
        }

        internal static bool IsInSharedPool(long id)
        {
            return SharedIdPool.Contains(id);
        }

        public ZKillLiveFeedModule()
        {
            Settings.ZKBSettingsModule.OldKMDaysThreshold = Settings.ZKBSettingsModule.OldKMDaysThreshold < 0
                ? 0
                : Settings.ZKBSettingsModule.OldKMDaysThreshold;
        }

        public override async Task Run(object prm)
        {
            if (IsRunning || Queryables.Count == 0 || Program.IsClosing) return;
            var minus = Settings.ZKBSettingsModule.OldKMDaysThreshold == 0 ? DateTime.Now : DateTime.Now.Subtract(TimeSpan.FromDays(Settings.ZKBSettingsModule.OldKMDaysThreshold));
            try
            {
                IsRunning = true;
                if (TickManager.IsNoConnection || !Queryables.Any()) return;
                JsonZKill.Killmail entry = null;

                if (Settings.ZKBSettingsModule.UseSocketsForZKillboard)
                {
                    var currentEntry = await APIHelper.ZKillAPI.GetSocketResponce();
                    if (currentEntry == null) return;
                    if (!IsUniqueId(currentEntry.killmail_id)) return;
                    entry = currentEntry;
                }
                else
                {
                    var currentEntry =  await APIHelper.ZKillAPI.GetRedisqResponce();
                    if (currentEntry?.package == null) return;
                    if (!IsUniqueId(currentEntry.package.killID)) return;
                    currentEntry.package.killmail.zkb = currentEntry.package.zkb;
                    entry = currentEntry.package.killmail;
                }

                //do the minus days check
                if(entry.killmail_time < minus)
                    return;

                foreach (var q in Queryables)
                {
                    await q(entry).ConfigureAwait(false);
                }

            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private bool IsUniqueId(long id)
        {
            if (_receivedIds.Contains(id)) return false;
            _receivedIds.Add(id);
            if(_receivedIds.Count > 20)
                _receivedIds.RemoveAt(0);
            return true;
        }

        public void Dispose()
        {
            Queryables.Clear();
        }
    }
}