using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dasync.Collections;
using Discord;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Thd;

namespace ThunderED.Modules
{
    public sealed partial class NotificationModule: AppModuleBase
    {
        private DateTime _nextNotificationCheck = DateTime.FromFileTime(0);
        private DateTime _nextTrackerCheck = DateTime.FromFileTime(0);
        private long _lastNotification;

        public override LogCat Category => LogCat.Notification;

        private readonly ConcurrentDictionary<long, string> _tags = new ConcurrentDictionary<long, string>();
        private readonly ConcurrentDictionary<long, List<JsonClasses.Notification>> _passNotifications = new ConcurrentDictionary<long, List<JsonClasses.Notification>>();
       // private long _lastLoggerNotificationId;
        private DateTime _lastCleanupCheck = DateTime.FromFileTime(0);

        public override async Task Initialize()
        {
            await LogHelper.LogModule("Initializing Notifications module...", Category);
            await WebPartInitialization();

            var data = Settings.NotificationFeedModule.GetEnabledGroups().ToDictionary(pair => pair.Key, pair => pair.Value.CharacterEntities);
            await ParseMixedDataArray(data, MixedParseModeEnum.Member);

            foreach (var (key, value) in Settings.NotificationFeedModule.Tracker.Groups)
            {
                if (value.DiscordChannels == null || value.DiscordChannels.Count == 0)
                {
                    await SendOneTimeWarning(key, $"Group has no Discord channels specified");
                    continue;
                }

                if (value.Notifications == null || value.Notifications.Count == 0)
                {
                    await SendOneTimeWarning(key, $"Group has no Notifications specified");
                    continue;
                }
            }
        }

        public override async Task Run(object prm)
        {
            if(IsRunning || !APIHelper.IsDiscordAvailable) return;
            if (TickManager.IsNoConnection || TickManager.IsESIUnreachable) return;
            IsRunning = true;
            try
            {
                await UpdateTracker2().ConfigureAwait(false);
                await NotificationFeed();
                await CleanupNotifyList();

            }
            finally
            {
                IsRunning = false;
            }
        }

        #region Tracker
        //private readonly ConcurrentDictionary<string, DateTime> _trackerIntervals = new ConcurrentDictionary<string, DateTime>();
        //private readonly ConcurrentDictionary<string, string> _trackerEtags = new ConcurrentDictionary<string, string>();
        private readonly List<long> _trackerNotifications = new List<long>();

        private readonly ConcurrentDictionary<long, TrackerData> _trackerKeys = new ConcurrentDictionary<long, TrackerData>();

        private class TrackerData
        {
            public string Etag;
            public readonly DateTime KeyUpdate;
            public readonly string Key;

            public TrackerData(string key)
            {
                Key = key;
                KeyUpdate = DateTime.Now;
                Etag = null;
            }

        }



        private DateTime _lastTrackerCheckTime = DateTime.Now.Subtract(TimeSpan.FromMinutes(15));
        private volatile bool _isTrackerRunning;

