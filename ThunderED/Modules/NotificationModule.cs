using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;

namespace ThunderED.Modules
{
    public class NotificationModule: AppModuleBase
    {
        private DateTime _nextNotificationCheck = DateTime.FromFileTime(0);
        private int _lastNotification;
        private volatile bool _isRunning;

        public override LogCat Category => LogCat.Notification;
        public override async Task Run(object prm)
        {
            await NotificationFeed();
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
            if(_isRunning) return;
            _isRunning = true;
            try
            {
                if (DateTime.Now > _nextNotificationCheck)
                {
                    await LogHelper.LogInfo("Running Notification Check", Category, LogToConsole, false);
                    var guildID = SettingsManager.GetULong("config", "discordGuildId");
                    var notifyCharKeys = SettingsManager.GetSubList("notifications", "keys");
                    var filters = SettingsManager.GetSubList("notifications", "filters").ToDictionary(x => x.Key, x => x.Value);

                    foreach (var key in notifyCharKeys)
                    {
                        var characterID = key["characterID"];
                        var overrideChannel = Convert.ToUInt64(key["overrideChannel"]);

                        var nData = await SQLiteHelper.SQLiteDataQuery("notifications", "lastNotificationID", "characterID", characterID);
                        _lastNotification = string.IsNullOrEmpty(nData) ? 0 : Convert.ToInt32(nData);

                        var rToken = await SQLiteHelper.SQLiteDataQuery("refreshTokens", "token", "id", Convert.ToInt32(characterID));
                        var token = await APIHelper.ESIAPI.RefreshToken(rToken, SettingsManager.Get("auth", "ccpAppClientId"), SettingsManager.Get("auth", "ccpAppSecret"));
                        if(!string.IsNullOrEmpty(token))
                            await LogHelper.LogInfo($"Checking characterID:{characterID}", Category, LogToConsole, false);
                        else continue;


                        var notifications = (token == null ? null : await APIHelper.ESIAPI.GetNotifications(Reason, characterID, token)) ?? new List<JsonClasses.Notification>();

                        if (_lastNotification == 0)
                        {
                            var now = DateTime.Now;
                            notifications = notifications.Where(a =>
                            {
                                DateTime.TryParse(a.timestamp, out var timestamp);
                                return (now - timestamp).Days < 7;
                            }).ToList();
                        }
                        var notificationsSort = notifications.OrderBy(x => x.notification_id).ToList();

                        //check if there are new notifications to process
                        if (notifications.Count > 0 && notificationsSort.Last().notification_id != _lastNotification)
                        {
                            foreach (var notification in notificationsSort)
                            {
                                try
                                {
                                    if (notification.notification_id > _lastNotification)
                                    {
                                        if (filters.ContainsKey(notification.type) || filters.ContainsKey("ALL"))
                                        {
                                            //skip remembered notifications
                                            if (!string.IsNullOrEmpty(await SQLiteHelper.SQLiteDataQuery("notificationsList", "id", "id", notification.notification_id)))
                                            {
                                                await SetLastNotificationId(notification.notification_id, characterID);
                                                continue;
                                            }

                                            if (SettingsManager.GetBool("config", "logNewNotifications"))
                                                await LogHelper.LogNotification(notification.type, notification.text);
                                            var filterChannelID = Convert.ToUInt64(filters.FirstOrDefault(x => x.Key == notification.type).Value);
                                            var iChannelId = overrideChannel != 0 && filterChannelID != 0 ? overrideChannel : filterChannelID;
                                            if (iChannelId == 0)
                                            {
                                                await SetLastNotificationId(notification.notification_id, characterID);
                                                continue;
                                            }

                                            var channel = APIHelper.DiscordAPI.GetChannel(guildID, iChannelId);
                                            var data = HelpersAndExtensions.ParseNotificationText(notification.text);
                                            var atCorpName = GetData("corpName", data) ?? LM.Get("Unknown");
                                            var systemId = GetData("solarSystemID", data);
                                            var system = string.IsNullOrEmpty(systemId) ? null : await APIHelper.ESIAPI.GetSystemData(Reason, systemId);
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
                                                }
                                            }

                                            var moonFiredBy = data.ContainsKey("firedBy")
                                                ? ((await APIHelper.ESIAPI.GetCharacterData(Reason, data["firedBy"]))?.name ?? LM.Get("Auto"))
                                                : null;

                                            DateTime.TryParse(notification.timestamp, out var timestamp);
                                            Embed embed;
                                            EmbedBuilder builder;
                                            string textAdd;
                                            switch (notification.type)
                                            {
                                                case "StructureUnderAttack":
                                                    //"text": "allianceID: 99007289\nallianceLinkData:\n- showinfo\n- 16159\n- 99007289\nallianceName: Federation Uprising\narmorPercentage: 100.0\ncharID: 96734115\ncorpLinkData:\n- showinfo\n- 2\n- 98502090\ncorpName: Federal Vanguard\nhullPercentage: 100.0\nshieldPercentage: 99.93084364472988\nsolarsystemID: 30003842\nstructureID: &id001 1026660410904\nstructureShowInfoData:\n- showinfo\n- 35832\n- *id001\nstructureTypeID: 35832\n"
                                                    await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                    builder = new EmbedBuilder()
                                                        .WithColor(new Color(0xdd5353))
                                                        .WithThumbnailUrl(SettingsManager.Get("resources", "imgCitUnderAttack"))
                                                        .WithAuthor(author => author.WithName(string.Format(LM.Get("NotifyHeader_StructureUnderAttack"),
                                                            structureType == null ? LM.Get("structure").ToLower() : structureType.name)))
                                                        .AddInlineField(LM.Get("System"), system?.name)
                                                        .AddInlineField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"))
                                                        .AddInlineField(LM.Get("Corporation"), atCorpName) //.WithUrl($"http://www.zkillboard.com/corporation/{}")
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
                                                    break;
                                                case "StructureWentLowPower":
                                                case "StructureWentHighPower":
                                                {
                                                    //"text": "solarsystemID: 30045335\nstructureID: &id001 1026192163696\nstructureShowInfoData:\n- showinfo\n- 35835\n- *id001\nstructureTypeID: 35835\n"
                                                    await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                    var color = notification.type == "StructureWentLowPower" ? new Color(0xdd5353) : new Color(0x00ff00);
                                                    var text = notification.type == "StructureWentLowPower" ? LM.Get("LowPower") : LM.Get("HighPower");
                                                    builder = new EmbedBuilder()
                                                        .WithColor(color)
                                                        .WithThumbnailUrl(SettingsManager.Get("resources", "imgCitLowPower"))
                                                        .WithAuthor(author =>
                                                            author.WithName(string.Format(LM.Get("StructureWentLowPower"),
                                                                structureType == null ? LM.Get("structure").ToLower() : structureType.name, text)))
                                                        .AddInlineField(LM.Get("System"), system?.name)
                                                        .AddInlineField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"))
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
                                                }
                                                    break;
                                                case "StructureLostArmor":
                                                case "StructureLostShields":
                                                    // "text": "solarsystemID: 30003842\nstructureID: &id001 1026660410904\nstructureShowInfoData:\n- showinfo\n- 35832\n- *id001\nstructureTypeID: 35832\ntimeLeft: 1557974732906\ntimestamp: 131669979190000000\nvulnerableTime: 9000000000\n"
                                                    await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                    textAdd = notification.type == "StructureLostArmor" ? LM.Get("armorSmall") : LM.Get("shieldSmall");
                                                    builder = new EmbedBuilder()
                                                        .WithColor(new Color(0xdd5353))
                                                        .WithThumbnailUrl(SettingsManager.Get("resources", "imgCitLostShield"))
                                                        .WithAuthor(author =>
                                                            author.WithName(string.Format(LM.Get("StructureLostArmor"),
                                                                structureType == null ? LM.Get("Structure") : structureType.name, textAdd)))
                                                        .AddInlineField(LM.Get("System"), system?.name)
                                                        .AddInlineField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"))
                                                        .AddInlineField("Time Left", timeleft ?? LM.Get("Unknown"))
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
                                                    break;
                                                case "StructureDestroyed":
                                                case "StructureOnline":
                                                {
                                                    await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                    var owner = GetData("ownerCorpName", data) ?? LM.Get("Unknown");
                                                    var iUrl = notification.type == "StructureDestroyed"
                                                        ? SettingsManager.Get("imgCitDestroyed", "imgCitLostShield")
                                                        : SettingsManager.Get("imgCitOnline", "imgCitLostShield");
                                                    var text = notification.type == "StructureDestroyed"
                                                        ? string.Format(LM.Get("StructureDestroyed"), owner, structureType == null ? LM.Get("Unknown") : structureType.name)
                                                        : string.Format(LM.Get("StructureOnline"), structureType == null ? LM.Get("Unknown") : structureType.name);
                                                    var color = notification.type == "StructureDestroyed" ? new Color(0xdd5353) : new Color(0x00ff00);
                                                    builder = new EmbedBuilder()
                                                        .WithColor(color)
                                                        .WithThumbnailUrl(iUrl)
                                                        .WithAuthor(author =>
                                                            author.WithName(text))
                                                        .AddInlineField(LM.Get("System"), system?.name)
                                                        .AddInlineField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"))
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
                                                }
                                                    break;
                                                case "StructureAnchoring":
                                                {
                                                    await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                    var owner = GetData("ownerCorpName", data) ?? LM.Get("Unknown");
                                                    var text = string.Format(LM.Get("StructureAnchoring"), owner, structureType == null ? LM.Get("Structure") : structureType.name);
                                                    builder = new EmbedBuilder()
                                                        .WithColor(new Color(0xff0000))
                                                        .WithThumbnailUrl(SettingsManager.Get("resources", "imgCitAnchoring"))
                                                        .WithAuthor(author =>
                                                            author.WithName(text))
                                                        .AddInlineField(LM.Get("System"), system?.name)
                                                        .AddInlineField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"))
                                                        .AddInlineField(LM.Get("TimeLeft"), timeleft ?? LM.Get("Unknown"))
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
                                                }
                                                    break;
                                                case "StructureFuelAlert":
                                                    //"text": "listOfTypesAndQty:\n- - 307\n  - 4246\nsolarsystemID: 30045331\nstructureID: &id001 1027052813591\nstructureShowInfoData:\n- showinfo\n- 35835\n- *id001\nstructureTypeID: 35835\n"
                                                    await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                    builder = new EmbedBuilder()
                                                        .WithColor(new Color(0xf2882b))
                                                        .WithThumbnailUrl(SettingsManager.Get("resources", "imgCitFuelAlert"))
                                                        .WithAuthor(author => author.WithName(string.Format(LM.Get("StructureFuelAlert"),
                                                            structureType == null ? LM.Get("Structure") : structureType.name)))
                                                        .AddInlineField(LM.Get("System"), system?.name)
                                                        .AddInlineField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"))
                                                        .AddInlineField(LM.Get("Fuel"), itemQuantity == 0 ? LM.Get("Unknown") : $"{itemQuantity} {itemName}")
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
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
                                                        .WithThumbnailUrl(SettingsManager.Get("resources", "imgMoonComplete"))
                                                        .WithAuthor(author =>
                                                            author.WithName(string.Format(LM.Get("MoonminingExtractionFinished"),
                                                                structureType == null ? LM.Get("Structure") : structureType.name)))
                                                        .AddInlineField(LM.Get("Structure"), structureNameDirect ?? LM.Get("Unknown"))
                                                        .AddInlineField(LM.Get("Composition"), compText.ToString())
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
                                                    break;
                                                case "MoonminingLaserFired":
                                                    //"text": "firedBy: 91684736\nfiredByLink: <a href=\"showinfo:1386\/\/91684736\">Mike Myzukov<\/a>\nmoonID: 40349232\nmoonLink: <a href=\"showinfo:14\/\/40349232\">Teskanen IV - Moon 14<\/a>\noreVolumeByType:\n  45513: 241789.6056930224\n  46676: 930097.5066294272\n  46681: 465888.4702051679\n  46687: 1248541.084139049\nsolarSystemID: 30045335\nsolarSystemLink: <a href=\"showinfo:5\/\/30045335\">Teskanen<\/a>\nstructureID: 1026192163696\nstructureLink: <a href=\"showinfo:35835\/\/1026192163696\">Teskanen - Nebula Prime<\/a>\nstructureName: Teskanen - Nebula Prime\nstructureTypeID: 35835\n"
                                                    await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);

                                                    builder = new EmbedBuilder()
                                                        .WithColor(new Color(0xb386f7))
                                                        .WithThumbnailUrl(SettingsManager.Get("resources", "imgMoonComplete"))
                                                        .WithAuthor(author =>
                                                            author.WithName(string.Format(LM.Get("MoonminingLaserFired"),
                                                                structureType == null ? LM.Get("Structure") : structureType.name)))
                                                        .AddInlineField(LM.Get("Structure"), structureNameDirect ?? LM.Get("Unknown"))
                                                        .AddInlineField(LM.Get("FiredBy"), moonFiredBy)
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
                                                    break;
                                                case "CharLeftCorpMsg":
                                                case "CharAppAcceptMsg":
                                                {
                                                    await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                    var character = await APIHelper.ESIAPI.GetCharacterData(Reason, GetData("charID", data), true);
                                                    var corp = await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("corpID", data), true);
                                                    var text = notification.type == "CharLeftCorpMsg"
                                                        ? string.Format(LM.Get("CharLeftCorpMsg"), character?.name, corp?.name)
                                                        : string.Format(LM.Get("CharAppAcceptMsg"), character?.name, corp?.name);
                                                    var color = notification.type == "" ? new Color(0xdd5353) : new Color(0x00ff00);
                                                    builder = new EmbedBuilder()
                                                        .WithColor(color)
                                                        .WithAuthor(author => author.WithName(text).WithUrl($"https://zkillboard.com/character/{GetData("charID", data)}/"))
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
                                                }
                                                    break;

                                                case "SovStructureDestroyed":
                                                {
                                                    await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                    builder = new EmbedBuilder()
                                                        .WithColor(new Color(0xdd5353))
                                                        .WithAuthor(author => author.WithName(string.Format(LM.Get("SovStructureDestroyed"), structureType?.name, system?.name)))
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
                                                }
                                                    break;

                                                case "SovStationEnteredFreeport":
                                                {
                                                    await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                    var exittime = DateTime.FromFileTime(Convert.ToInt64(GetData("freeportexittime", data)));
                                                    builder = new EmbedBuilder()
                                                        .WithColor(new Color(0xdd5353))
                                                        .WithAuthor(
                                                            author => author.WithName(string.Format(LM.Get("SovStationEnteredFreeport"), structureType?.name, system?.name)))
                                                        .AddInlineField("Exit Time", exittime)
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
                                                }
                                                    break;
                                                case "StationServiceDisabled":
                                                {
                                                    await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                    builder = new EmbedBuilder()
                                                        .WithColor(new Color(0xdd5353))
                                                        .WithThumbnailUrl(SettingsManager.Get("resources", "imgCitServicesOffline"))
                                                        .WithAuthor(author => author.WithName(string.Format(LM.Get("StationServiceDisabled"), structureType?.name, system?.name)))
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
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
                                                            author.WithName(string.Format(LM.Get("SovCommandNodeEventStarted"), cmp, system?.name, constellation?.name)))
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
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
                                                            author.WithName(string.Format(LM.Get("SovStructureReinforced"), cmp, system?.name)))
                                                        .AddInlineField("Decloak Time", decloakTime.ToString())
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
                                                }
                                                    break;

                                                case "EntosisCaptureStarted":
                                                {
                                                    await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                    builder = new EmbedBuilder()
                                                        .WithColor(new Color(0xdd5353))
                                                        .WithAuthor(author => author.WithName(string.Format(LM.Get("EntosisCaptureStarted"), structureType?.name, system?.name)))
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
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
                                                        ? SettingsManager.Get("resources", "imgWarDeclared")
                                                        : SettingsManager.Get("resources", "imgWarInvalidate");

                                                    var template = notification.type == "AllWarDeclaredMsg" || notification.type == "CorpWarDeclaredMsg"
                                                        ? $"{(isAllianceDecl ? LM.Get("Alliance") : LM.Get("Corporation"))} {declName} {LM.Get("declaresWarAgainst")} {declNameAgainst}!"
                                                        : $"{(isAllianceDecl ? LM.Get("Alliance") : LM.Get("Corporation"))} {declName} {LM.Get("invalidatesWarAgainst")} {declNameAgainst}!";
                                                    var template2 = notification.type == "AllWarDeclaredMsg" || notification.type == "CorpWarDeclaredMsg"
                                                        ? string.Format(LM.Get("fightWillBegin"), GetData("delayHours", data))
                                                        : string.Format(LM.Get("fightWillEnd"),  GetData("delayHours", data));
                                                    var color = notification.type == "AllWarDeclaredMsg" || notification.type == "CorpWarDeclaredMsg"
                                                        ? new Color(0xdd5353)
                                                        : new Color(0x00ff00);
                                                    builder = new EmbedBuilder()
                                                        .WithColor(color)
                                                        .WithThumbnailUrl(iUrl)
                                                        .WithAuthor(author => author.WithName(template))
                                                        .AddInlineField(LM.Get("Note"), template2)
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
                                                }
                                                    break;
                                                case "AllyJoinedWarAggressorMsg":
                                                {
                                                    await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                    var ally = (await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("allyID", data)))?.name ??
                                                               (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("allyID", data)))?.name;
                                                    var defender = (await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("defenderID", data)))?.name ??
                                                                   (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("defenderID", data)))?.name;
                                                    builder = new EmbedBuilder()
                                                        .WithColor(new Color(0xff0000))
                                                        .WithThumbnailUrl(SettingsManager.Get("resources", "imgWarAssist"))
                                                        .WithAuthor(author => author.WithName(string.Format(LM.Get("AllyJoinedWarAggressorMsg"), ally, defender)))
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
                                                }
                                                    break;
                                                case "AllyJoinedWarDefenderMsg":
                                                {
                                                    await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                    var ally = (await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("allyID", data)))?.name ??
                                                               (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("allyID", data)))?.name;
                                                    var defender = (await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("defenderID", data)))?.name ??
                                                                   (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("defenderID", data)))?.name;
                                                    var agressor = (await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("agressorID", data)))?.name ??
                                                                   (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("agressorID", data)))?.name;
                                                    builder = new EmbedBuilder()
                                                        .WithColor(new Color(0xff0000))
                                                        .WithThumbnailUrl(SettingsManager.Get("resources", "imgWarAssist"))
                                                        .WithAuthor(author => author.WithName(string.Format(LM.Get("AllyJoinedWarDefenderMsg"), ally, defender, agressor)))
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
                                                }
                                                    break;
                                                case "AllyJoinedWarAllyMsg":
                                                {
                                                    await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                    var agressor = (await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("agressorID", data)))?.name ??
                                                                   (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("agressorID", data)))?.name;
                                                    var ally = (await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("allyID", data)))?.name ??
                                                               (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("allyID", data)))?.name;
                                                    var defender = (await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("defenderID", data)))?.name ??
                                                                   (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("defenderID", data)))?.name;
                                                    builder = new EmbedBuilder()
                                                        .WithColor(new Color(0x00ff00))
                                                        .WithThumbnailUrl(SettingsManager.Get("resources", "imgWarAssist"))
                                                        .WithAuthor(author => author.WithName(string.Format(LM.Get("AllyJoinedWarAllyMsg"), ally, defender, agressor)))
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
                                                }
                                                    break;
                                                case "FWAllianceWarningMsg":
                                                {
                                                    //"text": "allianceID: 99005333\ncorpList: <br>Quarian Fleet - standings:-0.0500\nfactionID: 500001\nrequiredStanding: 0.0001\n"
                                                    await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);
                                                    var allianceId = GetData("allianceID", data);
                                                    var ally = !string.IsNullOrEmpty(allianceId)
                                                        ? (await APIHelper.ESIAPI.GetAllianceData(Reason, allianceId, true))?.name
                                                        : LM.Get("Unknown");
                                                    var corp = data.ContainsKey("corpList")
                                                        ? data["corpList"].Trim().Replace("<br>", "").Replace(" - standings", "")
                                                        : LM.Get("Unknown");
                                                    var required = GetData("requiredStanding", data);

                                                    builder = new EmbedBuilder()
                                                        .WithColor(new Color(0xff0000))
                                                        .WithThumbnailUrl(SettingsManager.Get("resources", "imgLowFWStand"))
                                                        .WithAuthor(author => author.WithName(string.Format(LM.Get("FWAllianceWarningMsg"), ally)))
                                                        .AddInlineField(LM.Get("BlameCorp"), string.Format(LM.Get("standMissing"), corp, required))
                                                        .WithTimestamp(timestamp);
                                                    embed = builder.Build();

                                                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "@everyone", embed);
                                                }
                                                    break;
                                            }

                                            await SetLastNotificationId(notification.notification_id, null);
                                        }
                                        await SetLastNotificationId(notification.notification_id, characterID);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    await LogHelper.LogEx("Error Notification", ex, Category);
                                }
                            }                              
                        }
                    }
                    var interval = SettingsManager.GetShort("notifications", "checkIntervalInMinutes");
                    await SQLiteHelper.SQLiteDataUpdate("cacheData", "data", DateTime.Now.AddMinutes(interval).ToString(), "name", "nextNotificationCheck");
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
                _isRunning = false;
            }
        }

        private async Task SetLastNotificationId(int id, string characterID)
        {
            _lastNotification = id;
            await SQLiteHelper.RunCommand($"insert or replace into notificationsList (id) values({_lastNotification})");
            if(!string.IsNullOrEmpty(characterID))
                await SQLiteHelper.SQLiteDataInsertOrUpdateLastNotification(characterID, _lastNotification.ToString());
        }

        #endregion

    }
}
