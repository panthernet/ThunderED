using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Zkb;

namespace ThunderED.Modules.Static
{
    internal class StatsModule: AppModuleBase
    {
        public override LogCat Category => LogCat.Stats;

        public static async Task Stats(ICommandContext context, string commandText)
        {
            if(!SettingsManager.Settings.Config.ModuleStats) return;

            try
            {
                var now = DateTime.Now;
                var today = DateTime.Today;
                var comms = commandText.Split(' ').ToList();
                var isSingle = comms.Count == 1;
                var command = string.IsNullOrEmpty(commandText) ? "t" : comms[0].ToLower();
                string entity = null;
                if (!isSingle)
                {
                    comms.RemoveAt(0);
                    entity = string.Join(' ', comms);
                }

                var requestHandler = new ZkbRequestHandler(new JsonSerializer("yyyy-MM-dd HH:mm:ss"));
                //TODO introduce own variables and may be even settings section
                var allyId = SettingsManager.Settings.StatsModule.AutodailyStatsDefaultAlliance;
                if(!string.IsNullOrEmpty(entity))
                    allyId = (await APIHelper.ESIAPI.SearchAllianceId("Stats", entity))?.alliance?.FirstOrDefault() ?? 0;
                var corpId = SettingsManager.Settings.StatsModule.AutoDailyStatsDefaultCorp;
                if(!string.IsNullOrEmpty(entity) && allyId == 0)
                    corpId = (await APIHelper.ESIAPI.SearchCorporationId("Stats", entity))?.corporation.FirstOrDefault() ?? 0;
                if(allyId == 0 && corpId == 0)
                    return;

                bool isAlliance = corpId == 0;
                var id = isAlliance ? allyId : corpId;
                entity = string.IsNullOrEmpty(entity) ? (isAlliance ? (await APIHelper.ESIAPI.GetAllianceData("Stats", id))?.name :(await APIHelper.ESIAPI.GetCorporationData("Stats", id))?.name) : entity;

                if (command == "t" || command== "today" || command== "newday")
                {
                    var isNewDay = command == "newday";
                    var channel = SettingsManager.Settings.StatsModule.AutoDailyStatsChannel;
                    if(isNewDay && channel == 0) return;
                    var to = now.Add(TimeSpan.FromHours(1));
                    to = to.Subtract(TimeSpan.FromMinutes(to.Minute));
                    var startTime = isNewDay ? today.Subtract(TimeSpan.FromDays(1)) : today;
                    var endTime = isNewDay ? startTime.AddHours(24) : to;
                    var options = new ZKillboardOptions
                    {
                        StartTime = startTime,
                        EndTime = endTime,
                    };
                    if (isAlliance)
                        options.AllianceId = new List<long> {id};
                    else options.CorporationId = new List<long> {id};

                    string relPath = "/api/losses";
                    relPath = options.GetQueryString(relPath);
                    var losses = await RequestAsync<List<ZkbResponse.ZkbKill>>(requestHandler, new Uri(new Uri("https://zkillboard.com"), relPath)) ??
                                 new List<ZkbResponse.ZkbKill>();
                    relPath = "/api/kills";
                    relPath = options.GetQueryString(relPath);
                    var kills = await RequestAsync<List<ZkbResponse.ZkbKill>>(requestHandler, new Uri(new Uri("https://zkillboard.com"), relPath)) ??
                                new List<ZkbResponse.ZkbKill>();
                    var shipsDestroyed = kills.Count;
                    var shipsLost = losses.Count;
                    var iskDestroyed = kills.Sum(a => a.Stats.TotalValue);
                    var iskLost = losses.Sum(a => a.Stats.TotalValue);
                    var date = today;
                    if (isNewDay)
                    {
                        date = today.Subtract(TimeSpan.FromDays(1));
                        var msg =
                            $"**{LM.Get("dailyStats", date, entity)}**\n{LM.Get("Killed")}:\t**{shipsDestroyed}** ({iskDestroyed:n0} ISK)\n{LM.Get("Lost")}:\t**{shipsLost}** ({iskLost:n0} ISK)";
                        await APIHelper.DiscordAPI.SendMessageAsync(APIHelper.DiscordAPI.GetChannel(channel), msg).ConfigureAwait(false);
                    }
                    else
                    {
                        var msg =
                            $"**{LM.Get("dailyStats", date, entity)}**\n{LM.Get("Killed")}:\t**{shipsDestroyed}** ({iskDestroyed:n0} ISK)\n{LM.Get("Lost")}:\t**{shipsLost}** ({iskLost:n0} ISK)";
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context, msg, true).ConfigureAwait(false);
                    }
                }
                else
                {
                    var t = isAlliance ? "allianceID" : "corporationID";
                    var relPath = $"/api/stats/{t}/{id}/";
                    var result = await RequestAsync<ZkbStatResponse>(requestHandler, new Uri(new Uri("https://zkillboard.com"), relPath));
                    if (command == "month" || command == "m")
                    {
                        var data = result.Months.FirstOrDefault(a => a.Value.Year == now.Year && a.Value.Month == now.Month).Value;
                        if (data == null) return;
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context,
                            $"**{LM.Get("monthlyStats", result.Info.Name)}**\n{LM.Get("Killed")}:\t**{data.ShipsDestroyed}** ({data.IskDestroyed:n0} ISK)\n{LM.Get("Lost")}:\t**{data.ShipsLost}** ({data.IskLost:n0} ISK)");
                    }
                    else if (command.All(char.IsDigit))
                    {
                        var list = result.Months.Where(a => a.Value.Year.ToString() == command).ToList();
                        if (!list.Any()) return;
                        var shipsDestroyed = list.Sum(a => a.Value.ShipsDestroyed);
                        var shipsLost = list.Sum(a => a.Value.ShipsLost);
                        var iskDestroyed = list.Sum(a => a.Value.IskDestroyed);
                        var iskLost = list.Sum(a => a.Value.IskLost);
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context,
                            $"**{LM.Get("yearlyStats", result.Info.Name, command)}**\n{LM.Get("Killed")}:\t**{shipsDestroyed}** ({iskDestroyed:n0} ISK)\n{LM.Get("Lost")}:\t**{shipsLost}** ({iskLost:n0} ISK)");
                    }
                    else if (command.Contains("/"))
                    {
                        var tmp = command.Split("/");
                        if (!tmp[0].All(char.IsDigit) || tmp[0].Length != 4) return;
                        if (!tmp[1].All(char.IsDigit) || tmp[1].Length < 1 || tmp[1].Length > 2) return;
                        var m = int.Parse(tmp[1]);
                        var list = result.Months.Where(a => a.Value.Year.ToString() == tmp[0] && a.Value.Month == m).ToList();
                        if (!list.Any()) return;
                        var shipsDestroyed = list.Sum(a => a.Value.ShipsDestroyed);
                        var shipsLost = list.Sum(a => a.Value.ShipsLost);
                        var iskDestroyed = list.Sum(a => a.Value.IskDestroyed);
                        var iskLost = list.Sum(a => a.Value.IskLost);
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context,
                            $"**{LM.Get("monthlyCustomStats", result.Info.Name, command)}**\n{LM.Get("Killed")}:\t**{shipsDestroyed}** ({iskDestroyed:n0} ISK)\n{LM.Get("Lost")}:\t**{shipsLost}** ({iskLost:n0} ISK)");
                    }
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, LogCat.Stats);
            }
        }

        
        private static Task<T> RequestAsync<T>(ZkbRequestHandler h, Uri uri) {
            return h.RequestAsync<T>(uri);
        }
    }
}
