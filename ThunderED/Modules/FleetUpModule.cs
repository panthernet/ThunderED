using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json.FleetUp;

namespace ThunderED.Modules
{
    public class FleetUpModule: AppModuleBase
    {
        public override LogCat Category => LogCat.FleetUp;

        private static DateTime? _lastChecked;

        public override async Task Run(object prm)
        {
            await FleetUp();
        }

        private static async Task SendMessage(JsonFleetup.Datum operation, IMessageChannel channel, string message, bool addReactions)
        {
            var name = operation.Subject;
            var startTime = operation.Start;
            var location = operation.Location;
            var details = operation.Details;

            var format = SettingsManager.Settings.Config.TimeFormat ?? "dd.MM.yyyy HH:mm";

            var url = $"http://fleet-up.com/Operation#{operation.OperationId}";
            var locationText = $"[{ location}](http://evemaps.dotlan.net/system/{location})";
            var builder = new EmbedBuilder()
                .WithUrl(url)
                .WithColor(new Color(0x7CB0D0))
                .WithTitle($"{name}")
                .WithThumbnailUrl("http://fleet-up.com/Content/Images/logo_title.png")
                .WithAuthor(author =>
                {
                    author
                        .WithName(LM.Get("fuNotification"));
                })
                .AddInlineField(LM.Get("fuFormUpTime"), startTime.ToString(format))
                .AddInlineField(LM.Get("fuFormUpSystem"), string.IsNullOrWhiteSpace(location) ? LM.Get("None") : locationText)
                .AddField(LM.Get("Details"), string.IsNullOrWhiteSpace(details) ? LM.Get("None") : details)
                .WithFooter($"EVE Time: {DateTime.UtcNow.ToString(format)}")
                .WithTimestamp(DateTime.UtcNow);



            var embed = builder.Build();
            var sendres = await APIHelper.DiscordAPI.SendMessageAsync(channel, message, embed);
            await LogHelper.LogInfo($"Posting Fleetup OP {name} ({operation.OperationId})", LogCat.FleetUp);
            if (addReactions)
            {

                await sendres.AddReactionAsync(new Emoji("✅"));
                await sendres.AddReactionAsync(new Emoji("❔"));
                await sendres.AddReactionAsync(new Emoji("❌"));
            }
        }

