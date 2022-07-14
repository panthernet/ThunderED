using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Thd;

namespace ThunderED.Modules
{
    internal class AggregatorModule: AppModuleBase
    {
        public override LogCat Category { get; } = LogCat.Aggregator;
        private readonly CancellationTokenSource _cts = new();

        public override async Task Initialize()
        {
            await LogHelper.LogInfo($"Starting Aggregator...");

            if (!DbHelper.IsSQLite)
            {
                await LogHelper.LogWarning($"Aggregator is currently only available under SQLite database provider!");
                return;
            }

            await Task.Factory.StartNew(RunAggregator).ConfigureAwait(false);
        }

        private async Task RunAggregator()
        {
            while (!_cts.IsCancellationRequested)
            {
                var start = DateTime.Now;
                try
                {
                    await AggregateNotifications();

                    await AggregateMail();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(ex, Category);
                }

                //1 hr minus time spent on last op
                var leftToWait = 3600000 - (int)(DateTime.Now - start).TotalMilliseconds;
                if(leftToWait>0)
                    await Task.Delay(leftToWait, _cts.Token);
            }
        }

        private async Task AggregateMail()
        {
            try
            {
                var tokens = await DbHelper.GetTokensByScope(SettingsManager.GetMailESIScope());
                await Parallel.ForEachAsync(tokens, _cts.Token, async (token, ct) =>
                {
                    var tagName = $"agm|{token.CharacterId}";
                    var tagNameMailList = $"agml|{token.CharacterId}";
                    //fetch access token
                    var accessToken = await APIHelper.ESIAPI.GetAccessTokenWithScopes(token, SettingsManager.GetMailESIScope());
                    //check if it is correct
                    if(!await CheckAccessToken(token, accessToken.Data))
                        return;
                    //get last mail id
                    //var lastMailId = await DbHelper.AggregateGetLastMailId(token.CharacterId);
                    //fetch etag if any
                    var etag = await GetEtag(tagName);
                    //get notifications using etag and access token
                    var result = await APIHelper.ESIAPI.GetMailHeaders(Reason, token.CharacterId,
                        accessToken.Result, 0, etag);
                    //check if operation is correct
                    if(!await CheckAccessToken(token, result.Data))
                        return;

                    //fetch etag if any
                    var mlEtag = await GetEtag(tagNameMailList);
                    var mailLists = await APIHelper.ESIAPI.GetMailLists(Reason, token.CharacterId, accessToken.Result, mlEtag);
                    //check if operation is correct
                    if (mailLists != null && await CheckAccessToken(token, mailLists.Data))
                    {
                        await DbHelper.AggregateAddMailLists(mailLists.Result.Select(a => new ThdHistoryMailList
                        {
                            Id = a.mailing_list_id, Name = a.name
                        }).ToList());
                    }
                    //update etag if it has been changed
                    if (!string.IsNullOrEmpty(mailLists.Data.ETag) && mailLists.Data.ETag != mlEtag)
                        await DbHelper.UpdateCache(tagNameMailList, mailLists.Data.ETag, 7);

                    var list = new List<ThdHistoryMail>();
                    var rcpList = new List<ThdHistoryMailRcp>();

                    foreach (var header in result.Result)
                    {
                        var newItem = new ThdHistoryMail
                            {
                                Subject = header.subject, ReceiveDate = header.Date, SenderId = header.@from,
                                Id = header.mail_id
                            };

                        foreach (var recipient in header.recipients)
                        {
                            var rcp = new ThdHistoryMailRcp
                            {
                                RecipientId = recipient.recipient_id,
                                MailId = newItem.Id,
                                RecipientType = recipient.recipient_type,
                                RecipientSnapshot = await GetMailRecipientSnapshot(recipient.recipient_type, recipient.recipient_id)
                            };
                            if(rcpList.FirstOrDefault(a=> a.RecipientId == rcp.RecipientId && a.MailId == rcp.MailId) == null)
                                rcpList.Add(rcp);
                        }

                        var r = await APIHelper.ESIAPI.GetMailLabels(Reason, token.CharacterId,
                            accessToken.Result);

                        if (r != null)
                        {
                            var labelsSb = new StringBuilder();
                            foreach (var labelId in header.labels)
                            {
                                var label = r.labels.FirstOrDefault(a => a.label_id == labelId);
                                if (label != null)
                                {
                                    labelsSb.Append(label.name);
                                    labelsSb.Append(",");
                                }
                            }

                            if (labelsSb.Length > 0)
                                labelsSb.Remove(labelsSb.Length - 1, 1);
                            newItem.Labels = labelsSb.ToString();
                        }

                        var body = await APIHelper.ESIAPI.GetMail(Reason, token.CharacterId, accessToken.Result,
                            header.mail_id);

                        if (body != null)
                            newItem.Body = body.body;

                        var data = await GetCharacterSnapshot(token.CharacterId, newItem.ReceiveDate);
                        newItem.SenderSnapshot = data?.Snapshot;
                        newItem.SenderCorporationId = data?.CorpId;
                        newItem.SenderAllianceId = data?.AllyId;

                        list.Add(newItem);
                    }


                    await DbHelper.AggregateAddMail(list);

                    await DbHelper.AggregateAddMailRcp(rcpList);

                    //update etag if it has been changed
                    if (!string.IsNullOrEmpty(result.Data.ETag) && result.Data.ETag != etag)
                        await DbHelper.UpdateCache(tagName, result.Data.ETag, 7);
                });
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, Category);
                throw;
            }
        }