        private async Task UpdateTracker2()
        {
            if (_isTrackerRunning) return;
            _isTrackerRunning = true;
            try
            {

                const bool logConsole = false;
                const bool logFile = false;

                if (DateTime.Now > _nextTrackerCheck)
                {
                    _nextTrackerCheck =
                        DateTime.Now.AddMinutes(Settings.NotificationFeedModule.Tracker.UpdateIntervalInMinutes);
                    await LogHelper.LogInfo($"Starting tracker update... Count: {_trackerNotifications.Count}. Threads: {Settings.Config.ConcurrentThreadsCount}",
                        LogCat.UpdateTracker);

                    if (_trackerNotifications.Count > 500)
                        _trackerNotifications.RemoveRange(0, 100);


                    await Swatch.Run(async () =>
                    {
                        try
                        {

                            var enabledGroups = Settings.NotificationFeedModule.Tracker.GetEnabledGroups()
                                .Where(a => (a.Value.DiscordChannels?.Any() ?? false) &&
                                            (a.Value.Notifications?.Any() ?? false))
                                .ToDictionary(a => a.Key, a => a.Value);
                            if (!enabledGroups.Any()) return;
                            await LogHelper.LogInfo($"Found {enabledGroups.Count} groups", LogCat.UpdateTracker,
                                logConsole,
                                logFile);

                            var tokens = await DbHelper.GetTokens(TokenEnum.Notification);
                            await LogHelper.LogInfo($"Fetched {tokens.Count} tokens", LogCat.UpdateTracker, logConsole,
                                logFile);

                            var lastCheck = _lastTrackerCheckTime.Subtract(TimeSpan.FromSeconds(1));
                            _lastTrackerCheckTime = DateTime.Now;
                            var allEnabledTypes = enabledGroups.Values.SelectMany(a => a.Notifications).Distinct()
                                .ToList();

                            await tokens.ParallelForEachAsync(async token =>
                            {
                                await LogHelper.LogInfo($"Token charId={token.CharacterId}", LogCat.UpdateTracker,
                                    logConsole, logFile);
                                try
                                {

                                    #region Apply global filters

                                    var feederChar =
                                        await APIHelper.ESIAPI.GetCharacterData(Reason, token.CharacterId, true);
                                    //skip npc corp characters
                                    if (feederChar == null || (Settings.NotificationFeedModule.Tracker.SkipCharactersInNpcCorps &&
                                        APIHelper.ESIAPI.IsNpcCorporation(feederChar.corporation_id)))
                                        return;

                                    var feederCorp =
                                        await APIHelper.ESIAPI.GetCorporationData(Reason, feederChar?.corporation_id);
                                    var feederAlliance = feederChar?.alliance_id > 0
                                        ? await APIHelper.ESIAPI.GetAllianceData(Reason, feederChar?.alliance_id)
                                        : null;
                                    var skip = false;
                                    if (feederChar == null || feederCorp == null) return;

                                    //filter all chars that doesn't fit input 
                                    if (Settings.NotificationFeedModule.Tracker.GlobalFilterIn.Any())
                                    {
                                        foreach (var filter in Settings.NotificationFeedModule.Tracker.GlobalFilterIn)
                                        {
                                            if (!feederChar.name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                                                !feederCorp.name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                                                (feederAlliance == null ||
                                                 !feederAlliance.name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                                            {
                                                skip = true;
                                                break;
                                            }
                                        }
                                    }

                                    //filter out all chars that falls into out
                                    foreach (var filter in Settings.NotificationFeedModule.Tracker.GlobalFilterOut)
                                    {
                                        if (feederChar.name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                            feederCorp.name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                            (feederAlliance != null &&
                                             feederAlliance.name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                                        {
                                            skip = true;
                                            break;
                                        }
                                    }

                                    if (skip) return;

                                    #endregion

                                    if (!_trackerKeys.ContainsKey(token.CharacterId) ||
                                        (DateTime.Now - _trackerKeys[token.CharacterId].KeyUpdate).Minutes >= 19)
                                    {
                                        await LogHelper.LogInfo($"Key update...", LogCat.UpdateTracker, logConsole,
                                            logFile);

                                        var key = (await APIHelper.ESIAPI.GetAccessTokenWithScopes(token, new ESIScope().AddNotifications().AddUniverseStructure().Merge()))?.Result;
                                        await LogHelper.LogInfo($"Key: {key != null}", LogCat.UpdateTracker, logConsole,
                                            logFile);
                                        if (key == null) return;
                                        _trackerKeys.AddOrUpdate(token.CharacterId, new TrackerData(key));
                                    }

                                    var track = _trackerKeys[token.CharacterId];
                                    var cacheHeader = $"nmt|{token.CharacterId}";

                                    //try fetch last ETAG
                                    if (string.IsNullOrEmpty(track.Etag))
                                    {
                                        var tag = await DbHelper.GetCache<string>(cacheHeader, 60);
                                        if (tag != null)
                                            track.Etag = tag;
                                    }

                                    //get notifications
                                    var nResult = await APIHelper.ESIAPI.GetNotifications(Reason, token.CharacterId,
                                        track.Key, track.Etag);
                                    await LogHelper.LogInfo(
                                        $"Notif raw: {nResult?.Result?.Count} Result: {nResult?.Data?.ErrorCode} NoCon: {nResult?.Data?.IsNoConnection}",
                                        LogCat.UpdateTracker, logConsole, logFile);
                                    //continue if failed badly
                                    if (nResult?.Result == null) return;
                                    //update ETAG
                                    track.Etag = nResult.Data.ETag;
                                    await DbHelper.UpdateCache(cacheHeader, track.Etag);
                                    //continue if no new data
                                    if (nResult.Data.IsNotModified) return;

                                    var notifications = nResult.Result.Where(a =>
                                            a.Date >= lastCheck && allEnabledTypes.ContainsCaseInsensitive(a.type))
                                        .ToList();
                                    await LogHelper.LogInfo($"Notif filtered: {notifications.Count}",
                                        LogCat.UpdateTracker,
                                        logConsole, logFile);

                                    foreach (var notification in notifications)
                                    {
                                        if (_trackerNotifications.Contains(notification.notification_id))
                                            continue;

                                        foreach (var (key, value) in enabledGroups)
                                        {
                                            if (!value.Notifications.ContainsCaseInsensitive(notification.type))
                                                continue;

                                            var filters =
                                                Settings.NotificationFeedModule.Tracker.GlobalFilterOut.ToList();
                                            filters.AddRange(value.FilterOut);
                                            filters = filters.Distinct().ToList();

                                            var outResult = await OutPutNotification(notification,
                                                value.DiscordChannels,
                                                track.Key,
                                                token.CharacterId, null, null, filters, true);
                                            if (outResult &&
                                                !_trackerNotifications.Contains(notification.notification_id))
                                                _trackerNotifications.Add(notification.notification_id);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    await LogHelper.LogEx(ex, Category);
                                }
                            }, Settings.Config.ConcurrentThreadsCount);
                        }
                        catch (Exception ex)
                        {
                            await LogHelper.LogEx(ex, Category);
                        }
                    }).ContinueWith(async msec=> await LogHelper.LogWarning($"Tracker: {TimeSpan.FromMilliseconds(msec.Result).ToFormattedString()}", LogCat.UpdateTracker, logConsole));

                    
                }
            }
            finally
            {
                _isTrackerRunning = false;
            }
        }

        #endregion

        #region Notifications
        private async Task NotificationFeed()
        {
            try
            {
                if (DateTime.Now > _nextNotificationCheck)
                {
                    await LogHelper.LogModule("Running Notifications module check...", Category);
                    //update timers
                    var interval = Settings.NotificationFeedModule.CheckIntervalInMinutes;
                    await DbHelper.UpdateCacheDataEntry("nextNotificationCheck", DateTime.Now.AddMinutes(interval).ToString(CultureInfo.InvariantCulture));
                    _nextNotificationCheck = DateTime.Now.AddMinutes(interval);


                    //var guildID = Settings.Config.DiscordGuildId;

                    foreach (var (groupName, group) in Settings.NotificationFeedModule.GetEnabledGroups())
                    {
                        var ids = GetParsedCharacters(groupName);
                        if (ids == null || !ids.Any() || ids.All(a => a == 0))
                        {
                            await LogHelper.LogError($"[CONFIG] Notification group {groupName} has no character specified!");
                            continue;
                        }

                        if (group.DefaultDiscordChannelID == 0)
                        {
                            await LogHelper.LogError($"[CONFIG] Notification group {groupName} has no DefaultDiscordChannelID specified!");
                            continue;
                        }

                        //skip empty group
                        if (group.Filters.Values.All(a => a.Notifications.Count == 0)) continue;


                        foreach (var charId in ids)
                        {
                            var rToken = await DbHelper.GetToken(charId, TokenEnum.Notification);
                            if (rToken == null)
                            {
                                await SendOneTimeWarning(charId + 100, $"Failed to get notifications refresh token for character {charId}! User is not authenticated.");
                                continue;
                            }

                            var tq = await APIHelper.ESIAPI.GetAccessTokenWithScopes(rToken, new ESIScope().AddNotifications().AddUniverseStructure().Merge(), $"From {Category} | Char ID: {charId}");
                            var token = tq.Result;
                            if (tq.Data.IsNoConnection) return;
                            if (string.IsNullOrEmpty(token))
                            {
                                if (tq.Data.IsNotValid && !tq.Data.IsNoConnection)
                                {
                                    await SendOneTimeWarning(charId,
                                        $"Notifications token for character {charId} is outdated or no more valid!");
                                    await LogHelper.LogWarning($"Deleting invalid notification refresh token for {charId}", Category);
                                    await DbHelper.DeleteToken(charId, TokenEnum.Notification);
                                }
                                else
                                    await LogHelper.LogWarning(
                                        $"Unable to get notifications token for character {charId}. Current check cycle will be skipped. {tq.Data.ErrorCode}({tq.Data.Message})");
                                continue;
                            }
                            await LogHelper.LogInfo($"Checking characterID:{charId}", Category, LogToConsole, false);

                            var etag = _tags.GetOrNull(charId);
                            var result = await APIHelper.ESIAPI.GetNotifications(Reason, charId, token, etag);
                            if(result == null) continue;
                            _tags.AddOrUpdateEx(charId, result.Data.ETag);
                            //abort if no connection
                            if(result.Data.IsNoConnection)
                                return;
                            if (result.Data.IsNotModified || result.Result == null)
                            {
                                if (!_passNotifications.ContainsKey(charId))
                                    continue;
                                result.Result = _passNotifications[charId];
                            }
                            else _passNotifications.AddOrUpdate(charId, result.Result);

                            var notifications = result.Result;

                             /*notifications.Add(new JsonClasses.Notification
                             {
                                 text = @"daysUntilAbandon: 2\nisCorpOwned: false\nsolarsystemID: 30004587\nstructureID: &id001 1035894524206\nstructureLink: <a href=\""showinfo:35832//1035894524206\"">7X-02R - Villa Kebab</a>\nstructureShowInfoData:\n- showinfo\n- 35832\n- *id001\nstructureTypeID: 35832\n",


                                 notification_id = 1525221813,
                                 type = "StructureImpendingAbandonmentAssetsAtRisk",
                                 timestamp = "2021-02-25T11:20:00Z"
                             });*/



                            //process filters
                            foreach (var filterPair in group.Filters)
                            {
                                var filter = filterPair.Value;
                                _lastNotification = await DbHelper.GetLastNotification(groupName, filterPair.Key);

                                var fNotifications = new List<JsonClasses.Notification>();
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
                                        _lastNotification = notifications.Max(a => a.notification_id);
                                        await UpdateNotificationList(groupName, filterPair.Key, true);
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
                                            //if (Settings.Config.LogNewNotifications)
                                             //   await LogHelper.LogNotification($"{notification.type} [{notification.notification_id}]", notification.text);

                                            var discordChannel = APIHelper.DiscordAPI.GetChannel(filter.ChannelID != 0 ? filter.ChannelID : group.DefaultDiscordChannelID);
                                            //var atCorpName = GetData("corpName", data) ?? LM.Get("Unknown");

                                            #region mentions

                                            var mention = filter.DefaultMention ?? " ";
                                            if (filter.CharMentions.Count > 0)
                                            {
                                                var list = filter.CharMentions.Select(a =>
                                                        DbHelper.GetAuthUser(a).GetAwaiter().GetResult()?.DiscordId ?? 0).Where(a => a != 0)
                                                    .ToList();
                                                if (list.Count > 0)
                                                {
                                                    var mList = list.Select(a =>
                                                    {
                                                        try
                                                        {
                                                            return APIHelper.DiscordAPI.GetUserMention(a).GetAwaiter().GetResult();
                                                        }
                                                        catch
                                                        {
                                                            return null;
                                                        }
                                                    }).Where(a => a != null);
                                                    if (mList.Any())
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
                                            #endregion

                                            #region pre checks
                                            var data = HelpersAndExtensions.ParseNotificationText(notification.text);
                                            if (notification.type.Equals("StructureUnderAttack", StringComparison.OrdinalIgnoreCase))
                                            {
                                                var aggCharId = GetData("charID", data);

                                                //skip NPC bash
                                                if (APIHelper.ESIAPI.IsNpcCharacter(aggCharId) && filter.SpecialSettings
                                                    .DoNotReportNpcBashForCitadels)
                                                {
                                                    continue;
                                                }
                                            }
                                            #endregion

                                            var channels = new List<ulong> {discordChannel.Id};

                                            await OutPutNotification(notification,channels, token, charId, mention, data);

                                            await SetLastNotificationId(notification.notification_id, groupName, filterPair.Key);
                                            
                                        }
                                        catch (Exception ex)
                                        {
                                            await LogHelper.LogEx($"Error Notification: {notification?.type} - {notification?.text}\n", ex, Category);
                                        }
                                    }
                                }
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
            finally
            {
                _passNotifications.Clear();
            }
        }

        private async Task<bool> OutPutNotification(JsonClasses.Notification notification, List<ulong> discordChannels,
            string token, long charId, string mention = null, Dictionary<string, string> data = null,
            List<string> valueFilterOut=null, bool fromTracker = false)
        {
            Embed embed;
            EmbedBuilder builder;
            string textAdd;
            DateTime.TryParse(notification.timestamp, out var localTimestamp);
            var timestamp = localTimestamp.ToUniversalTime();

            var feederChar = await APIHelper.ESIAPI.GetCharacterData(Reason, charId);
            var feederCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, feederChar?.corporation_id);
            var feederAlliance = feederChar?.alliance_id > 0 ? await APIHelper.ESIAPI.GetAllianceData(Reason, feederChar?.alliance_id) : null;

            mention ??= $"From {feederChar?.name} ";

            if (Settings.Config.LogNewNotifications)
                await LogHelper.LogNotification($"{notification.type} [{notification.notification_id}]", notification.text);

            //check word filters
            if (valueFilterOut != null)
            {
                foreach (var filter in valueFilterOut)
                {
                    if (feederChar.name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        feederCorp.name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        (feederAlliance != null &&
                         feederAlliance.name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                        return false;
                }
            }

            data ??= HelpersAndExtensions.ParseNotificationText(notification.text);


            #region Process data
            var systemId = GetData("solarSystemID", data);
            var system = string.IsNullOrEmpty(systemId) ? null : await APIHelper.ESIAPI.GetSystemData(Reason, systemId);
            var systemName = system == null ? LM.Get("Unknown") : (system.SolarSystemName == system.SolarSystemId.ToString() ? "Abyss" : system.SolarSystemName);
            var structureId = GetData("structureID", data);
            var strunctureNumId = string.IsNullOrEmpty(structureId) ? 0 : long.Parse(structureId);
            var structure = string.IsNullOrEmpty(structureId) ? null : await APIHelper.ESIAPI.GetUniverseStructureData(Reason, structureId, token);
            //structure = structure != null ? structure : (await APIHelper.ESIAPI.GetCorpStructures(Reason, structureId, token))?.FirstOrDefault(a=> a.structure_id == strunctureNumId);
            var structureNameDirect = GetData("structureName", data);
            //parse structure name from link
            var sname2 = GetData("structureLink", data);

            if (!string.IsNullOrEmpty(sname2) && string.IsNullOrEmpty(structureNameDirect))
            {
                try
                {
                    var from = sname2.IndexOf('>') + 1;
                    var to = sname2.IndexOf("</a>");
                    if (from != 0 && to != -1)
                        structureNameDirect = sname2.Substring(from, to - from);
                }
                catch
                {
                    // ignore
                }
            }



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
                    itemName = (await APIHelper.ESIAPI.GetTypeId(Reason, keys[ltqIndex + 2].Split(' ').Last()))?.Name ?? LM.Get("Unknown");
                }
                catch
                {
                    //ignore
                }
            }

            Dictionary<string, string> oreComposition = null;
            Dictionary<long, int> oreCompositionRaw = null;

            if (data.ContainsKey("oreVolumeByType"))
            {
                try
                {
                    var keys = data.Keys.ToList();
                    var ltqIndex = keys.IndexOf("oreVolumeByType");
                    var endIndex = keys.IndexOf("solarSystemLink");
                    if (endIndex == -1)
                        endIndex = keys.IndexOf("solarSystemID");
                    var pass = endIndex - ltqIndex;
                    if (pass > 0)
                    {
                        oreComposition = new Dictionary<string, string>();
                        oreCompositionRaw = new Dictionary<long, int>();
                        for (int i = ltqIndex + 1; i < endIndex; i++)
                        {
                            if (!keys[i].All(char.IsDigit)) continue;
                            var typeName = (await APIHelper.ESIAPI.GetTypeId(Reason, keys[i])).Name;
                            var value = double.Parse(data[keys[i]].Split('.')[0]).ToString("N");
                            oreComposition.Add(typeName, value);
                            oreCompositionRaw.Add(Convert.ToInt64(keys[i]), Convert.ToInt32(value.Split('.')[0].Replace(",", "")));
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
            #endregion
            await LogHelper.LogInfo($"Sending Notification ({notification.type})", Category);

            switch (notification.type)
            {
                case "TowerAlertMsg": //pos
                    {
                        var struc = await APIHelper.ESIAPI.GetTypeId(Reason, GetData("typeID", data));
                        var moon = await APIHelper.ESIAPI.GetMoon(Reason, GetData("moonID", data));

                        var aggressor = (await APIHelper.ESIAPI.GetCharacterData(Reason, GetData("aggressorID", data)))?.name;
                        var aggCorp = (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("aggressorCorpID", data)))?.name;
                        var aggAllyId = GetData("aggressorAllianceID", data);
                        var aggAlly = string.IsNullOrEmpty(aggAllyId) || aggAllyId == "0"
                            ? null
                            : (await APIHelper.ESIAPI.GetAllianceData(Reason, aggAllyId))?.ticker;
                        var aggText = $"{aggressor} - {aggCorp}{(string.IsNullOrEmpty(aggAlly) ? null : $"[{aggAlly}]")}";

                        builder = new EmbedBuilder()
                            .WithColor(new Color(0xdd5353))
                            .WithThumbnailUrl(Settings.Resources.ImgCitUnderAttack)
                            .WithAuthor(author => author.WithName(LM.Get("NotifyHeader_TowerAlertMsg",
                                    struc?.Name, feederCorp?.name))
                                .WithUrl($"https://zkillboard.com/character/{GetData("aggressorID", data)}"))
                            .AddField(LM.Get("Location"), $"{systemName} - {moon?.name ?? LM.Get("Unknown")}", true)
                            .AddField(LM.Get("Aggressor"), aggText, true)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;
                case "OrbitalAttacked": //customs
                    {
                        var struc = await APIHelper.ESIAPI.GetTypeId(Reason, GetData("typeID", data));
                        var planet = await APIHelper.ESIAPI.GetPlanet(Reason, GetData("planetID", data));

                        var aggressor = (await APIHelper.ESIAPI.GetCharacterData(Reason, GetData("aggressorID", data)))?.name;
                        var aggCorp = (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("aggressorCorpID", data)))?.name;
                        var aggAllyId = GetData("aggressorAllianceID", data);
                        var aggAlly = string.IsNullOrEmpty(aggAllyId) || aggAllyId == "0"
                            ? null
                            : (await APIHelper.ESIAPI.GetAllianceData(Reason, aggAllyId))?.ticker;
                        var aggText = $"{aggressor} - {aggCorp}{(string.IsNullOrEmpty(aggAlly) ? null : $"[{aggAlly}]")}";


                        builder = new EmbedBuilder()
                            .WithColor(new Color(0xdd5353))
                            .WithThumbnailUrl(Settings.Resources.ImgCitUnderAttack)
                            .WithAuthor(author => author.WithName(LM.Get("NotifyHeader_OrbitalAttacked",
                                    struc?.Name, feederCorp?.name))
                                .WithUrl($"https://zkillboard.com/character/{GetData("aggressorID", data)}"))
                            .AddField(LM.Get("Location"), $"{systemName} - {planet?.name ?? LM.Get("Unknown")}", true)
                            .AddField(LM.Get("Aggressor"), aggText, true)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;
                case "OrbitalReinforced":
                    {
                        var struc = await APIHelper.ESIAPI.GetTypeId(Reason, GetData("typeID", data));
                        var planet = await APIHelper.ESIAPI.GetPlanet(Reason, GetData("planetID", data));

                        var agressor = (await APIHelper.ESIAPI.GetCharacterData(Reason, GetData("aggressorID", data)))?.name;
                        var aggCorp = (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("aggressorCorpID", data)))?.name;
                        var aggAllyId = GetData("aggressorAllianceID", data);
                        var aggAlly = string.IsNullOrEmpty(aggAllyId) || aggAllyId == "0"
                            ? null
                            : (await APIHelper.ESIAPI.GetAllianceData(Reason, aggAllyId))?.ticker;
                        var aggText = $"{agressor} - {aggCorp}{(string.IsNullOrEmpty(aggAlly) ? null : $"[{aggAlly}]")}";
                        var exitTime = DateTime.FromFileTime(Convert.ToInt64(GetData("reinforceExitTime", data)));

                        builder = new EmbedBuilder()
                            .WithColor(new Color(0xdd5353))
                            .WithThumbnailUrl(Settings.Resources.ImgCitUnderAttack)
                            .WithAuthor(author => author.WithName(LM.Get("NotifyHeader_OrbitalReinforced",
                                    struc?.Name, feederCorp?.name, exitTime))
                                .WithUrl($"https://zkillboard.com/character/{GetData("aggressorID", data)}"))
                            .AddField(LM.Get("Location"), $"{systemName} - {planet?.name ?? LM.Get("Unknown")}", true)
                            .AddField(LM.Get("Aggressor"), aggText, true)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);

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
                        var aggAlly = string.IsNullOrEmpty(aggAllyId) || aggAllyId == "0"
                            ? null
                            : (await APIHelper.ESIAPI.GetAllianceData(Reason, aggAllyId))?.ticker;
                        var aggText = $"{aggName} - {aggCorp}{(string.IsNullOrEmpty(aggAlly) ? null : $"[{aggAlly}]")}";

                        builder = new EmbedBuilder()
                            .WithColor(new Color(0xdd5353))
                            .WithThumbnailUrl(Settings.Resources.ImgCitUnderAttack)
                            .WithAuthor(author => author.WithName(LM.Get("NotifyHeader_StructureUnderAttack",
                                    structureType == null ? LM.Get("structure").ToLower() : structureType.Name))
                                .WithUrl($"https://zkillboard.com/character/{aggCharId}"))
                            .AddField(LM.Get("System"), systemName, true)
                            .AddField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"), true)
                            .AddField(LM.Get("Aggressor"), aggText, true) //.WithUrl(")
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();
                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);

                        break;
                    }

                case "StructureWentLowPower":
                case "StructureWentHighPower":
                    {
                        //"text": "solarsystemID: 30045335\nstructureID: &id001 1026192163696\nstructureShowInfoData:\n- showinfo\n- 35835\n- *id001\nstructureTypeID: 35835\n"
                        var color = notification.type == "StructureWentLowPower" ? new Color(0xdd5353) : new Color(0x00ff00);
                        var text = notification.type == "StructureWentLowPower" ? LM.Get("LowPower") : LM.Get("HighPower");
                        builder = new EmbedBuilder()
                            .WithColor(color)
                            .WithThumbnailUrl(Settings.Resources.ImgCitLowPower)
                            .WithAuthor(author =>
                                author.WithName(LM.Get("StructureWentLowPower",
                                    (structureType == null ? LM.Get("structure").ToLower() : structureType.Name) ?? LM.Get("Unknown"), text)))
                            .AddField(LM.Get("System"), systemName, true)
                            .AddField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"), true)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);

                    }
                    break;
                case "StructureLostArmor":
                case "StructureLostShields":
                    {
                        // "text": "solarsystemID: 30003842\nstructureID: &id001 1026660410904\nstructureShowInfoData:\n- showinfo\n- 35832\n- *id001\nstructureTypeID: 35832\ntimeLeft: 1557974732906\ntimestamp: 131669979190000000\nvulnerableTime: 9000000000\n"
                        textAdd = notification.type == "StructureLostArmor" ? LM.Get("armorSmall") : LM.Get("shieldSmall");
                        builder = new EmbedBuilder()
                            .WithColor(new Color(0xdd5353))
                            .WithThumbnailUrl(notification.type == "StructureLostShields"
                                ? Settings.Resources.ImgCitLostShield
                                : Settings.Resources.ImgCitLostArmor)
                            .WithAuthor(author =>
                                author.WithName(LM.Get("StructureLostArmor",
                                    structureType == null ? LM.Get("Structure") : structureType.Name, textAdd)))
                            .AddField(LM.Get("System"), systemName, true)
                            .AddField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"), true)
                            .AddField("Time Left", timeleft ?? LM.Get("Unknown"), true)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        if (Settings.Config.ModuleTimers && Settings.TimersModule.AutoAddTimerForReinforceNotifications && !fromTracker)
                        {
                            await DbHelper.UpdateTimer(new ThdTimer
                            {
                                TimerChar = "Auto",
                                Date = (timestamp + TimeSpan.FromTicks(Convert.ToInt64(strTime))),
                                Location = $"{systemName} - {structureType.Name} - {structure?.name}",
                                Stage = notification.type == "StructureLostShields" ? 2 : 1,
                                Type = 2,
                                Owner = "Alliance"
                            });
                        }


                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;
                case "StructureDestroyed":
                case "StructureOnline":
                    {
                        var d = GetData("isAbandoned", data);
                        var isAbandoned = !string.IsNullOrEmpty(d) && Convert.ToBoolean(d);
                        var core = GetData("requiresDeedTypeID", data);
                        if (!string.IsNullOrEmpty(core))
                        {
                            try
                            {
                                var coreType = await APIHelper.ESIAPI.GetTypeId(Reason,
                                    core.RemoveDotValue());
                                if (coreType != null)
                                    core = coreType.Name;
                            }
                            catch
                            {
                                // ignore
                            }
                        }

                        var owner = GetData("ownerCorpName", data) ?? LM.Get("Unknown");
                        var iUrl = notification.type == "StructureDestroyed"
                            ? Settings.Resources.ImgCitDestroyed
                            : Settings.Resources.ImgCitOnline;
                        var text = notification.type == "StructureDestroyed"
                            ? LM.Get("StructureDestroyed", owner, structureType == null ? LM.Get("Unknown") : structureType.Name)
                            : LM.Get("StructureOnline", structureType == null ? LM.Get("Unknown") : structureType.Name);
                        var color = notification.type == "StructureDestroyed" ? new Color(0xdd5353) : new Color(0x00ff00);
                        builder = new EmbedBuilder()
                            .WithColor(color)
                            .WithThumbnailUrl(iUrl)
                            .WithAuthor(author =>
                                author.WithName(text))
                            .AddField(LM.Get("System"), systemName, true)
                            .AddField(string.IsNullOrEmpty(core) ? LM.Get("Abandoned") : LM.Get("NeedCore"), string.IsNullOrEmpty(core) ? LM.Get(isAbandoned ? "webYes" : "webNo") : core, true)
                            .AddField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"), true)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;
                case "StructureAnchoring":
                    {
                        var owner = GetData("ownerCorpName", data) ?? LM.Get("Unknown");
                        var text = LM.Get("StructureAnchoring", owner,
                            structureType == null ? LM.Get("Structure") : structureType.Name);
                        builder = new EmbedBuilder()
                            .WithColor(new Color(0xff0000))
                            .WithThumbnailUrl(Settings.Resources.ImgCitAnchoring)
                            .WithAuthor(author =>
                                author.WithName(text))
                            .AddField(LM.Get("System"), systemName, true)
                            .AddField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"), true)
                            .AddField(LM.Get("TimeLeft"), timeleft ?? LM.Get("Unknown"), true)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;
                case "AllAnchoringMsg":
                    {
                        var corpName = (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("corpID", data) ?? null))?.name ?? LM.Get("Unknown");
                        var allianceName = (await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("allianceID", data) ?? null))?.name;
                        var typeName = (await APIHelper.ESIAPI.GetTypeId(Reason, GetData("typeID", data)))?.Name ?? LM.Get("Unknown");
                        var moonName = (await APIHelper.ESIAPI.GetMoon(Reason, GetData("moonID", data)))?.name ?? LM.Get("Unknown");
                        var text = LM.Get("PosAnchoring", string.IsNullOrEmpty(allianceName) ? corpName : $"{allianceName} - {corpName}");
                        builder = new EmbedBuilder()
                            .WithColor(new Color(0xff0000))
                            .WithThumbnailUrl(Settings.Resources.ImgCitAnchoring)
                            .WithAuthor(author =>
                                author.WithName(text))
                            .AddField(LM.Get("System"), $"{systemName} {LM.Get("AtSmall")} {moonName}", true)
                            .AddField(LM.Get("Structure"), typeName, true)
                            .AddField(LM.Get("TimeLeft"), timeleft ?? LM.Get("Unknown"), true)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;
                case "StructureUnanchoring":
                    {
                        var owner = GetData("ownerCorpName", data) ?? LM.Get("Unknown");
                        var text = LM.Get("StructureUnanchoring", owner,
                            structureType == null ? LM.Get("Structure") : structureType.Name);
                        builder = new EmbedBuilder()
                            .WithColor(new Color(0xff0000))
                            .WithThumbnailUrl(Settings.Resources.ImgCitAnchoring)
                            .WithAuthor(author =>
                                author.WithName(text))
                            .AddField(LM.Get("System"), systemName, true)
                            .AddField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"), true)
                            .AddField(LM.Get("TimeLeft"), timeleft ?? LM.Get("Unknown"), true)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;
                case "TowerResourceAlertMsg":
                    {
                        var typeName = (await APIHelper.ESIAPI.GetTypeId(Reason, GetData("typeID", data)))?.Name ?? LM.Get("Unknown");
                        var moonName = (await APIHelper.ESIAPI.GetMoon(Reason, GetData("moonID", data)))?.name ?? LM.Get("Unknown");
                        var location = $"{systemName}-{moonName}";
                        var corpName = (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("corpID", data) ?? null))?.name ?? LM.Get("Unknown");
                        var allianceName = (await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("allianceID", data) ?? null))?.name;
                        var text = LM.Get("PosFuelAlert", typeName, string.IsNullOrEmpty(allianceName) ? corpName : $"{allianceName} - {corpName}");

                        builder = new EmbedBuilder()
                            .WithColor(new Color(0xf2882b))
                            .WithThumbnailUrl(Settings.Resources.ImgCitFuelAlert)
                            .WithAuthor(author => author.WithName(text))
                            .AddField(LM.Get("System"), location, true)
                            .AddField(LM.Get("Structure"), typeName, true)
                            .AddField(LM.Get("Fuel"), "???", true)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();
                    }
                    break;
                case "StructureFuelAlert":
                    //"text": "listOfTypesAndQty:\n- - 307\n  - 4246\nsolarsystemID: 30045331\nstructureID: &id001 1027052813591\nstructureShowInfoData:\n- showinfo\n- 35835\n- *id001\nstructureTypeID: 35835\n"
                    builder = new EmbedBuilder()
                        .WithColor(new Color(0xf2882b))
                        .WithThumbnailUrl(Settings.Resources.ImgCitFuelAlert)
                        .WithAuthor(author => author.WithName(LM.Get("StructureFuelAlert",
                            structureType == null ? LM.Get("Structure") : structureType.Name)))
                        .AddField(LM.Get("System"), systemName, true)
                        .AddField(LM.Get("Structure"), structure?.name ?? LM.Get("Unknown"), true)
                        .AddField(LM.Get("Fuel"), itemQuantity == 0 ? LM.Get("Unknown") : $"{itemQuantity} {itemName}", true)
                        .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                        .WithTimestamp(timestamp);
                    embed = builder.Build();

                    foreach (var channel in discordChannels)
                        await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    break;
                case "MoonminingExtractionCancelled":
                case "MoonminingExtractionStarted":
                case "MoonminingExtractionFinished":

                    //"text": "autoTime: 131632776620000000\nmoonID: 40349232\nmoonLink: <a href=\"showinfo:14\/\/40349232\">Teskanen IV - Moon 14<\/a>\noreVolumeByType:\n  45513: 1003894.7944164276\n  46676: 3861704.652392864\n  46681: 1934338.7763798237\n  46687: 5183861.7768108845\nsolarSystemID: 30045335\nsolarSystemLink: <a href=\"showinfo:5\/\/30045335\">Teskanen<\/a>\nstructureID: 1026192163696\nstructureLink: <a href=\"showinfo:35835\/\/1026192163696\">Teskanen - Nebula Prime<\/a>\nstructureName: Teskanen - Nebula Prime\nstructureTypeID: 35835\n"
                    var compText = new StringBuilder();
                    if (oreComposition != null)
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
                            author.WithName(LM.Get(notification.type, structureType == null ? LM.Get("Structure") : structureType.Name)))
                        .AddField(LM.Get("Structure"), structureNameDirect ?? LM.Get("Unknown"), true)
                        .AddField(LM.Get("Composition"), compText.ToString(), true)
                        .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                        .WithTimestamp(timestamp);

                    string startedBy = null;
                    if (notification.type == "MoonminingExtractionStarted")
                    {
                        startedBy = data.ContainsKey("startedBy")
                            ? ((await APIHelper.ESIAPI.GetCharacterData(Reason, data["startedBy"]))?.name ?? LM.Get("Auto"))
                            : null;
                        builder.AddField(LM.Get("moonminingStartedBy"), startedBy ?? LM.Get("Unknown"));
                    }


                    if (notification.type.Equals("MoonminingExtractionStarted",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            DateTime.TryParse(data["autoTime"], out var autoTime);
                            await MiningScheduleModule.UpdateNotificationFromFeed(
                                compText.ToString().Replace("|", "<br>").Replace(".00", ""), Convert.ToInt64(structureId), autoTime.ToUniversalTime(), startedBy ?? LM.Get("Unknown"));
                        }
                        catch (Exception ex)
                        {
                            await LogHelper.LogEx(ex, Category);
                        }
                    }

                    embed = builder.Build();

                    foreach (var channel in discordChannels)
                        await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    break;
                case "MoonminingAutomaticFracture":
                case "MoonminingLaserFired":
                    //"text": "firedBy: 91684736\nfiredByLink: <a href=\"showinfo:1386\/\/91684736\">Mike Myzukov<\/a>\nmoonID: 40349232\nmoonLink: <a href=\"showinfo:14\/\/40349232\">Teskanen IV - Moon 14<\/a>\noreVolumeByType:\n  45513: 241789.6056930224\n  46676: 930097.5066294272\n  46681: 465888.4702051679\n  46687: 1248541.084139049\nsolarSystemID: 30045335\nsolarSystemLink: <a href=\"showinfo:5\/\/30045335\">Teskanen<\/a>\nstructureID: 1026192163696\nstructureLink: <a href=\"showinfo:35835\/\/1026192163696\">Teskanen - Nebula Prime<\/a>\nstructureName: Teskanen - Nebula Prime\nstructureTypeID: 35835\n"

                    builder = new EmbedBuilder()
                        .WithColor(new Color(0xb386f7))
                        .WithThumbnailUrl(Settings.Resources.ImgMoonComplete)
                        .WithAuthor(author =>
                            author.WithName(LM.Get(notification.type, structureType == null ? LM.Get("Structure") : structureType.Name)))
                        .AddField(LM.Get("Structure"), structureNameDirect ?? LM.Get("Unknown"), true);
                    if (notification.type == "MoonminingLaserFired")
                    {
                        builder.AddField(LM.Get("FiredBy"), moonFiredBy, true);
                    }

                    builder.WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                        .WithTimestamp(timestamp);
                    embed = builder.Build();

                    if (!string.IsNullOrEmpty(structureId))
                    {
                        await MiningScheduleModule.UpdateOreVolumeFromFeed(Convert.ToInt64(structureId),
                            oreCompositionRaw.ToJson());
                    }

                    foreach (var channel in discordChannels)
                        await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    break;
                case "CorpBecameWarEligible":
                case "CorpNoLongerWarEligible":
                    {
                        var isEligible = notification.type == "CorpBecameWarEligible";
                        var color = isEligible ? new Color(0xFF0000) : new Color(0x00FF00);
                        builder = new EmbedBuilder()
                            .WithColor(color);
                        var corp = feederCorp?.name ?? LM.Get("Unknown");
                        var text = isEligible ? LM.Get("notifCorpWarEligible", corp) : LM.Get("notifCorpNotWarEligible", corp);
                        builder.WithAuthor(author => author.WithName(text))
                            .WithThumbnailUrl(isEligible ? Settings.Resources.ImgBecameWarEligible : Settings.Resources.ImgNoLongerWarEligible)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;

                case "MutualWarInviteAccepted":
                case "MutualWarInviteRejected":
                case "MutualWarInviteSent":
                case "MutualWarExpired":
                    {
                        var corp = feederAlliance?.name ?? feederCorp?.name ?? LM.Get("Unknown");
                        var color = new Color(0x00FF00);
                        string text;
                        string image;
                        switch (notification.type)
                        {
                            case "MutualWarInviteAccepted":
                                text = LM.Get("notifMutualWarInviteAccepted", corp);
                                image = Settings.Resources.ImgWarInviteAccepted;
                                break;
                            case "MutualWarInviteRejected":
                                color = new Color(0xFF0000);
                                text = LM.Get("notifMutualWarInviteRejected", corp);
                                image = Settings.Resources.ImgWarInviteRejected;
                                break;
                            case "MutualWarInviteSent":
                                text = LM.Get("notifMutualWarInviteSent", corp);
                                image = Settings.Resources.ImgWarInviteSent;
                                break;
                            case "MutualWarExpired":
                                color = new Color(0xFF0000);
                                text = LM.Get("notifMutualWarExpired", corp);
                                image = Settings.Resources.ImgWarInvalidate;
                                break;
                            default:
                                color = new Color(0xFF0000);
                                text = $"Unknown war expiration event for {corp}";
                                image = Settings.Resources.ImgWarInvalidate;
                                break;
                        }

                        builder = new EmbedBuilder()
                            .WithColor(color)
                            .WithAuthor(author => author.WithName(text))
                            .WithThumbnailUrl(image)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;

                case "AllMaintenanceBillMsg":
                case "BillOutOfMoneyMsg":
                case "AllianceCapitalChanged":
                case "BountyPlacedAlliance":
                case "BountyPlacedCorp":
                case "CorpKicked":
                case "CorpNewCEOMsg":
                case "CorpTaxChangeMsg":
                case "OwnershipTransferred":
                    {
                        var ally = feederAlliance?.name ?? LM.Get("Unknown");
                        var corp = feederCorp?.name ?? LM.Get("Unknown");
                        var color = new Color(0x00FF00);
                        string text;
                        string image;
                        switch (notification.type)
                        {
                            case "AllMaintenanceBillMsg":
                                text = LM.Get("notifAllMaintenanceBillMsg", ally, GetData("dueDate", data)?.ToEveTimeString());
                                image = Settings.Resources.ImgAllMaintenanceBillMsg;
                                break;
                            case "BillOutOfMoneyMsg":
                                color = new Color(0xFF0000);
                                var btype = (await APIHelper.ESIAPI.GetTypeId(Reason, GetData("billTypeID", data)))?.Name;
                                text = LM.Get("notifBillOutOfMoneyMsg", corp, btype, GetData("dueDate", data)?.ToEveTimeString());
                                image = Settings.Resources.ImgBillOutOfMoneyMsg;
                                break;
                            case "AllianceCapitalChanged":
                                var allyId = GetData("allianceID", data);
                                var accAlly = string.IsNullOrEmpty(allyId) ? null : (await APIHelper.ESIAPI.GetAllianceData(Reason, allyId))?.name;
                                text = LM.Get("notifAllianceCapitalChanged", accAlly, system?.SolarSystemName);
                                image = Settings.Resources.ImgAllMaintenanceBillMsg;
                                break;
                            case "BountyPlacedAlliance":
                                color = new Color(0xFF0000);
                                var placer = await APIHelper.ESIAPI.GetMemberEntityProperty(Reason, GetData("bountyPlacerID", data), "Name");
                                text = LM.Get("notifBountyPlacedAlliance", ally, placer, Convert.ToInt64(GetData("bounty", data)).ToKMB());
                                image = Settings.Resources.ImgBountyPlacedAlliance;
                                break;
                            case "CorpKicked":
                                color = new Color(0xFF0000);
                                var kcorp = (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("corpID", data)))?.name;
                                text = LM.Get("notifCorpKicked", kcorp, ally);
                                image = Settings.Resources.ImgCorpKicked;
                                break;
                            case "CorpNewCEOMsg":
                                var ccorp = (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("corpID", data)))?.name;
                                var newCeo = await APIHelper.ESIAPI.GetCharacterData(Reason, GetData("newCeoID", data));
                                var oldCeo = await APIHelper.ESIAPI.GetCharacterData(Reason, GetData("oldCeoID", data));
                                text = LM.Get("notifCorpNewCEOMsg", ccorp, newCeo?.name, oldCeo?.name);
                                image = Settings.Resources.ImgCorpNewCEOMsg;
                                break;
                            case "CorpTaxChangeMsg":
                                var corp1 = (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("corpID", data)))?.name;
                                text = LM.Get("notifCorpTaxChangeMsg", corp1, GetData("oldTaxRate", data), GetData("newTaxRate", data));
                                image = Settings.Resources.ImgCorpTaxChangeMsg;
                                break;
                            case "OwnershipTransferred":
                                {
                                    var oldOwner =
                                        await APIHelper.ESIAPI.GetCorporationData(Reason,
                                            GetData("oldOwnerCorpID", data));
                                    var newOwner =
                                        await APIHelper.ESIAPI.GetCorporationData(Reason,
                                            GetData("newOwnerCorpID", data));
                                    text = LM.Get("notifOwnershipTransferred", GetData("structureName", data),
                                        oldOwner?.name, newOwner?.name);
                                    image = Settings.Resources.ImgCitFuelAlert;

                                    builder = new EmbedBuilder()
                                        .WithColor(color)
                                        .WithAuthor(author => author.WithName(text))
                                        .WithThumbnailUrl(image)
                                        .AddField(LM.Get("Structure"), structureType?.Name ?? LM.Get("Unknown"), true)
                                        .AddField(LM.Get("System"), systemName ?? LM.Get("Unknown"), true)
                                        .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                                        .WithTimestamp(timestamp);
                                    embed = builder.Build();

                                    foreach (var channel in discordChannels)
                                        await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                                    return true;
                                }
                            default:
                                await LogHelper.LogWarning($"Unknown notif type {notification.type}");
                                return false;
                        }

                        builder = new EmbedBuilder()
                            .WithColor(color)
                            .WithAuthor(author => author.WithName(text))
                            .WithThumbnailUrl(image)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;

                case "WarAdopted":
                case "WarAllyInherited":
                case "WarConcordInvalidates":
                case "WarDeclared":
                case "WarHQRemovedFromSpace":
                case "WarInherited":
                case "WarInvalid":
                case "WarRetracted":
                case "WarRetractedByConcord":
                    {
                        var corp = feederAlliance?.name ?? feederCorp?.name ?? LM.Get("Unknown");
                        var color = new Color(0x00FF00);
                        string text;
                        string image;

                        var declaredById = GetData("declaredByID", data);
                        var declareByName = !string.IsNullOrEmpty(declaredById)
                            ? ((await APIHelper.ESIAPI.GetAllianceData(Reason, declaredById, true))?.name ?? (await APIHelper.ESIAPI.GetCorporationData(Reason, declaredById, true))?.name)
                            : null;
                        var declaredAgainstId = GetData("againstID", data);
                        var declareAgainstName = !string.IsNullOrEmpty(declaredAgainstId)
                            ? ((await APIHelper.ESIAPI.GetAllianceData(Reason, declaredAgainstId, true))?.name ?? (await APIHelper.ESIAPI.GetCorporationData(Reason, declaredAgainstId, true))?.name)
                            : null;
                        var hq = GetData("warHQ", data)?.Replace("<b>", "").Replace("</b>", "");

                        switch (notification.type)
                        {
                            case "WarAdopted":
                                color = new Color(0xFF0000);
                                text = LM.Get("notifWarAdopted", declareByName, declareAgainstName);
                                image = Settings.Resources.ImgWarDeclared;
                                break;
                            case "WarAllyInherited":
                                text = LM.Get("notifWarAllyInherited", corp);
                                image = Settings.Resources.ImgWarInviteSent;
                                break;
                            case "WarConcordInvalidates":
                                text = LM.Get("notifWarConcordInvalidates", declareByName, declareAgainstName);
                                image = Settings.Resources.ImgWarInvalidate;
                                break;
                            case "WarDeclared":
                                color = new Color(0xFF0000);
                                text = LM.Get("notifWarDeclared", declareByName, declareAgainstName, hq);
                                image = Settings.Resources.ImgWarDeclared;
                                break;
                            case "WarHQRemovedFromSpace":
                                color = new Color(0xFF0000);
                                text = LM.Get("notifWarHQRemovedFromSpace", corp);
                                image = Settings.Resources.ImgCitDestroyed;
                                break;
                            case "WarInherited":
                                /*WarInherited [1092637000]
                                againstID: 99008923
                                allianceID: 99001134
                                declaredByID: 99001134
                                opponentID: 99008923
                                quitterID: 98608854*/
                                color = new Color(0xFF0000);
                                text = LM.Get("notifWarInherited", declareByName, declareAgainstName);
                                image = Settings.Resources.ImgWarInviteSent;
                                break;
                            case "WarInvalid":
                                text = LM.Get("notifWarInvalid", declareByName, declareAgainstName);
                                image = Settings.Resources.ImgWarInvalidate;
                                break;
                            case "WarRetracted":
                                text = LM.Get("notifWarRetracted", declareByName, declareAgainstName);
                                image = Settings.Resources.ImgWarInvalidate;
                                break;
                            case "WarRetractedByConcord":
                                text = LM.Get("notifWarRetractedByConcord", declareByName, declareAgainstName);
                                image = Settings.Resources.ImgWarInvalidate;
                                break;
                            default:
                                await LogHelper.LogWarning($"Unknown notif type {notification.type}");
                                return false;
                        }

                        builder = new EmbedBuilder()
                            .WithColor(color)
                            .WithAuthor(author => author.WithName(text))
                            .WithThumbnailUrl(image)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;

                case "CharLeftCorpMsg":
                case "CharAppAcceptMsg":
                case "CorpAppNewMsg":
                case "CharAppWithdrawMsg":
                    {
                        var character = await APIHelper.ESIAPI.GetCharacterData(Reason, GetData("charID", data), true);
                        var corp = await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("corpID", data), true);
                        var text = "";
                        switch (notification.type)
                        {
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

                        var applicationText = string.Empty;
                        if (notification.type != "CharLeftCorpMsg")
                        {
                            //GetData("applicationText", data);

                            var sb = new StringBuilder();
                            foreach (var (key, value) in data)
                            {
                                if (key.Equals("applicationText", StringComparison.OrdinalIgnoreCase))
                                {
                                    sb.Append(value);
                                    sb.Append(" ");
                                    continue;
                                }

                                if (key == "charID" || key == "corpID")
                                    break;
                                sb.Append(key);
                                sb.Append(" ");
                                sb.Append(value);
                            }

                            applicationText = sb.ToString();
                        }
                        Color color;
                        if (notification.type == "CharLeftCorpMsg" || notification.type == "CharAppWithdrawMsg")
                        {
                            color = new Color(0xdd5353);
                        }
                        else if (notification.type == "CharAppAcceptMsg")
                        {
                            color = new Color(0x00ff00);
                        }
                        else
                        {
                            color = new Color(0x555555);
                        }

                        builder = new EmbedBuilder()
                            .WithColor(color);
                        if (!string.IsNullOrEmpty(applicationText) && applicationText != "''")
                        {
                            applicationText = (await MailModule.PrepareBodyMessage(applicationText))[0];
                            applicationText = applicationText.ConvertToCyrillic();
                            builder.WithDescription(applicationText);
                        }

                        builder.WithAuthor(author => author.WithName(text).WithUrl($"https://zkillboard.com/character/{GetData("charID", data)}/"))
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;

                case "SovStructureDestroyed":
                    {
                        builder = new EmbedBuilder()
                            .WithColor(new Color(0xdd5353))
                            .WithAuthor(
                                author => author.WithName(LM.Get("SovStructureDestroyed", structureType?.Name, systemName)))
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;

                case "SovStationEnteredFreeport":
                    {
                        var exittime = DateTime.FromFileTime(Convert.ToInt64(GetData("freeportexittime", data)));
                        builder = new EmbedBuilder()
                            .WithColor(new Color(0xdd5353))
                            .WithAuthor(
                                author => author.WithName(LM.Get("SovStationEnteredFreeport", structureType?.Name, systemName)))
                            .AddField("Exit Time", exittime, true)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;
                case "StationServiceDisabled":
                    {
                        builder = new EmbedBuilder()
                            .WithColor(new Color(0xdd5353))
                            .WithThumbnailUrl(Settings.Resources.ImgCitServicesOffline)
                            .WithAuthor(author =>
                                author.WithName(LM.Get("StationServiceDisabled", structureType?.Name, systemName)))
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;



                case "SovCommandNodeEventStarted":
                    {
                        var constellation = await APIHelper.ESIAPI.GetConstellationData(Reason, GetData("constellationID", data));
                        var campaignId = Convert.ToInt32(GetData("campaignEventType", data));
                        var cmp = campaignId == 1 ? "1" : (campaignId == 2 ? "2" : "3");
                        builder = new EmbedBuilder()
                            .WithColor(new Color(0xdd5353))
                            .WithAuthor(author =>
                                author.WithName(LM.Get("SovCommandNodeEventStarted", cmp, systemName, constellation?.ConstellationName)))
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;

                case "SovStructureReinforced":
                    {
                        var decloakTime = DateTime.FromFileTime(Convert.ToInt64(GetData("decloakTime", data)));
                        var campaignId = Convert.ToInt32(GetData("campaignEventType", data));
                        var cmp = campaignId == 1 ? "1" : (campaignId == 2 ? "2" : "3");
                        builder = new EmbedBuilder()
                            .WithColor(new Color(0xdd5353))
                            .WithAuthor(author =>
                                author.WithName(LM.Get("SovStructureReinforced", cmp, systemName)))
                            .AddField("Decloak Time", decloakTime.ToString(), true)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;

                case "EntosisCaptureStarted":
                    {
                        builder = new EmbedBuilder()
                            .WithColor(new Color(0xdd5353))
                            .WithAuthor(
                                author => author.WithName(LM.Get("EntosisCaptureStarted", structureType?.Name, systemName)))
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;


                case "AllianceWarDeclaredV2":
                case "CorpWarDeclaredV2":
                case "AllWarDeclaredMsg":
                case "CorpWarDeclaredMsg":
                case "AllWarInvalidatedMsg":
                case "CorpWarInvalidatedMsg":
                    {
                        //"text": "againstID: 98464487\ncost: 50000000.0\ndeclaredByID: 99005333\ndelayHours: 24\nhostileState: 0\n"
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
                            ? $"https://zkillboard.com/alliance/{declaredById}"
                            : $"https://zkillboard.com/corporation/{declaredById}";

                        builder = new EmbedBuilder()
                            .WithColor(color)
                            .WithThumbnailUrl(iUrl)
                            .WithAuthor(author => author.WithName(template).WithUrl(url))
                            .AddField(LM.Get("Note"), template2, true)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;
                case "AllyJoinedWarAggressorMsg":
                    {
                        var allyID = GetData("allyID", data);
                        var ally = (await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("allyID", data)))?.name ??
                                   (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("allyID", data)))?.name;
                        var defender = (await APIHelper.ESIAPI.GetAllianceData(Reason, GetData("defenderID", data)))?.name ??
                                       (await APIHelper.ESIAPI.GetCorporationData(Reason, GetData("defenderID", data)))?.name;
                        builder = new EmbedBuilder()
                            .WithColor(new Color(0xff0000))
                            .WithThumbnailUrl(Settings.Resources.ImgWarAssist)
                            .WithAuthor(author => author.WithName(LM.Get("AllyJoinedWarAggressorMsg", ally, defender))
                                .WithUrl($"https://zkillboard.com/alliance/{allyID}"))
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;
                case "AllyJoinedWarDefenderMsg":
                    {
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
                                    .WithUrl($"https://zkillboard.com/alliance/{allyID}"))
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;
                case "AllyJoinedWarAllyMsg":
                    {
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
                                    .WithUrl($"https://zkillboard.com/alliance/{allyID}"))
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;
                case "FWAllianceWarningMsg":
                    {
                        //"text": "allianceID: 99005333\ncorpList: <br>Quarian Fleet - standings:-0.0500\nfactionID: 500001\nrequiredStanding: 0.0001\n"
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
                            .AddField(LM.Get("BlameCorp"), LM.Get("standMissing", corpStr3, required), true)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;
                case "StructureImpendingAbandonmentAssetsAtRisk":
                    {
                        var daysLeft = Convert.ToInt32(GetData("daysUntilAbandon", data));
                        var isCorp = Convert.ToBoolean(GetData("isCorpOwned", data));

                        var stTypeName = structureType == null ? LM.Get("Structure") : structureType.Name;
                        var stName = structureNameDirect ?? LM.Get("Unknown");

                        builder = new EmbedBuilder()
                            .WithColor(new Color(0xff9900))
                            .WithThumbnailUrl(Settings.Resources.ImgCitServicesOffline)
                            .WithAuthor(author => author.WithName(LM.Get("StructureImpendingAbandonmentAssetsAtRiskMsg", stName, daysLeft)))
                            //.AddField(LM.Get("BlameCorp"), isCorp, true)
                            .AddField(LM.Get("System"), $"{systemName}({Math.Round(system.Security, 1):0.0})", true)
                            .AddField(LM.Get("Structure"), stTypeName, true)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;
                case "StructureItemsMovedToSafety":
                    {
                        builder = new EmbedBuilder()
                            .WithColor(new Color(0xff0000))
                            .WithThumbnailUrl(Settings.Resources.ImgCitServicesOffline)
                            .WithAuthor(author => author.WithName(LM.Get("StructureItemsMovedToSafety", systemName)))
                            //.AddField(LM.Get("BlameCorp"), isCorp, true)
                            .AddField(LM.Get("System"), systemName, true)
                            .WithFooter($"EVE Time: {timestamp.ToShortDateString()} {timestamp.ToShortTimeString()}")
                            .WithTimestamp(timestamp);
                        embed = builder.Build();

                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, mention, embed).ConfigureAwait(false);
                    }
                    break;
                default:
                    if (notification != null)
                    {
                        foreach (var channel in discordChannels)
                            await APIHelper.DiscordAPI.SendMessageAsync(channel,
                                    $"Unknown notification type '{notification.type}'\n```\n{notification.text}\n```\n")
                                .ConfigureAwait(false);
                    }

                    return false;
            }

            return true;
        }


        #endregion

        private async Task CleanupNotifyList()
        {
            if ((DateTime.Now - _lastCleanupCheck).TotalHours > 8)
            {
                _lastCleanupCheck = DateTime.Now;
                await DbHelper.CleanupNotificationsList();
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

        private async Task UpdateNotificationList(string groupName, string filterName, bool isNew)
        {
            await DbHelper.SetLastNotification(groupName, filterName, _lastNotification);
        }

        private async Task SetLastNotificationId(long id, string groupName = null, string filterName = null)
        {
            var isNew = _lastNotification == 0;
            _lastNotification = id;

            if (!string.IsNullOrEmpty(groupName) && !string.IsNullOrEmpty(filterName))
                await UpdateNotificationList(groupName, filterName, isNew);
        }

        private bool IsValidCharacter(long numericCharId)
        {
            return Settings.NotificationFeedModule.GetEnabledGroups().Any(group =>
                (GetParsedCharacters(group.Key) ?? new List<long>()).Contains(numericCharId));
        }
    }
}
