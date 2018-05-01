using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            try
            {
                var now = DateTime.Now;
                var today = DateTime.Today;

                var requestHandler = new ZkbRequestHandler(new JsonSerializer("yyyy-MM-dd HH:mm:ss"));
                var allyId = SettingsManager.GetSubList("killFeed", "groupsConfig").First()["allianceID"];
                var corpId = SettingsManager.GetSubList("killFeed", "groupsConfig").First()["corpID"];
                bool isAlliance = corpId == "0";
                var id = Convert.ToInt64(allyId == "0" ? corpId : allyId);

                if (commandText.ToLower() == "t" || commandText.ToLower() == "today" || commandText.ToLower() == "newday")
                {
                    var isNewDay = commandText.ToLower() == "newday";
                    var channel = SettingsManager.GetULong("config", "autoDailyStatsChannel");
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
                            $"{LM.Get("dailyStats")} ({date:dd.MM.yyyy}):\n{LM.Get("Killed")}:\t**{shipsDestroyed}** ({iskDestroyed:### ### ### ### ###} ISK)\n{LM.Get("Lost")}:\t**{shipsLost}** ({iskLost:### ### ### ### ###} ISK)";
                        await APIHelper.DiscordAPI.SendMessageAsync(APIHelper.DiscordAPI.GetChannel(channel), msg);
                    }
                    else
                    {
                        var msg =
                            $"{LM.Get("dailyStats")} ({date:dd.MM.yyyy}):\n{LM.Get("Killed")}:\t**{shipsDestroyed}** ({iskDestroyed:### ### ### ### ###} ISK)\n{LM.Get("Lost")}:\t**{shipsLost}** ({iskLost:### ### ### ### ###} ISK)";
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context, msg, true);
                    }
                }
                else
                {
                    var t = isAlliance ? "allianceID" : "corporationID";
                    var relPath = $"/api/stats/{t}/{id}/";
                    var result = await RequestAsync<ZkbStatResponse>(requestHandler, new Uri(new Uri("https://zkillboard.com"), relPath));
                    if (commandText.ToLower() == "month" || commandText.ToLower() == "m")
                    {
                        var data = result.Months.FirstOrDefault(a => a.Value.Year == now.Year && a.Value.Month == now.Month).Value;
                        if (data == null) return;
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context,
                            $"{string.Format(LM.Get("monthlyStats"), result.Info.Name)}**:\n{LM.Get("Killed")}:        \t**{data.ShipsDestroyed}** ({data.IskDestroyed:### ### ### ### ###} ISK)\n{LM.Get("Lost")}: \t**{data.ShipsLost}** ({data.IskLost:### ### ### ### ###} ISK)");
                    }
                    else if (commandText.ToLower().All(char.IsDigit))
                    {
                        var list = result.Months.Where(a => a.Value.Year.ToString() == commandText).ToList();
                        if (!list.Any()) return;
                        var shipsDestroyed = list.Sum(a => a.Value.ShipsDestroyed);
                        var shipsLost = list.Sum(a => a.Value.ShipsLost);
                        var iskDestroyed = list.Sum(a => a.Value.IskDestroyed);
                        var iskLost = list.Sum(a => a.Value.IskLost);
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context,
                            $"{string.Format(LM.Get("yearlyStats"), result.Info.Name, commandText)}:\n{LM.Get("Killed")}:        \t**{shipsDestroyed}** ({iskDestroyed:### ### ### ### ###} ISK)\n{LM.Get("Lost")}: \t**{shipsLost}** ({iskLost:### ### ### ### ###} ISK)");
                    }
                    else if (commandText.ToLower().Contains("/"))
                    {
                        var tmp = commandText.Split("/");
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
                            $"{string.Format(LM.Get("monthlyCustomStats"), result.Info.Name, commandText)}:\n{LM.Get("Killed")}:        \t**{shipsDestroyed}** ({iskDestroyed:### ### ### ### ###} ISK)\n{LM.Get("Lost")}: \t**{shipsLost}** ({iskLost:### ### ### ### ###} ISK)");
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
