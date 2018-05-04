using System;
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
                .AddInlineField(LM.Get("fuFormUpTime"), startTime.ToString(SettingsManager.Get("config", "timeformat")))
                .AddInlineField(LM.Get("fuFormUpSystem"), string.IsNullOrWhiteSpace(location) ? LM.Get("None") : locationText)
                .AddField(LM.Get("Details"), string.IsNullOrWhiteSpace(details) ? LM.Get("None") : details)
                .WithTimestamp(startTime);



            var embed = builder.Build();
            var sendres = await channel.SendMessageAsync(message, false, embed);
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
            try
            {
                //Check Fleetup Operations
                _lastChecked = _lastChecked ?? DateTime.Parse(await SQLiteHelper.SQLiteDataQuery("cacheData", "data", "name", "fleetUpLastChecked"));

                if (DateTime.Now > _lastChecked)
                {
                    var userId = SettingsManager.Get("fleetup", "UserId");
                    var apiCode = SettingsManager.Get("fleetup", "APICode");
                    var appKey = SettingsManager.Get("fleetup", "AppKey");
                    var groupID =SettingsManager.Get("fleetup", "GroupID");
                    var channelid = SettingsManager.GetULong("fleetup", "channel");
                    var lastopid = await SQLiteHelper.SQLiteDataQuery("cacheData", "data", "name", "fleetUpLastPostedOperation");
                    var announcePost = SettingsManager.GetBool("fleetup", "announce_post");
                    var channel = APIHelper.DiscordAPI.GetChannel(channelid);

                    if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(apiCode) || string.IsNullOrWhiteSpace(groupID))
                    {
                        await LogHelper.LogInfo(LM.Get("fuNeedSetup"), Category);
                        _lastChecked = DateTime.Now;
                        await SQLiteHelper.SQLiteDataUpdate("cacheData", "data", _lastChecked.ToString(), "name", "fleetUpLastChecked");
                        return;
                    }

                    var result = await APIHelper.FleetUpAPI.GetOperations(Reason, userId, apiCode, appKey, groupID);
                    if (result == null)
                    {
                        _lastChecked = DateTime.Now;
                        await SQLiteHelper.SQLiteDataUpdate("cacheData", "data", _lastChecked.ToString(), "name", "fleetUpLastChecked");
                        return;
                    }
                    foreach (var operation in result.Data)
                    {
                        if (operation.OperationId > Convert.ToInt32(lastopid) && announcePost)
                        {
                            await SendMessage(operation, channel,$"@everyone FleetUp Op <http://fleet-up.com/Operation#{operation.OperationId}>", true);
                            await SQLiteHelper.SQLiteDataUpdate("cacheData", "data", operation.OperationId.ToString(), "name", "fleetUpLastPostedOperation");
                        }

                        var timeDiff = TimeSpan.FromTicks(operation.Start.Ticks - DateTime.UtcNow.Ticks);
                        var array = SettingsManager.GetSubList("fleetup", "announce").Select(x => x.Value).ToArray();

                        foreach (var i in array)
                        {
                            var epic1 = TimeSpan.FromMinutes(Convert.ToInt16(i));
                            var epic2 = TimeSpan.FromMinutes(Convert.ToInt16(i) + 1);

                            if (timeDiff >= epic1 && timeDiff <= epic2)
                                await SendMessage(operation, channel, $"@everyone {string.Format(LM.Get("fuFormIn"), i, $"http://fleet-up.com/Operation#{operation.OperationId}")}",
                                    false);
                        }

                        //NOW
                        if (timeDiff.TotalMinutes < 1 && timeDiff.TotalMinutes > 0)
                            await SendMessage(operation, channel, $"@everyone {string.Format(LM.Get("fuFormNow"), $"http://fleet-up.com/Operation#{operation.OperationId}")}", false);

                        _lastChecked = DateTime.Now;
                        await SQLiteHelper.SQLiteDataUpdate("cacheData", "data", _lastChecked.ToString(), "name", "fleetUpLastChecked");
                    }
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx($"ERROR {ex.Message}", ex, Category);
            }
        }

        public static async Task Ops(ICommandContext context, string x)
        {
            try
            {
                var Reason = LogCat.FleetUp.ToString();
                int.TryParse(x, out var amount);
                var userId = SettingsManager.Get("fleetup", "UserId");
                var apiCode = SettingsManager.Get("fleetup", "APICode");
                var groupID = SettingsManager.Get("fleetup", "GroupID");
                var appKey = SettingsManager.Get("fleetup", "AppKey");

                var result = await APIHelper.FleetUpAPI.GetOperations(Reason, userId, apiCode, appKey, groupID);

                if (!result.Data.Any())
                {
                    await context.Message.Channel.SendMessageAsync($"{context.Message.Author.Mention}, No Ops Scheduled");
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
}
