using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ByteSizeLib;
using Discord;
using Discord.Commands;
using ThunderED.API;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Zkb;

namespace ThunderED.Modules
{
    public class ContinuousCheckModule: AppModuleBase
    {
        public override LogCat Category => LogCat.Continuous;

        private const StringComparison stringComparison = StringComparison.InvariantCultureIgnoreCase;
        private DateTime _checkOneSec = DateTime.Now;
        private DateTime _checkDailyPost = DateTime.Now;
        private static DateTime _30minTime = DateTime.Now;
        private static DateTime _lastCacheCheckDate = DateTime.Now;
        private static int _cacheInterval;

        private static bool? _IsTQOnline;
        private static bool _isTQOnlineRunning;
        private static bool _isPostingDailyStats;

        public override async Task Run(object prm)
        {
            if(IsRunning) return;
            IsRunning = true;
            try
            {
                var now = DateTime.Now;

                //onesec ops
                await OneSecOps(now);

                //30 min
                await ThirtyMinOps(now);

                //custom
                //purge unused cache from memory
                await Custom_CacheCheck(now).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("Continous", ex, Category);
            }
            finally
            {
                IsRunning = false;
            }

        }

        private async Task Custom_CacheCheck(DateTime now)
        {
            _cacheInterval = _cacheInterval != 0? _cacheInterval : SettingsManager.Settings.Config.CachePurgeInterval;
            if ((now - _lastCacheCheckDate).TotalMinutes >= _cacheInterval)
            {
                _lastCacheCheckDate = now;
                await LogHelper.LogInfo("Running cache purge...", LogCat.Tick);
                APIHelper.PurgeCache();
            }
        }

        private async Task ThirtyMinOps(DateTime now)
        {
            //skip wait for ALL inside
            if ((now - _30minTime).TotalMinutes >= 30)
            {
                //cache handling
                await ThirtyMin_MemoryCheck(now).ConfigureAwait(false);

                _30minTime = now;
            }
        }

        private async Task ThirtyMin_MemoryCheck(DateTime now)
        {
            try
            {
                var mem = SettingsManager.Settings.Config.MemoryUsageLimitMb;
                if (mem > 0)
                {
                    var size = ByteSize.FromBytes(Process.GetCurrentProcess().WorkingSet64);
                    if (size.MegaBytes > mem)
                    {
                        // APIHelper.ResetCache();
                        GC.Collect();
                    }
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("Cache handling", ex, Category);
            }
        }

        private async Task OneSecOps(DateTime now)
        {
            //skip wait for ALL inside
            if ((now - _checkOneSec).TotalSeconds >= 1)
            {      
                //display day stats on day change
                await OneSec_ReportDailyStatus(now);

                //TQ status post
               // await OneSec_TQStatusPost(now).ConfigureAwait(false);
                
                _checkOneSec = now;
            }
        }

        public static async Task OneSec_TQStatusPost(DateTime now)
        {
            if (!_IsTQOnline.HasValue)
                _IsTQOnline = TickManager.IsConnected;

            if (!_isTQOnlineRunning && SettingsManager.Settings.ContinousCheckModule.EnableTQStatusPost && SettingsManager.Settings.ContinousCheckModule.TQStatusPostChannels.Any() && _IsTQOnline != TickManager.IsConnected && !TickManager.IsESIUnreachable)
            {
                try
                {
                    _isTQOnlineRunning = true;
                    if (APIHelper.DiscordAPI.IsAvailable)
                    {
                        var msg = _IsTQOnline.Value ? $"{LM.Get("autopost_tq")} {LM.Get("Offline")}" : $"{LM.Get("autopost_tq")} {LM.Get("Online")}";
                        var color = _IsTQOnline.Value ? new Discord.Color(0xFF0000) : new Discord.Color(0x00FF00);
                        foreach (var channelId in SettingsManager.Settings.ContinousCheckModule.TQStatusPostChannels)
                        {
                            try
                            {
                                var embed = new EmbedBuilder().WithTitle(msg).WithColor(color);
                                await APIHelper.DiscordAPI.SendMessageAsync(channelId, SettingsManager.Settings.ContinousCheckModule.TQStatusPostMention, embed.Build()).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                await LogHelper.LogEx("Autopost - TQStatus", ex, LogCat.Continuous);
                            }
                        }

                        _IsTQOnline = TickManager.IsConnected;
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("OneSec_TQStatusPost", ex, LogCat.Continuous);
                }
                finally
                {
                    _isTQOnlineRunning = false;
                }
            }
        }

        private async Task OneSec_ReportDailyStatus(DateTime now)
        {
            var d = now.Date;
            if (!_isPostingDailyStats && _checkDailyPost.Date != d)
            {
                _checkDailyPost = now;
                _isPostingDailyStats = true;
                try
                {
                    await LogHelper.LogInfo("Running auto day stats post...", LogCat.Tick);
                    await Stats(null, "newday").ConfigureAwait(false);
                }
                finally
                {
                    _isPostingDailyStats = false;
                }
            }
        }

        public static async Task Stats(ICommandContext context, string commandText)
        {
            if(!SettingsManager.Settings.Config.ModuleStats) return;

            try
            {
                var comms = commandText.Split(' ').ToList();
                var isSingle = comms.Count == 1;
                var command = string.IsNullOrEmpty(commandText) ? "t" : comms[0].ToLower();
                var isNewDay = command.Equals("newday", stringComparison);
                if(!isNewDay && !SettingsManager.Settings.StatsModule.EnableStatsCommand)
                    return;

                string entity = null;
                if (!isSingle)
                {
                    comms.RemoveAt(0);
                    entity = string.Join(' ', comms);
                }

                if (isNewDay)
                {
                    foreach (var group in SettingsManager.Settings.StatsModule.GetEnabledGroups().Where(a=> !a.Value.IncludeInRating))
                    {
                        await ProcessStats(context, command, entity, group.Value);
                    }

                    var groups = SettingsManager.Settings.StatsModule.GetEnabledGroups().Values.Where(a => a.IncludeInRating);
                    if (groups.Any())
                        await ProcessStats(context, command, entity, null);
                }
                else
                {
                    if (command != "d" && command != "t" && command != "today" && command != "y" && command != "year" && command != "m" && command != "month" && 
                        command != "w"  && command != "week" && command != "lastweek" && command != "lw" && command != "lastday" && command != "ld" && !command.All(char.IsDigit) && !command.Contains('/') &&
                        command != "r" && command != "rating")
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("statUnknownCommandSyntax", SettingsManager.Settings.Config.BotDiscordCommandPrefix));
                        return;
                    }
                    await ProcessStats(context, command, entity, null);
                }



                
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, LogCat.Stats);
            }
        }

