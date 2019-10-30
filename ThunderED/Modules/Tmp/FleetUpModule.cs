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
            if(IsRunning) return;
            IsRunning = true;
            try
            {
                await FleetUp();
            }
            finally
            {
                IsRunning = false;
            }
        }

        private static async Task SendMessage(JsonFleetup.Datum operation, IMessageChannel channel, string message, bool addReactions)
        {
            var name = operation.Subject;
            var startTime = operation.Start;
            var location = operation.Location;
            var details = operation.Details;

            var format = SettingsManager.Settings.Config.ShortTimeFormat ?? "dd.MM.yyyy HH:mm";

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
                .AddField(LM.Get("fuFormUpTime"), startTime.ToString(format), true)
                .AddField(LM.Get("fuFormUpSystem"), string.IsNullOrWhiteSpace(location) ? LM.Get("None") : locationText, true)
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

        protected async Task FleetUp()
        {
            try
            {
                //Check Fleetup Operations

                if (_lastChecked == null)
                {
                    var dateStr = await SQLHelper.GetCacheDataFleetUpLastChecked();
                    _lastChecked = DateTime.TryParseExact(dateStr,
                        new[]
                        {
                            "dd.MM.yyyy HH:mm:ss", $"{CultureInfo.InvariantCulture.DateTimeFormat.ShortDatePattern} {CultureInfo.InvariantCulture.DateTimeFormat.LongTimePattern}"
                        },
                        CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.None, out var time) ? time : DateTime.MinValue;
                }

                if (DateTime.Now > _lastChecked.Value.AddMinutes(1))
                {
                    await LogHelper.LogModule("Running FleetUp module check...", Category);

                    var userId = SettingsManager.Settings.FleetupModule.UserId;
                    var apiCode = SettingsManager.Settings.FleetupModule.APICode;
                    var appKey = SettingsManager.Settings.FleetupModule.AppKey;
                    var groupID = SettingsManager.Settings.FleetupModule.GroupID;
                    var channelid = SettingsManager.Settings.FleetupModule.Channel;
                    var lastopid = Convert.ToInt64(await SQLHelper.GetCacheDataFleetUpLastPosted());
                    var announcePost = SettingsManager.Settings.FleetupModule.Announce_Post;
                    var channel = channelid == 0 ? null : APIHelper.DiscordAPI.GetChannel(channelid);

                    _lastChecked = DateTime.Now;
                    await SQLHelper.SetCacheDataFleetUpLastChecked(_lastChecked);

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
                        var lastAnnounce = await SQLHelper.GetFleetupAnnounce(operation.Id);

                        if (operation.OperationId > lastopid && announcePost)
                        {
                            await SendMessage(operation, channel, $"{Settings.FleetupModule.DefaultMention} FleetUp Op <http://fleet-up.com/Operation#{operation.OperationId}>", true);
                            await SQLHelper.SetCacheDataFleetUpLastPosted(operation.OperationId);
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
                                await SendMessage(operation, channel, $"{Settings.FleetupModule.DefaultMention} {LM.Get("fuFormIn", i, $"http://fleet-up.com/Operation#{operation.OperationId}")}",
                                    false);
                                await SQLHelper.AddFleetupOp(operation.Id, i);
                            }
                        }

                        //NOW
                        if (timeDiff.TotalMinutes < 1)
                        {
                            await SendMessage(operation, channel, $"{Settings.FleetupModule.FinalTimeMention} {LM.Get("fuFormNow", $"http://fleet-up.com/Operation#{operation.OperationId}")}",
                                false);
                            await SQLHelper.DeleteFleetupOp(operation.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx($"ERROR {ex.Message}", ex, Category);
            }
        }

       // private Dictionary<string, List<FleetUpCache> _cache = new Dictionary<string, FleetUpCache>();



        public static async Task Ops(ICommandContext context, string x)
        {
            try
            {
                var reason = LogCat.FleetUp.ToString();
                int.TryParse(x, out var amount);
                var userId = SettingsManager.Settings.FleetupModule.UserId;
                var apiCode = SettingsManager.Settings.FleetupModule.APICode;
                var groupID = SettingsManager.Settings.FleetupModule.GroupID;
                var appKey = SettingsManager.Settings.FleetupModule.AppKey;

                var result = await APIHelper.FleetUpAPI.GetOperations(reason, userId, apiCode, appKey, groupID);

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