        private async Task<CharacterSnapshot> GetMailRecipientSnapshot(string recipientType, long id)
        {
            try
            {
                var snapshot = new CharacterSnapshot();
                switch (recipientType)
                {
                    case "character":
                        {
                            var ch = await APIHelper.ESIAPI.GetCharacterData(Reason, id);
                            var corp = await APIHelper.ESIAPI.GetCorporationData(Reason, ch.corporation_id);
                            var ally = corp.alliance_id.HasValue
                                ? await APIHelper.ESIAPI.GetCorporationData(Reason, ch.corporation_id)
                                : null;
                            snapshot.AllianceName = ally?.name;
                            snapshot.AllianceTicker = ally?.ticker;
                            snapshot.CorporationName = corp.name;
                            snapshot.CorporationTicker = corp.name;
                            snapshot.CharacterName = ch.name;
                        }
                        break;
                    case "corporation":
                        {
                            var corp = await APIHelper.ESIAPI.GetCorporationData(Reason, id);
                            var ally = corp.alliance_id.HasValue
                                ? await APIHelper.ESIAPI.GetCorporationData(Reason, id)
                                : null;
                            snapshot.AllianceName = ally?.name;
                            snapshot.AllianceTicker = ally?.ticker;
                            snapshot.CorporationName = corp.name;
                            snapshot.CorporationTicker = corp.name;
                        }
                        break;
                    case "alliance":
                        {
                            var ally = await APIHelper.ESIAPI.GetCorporationData(Reason, id);
                            snapshot.AllianceName = ally?.name;
                            snapshot.AllianceTicker = ally?.ticker;
                        }
                        break;
                    case "mailing_list":
                        {
                        }
                        break;
                    default:
                        throw new Exception($"Unknown type value {recipientType}");
                }
                return snapshot;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, Category);
                return null;
            }
        }

        private async Task AggregateNotifications()
        {
            try
            {
                var tokens = await DbHelper.GetTokensByScope(SettingsManager.GetNotificationsESIScope());

                await Parallel.ForEachAsync(tokens, _cts.Token, async (token,ct) =>
               // foreach(var token in tokens)
                {
                    var tagName = $"agn|{token.CharacterId}";
                    //fetch access token
                    var accessToken = await APIHelper.ESIAPI.GetAccessTokenWithScopes(token, SettingsManager.GetNotificationsESIScope());
                    //check if it is correct
                    if(!await CheckAccessToken(token, accessToken.Data))
                        return;
                    //get last notification id
                    //var lastNotificationId = await DbHelper.GetLastAggregateNotificationId(token.CharacterId);
                    //fetch etag if any
                    var etag = await GetEtag(tagName);
                    //get notifications using etag and access token
                    var result = await APIHelper.ESIAPI.GetNotifications(Reason, token.CharacterId,
                        accessToken.Result, etag);
                    //check if operation is correct
                    if(!await CheckAccessToken(token, result.Data))
                        return;
                    //filter out old notifications
                    //var notifications = lastNotificationId > 0
                    //    ? result.Result.Where(a => a.notification_id > lastNotificationId).ToList()
                     //   : result.Result;
                    var notifications = result.Result;
                    //update etag if it has been changed
                    if (!string.IsNullOrEmpty(result.Data.ETag) && result.Data.ETag != etag)
                        await DbHelper.UpdateCache(tagName, result.Data.ETag, 7);

                    var corpHistory = await APIHelper.ESIAPI.GetCharCorpHistory(Reason, token.CharacterId);
                    var character = await APIHelper.ESIAPI.GetCharacterData(Reason, token.CharacterId);
                    
                    var finalResult = new List<ThdHistoryNotification>();
                    foreach(var item in notifications)
                    {
                        var snap = await GetCharacterSnapshot(token.CharacterId, item.Date, corpHistory, character);
                        var mail =  new ThdHistoryNotification
                        {
                            Id = item.notification_id,
                            SenderId = item.sender_id,
                            SenderType = item.sender_type,
                            Type = item.type,
                            ReceiveDate = item.Date,
                            Data = item.text,
                            SenderSnapshot = snap?.Snapshot,
                            SenderAllianceId = snap?.AllyId,
                            SenderCorporationId = snap?.CorpId,
                            FeederId = token.CharacterId
                        };
                        finalResult.Add(mail);
                    }

                    //save new notifications
                    await DbHelper.AggregateAddNotifications(finalResult);
                });
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, Category);
                throw;
            }
        }

        private async Task<dynamic> GetCharacterSnapshot(long characterId, DateTime date,
            List<JsonClasses.CorporationHistoryEntry> corpHistory = null, JsonClasses.CharacterData characterData = null)
        {
            try
            { 
                corpHistory = corpHistory ?? await APIHelper.ESIAPI.GetCharCorpHistory(Reason, characterId);
                characterData = characterData ?? await APIHelper.ESIAPI.GetCharacterData(Reason, characterId);

                var corp = corpHistory.OrderByDescending(a => a.Date)
                    .FirstOrDefault(a => date >= a.Date);
                var fromCorp = corp == null
                    ? null
                    : await APIHelper.ESIAPI.GetCorporationData(Reason, corp.corporation_id);
                var fromAlliance = fromCorp?.alliance_id == null
                    ? null
                    : await APIHelper.ESIAPI.GetAllianceData(Reason, fromCorp.alliance_id);

                return new
                {
                    Snapshot = new CharacterSnapshot
                    {
                        AllianceName = fromAlliance?.name,
                        AllianceTicker = fromAlliance?.ticker,
                        CorporationName = fromCorp?.name,
                        CorporationTicker = fromCorp?.ticker,
                        CharacterName = characterData?.name
                    },
                    CorpId = corp?.corporation_id,
                    AllyId = fromCorp?.alliance_id
                };
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, Category);
                return null;
            }
        }

        private async Task<string> GetEtag(string name)
        {
            return await DbHelper.GetCache<string>(name, 60);
        }

        private async Task<bool> CheckAccessToken(ThdToken token, QueryData data)
        {
            if (data.IsFailed)
            {
                if (data.IsNotValid)
                {
                    token.Roles = 99;
                    await DbHelper.UpdateTokenEx(token);
                }

                return false;
            }

            return true;
        }
    }
}