        private static async Task ProcessStats(ICommandContext context, string command, string entity, DailyStatsGroup grp)
        {
            var now = DateTime.Now;
            var today = DateTime.Today;

            var isNewDay = command.Equals("newday", stringComparison);
            var isRatingCommand = command.Equals("r", stringComparison) || command.Equals("rating",stringComparison);
            var requestHandler = new ZkbRequestHandler(new JsonSerializer("yyyy-MM-dd HH:mm:ss"));

            //daily rating
            if (isRatingCommand || (isNewDay && grp == null && SettingsManager.Settings.StatsModule.RatingModeChannelId > 0))
            {
                if (context != null)
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("dailyStatsWait"), false).ConfigureAwait(false);

                var groups = SettingsManager.Settings.StatsModule.GetEnabledGroups().Values.Where(a => a.IncludeInRating);
                var channel = isRatingCommand ? context.Channel.Id : SettingsManager.Settings.StatsModule.RatingModeChannelId;
                var to = (now.Add(TimeSpan.FromHours(1)));
                to = to.Subtract(TimeSpan.FromMinutes(to.Minute));
                var startTime = isNewDay ? today.Subtract(TimeSpan.FromDays(1)) : today;
                var endTime = isNewDay ? startTime.AddHours(24) : to;

                var list = new List<ZKillAPI.ZkillEntityStats>();

                foreach (var @group in groups)
                {
                    var data = await APIHelper.ZKillAPI.GetKillsLossesStats(@group.DailyStatsAlliance > 0 ? group.DailyStatsAlliance : group.DailyStatsCorp, group.DailyStatsAlliance > 0, startTime, endTime);
                    if (group.DailyStatsAlliance > 0)
                    {
                        var alliance = await APIHelper.ESIAPI.GetAllianceData(LogCat.Stats.ToString(), group.DailyStatsAlliance);
                        data.EntityName = $"{alliance?.name}[{alliance?.ticker}]";
                    }
                    else
                    {
                        if (group.DailyStatsCorp > 0)
                        {
                            var corp = await APIHelper.ESIAPI.GetCorporationData(LogCat.Stats.ToString(), group.DailyStatsCorp);
                            data.EntityName = $"{corp?.name}[{corp?.ticker}]";
                        }
                    }
                    list.Add(data);
                    //ZKB limitation one request per second
                    await Task.Delay(1000);
                }
                list = list.OrderByDescending(item =>
                {
                   /* var iskDiff = item.IskDestroyed - item.IskLost;
                    var isk = iskDiff / 1000000d;
                    var pts = (item.PointsDestroyed - item.PointsLost) / 10d;
                    var ships = item.ShipsDestroyed - item.ShipsLost;
                    isk = isk < 0 ? 0 : isk;
                    pts = pts < 0 ? 0 : pts;
                    ships = ships < 0 ? 0 : ships;
                    var avg = (3 * isk + 2*pts + 1 * ships + 1) / (isk + pts + ships + 1);*/
                    
                    var isk = item.IskDestroyed / (double)item.IskLost.ReturnMinimum(1);
                    var pts = item.PointsDestroyed / (double)item.PointsLost.ReturnMinimum(1);
                    var ships = item.ShipsDestroyed / (double)item.ShipsLost.ReturnMinimum(1);

                    var avg = (3 * isk + 2*ships + 1*pts + 1) / (isk + pts + ships + 1);

                  /*  isk = item.IskLost / 1000000d;
                    pts = item.PointsLost / 10d;
                    var avg2 = (2*pts + 1 * item.ShipsLost + 1) / (pts + item.ShipsLost + 1);

                    if (avg == 0 && avg2 == 0) return -1000;
                    
                    var result = avg - avg2;*/
                    return avg;

                }).ToList();

                var date = today.Subtract(TimeSpan.FromDays(1));
                var sb = new StringBuilder();
                sb.AppendLine("```css");
                sb.Append($"{"#".FixedLength(15)} {"ISK  ".FillSpacesBefore(8)} {LM.Get("dailyRatingColShips").FillSpacesBefore(5)} {LM.Get("dailyRatingColPoints").FillSpacesBefore(10)} {LM.Get("dailyRatingColSystem").FillSpacesBefore(15)}{Environment.NewLine}");
                sb.AppendLine("```");
                int count = 1;
                var topIskIndex = list.IndexOf(list.OrderByDescending(a => a.IskDestroyed).FirstOrDefault()) + 1;
                var topKillsIndex = list.IndexOf(list.OrderByDescending(a => a.ShipsDestroyed).FirstOrDefault()) + 1;
                var topPtsIndex = list.IndexOf(list.OrderByDescending(a => a.PointsDestroyed).FirstOrDefault()) + 1;
                foreach (var item in list)
                {
                    var addon = string.Empty;
                    if(count == 1)
                        addon += $"{LM.Get("dailyRatingTopEff")} / ";
                    if (count == topKillsIndex)
                        addon += $"{LM.Get("dailyRatingTopKill")} / ";
                    if (count == topIskIndex)
                        addon += $"{LM.Get("dailyRatingTopIsk")} / ";
                    if (count == topPtsIndex)
                        addon += $"{LM.Get("dailyRatingTopPts")} / ";
                    if (addon.EndsWith(" / "))
                        addon = addon.Substring(0, addon.Length - 3);

                    sb.Append(count.ToString().FixedLength(3));
                    sb.Append($"**{item.EntityName}** ");
                    if (!string.IsNullOrEmpty(addon))
                        sb.Append($"***{addon}***");
                    sb.Append(Environment.NewLine);
                    sb.AppendLine("```C");
                    sb.Append($"   {LM.Get("dailyRatingKilled")}:".FixedLength(15));
                    sb.Append(item.IskDestroyed.ToKMB().FillSpacesBefore(8));
                    sb.Append(item.ShipsDestroyed.ToString("N0").FillSpacesBefore(5));
                    sb.Append(item.PointsDestroyed.ToKMB().FillSpacesBefore(10));
                    sb.Append(item.MostSystems?.FillSpacesBefore(15));
                    sb.Append(Environment.NewLine);
                    sb.Append($"   {LM.Get("dailyRatingLost")}:".FixedLength(15));
                    sb.Append(item.IskLost.ToKMB().FillSpacesBefore(8));
                    sb.Append(item.ShipsLost.ToString("N0").FillSpacesBefore(5));
                    sb.Append(item.PointsLost.ToKMB().FillSpacesBefore(10));
                    sb.AppendLine("```");

                    count++;
                }
                var msg = $"**{LM.Get("dailyStatsRating", date.ToString(SettingsManager.Settings.Config.DateFormat))}**\n{sb}\n";
                  //  $"**{LM.Get("dailyStats", date, entity)}**\n{LM.Get("Killed")}:\t**{data.ShipsDestroyed}** ({data.IskDestroyed:n0} ISK)\n{LM.Get("Lost")}:\t**{data.ShipsLost}** ({data.IskLost:n0} ISK)";
                await APIHelper.DiscordAPI.SendMessageAsync(APIHelper.DiscordAPI.GetChannel(channel), msg).ConfigureAwait(false);
                
                return;
            }

            var allyId = grp?.DailyStatsAlliance ?? ((await APIHelper.ESIAPI.SearchAllianceId("Stats", entity))?.alliance?.FirstOrDefault() ?? 0);
            var corpId = grp?.DailyStatsCorp ?? (await APIHelper.ESIAPI.SearchCorporationId("Stats", entity))?.corporation?.FirstOrDefault() ?? 0;
            if (allyId == 0 && corpId == 0)
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("entryNotFound"), true).ConfigureAwait(false);           
                return;
            }

            var isAlliance = allyId > 0;
            var id = isAlliance ? allyId : corpId;
            entity = string.IsNullOrEmpty(entity) ? (isAlliance ? (await APIHelper.ESIAPI.GetAllianceData("Stats", id))?.name :(await APIHelper.ESIAPI.GetCorporationData("Stats", id))?.name) : entity;
            var dayCommands = new List<string> { "d", "t", "today", "newday" };
            if (dayCommands.Contains(command,StringComparer.InvariantCultureIgnoreCase))
            {
               /* var channel = grp?.DailyStatsChannel ?? 0;
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
                var iskDestroyed = kills.Where(a=> a).Sum(a => a.Stats.TotalValue);
                var iskLost = losses.Sum(a => a.Stats.TotalValue);*/
                var channel = grp?.DailyStatsChannel ?? 0;
                if(isNewDay && channel == 0) return;

                var to = (now.Add(TimeSpan.FromHours(1)));
                to = to.Subtract(TimeSpan.FromMinutes(to.Minute));
                var startTime = isNewDay ? today.Subtract(TimeSpan.FromDays(1)) : today;
                var endTime = isNewDay ? startTime.AddHours(24) : to;
                var data = await APIHelper.ZKillAPI.GetKillsLossesStats(id, isAlliance,startTime, endTime);

                var date = today;
                if (isNewDay)
                {
                    date = today.Subtract(TimeSpan.FromDays(1));
                    var msg =
                        GetMsg(LM.Get("dailyStats", date, entity), data.ShipsDestroyed, data.IskDestroyed, data.ShipsLost, data.IskLost);
                    await APIHelper.DiscordAPI.SendMessageAsync(APIHelper.DiscordAPI.GetChannel(channel), msg).ConfigureAwait(false);
                }
                else
                {
                    var msg = GetMsg(LM.Get("dailyStats", date, entity), data.ShipsDestroyed, data.IskDestroyed, data.ShipsLost, data.IskLost);
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, msg, true).ConfigureAwait(false);
                }
            }
            else
            {
                if (command.Equals("week", stringComparison) || command.Equals("w", stringComparison))
                {
                    var data = await APIHelper.ZKillAPI.GetKillsLossesStats(id, isAlliance, DateTime.UtcNow.StartOfWeek(DayOfWeek.Monday), DateTime.UtcNow);
                    var msg = GetMsg(LM.Get("statsCalendarWeekly", entity), data.ShipsDestroyed, data.IskDestroyed, data.ShipsLost, data.IskLost);
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, msg);
                    return;
                }
                if (command.Equals("lastweek", stringComparison) || command.Equals("lw", stringComparison))
                {
                    var data = await APIHelper.ZKillAPI.GetKillsLossesStats(id, isAlliance, null, null, 604800);
                    var msg = GetMsg(LM.Get("statsLastWeek", entity), data.ShipsDestroyed, data.IskDestroyed, data.ShipsLost, data.IskLost);
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, msg);
                    return;
                }
                if (command.Equals("lastday", stringComparison) || command.Equals("ld", stringComparison))
                {
                    var data = await APIHelper.ZKillAPI.GetKillsLossesStats(id, isAlliance, null, null, 86400);
                    var msg = GetMsg(LM.Get("statsLastDay", entity), data.ShipsDestroyed, data.IskDestroyed, data.ShipsLost, data.IskLost);
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, msg);
                    return;
                }


                var t = isAlliance ? "allianceID" : "corporationID";
                var relPath = $"/api/stats/{t}/{id}/";
                var result = await RequestAsync<ZkbStatResponse>(requestHandler, new Uri(new Uri("https://zkillboard.com"), relPath));

                if (command.Equals("month", stringComparison) || command.Equals("m", stringComparison))
                {
                    var data = result.Months.FirstOrDefault(a => a.Value.Year == now.Year && a.Value.Month == now.Month).Value;
                    if (data == null)
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("statNoDataFound"), true).ConfigureAwait(false);
                        return;
                    }
                    var msg = GetMsg(LM.Get("monthlyStats", entity), data.ShipsDestroyed, data.IskDestroyed, data.ShipsLost, data.IskLost);
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, msg);
                }else if (command.Equals("year", stringComparison) || command.Equals("y", stringComparison))
                {
                    var data = result.Months.FirstOrDefault(a => a.Value.Year == now.Year).Value;
                    if (data == null)
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("statNoDataFound"), true).ConfigureAwait(false);
                        return;
                    }
                    var msg = GetMsg(LM.Get("yearlyStats", result.Info.Name, now.Year), data.ShipsDestroyed, data.IskDestroyed, data.ShipsLost, data.IskLost);
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, msg);
                }
                else if (command.All(char.IsDigit))
                {
                    var list = result.Months.Where(a => a.Value.Year.ToString() == command).ToList();
                    if (!list.Any())
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("statNoDataFound"), true).ConfigureAwait(false);
                        return;
                    }
                    var shipsDestroyed = list.Sum(a => a.Value.ShipsDestroyed);
                    var shipsLost = list.Sum(a => a.Value.ShipsLost);
                    var iskDestroyed = list.Sum(a => a.Value.IskDestroyed);
                    var iskLost = list.Sum(a => a.Value.IskLost);
                    var msg = GetMsg(LM.Get("yearlyStats", result.Info.Name, now.Year), shipsDestroyed, iskDestroyed, shipsLost, iskLost);
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, msg);
                }
                else if (command.Contains("/"))
                {
                    var tmp = command.Split("/");
                    if (!tmp[0].All(char.IsDigit) || tmp[0].Length != 4)
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("statUnknownCommandSyntax", SettingsManager.Settings.Config.BotDiscordCommandPrefix), true).ConfigureAwait(false);           
                        return;
                    }

                    if (!tmp[1].All(char.IsDigit) || tmp[1].Length < 1 || tmp[1].Length > 2)
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("statUnknownCommandSyntax", SettingsManager.Settings.Config.BotDiscordCommandPrefix), true).ConfigureAwait(false);           
                        return;
                    }
                    var m = int.Parse(tmp[1]);
                    var list = result.Months.Where(a => a.Value.Year.ToString() == tmp[0] && a.Value.Month == m).ToList();
                    if (!list.Any())
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("statNoDataFound"), true).ConfigureAwait(false);
                        return;
                    }
                    var shipsDestroyed = list.Sum(a => a.Value.ShipsDestroyed);
                    var shipsLost = list.Sum(a => a.Value.ShipsLost);
                    var iskDestroyed = list.Sum(a => a.Value.IskDestroyed);
                    var iskLost = list.Sum(a => a.Value.IskLost);
                    var msg = GetMsg(LM.Get("monthlyCustomStats", result.Info.Name, now.Year), shipsDestroyed, iskDestroyed, shipsLost, iskLost);
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, msg);
                }
            }
        }

        private static string GetMsg(string header, int shipsDestroyed, long iskDestroyed, int shipsLost, long iskLost)
        {
            double iskTotal = iskLost + iskDestroyed;
            double shipTotal = shipsLost + shipsDestroyed;
            var iskEfficiency = iskTotal == 0 ? 0 : iskDestroyed / iskTotal * 100;
            var shipEfficiency = shipTotal == 0 ? 0 : shipsDestroyed / shipTotal * 100;
            var stringArray = new string[] {
                $"**{header}**",
                $"{LM.Get("Killed")}:\t**{shipsDestroyed}** ({iskDestroyed:n0} ISK)",
                $"{LM.Get("Lost")}:\t**{shipsLost}** ({iskLost:n0} ISK)",
                $"{LM.Get("iskEfficiency")}:\t**{iskEfficiency:n0}%**",
                $"{LM.Get("shipEfficiency")}:\t**{shipEfficiency:n0}%**"
            };
            return String.Join("\n", stringArray);
        }

        private static Task<T> RequestAsync<T>(ZkbRequestHandler h, Uri uri) {
            return h.RequestAsync<T>(uri);
        }
    }
}
