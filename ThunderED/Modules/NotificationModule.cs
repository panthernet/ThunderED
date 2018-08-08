using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Json.Internal;
using ThunderED.Modules.OnDemand;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules
{
    public class NotificationModule: AppModuleBase
    {
        private DateTime _nextNotificationCheck = DateTime.FromFileTime(0);
        private int _lastNotification;

        public override LogCat Category => LogCat.Notification;

        public override async Task Run(object prm)
        {
            await NotificationFeed();

            await CleanupNotifyList();
        }

        public NotificationModule()
        {
            LogHelper.LogModule("Inititalizing Notifications module...", Category).GetAwaiter().GetResult();
            WebServerModule.ModuleConnectors.Add(Reason, Auth);
        }

        private DateTime _lastCleanupCheck = DateTime.FromFileTime(0);


        private async Task CleanupNotifyList()
        {
            if ((DateTime.Now - _lastCleanupCheck).TotalHours > 8)
            {
                _lastCleanupCheck = DateTime.Now;
                await SQLHelper.CleanupNotificationsList();
                await LogHelper.LogInfo("Notifications cleanup complete", Category, LogToConsole, false);
            }
        }

        private string GetData(string field, Dictionary<string, string> data, bool caseSensitive = false)
        {
            if (!caseSensitive)
            {
                var lowerField = field.ToLower();
                var key = data.Keys.FirstOrDefault(a => a.ToLower() == lowerField);
                return key == null ? null : data[key];
            }
            else
            {
                var key = data.Keys.FirstOrDefault(a => a == field);
                return key == null ? null : data[key];

            }
        }

        #region Notifications
        private async Task NotificationFeed()
        {
            if(IsRunning) return;
            IsRunning = true;
            try
            {
                if (DateTime.Now > _nextNotificationCheck)
                {
                    await LogHelper.LogModule("Running Notifications module check...", Category);
                    var guildID = Settings.Config.DiscordGuildId;

                    foreach (var groupPair in Settings.NotificationFeedModule.Groups)
                    {
                        var group = groupPair.Value;
                        if (group.CharacterID == 0)
                        {
                            await LogHelper.LogError($"[CONFIG] Notification group {groupPair.Key} has no characterID specified!");
                            continue;
                        }

                        if (group.DefaultDiscordChannelID == 0)
                        {
                            await LogHelper.LogError($"[CONFIG] Notification group {groupPair.Key} has no DefaultDiscordChannelID specified!");
                            continue;
                        }

                        //skip empty group
                        if (group.Filters.Values.All(a => a.Notifications.Count == 0)) continue;

                        var rToken = await SQLHelper.SQLiteDataQuery<string>("refreshTokens", "token", "id", group.CharacterID);
                        var token = await APIHelper.ESIAPI.RefreshToken(rToken, Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret);
                        if (!string.IsNullOrEmpty(token))
                            await LogHelper.LogInfo($"Checking characterID:{group.CharacterID}", Category, LogToConsole, false);
                        else
                        {
                            await LogHelper.LogInfo($"Failed to get refresh token for {groupPair.Key} group!", Category, LogToConsole);
                            continue;
                        }

                        var feederChar = await APIHelper.ESIAPI.GetCharacterData(Reason, group.CharacterID);
                        var feederCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, feederChar?.corporation_id);

                        var notifications = (token == null ? null : await APIHelper.ESIAPI.GetNotifications(Reason, group.CharacterID.ToString(), token)) ??
                                            new List<JsonClasses.Notification>();

                        //process filters
                        foreach (var filterPair in group.Filters)
                        {
                            var filter = filterPair.Value;
                            _lastNotification = await SQLHelper.SQLiteDataQuery<int>("notificationsList", "id", new Dictionary<string, object>{
                                {"groupName", groupPair.Key},
                                {"filterName", filterPair.Key}
                                });

                            List<JsonClasses.Notification> fNotifications;
                            if (_lastNotification == 0)
                            {
                                var now = DateTime.UtcNow;
                                if (group.FetchLastNotifDays > 0)
                                {
                                    fNotifications = notifications.Where(a =>
                                    {
                                        DateTime.TryParse(a.timestamp, out var timestamp);
                                        return (now - timestamp).Days < group.FetchLastNotifDays && filter.Notifications.Contains(a.type);
                                    }).OrderBy(x => x.notification_id).ToList();
                                }
                                else
                                {
                                    fNotifications = new List<JsonClasses.Notification>();
                                    _lastNotification = notifications.Max(a => a.notification_id);
                                    await UpdateNotificationList(groupPair.Key, filterPair.Key, true);
                                    continue;
                                }
                            }
                            else
                                fNotifications = notifications.Where(a => filter.Notifications.Contains(a.type) && a.notification_id > _lastNotification)
                                    .OrderBy(x => x.notification_id).ToList();


                            //check if there are new notifications to process
                            if (fNotifications.Count > 0 && fNotifications.Last().notification_id != _lastNotification)
                            {
                                foreach (var notification in fNotifications)
                                {
                                    try
                                    {
                                        //log new notifications to get essential data
                                        if (Settings.Config.LogNewNotifications)
                                            await LogHelper.LogNotification(notification.type, notification.text);

                                        var discordChannel = APIHelper.DiscordAPI.GetChannel(guildID, filter.ChannelID != 0 ? filter.ChannelID : group.DefaultDiscordChannelID);
                                        var data = HelpersAndExtensions.ParseNotificationText(notification.text);
                                        var atCorpName = GetData("corpName", data) ?? LM.Get("Unknown");
                                        var systemId = GetData("solarSystemID", data);
                                        var system = string.IsNullOrEmpty(systemId) ? null : await APIHelper.ESIAPI.GetSystemData(Reason, systemId);
                                        var systemName = system == null ? LM.Get("Unknown") : (system.name == system.system_id.ToString() ? "Abyss": system.name);
                                        var structureId = GetData("structureID", data);
                                        var structure = string.IsNullOrEmpty(structureId) ? null : await APIHelper.ESIAPI.GetStructureData(Reason, structureId, token);
                                        var structureNameDirect = GetData("structureName", data);

                                        var structureTypeId = GetData("structureTypeID", data);
                                        var structureType = string.IsNullOrEmpty(structureTypeId) ? null : await APIHelper.ESIAPI.GetTypeId(Reason, structureTypeId);
                                        var strTime = GetData("timeLeft", data);
                                        var timeleft = string.IsNullOrEmpty(strTime) ? null : TimeSpan.FromTicks(Convert.ToInt64(strTime)).ToFormattedString();

                                        int itemQuantity = 0;
                                        string itemName = null;
                                        if (data.ContainsKey("listOfTypesAndQty"))
                                        {
                                            try
                                            {
                                                var keys = data.Keys.ToList();
                                                var ltqIndex = keys.IndexOf("listOfTypesAndQty");
                                                int.TryParse(keys[ltqIndex + 1].Split(' ').Last(), out itemQuantity);
                                                itemName = (await APIHelper.ESIAPI.GetTypeId(Reason, keys[ltqIndex + 2].Split(' ').Last()))?.name ?? LM.Get("Unknown");
                                            }
                                            catch
                                            {
                                                //ignore
                                            }
                                        }

                                        Dictionary<string, string> oreComposition = null;
                                        if (data.ContainsKey("oreVolumeByType"))
                                        {
                                            try
                                            {
                                                var keys = data.Keys.ToList();
                                                var ltqIndex = keys.IndexOf("oreVolumeByType");
                                                var endIndex = keys.IndexOf("solarSystemLink");
                                                var pass = endIndex - ltqIndex;
                                                if (pass > 0)
                                                {
                                                    oreComposition = new Dictionary<string, string>();
                                                    for (int i = ltqIndex + 1; i < endIndex; i++)
                                                    {
                                                        var typeName = (await APIHelper.ESIAPI.GetTypeId(Reason, keys[i])).name;
                                                        var value = double.Parse(data[keys[i]].Split('.')[0]).ToString("N");
                                                        oreComposition.Add(typeName, value);
                                                    }
                                                }
                                            }
                                            catch
                                            {
                                                //ignore
                                            }
                                        }

                                        var moonFiredBy = data.ContainsKey("firedBy")
                                            ? ((await APIHelper.ESIAPI.GetCharacterData(Reason, data["firedBy"]))?.name ?? LM.Get("Auto"))
                                            : null;

                                        DateTime.TryParse(notification.timestamp, out var localTimestamp);
                                        var timestamp = localTimestamp.ToUniversalTime();
                                        Embed embed;
                                        EmbedBuilder builder;
                                        string textAdd;

                                        var mention = filter.DefaultMention;
                                        if (filter.CharMentions.Count > 0)
                                        {
                                            var list = filter.CharMentions.Select(a =>
                                                    SQLHelper.SQLiteDataQuery<ulong>("authUsers", "discordID", "characterID", a).GetAwaiter().GetResult()).Where(a=> a != 0)
                                                .ToList();
                                            if (list.Count > 0)
                                            {
                                                var mList = list.Select(a =>
                                                {
                                                    try
                                                    {
                                                        return APIHelper.DiscordAPI.GetUserMention(a);
                                                    }
                                                    catch
                                                    {
                                                        return null;
                                                    }
                                                }).Where(a=> a != null);
                                                if(mList.Any())
                                                    mention = string.Join(" ", mList);
                                            }
                                        }

                                        if (filter.RoleMentions.Count > 0)
                                        {
                                            var mentionList = new List<string>();
                                            foreach (var role in filter.RoleMentions)
                                            {
                                                mentionList.Add(APIHelper.DiscordAPI.GetRoleMention(role));
                                            }

                                            if (mentionList.Count > 0)
                                            {
                                                var str = string.Join(' ', mentionList);
                                                mention = mention == filter.DefaultMention ? str : $"{mention} {str}";
                                            }
                                        }

                                        mention = string.IsNullOrEmpty(mention) ? " " : mention;

                                        switch (notification.type)
                                        {
                                            case "OrbitalAttacked":
                                            {
                                                var struc = await APIHelper.ESIAPI.GetTypeId(Reason,  GetData("typeID", data));
                                                var planet = await APIHelper.ESIAPI.GetPlanet(Reason, GetData("planetID", data));

                                                var agressor = (await APIHelper.ESIAPI.GetCharacterData(Reason, GetData("aggressorID", data)))?.name;
                                                var aggCorp = (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("aggressorCorpID", data)))?.name;
                                                var aggAllyId = GetData("aggressorAllianceID", data);
                                                var aggAlly = string.IsNullOrEmpty(aggAllyId) || aggAllyId == "0" ? null : (await APIHelper.ESIAPI.GetAllianceData(Reason, aggAllyId))?.ticker;
                                                var aggText = $"{agressor} - {aggCorp}{(string.IsNullOrEmpty(aggAlly) ? null : $"[{aggAlly}]")}";

                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);

                                                builder = new EmbedBuilder()
                                                    .WithColor(new Color(0xdd5353))
                                                    .WithThumbnailUrl(Settings.Resources.ImgCitUnderAttack)
                                                    .WithAuthor(author => author.WithName(LM.Get("NotifyHeader_OrbitalAttacked",
                                                        struc?.name, feederCorp?.name))
                                                        .WithUrl($"http://www.zkillboard.com/character/{GetData("aggressorID", data)}"))
                                                    .AddInlineField(LM.Get("Location"), $"{systemName} - {planet?.name ?? LM.Get("Unknown")}")
                                                    .AddInlineField(LM.Get("Aggressor"), aggText)
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                            }
                                            break;

                                            case "StructureUnderAttack":
                                            {
                                                //"text": "allianceID: 99007289\nallianceLinkData:\n- showinfo\n- 16159\n- 99007289\nallianceName: Federation Uprising\narmorPercentage: 100.0\ncharID: 96734115\ncorpLinkData:\n- showinfo\n- 2\n- 98502090\ncorpName: Federal Vanguard\nhullPercentage: 100.0\nshieldPercentage: 99.93084364472988\nsolarsystemID: 30003842\nstructureID: &id001 1026660410904\nstructureShowInfoData:\n- showinfo\n- 35832\n- *id001\nstructureTypeID: 35832\n"

                                                var aggCharId = GetData("charID", data);
                                                var agressor = await APIHelper.ESIAPI.GetCharacterData(Reason, aggCharId);
                                                var aggName = agressor?.name;
                                                var aggCorp = GetData("corpName", data);
                                                var aggAllyId = GetData("allianceID", data);
                                                var aggAlly = string.IsNullOrEmpty(aggAllyId) || aggAllyId == "0" ? null : (await APIHelper.ESIAPI.GetAllianceData(Reason, aggAllyId))?.ticker;
                                                var aggText = $"{aggName} - {aggCorp}{(string.IsNullOrEmpty(aggAlly) ? null : $"[{aggAlly}]")}";

                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                builder = new EmbedBuilder()
                                                    .WithColor(new Color(0xdd5353))
                                                    .WithThumbnailUrl(Settings.Resources.ImgCitUnderAttack)
                                                    .WithAuthor(author => author.WithName(LM.Get("NotifyHeader_StructureUnderAttack",
                                                        structureType == null ? LM.Get("structure").ToLower() : structureType.name))
                                                        .WithUrl($"http://www.zkillboard.com/character/{aggCharId}"))
                                                    .AddInlineField(LM.Get("System"), systemName)
                                                    .AddInlineField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"))
                                                    .AddInlineField(LM.Get("Aggressor"), aggText) //.WithUrl(")
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                                break;
                                            }
                                            case "StructureWentLowPower":
                                            case "StructureWentHighPower":
                                            {
                                                //"text": "solarsystemID: 30045335\nstructureID: &id001 1026192163696\nstructureShowInfoData:\n- showinfo\n- 35835\n- *id001\nstructureTypeID: 35835\n"
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                var color = notification.type == "StructureWentLowPower" ? new Color(0xdd5353) : new Color(0x00ff00);
                                                var text = notification.type == "StructureWentLowPower" ? LM.Get("LowPower") : LM.Get("HighPower");
                                                builder = new EmbedBuilder()
                                                    .WithColor(color)
                                                    .WithThumbnailUrl(Settings.Resources.ImgCitLowPower)
                                                    .WithAuthor(author =>
                                                        author.WithName(LM.Get("StructureWentLowPower",
                                                            (structureType == null ? LM.Get("structure").ToLower() : structureType.name) ?? LM.Get("Unknown"), text)))
                                                    .AddInlineField(LM.Get("System"), systemName)
                                                    .AddInlineField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"))
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                            }
                                                break;
                                            case "StructureLostArmor":
                                            case "StructureLostShields":
                                            {                                                   
                                                // "text": "solarsystemID: 30003842\nstructureID: &id001 1026660410904\nstructureShowInfoData:\n- showinfo\n- 35832\n- *id001\nstructureTypeID: 35832\ntimeLeft: 1557974732906\ntimestamp: 131669979190000000\nvulnerableTime: 9000000000\n"
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                textAdd = notification.type == "StructureLostArmor" ? LM.Get("armorSmall") : LM.Get("shieldSmall");
                                                builder = new EmbedBuilder()
                                                    .WithColor(new Color(0xdd5353))
                                                    .WithThumbnailUrl(notification.type == "StructureLostShields" ? Settings.Resources.ImgCitLostShield : Settings.Resources.ImgCitLostArmor)
                                                    .WithAuthor(author =>
                                                        author.WithName(LM.Get("StructureLostArmor",
                                                            structureType == null ? LM.Get("Structure") : structureType.name, textAdd)))
                                                    .AddInlineField(LM.Get("System"), systemName)
                                                    .AddInlineField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"))
                                                    .AddInlineField("Time Left", timeleft ?? LM.Get("Unknown"))
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);

                                                if (Settings.Config.ModuleTimers && Settings.TimersModule.AutoAddTimerForReinforceNotifications)
                                                {
                                                    await TimersModule.AddTimer(new TimerItem
                                                    {
                                                        timerChar = "Auto",
                                                        timerET = (timestamp + TimeSpan.FromTicks(Convert.ToInt64(strTime))).ToString(),
                                                        timerLocation = $"{systemName} - {structureType.name} - {structure?.name}",
                                                        timerStage = notification.type == "StructureLostShields" ? 2 : 1,
                                                        timerType = 2,
                                                        timerOwner = "Alliance"
                                                    });
                                                }
                                            }
                                                break;
                                            case "StructureDestroyed":
                                            case "StructureOnline":
                                            {
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                var owner = GetData("ownerCorpName", data) ?? LM.Get("Unknown");
                                                var iUrl = notification.type == "StructureDestroyed"
                                                    ? Settings.Resources.ImgCitDestroyed
                                                    : Settings.Resources.ImgCitOnline;
                                                var text = notification.type == "StructureDestroyed"
                                                    ? LM.Get("StructureDestroyed", owner, structureType == null ? LM.Get("Unknown") : structureType.name)
                                                    : LM.Get("StructureOnline", structureType == null ? LM.Get("Unknown") : structureType.name);
                                                var color = notification.type == "StructureDestroyed" ? new Color(0xdd5353) : new Color(0x00ff00);
                                                builder = new EmbedBuilder()
                                                    .WithColor(color)
                                                    .WithThumbnailUrl(iUrl)
                                                    .WithAuthor(author =>
                                                        author.WithName(text))
                                                    .AddInlineField(LM.Get("System"), systemName)
                                                    .AddInlineField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"))
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                            }
                                                break;
                                            case "StructureAnchoring":
                                            {
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                var owner = GetData("ownerCorpName", data) ?? LM.Get("Unknown");
                                                var text = LM.Get("StructureAnchoring", owner,
                                                    structureType == null ? LM.Get("Structure") : structureType.name);
                                                builder = new EmbedBuilder()
                                                    .WithColor(new Color(0xff0000))
                                                    .WithThumbnailUrl(Settings.Resources.ImgCitAnchoring)
                                                    .WithAuthor(author =>
                                                        author.WithName(text))
                                                    .AddInlineField(LM.Get("System"), systemName)
                                                    .AddInlineField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"))
                                                    .AddInlineField(LM.Get("TimeLeft"), timeleft ?? LM.Get("Unknown"))
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                            }
                                                break;
                                            case "StructureFuelAlert":
                                                //"text": "listOfTypesAndQty:\n- - 307\n  - 4246\nsolarsystemID: 30045331\nstructureID: &id001 1027052813591\nstructureShowInfoData:\n- showinfo\n- 35835\n- *id001\nstructureTypeID: 35835\n"
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                builder = new EmbedBuilder()
                                                    .WithColor(new Color(0xf2882b))
                                                    .WithThumbnailUrl(Settings.Resources.ImgCitFuelAlert)
                                                    .WithAuthor(author => author.WithName(LM.Get("StructureFuelAlert",
                                                        structureType == null ? LM.Get("Structure") : structureType.name)))
                                                    .AddInlineField(LM.Get("System"), systemName)
                                                    .AddInlineField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"))
                                                    .AddInlineField(LM.Get("Fuel"), itemQuantity == 0 ? LM.Get("Unknown") : $"{itemQuantity} {itemName}")
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                                break;
                                            case "MoonminingExtractionFinished":
                                                //"text": "autoTime: 131632776620000000\nmoonID: 40349232\nmoonLink: <a href=\"showinfo:14\/\/40349232\">Teskanen IV - Moon 14<\/a>\noreVolumeByType:\n  45513: 1003894.7944164276\n  46676: 3861704.652392864\n  46681: 1934338.7763798237\n  46687: 5183861.7768108845\nsolarSystemID: 30045335\nsolarSystemLink: <a href=\"showinfo:5\/\/30045335\">Teskanen<\/a>\nstructureID: 1026192163696\nstructureLink: <a href=\"showinfo:35835\/\/1026192163696\">Teskanen - Nebula Prime<\/a>\nstructureName: Teskanen - Nebula Prime\nstructureTypeID: 35835\n"
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                var compText = new StringBuilder();
                                                foreach (var pair in oreComposition)
                                                {
                                                    compText.Append($"{pair.Key}: {pair.Value} | ");
                                                }

                                                if (compText.Length > 0)
                                                    compText.Remove(compText.Length - 3, 3);
                                                else compText.Append(LM.Get("Unknown"));

                                                builder = new EmbedBuilder()
                                                    .WithColor(new Color(0xb386f7))
                                                    .WithThumbnailUrl(Settings.Resources.ImgMoonComplete)
                                                    .WithAuthor(author =>
                                                        author.WithName(LM.Get("MoonminingExtractionFinished",
                                                            structureType == null ? LM.Get("Structure") : structureType.name)))
                                                    .AddInlineField(LM.Get("Structure"), structureNameDirect ?? LM.Get("Unknown"))
                                                    .AddInlineField(LM.Get("Composition"), compText.ToString())
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                                break;
                                            case "MoonminingLaserFired":
                                                //"text": "firedBy: 91684736\nfiredByLink: <a href=\"showinfo:1386\/\/91684736\">Mike Myzukov<\/a>\nmoonID: 40349232\nmoonLink: <a href=\"showinfo:14\/\/40349232\">Teskanen IV - Moon 14<\/a>\noreVolumeByType:\n  45513: 241789.6056930224\n  46676: 930097.5066294272\n  46681: 465888.4702051679\n  46687: 1248541.084139049\nsolarSystemID: 30045335\nsolarSystemLink: <a href=\"showinfo:5\/\/30045335\">Teskanen<\/a>\nstructureID: 1026192163696\nstructureLink: <a href=\"showinfo:35835\/\/1026192163696\">Teskanen - Nebula Prime<\/a>\nstructureName: Teskanen - Nebula Prime\nstructureTypeID: 35835\n"
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);

                                                builder = new EmbedBuilder()
                                                    .WithColor(new Color(0xb386f7))
                                                    .WithThumbnailUrl(Settings.Resources.ImgMoonComplete)
                                                    .WithAuthor(author =>
                                                        author.WithName(LM.Get("MoonminingLaserFired",
                                                            structureType == null ? LM.Get("Structure") : structureType.name)))
                                                    .AddInlineField(LM.Get("Structure"), structureNameDirect ?? LM.Get("Unknown"))
                                                    .AddInlineField(LM.Get("FiredBy"), moonFiredBy)
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                                break;
                                            case "CharLeftCorpMsg":
                                            case "CharAppAcceptMsg":
					    case "CorpAppNewMsg":
					    case "CharAppWithdrawMsg":
                                            {
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                var character = await APIHelper.ESIAPI.GetCharacterData(Reason, GetData("charID", data), true);
                                                var corp = await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("corpID", data), true);
						var text = "";
						switch(notification.type) {
							case "CharLeftCorpMsg":
								text = LM.Get("CharLeftCorpMsg", character?.name, corp?.name);
								break;
							case "CharAppAcceptMsg":
								text = LM.Get("CharAppAcceptMsg", character?.name, corp?.name);
								break;
							case "CorpAppNewMsg":
								text = LM.Get("CorpAppNewMsg", character?.name, corp?.name);
								break;
							case "CharAppWithdrawMsg":
								text = LM.Get("CharAppWithdrawMsg", character?.name, corp?.name);
								break;
						}
						var applicationText = notification.type == "CharLeftCorpMsg" ? "" : GetData("applicationText", data);
						Color color;
						if(notification.type == "CharLeftCorpMsg" || notification.type == "CharAppWithdrawMsg") {
							color = new Color(0xdd5353);
						} else if(notification.type == "CharAppAcceptMsg") {
							color = new Color(0x00ff00);
						} else {
							color = new Color(0x555555);
						}
                                                builder = new EmbedBuilder()
                                                    .WithColor(color)
						    .WithDescription(applicationText)
                                                    .WithAuthor(author => author.WithName(text).WithUrl($"https://zkillboard.com/character/{GetData("charID", data)}/"))
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                            }
                                                break;

                                            case "SovStructureDestroyed":
                                            {
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                builder = new EmbedBuilder()
                                                    .WithColor(new Color(0xdd5353))
                                                    .WithAuthor(
                                                        author => author.WithName(LM.Get("SovStructureDestroyed", structureType?.name, systemName)))
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                            }
                                                break;

                                            case "SovStationEnteredFreeport":
                                            {
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                var exittime = DateTime.FromFileTime(Convert.ToInt64(GetData("freeportexittime", data)));
                                                builder = new EmbedBuilder()
                                                    .WithColor(new Color(0xdd5353))
                                                    .WithAuthor(
                                                        author => author.WithName(LM.Get("SovStationEnteredFreeport", structureType?.name, systemName)))
                                                    .AddInlineField("Exit Time", exittime)
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                            }
                                                break;
                                            case "StationServiceDisabled":
                                            {
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                builder = new EmbedBuilder()
                                                    .WithColor(new Color(0xdd5353))
                                                    .WithThumbnailUrl(Settings.Resources.ImgCitServicesOffline)
                                                    .WithAuthor(author =>
                                                        author.WithName(LM.Get("StationServiceDisabled", structureType?.name, systemName)))
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                            }
                                                break;



                                            case "SovCommandNodeEventStarted":
                                            {
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                var constellation = await APIHelper.ESIAPI.GetConstellationData(Reason, GetData("constellationID", data));
                                                var campaignId = Convert.ToInt32(GetData("campaignEventType", data));
                                                var cmp = campaignId == 1 ? "1" : (campaignId == 2 ? "2" : "3");
                                                builder = new EmbedBuilder()
                                                    .WithColor(new Color(0xdd5353))
                                                    .WithAuthor(author =>
                                                        author.WithName(LM.Get("SovCommandNodeEventStarted", cmp, systemName, constellation?.name)))
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                            }
                                                break;

                                            case "SovStructureReinforced":
                                            {
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                var decloakTime = DateTime.FromFileTime(Convert.ToInt64(GetData("decloakTime", data)));
                                                var campaignId = Convert.ToInt32(GetData("campaignEventType", data));
                                                var cmp = campaignId == 1 ? "1" : (campaignId == 2 ? "2" : "3");
                                                builder = new EmbedBuilder()
                                                    .WithColor(new Color(0xdd5353))
                                                    .WithAuthor(author =>
                                                        author.WithName(LM.Get("SovStructureReinforced", cmp, systemName)))
                                                    .AddInlineField("Decloak Time", decloakTime.ToString())
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                            }
                                                break;

                                            case "EntosisCaptureStarted":
                                            {
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                builder = new EmbedBuilder()
                                                    .WithColor(new Color(0xdd5353))
                                                    .WithAuthor(
                                                        author => author.WithName(LM.Get("EntosisCaptureStarted", structureType?.name, systemName)))
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                            }
                                                break;


                                            case "AllWarDeclaredMsg":
                                            case "CorpWarDeclaredMsg":
                                            case "AllWarInvalidatedMsg":
                                            case "CorpWarInvalidatedMsg":
                                            {
                                                //"text": "againstID: 98464487\ncost: 50000000.0\ndeclaredByID: 99005333\ndelayHours: 24\nhostileState: 0\n"
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                var declaredById = GetData("declaredByID", data);
                                                var declareByAlianceName = !string.IsNullOrEmpty(declaredById)
                                                    ? (await APIHelper.ESIAPI.GetAllianceData(Reason, declaredById, true))?.name
                                                    : null;
                                                var declareByCorpName = !string.IsNullOrEmpty(declaredById)
                                                    ? (await APIHelper.ESIAPI.GetCorporationData(Reason, declaredById, true))?.name
                                                    : null;
                                                var declName = declareByAlianceName ?? declareByCorpName ?? LM.Get("Unknown");
                                                bool isAllianceDecl = !string.IsNullOrEmpty(declareByAlianceName);

                                                var declaredAgainstId = GetData("againstID", data);
                                                var declareAgainstAlianceName = !string.IsNullOrEmpty(declaredAgainstId)
                                                    ? (await APIHelper.ESIAPI.GetAllianceData(Reason, declaredAgainstId, true))?.name
                                                    : null;
                                                var declareAgainstCorpName = !string.IsNullOrEmpty(declaredAgainstId)
                                                    ? (await APIHelper.ESIAPI.GetCorporationData(Reason, declaredAgainstId, true))?.name
                                                    : null;
                                                var declNameAgainst = declareAgainstAlianceName ?? declareAgainstCorpName ?? LM.Get("Unknown");
                                                // bool isAllianceAgainst = !string.IsNullOrEmpty(declareAgainstAlianceName);

                                                var iUrl = notification.type == "AllWarDeclaredMsg" || notification.type == "CorpWarDeclaredMsg"
                                                    ? Settings.Resources.ImgWarDeclared
                                                    : Settings.Resources.ImgWarInvalidate;

                                                var template = notification.type == "AllWarDeclaredMsg" || notification.type == "CorpWarDeclaredMsg"
                                                    ? $"{(isAllianceDecl ? LM.Get("Alliance") : LM.Get("Corporation"))} {declName} {LM.Get("declaresWarAgainst")} {declNameAgainst}!"
                                                    : $"{(isAllianceDecl ? LM.Get("Alliance") : LM.Get("Corporation"))} {declName} {LM.Get("invalidatesWarAgainst")} {declNameAgainst}!";
                                                var template2 = notification.type == "AllWarDeclaredMsg" || notification.type == "CorpWarDeclaredMsg"
                                                    ? LM.Get("fightWillBegin", GetData("delayHours", data))
                                                    : LM.Get("fightWillEnd", GetData("delayHours", data));
                                                var color = notification.type == "AllWarDeclaredMsg" || notification.type == "CorpWarDeclaredMsg"
                                                    ? new Color(0xdd5353)
                                                    : new Color(0x00ff00);

                                                var url = isAllianceDecl
                                                    ? $"http://www.zkillboard.com/alliance/{declaredById}"
                                                    : $"http://www.zkillboard.com/corporation/{declaredById}";

                                                builder = new EmbedBuilder()
                                                    .WithColor(color)
                                                    .WithThumbnailUrl(iUrl)
                                                    .WithAuthor(author => author.WithName(template).WithUrl(url))
                                                    .AddInlineField(LM.Get("Note"), template2)
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                            }
                                                break;
                                            case "AllyJoinedWarAggressorMsg":
                                            {
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                var allyID = GetData("allyID", data);
                                                var ally = (await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("allyID", data)))?.name ??
                                                           (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("allyID", data)))?.name;
                                                var defender = (await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("defenderID", data)))?.name ??
                                                               (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("defenderID", data)))?.name;
                                                builder = new EmbedBuilder()
                                                    .WithColor(new Color(0xff0000))
                                                    .WithThumbnailUrl(Settings.Resources.ImgWarAssist)
                                                    .WithAuthor(author => author.WithName(LM.Get("AllyJoinedWarAggressorMsg", ally, defender))
                                                        .WithUrl( $"http://www.zkillboard.com/alliance/{allyID}"))
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                            }
                                                break;
                                            case "AllyJoinedWarDefenderMsg":
                                            {
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                string allyStr = null;
                                                var allyData = await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("allyID", data));
                                                if (allyData != null) allyStr = allyData.name;
                                                else
                                                {
                                                    var corpData = await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("allyID", data));
                                                    allyStr = corpData?.name;
                                                }

                                                var defenderStr = string.Empty;
                                                allyData = await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("defenderID", data));
                                                if (allyData != null) defenderStr = allyData.name;
                                                else
                                                {
                                                    var corpData = await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("defenderID", data));
                                                    defenderStr = corpData?.name;
                                                }

                                                var agressorStr = (await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("aggressorID", data)))?.name;
                                                if (agressorStr == null)
                                                    agressorStr = (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("aggressorID", data)))?.name;
                                                var allyID = GetData("allyID", data);
                                                builder = new EmbedBuilder()
                                                    .WithColor(new Color(0xff0000))
                                                    .WithThumbnailUrl(Settings.Resources.ImgWarAssist)
                                                    .WithAuthor(author =>
                                                        author.WithName(LM.Get("AllyJoinedWarDefenderMsg", allyStr, defenderStr, agressorStr))
                                                            .WithUrl( $"http://www.zkillboard.com/alliance/{allyID}"))
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                            }
                                                break;
                                            case "AllyJoinedWarAllyMsg":
                                            {
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                var allyID = GetData("allyID", data);
                                                var agressorStr2 = (await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("aggressorID", data)))?.name ??
                                                                   (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("aggressorID", data)))?.name;
                                                var allyStr2 = (await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("allyID", data)))?.name ??
                                                               (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("allyID", data)))?.name;
                                                var defenderStr2 = (await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("defenderID", data)))?.name ??
                                                                   (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("defenderID", data)))?.name;
                                                builder = new EmbedBuilder()
                                                    .WithColor(new Color(0x00ff00))
                                                    .WithThumbnailUrl(Settings.Resources.ImgWarAssist)
                                                    .WithAuthor(author =>
                                                        author.WithName(LM.Get("AllyJoinedWarAllyMsg", allyStr2, defenderStr2, agressorStr2))
                                                            .WithUrl( $"http://www.zkillboard.com/alliance/{allyID}"))
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                            }
                                                break;
                                            case "FWAllianceWarningMsg":
                                            {
                                                //"text": "allianceID: 99005333\ncorpList: <br>Quarian Fleet - standings:-0.0500\nfactionID: 500001\nrequiredStanding: 0.0001\n"
                                                await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                var allianceId = GetData("allianceID", data);
                                                var allyStr3 = !string.IsNullOrEmpty(allianceId)
                                                    ? (await APIHelper.ESIAPI.GetAllianceData(Reason, allianceId, true))?.name
                                                    : LM.Get("Unknown");
                                                var corpStr3 = data.ContainsKey("corpList")
                                                    ? data["corpList"].Trim().Replace("<br>", "").Replace(" - standings", "")
                                                    : LM.Get("Unknown");
                                                var required = GetData("requiredStanding", data);

                                                builder = new EmbedBuilder()
                                                    .WithColor(new Color(0xff0000))
                                                    .WithThumbnailUrl(Settings.Resources.ImgLowFWStand)
                                                    .WithAuthor(author => author.WithName(LM.Get("FWAllianceWarningMsg", allyStr3)))
                                                    .AddInlineField(LM.Get("BlameCorp"), LM.Get("standMissing", corpStr3, required))
                                                    .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                                    .WithTimestamp(timestamp);
                                                embed = builder.Build();

                                                await APIHelper.DiscordAPI.SendMessageAsync(discordChannel, mention, embed).ConfigureAwait(false);
                                            }
                                                break;
                                        }

                                        //await SetLastNotificationId(notification.notification_id, null);                                            
                                        await SetLastNotificationId(notification.notification_id, groupPair.Key, filterPair.Key);

                                    
                                    }
                                    catch (Exception ex)
                                    {
                                        await LogHelper.LogEx("Error Notification", ex, Category);
                                    }
                                }
                            }
                        }
                    }

                    var interval = Settings.NotificationFeedModule.CheckIntervalInMinutes;
                    await SQLHelper.SQLiteDataUpdate("cacheData", "data", DateTime.Now.AddMinutes(interval).ToString(CultureInfo.InvariantCulture), "name", "nextNotificationCheck");
                    _nextNotificationCheck = DateTime.Now.AddMinutes(interval);
                    await LogHelper.LogInfo("Check complete", Category, LogToConsole, false);
                }
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

        public async Task<bool> Auth(HttpListenerRequestEventArgs context)
        {
            if (!Settings.Config.ModuleNotificationFeed) return false;

            var request = context.Request;
            var response = context.Response;
            var extPort = Settings.WebServerModule.WebExternalPort;
            var port = Settings.WebServerModule.WebListenPort;
            try
            {
                if (request.HttpMethod != HttpMethod.Get.ToString())
                    return false;
                if ((request.Url.LocalPath == "/callback.php" || request.Url.LocalPath == $"{extPort}/callback.php" || request.Url.LocalPath == $"{port}/callback.php")
                    && request.Url.Query.Contains("&state=9"))
                {
                    var prms = request.Url.Query.TrimStart('?').Split('&');
                    var code = prms[0].Split('=')[1];
                    // var state = prms.Length > 1 ? prms[1].Split('=')[1] : null;

                    var result = await WebAuthModule.GetCharacterIdFromCode(code, Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret);
                    if (result == null)
                    {
                        var message = LM.Get("ESIFailure");
                        await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", message)
                            .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{backText}", LM.Get("backText")), response);
                        return true;
                    }

                    var characterID = result[0];
                    var numericCharId = Convert.ToInt32(characterID);

                    if (string.IsNullOrEmpty(characterID))
                    {
                        await LogHelper.LogWarning("Bad or outdated notify feed request!");
                        await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuthNotifyFail)
                            .Replace("{message}", LM.Get("authTokenBadRequest"))
                            .Replace("{header}", LM.Get("authTokenHeader")).Replace("{body}", LM.Get("authTokenBodyFail")).Replace("{backText}", LM.Get("backText")), response);
                        return true;
                    }

                    if (TickManager.GetModule<NotificationModule>().Settings.NotificationFeedModule.Groups.Values.All(g => g.CharacterID != numericCharId))
                    {
                        await LogHelper.LogWarning($"Unathorized notify feed request from {characterID}");
                        await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuthNotifyFail)
                            .Replace("{message}", LM.Get("authTokenInvalid"))
                            .Replace("{header}", LM.Get("authTokenHeader")).Replace("{body}", LM.Get("authTokenBodyFail")).Replace("{backText}", LM.Get("backText")), response);
                        return true;
                    }

                    var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterID, true);

                    await SQLHelper.SQLiteDataInsertOrUpdateTokens(result[1] ?? "", characterID, null);
                    await LogHelper.LogInfo($"Notification feed added for character: {characterID}", LogCat.AuthWeb);
                    await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuthNotifySuccess)
                        .Replace("{body2}", LM.Get("authTokenRcv2", rChar.name))
                        .Replace("{body}", LM.Get("authTokenRcv")).Replace("{header}", LM.Get("authTokenHeader")).Replace("{backText}", LM.Get("backText")), response);
                    return true;
                }                
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }

            return false;
        }


        private async Task UpdateNotificationList(string groupName, string filterName, bool isNew)
        {
            if (isNew)
                await SQLHelper.SQLiteDataInsertOrUpdate("notificationsList", new Dictionary<string, object>
                {
                    {"id", _lastNotification},
                    {"groupName", groupName},
                    {"filterName", filterName},
                });
            else 
                await SQLHelper.SQLiteDataUpdate("notificationsList", "id", _lastNotification, new Dictionary<string, object>
                {
                    {"groupName", groupName},
                    {"filterName", filterName},
                });

        }

        private async Task SetLastNotificationId(int id, string groupName = null, string filterName = null)
        {
            bool isNew = _lastNotification == 0;
            _lastNotification = id;

            if (!string.IsNullOrEmpty(groupName) && !string.IsNullOrEmpty(filterName))
                await UpdateNotificationList(groupName, filterName, isNew);
        }

        #endregion

    }
}
