using System;
using System.Collections.Async;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThunderED.Helpers;
using ThunderED.Json.ZKill;

namespace ThunderED.Modules.Sub
{
    public class ZKillLiveFeedModule: AppModuleBase, IDisposable
    {
        public override LogCat Category => LogCat.KillFeed;

        public static List<Func<JsonZKill.ZKillboard, Task>> Queryables = new List<Func<JsonZKill.ZKillboard, Task>>();

        internal static JsonZKill.ZKillboard CurrentEntry;

        public override async Task Run(object prm)
        {
            if (IsRunning || Queryables.Count == 0) return;
            IsRunning = true;
            try
            {
                CurrentEntry = await APIHelper.ZKillAPI.GetRedisqResponce();
                if(CurrentEntry?.package == null ) return;
                await Queryables.ParallelForEachAsync(async q =>
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
                });
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

        public void Dispose()
        {
            Queryables.Clear();
        }
    }
}
