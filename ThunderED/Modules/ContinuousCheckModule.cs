using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ByteSizeLib;
using Discord;
using Discord.Commands;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Zkb;

namespace ThunderED.Modules
{
    public class ContinuousCheckModule: AppModuleBase
    {
        public override LogCat Category => LogCat.Continuous;

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
                var isNewDay = command == "newday";
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
                    foreach (var group in SettingsManager.Settings.StatsModule.DailyStatsGroups.Values)
                    {
                        await ProcessStats(context, command, entity, group);
                    }
                }
                else
                {
                    if (command != "d" && command != "t" && command != "today" && command != "y" && command != "year" && command != "m" && command != "month" && 
                        command != "w"  && command != "week" && command != "lastweek" && command != "lw" && command != "lastday" && command != "ld" && !command.All(char.IsDigit) && !command.Contains('/'))
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

            var isNewDay = command == "newday";
            var requestHandler = new ZkbRequestHandler(new JsonSerializer("yyyy-MM-dd HH:mm:ss"));

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

            if (command == "d" || command == "t" || command== "today" || command== "newday")
            {
                //TODO update with GetKillsLossesStats
                var channel = grp?.DailyStatsChannel ?? 0;
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
                if (command == "week" || command == "w")
                {
                    var data = await APIHelper.ZKillAPI.GetKillsLossesStats(id, isAlliance, DateTime.UtcNow.StartOfWeek(DayOfWeek.Monday), DateTime.UtcNow);
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context,
                        $"\n**{LM.Get("statsCalendarWeekly", entity)}**\n{LM.Get("Killed")}:\t**{data.ShipsDestroyed}** ({data.IskDestroyed:n0} ISK)\n{LM.Get("Lost")}:\t**{data.ShipsLost}** ({data.IskLost:n0} ISK)");
                    return;
                }
                if (command == "lastweek" || command == "lw")
                {
                    var data = await APIHelper.ZKillAPI.GetKillsLossesStats(id, isAlliance, null, null, 604800);
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context,
                        $"\n**{LM.Get("statsLastWeek", entity)}**\n{LM.Get("Killed")}:\t**{data.ShipsDestroyed}** ({data.IskDestroyed:n0} ISK)\n{LM.Get("Lost")}:\t**{data.ShipsLost}** ({data.IskLost:n0} ISK)");
                    return;
                }
                if (command == "lastday" || command == "ld")
                {
                    var data = await APIHelper.ZKillAPI.GetKillsLossesStats(id, isAlliance, null, null, 86400);
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context,
                        $"\n**{LM.Get("statsLastDay", entity)}**\n{LM.Get("Killed")}:\t**{data.ShipsDestroyed}** ({data.IskDestroyed:n0} ISK)\n{LM.Get("Lost")}:\t**{data.ShipsLost}** ({data.IskLost:n0} ISK)");
                    return;
                }


                var t = isAlliance ? "allianceID" : "corporationID";
                var relPath = $"/api/stats/{t}/{id}/";
                var result = await RequestAsync<ZkbStatResponse>(requestHandler, new Uri(new Uri("https://zkillboard.com"), relPath));

                if (command == "month" || command == "m")
                {
                    var data = result.Months.FirstOrDefault(a => a.Value.Year == now.Year && a.Value.Month == now.Month).Value;
                    if (data == null)
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("statNoDataFound"), true).ConfigureAwait(false);
                        return;
                    }
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context,
                        $"**{LM.Get("monthlyStats", result.Info.Name)}**\n{LM.Get("Killed")}:\t**{data.ShipsDestroyed}** ({data.IskDestroyed:n0} ISK)\n{LM.Get("Lost")}:\t**{data.ShipsLost}** ({data.IskLost:n0} ISK)");
                }else if (command == "year" || command == "y")
                {
                    var data = result.Months.FirstOrDefault(a => a.Value.Year == now.Year).Value;
                    if (data == null)
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("statNoDataFound"), true).ConfigureAwait(false);
                        return;
                    }
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context,
                        $"**{LM.Get("yearlyStats", result.Info.Name, now.Year)}**\n{LM.Get("Killed")}:\t**{data.ShipsDestroyed}** ({data.IskDestroyed:n0} ISK)\n{LM.Get("Lost")}:\t**{data.ShipsLost}** ({data.IskLost:n0} ISK)");
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
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context,
                        $"**{LM.Get("yearlyStats", result.Info.Name, command)}**\n{LM.Get("Killed")}:\t**{shipsDestroyed}** ({iskDestroyed:n0} ISK)\n{LM.Get("Lost")}:\t**{shipsLost}** ({iskLost:n0} ISK)");
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
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context,
                        $"**{LM.Get("monthlyCustomStats", result.Info.Name, command)}**\n{LM.Get("Killed")}:\t**{shipsDestroyed}** ({iskDestroyed:n0} ISK)\n{LM.Get("Lost")}:\t**{shipsLost}** ({iskLost:n0} ISK)");
                }
            }
        }

        
        private static Task<T> RequestAsync<T>(ZkbRequestHandler h, Uri uri) {
            return h.RequestAsync<T>(uri);
        }
    }
}