        public async Task FleetUp()
        {
            if(IsRunning) return;
            IsRunning = true;
            try
            {
                //Check Fleetup Operations

                if (_lastChecked == null)
                {
                    var dateStr = await SQLHelper.SQLiteDataQuery<string>("cacheData", "data", "name", "fleetUpLastChecked");
                    _lastChecked = DateTime.TryParseExact(dateStr,
                        new[]
                        {
                            "dd.MM.yyyy HH:mm:ss", $"{CultureInfo.InvariantCulture.DateTimeFormat.ShortDatePattern} {CultureInfo.InvariantCulture.DateTimeFormat.LongTimePattern}"
                        },
                        CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.None, out var time) ? time : DateTime.MinValue;
                }

                if (DateTime.Now > _lastChecked.Value.AddMinutes(1))
                {
                    var userId = SettingsManager.Settings.FleetupModule.UserId;
                    var apiCode = SettingsManager.Settings.FleetupModule.APICode;
                    var appKey = SettingsManager.Settings.FleetupModule.AppKey;
                    var groupID = SettingsManager.Settings.FleetupModule.GroupID;
                    var channelid = SettingsManager.Settings.FleetupModule.Channel;
                    var lastopid = await SQLHelper.SQLiteDataQuery<string>("cacheData", "data", "name", "fleetUpLastPostedOperation");
                    var announcePost = SettingsManager.Settings.FleetupModule.Announce_Post;
                    var channel = channelid == 0 ? null : APIHelper.DiscordAPI.GetChannel(channelid);

                    _lastChecked = DateTime.Now;
                    await SQLHelper.SQLiteDataUpdate("cacheData", "data", _lastChecked.Value.ToString(CultureInfo.InvariantCulture), "name", "fleetUpLastChecked");

                    if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(apiCode) || string.IsNullOrWhiteSpace(groupID) || string.IsNullOrWhiteSpace(appKey)
                        || channel == null)
                    {
                        await LogHelper.LogInfo(LM.Get("fuNeedSetup"), Category);
                        return;
                    }

                    var result = await APIHelper.FleetUpAPI.GetOperations(Reason, userId, apiCode, appKey, groupID);
                    if (result == null)
                        return;

                    foreach (var operation in result.Data)
                    {
                        var lastAnnounce = await SQLHelper.SQLiteDataQuery<int>("fleetup", "announce", "id", operation.Id.ToString());

                        if (operation.OperationId > Convert.ToInt32(lastopid) && announcePost)
                        {
                            await SendMessage(operation, channel, $"@everyone FleetUp Op <http://fleet-up.com/Operation#{operation.OperationId}>", true);
                            await SQLHelper.SQLiteDataUpdate("cacheData", "data", operation.OperationId.ToString(), "name", "fleetUpLastPostedOperation");
                        }

                        var timeDiff = TimeSpan.FromTicks(operation.Start.Ticks - DateTime.UtcNow.Ticks);
                        //no need to notify, it is already started
                        if (lastAnnounce == 0 && timeDiff.TotalMinutes < 1)
                            continue;
                        var array = SettingsManager.Settings.FleetupModule.Announce.Where(a=> a < lastAnnounce || lastAnnounce == 0).ToArray();

                        foreach (var i in array)
                        {
                            var epic1 = TimeSpan.FromMinutes(i);
                            var epic2 = TimeSpan.FromMinutes(i + 1);

                            if (timeDiff >= epic1 && timeDiff <= epic2)
                            {
                                await SendMessage(operation, channel, $"@everyone {LM.Get("fuFormIn", i, $"http://fleet-up.com/Operation#{operation.OperationId}")}",
                                    false);
                                await SQLHelper.SQLiteDataInsertOrUpdate("fleetup", new Dictionary<string, object>
                                {
                                    { "id", operation.Id.ToString()},
                                    { "announce", i}
                                });
                            }
                        }

                        //NOW
                        if (timeDiff.TotalMinutes < 1)
                        {
                            await SendMessage(operation, channel, $"@everyone {LM.Get("fuFormNow", $"http://fleet-up.com/Operation#{operation.OperationId}")}",
                                false);
                            await SQLHelper.SQLiteDataDelete("fleetup", "id", operation.Id.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx($"ERROR {ex.Message}", ex, Category);
            }
            finally
            {
                IsRunning = false;
            }
        }

       // private Dictionary<string, List<FleetUpCache> _cache = new Dictionary<string, FleetUpCache>();



        public static async Task Ops(ICommandContext context, string x)
        {
            try
            {
                var Reason = LogCat.FleetUp.ToString();
                int.TryParse(x, out var amount);
                var userId = SettingsManager.Settings.FleetupModule.UserId;
                var apiCode = SettingsManager.Settings.FleetupModule.APICode;
                var groupID = SettingsManager.Settings.FleetupModule.GroupID;
                var appKey = SettingsManager.Settings.FleetupModule.AppKey;

                var result = await APIHelper.FleetUpAPI.GetOperations(Reason, userId, apiCode, appKey, groupID);

                if (!result.Data.Any())
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, $"No Ops Scheduled", true);
                }
                else if (amount == 0)
                {
                    var operation = result.Data[0];
                    await SendMessage(operation, context.Message.Channel,$"FleetUp Op <http://fleet-up.com/Operation#{operation.OperationId}>)", false);
                }
                else
                {
                    var count = 0;
                    foreach (var operation in result.Data)
                    {
                        if (count >= amount) continue;
                        await SendMessage(operation, context.Message.Channel, $"FleetUp Op <http://fleet-up.com/Operation#{operation.OperationId}>)", false);
                        count++;
                    }
                }

            }
            catch (Exception ex)
            {
                await LogHelper.LogEx($"ERROR In Fleetup OPS {ex.Message}", ex, LogCat.FleetUp);
            }
        }
    }

    internal class FleetUpCache
    {
        //public List<string>
    }
}
