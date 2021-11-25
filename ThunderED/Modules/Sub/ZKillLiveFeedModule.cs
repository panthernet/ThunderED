using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Dasync.Collections;

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

        private ConcurrentQueue<JsonZKill.Killmail> _kmPool = new ConcurrentQueue<JsonZKill.Killmail>();

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

        public override async Task Initialize()
        {
            Settings.ZKBSettingsModule.OldKMDaysThreshold = Settings.ZKBSettingsModule.OldKMDaysThreshold < 0
                ? 0
                : Settings.ZKBSettingsModule.OldKMDaysThreshold;

            if (string.IsNullOrEmpty(SettingsManager.Settings.ZKBSettingsModule.ZKillboardWebSocketUrl))
            {
                await LogHelper.LogWarning($"ZKB socket param `ZKillboardWebSocketUrl` is not specified! Set it and restart the bot.",
                    LogCat.ZKill);
                Settings.Config.ModuleLiveKillFeed = false;
            }
        }

        //private volatile bool _threadisRunning = false;

        public override async Task Run(object prm)
        {
            if (IsRunning || Queryables.Count == 0 || Program.IsClosing || !APIHelper.IsDiscordAvailable) return;
            if(!Settings.Config.ModuleLiveKillFeed) return;

            if (TickManager.IsESIUnreachable) return; //TODO pooling

            var minus = Settings.ZKBSettingsModule.OldKMDaysThreshold == 0 ? DateTime.Now : DateTime.Now.Subtract(TimeSpan.FromDays(Settings.ZKBSettingsModule.OldKMDaysThreshold));
            try
            {
                IsRunning = true;
                if (!Queryables.Any()) return;
               // JsonZKill.Killmail entry = null;

                /*  if (Settings.ZKBSettingsModule.UseSocketsForZKillboard)
                {*/
                do
                {
                    var currentEntry = await APIHelper.ZKillAPI.GetSocketResponce();
                    if (currentEntry == null) break;
                    if (!IsUniqueId(currentEntry.killmail_id)) continue;
                    //do the minus days check
                    if (currentEntry.killmail_time < minus) continue;

                    _kmPool.Enqueue(currentEntry);
                    if (TickManager.IsConnected && !TickManager.IsESIUnreachable)
                        if (_kmPool.Count >= 10)
                            break;
                } while (true);
                /* }
                else
                {
                    var currentEntry =  await APIHelper.ZKillAPI.GetRedisqResponce();
                    if (currentEntry?.package == null) return;
                    if (!IsUniqueId(currentEntry.package.killID)) return;
                    currentEntry.package.killmail.zkb = currentEntry.package.zkb;
                    entry = currentEntry.package.killmail;
                }*/

                //skip on comms outage
                if (TickManager.IsESIUnreachable || TickManager.IsNoConnection)
                    return;

                //if(_threadisRunning) return;

                //use awaitable threads if pool is big
               /* if (_kmPool.Count > 15)
                {
                    _threadisRunning = true;

                    await Queryables.ParallelForEachAsync(async q =>
                    {
                        var hasItems = true;
                        do
                        {
                            hasItems = _kmPool.TryDequeue(out var entry);
                            if (hasItems)
                                await q(entry);
                        } while (hasItems);
                    }, SettingsManager.MaxConcurrentThreads);

                    _threadisRunning = false;
                }
                else
                {*/
                    var hasItems = true;
                    do
                    {
                        hasItems = _kmPool.TryDequeue(out var entry);
                        if (!hasItems) continue;
                        foreach (var q in Queryables)
                            await q(entry).ConfigureAwait(false);
                    } while (hasItems);
               // }
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