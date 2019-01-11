using System;
using System.Collections.Async;
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
        public static List<Func<JsonZKill.ZKillboard, Task>> Queryables = new List<Func<JsonZKill.ZKillboard, Task>>();
        internal static JsonZKill.ZKillboard CurrentEntry;
        private readonly List<long> _receivedIds = new List<long>();

        public override async Task Run(object prm)
        {
            if (IsRunning || Queryables.Count == 0) return;
            IsRunning = true;
            try
            {
                if(TickManager.IsNoConnection || !Queryables.Any()) return;

                CurrentEntry = await APIHelper.ZKillAPI.GetRedisqResponce();
                if(CurrentEntry?.package == null ) return;
                if(!IsUniqueId(CurrentEntry.package.killID)) return;

                foreach (var q in Queryables)
                {
                    await q(CurrentEntry).ConfigureAwait(false);
                }

               /* await Queryables.ParallelForEachAsync(async q =>
                {
                    try
                    {
                        await q(CurrentEntry);
                    }
                    catch (Exception ex)
                    {
                        await LogHelper.LogEx(ex.Message, ex, Category);
                        await LogHelper.LogWarning($"[ZKillCore] error processing {q.Method.Name}! Msg: {ex.Message}", Category);
                    }
                });*/
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