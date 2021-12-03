using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Thd;

namespace ThunderED.Modules
{
    public partial class MailModule: AppModuleBase
    {
        public override LogCat Category => LogCat.Mail;
        private int _checkInterval;
        private DateTime _lastCheckTime = DateTime.MinValue;

        private readonly ConcurrentDictionary<long, string> _tags = new ConcurrentDictionary<long, string>();

        public override async Task Initialize()
        {
            await LogHelper.LogModule("Initializing Mail module...", Category);
            _checkInterval = Settings.MailModule.CheckIntervalInMinutes;
            if (_checkInterval == 0)
                _checkInterval = 1;
            await WebPartInitialization();

            var data = Settings.MailModule.GetEnabledGroups().ToDictionary(pair => pair.Key, pair => pair.Value.CharacterEntities);
            await ParseMixedDataArray(data, MixedParseModeEnum.Member);
        }

        public override async Task Run(object prm)
        {
            if (IsRunning || !APIHelper.IsDiscordAvailable) return;
            if (TickManager.IsNoConnection || TickManager.IsESIUnreachable) return;
            IsRunning = true;
            try
            {
                if((DateTime.Now - _lastCheckTime).TotalMinutes < _checkInterval) return;
                _lastCheckTime = DateTime.Now;
                await LogHelper.LogModule("Running Mail module check...", Category);

                foreach (var (groupName, group) in Settings.MailModule.GetEnabledGroups())
                {
                    if (group.DefaultChannel == 0)
                        continue;
                    var defaultChannel = group.DefaultChannel;

                    var chars = GetParsedCharacters(groupName) ?? new List<long>();
                    foreach (var charId in chars)
                    {
                        if (charId == 0) continue;

                        var rToken = await DbHelper.GetToken(charId, TokenEnum.Mail);
                        if (rToken == null)
                        {
                            await SendOneTimeWarning(charId, $"Mail feed token for character {charId} not found! User is not authenticated or missing refresh token.");
                            continue;
                        }

                        var tq = await APIHelper.ESIAPI.GetAccessToken(rToken, $"From {Category} | Char ID: {charId}");
                        var token = tq.Result;
                        if (string.IsNullOrEmpty(token))
                        {
                            await LogHelper.LogWarning($"Unable to get mail token for character {charId}. Refresh token might be outdated or no more valid {tq.Data.ErrorCode}({tq.Data.Message})", Category);
                            if (tq.Data.IsNotValid && !tq.Data.IsNoConnection)
                            {
                                await LogHelper.LogWarning($"Deleting invalid mail refresh token for {charId}", Category);
                                await DbHelper.DeleteToken(charId, TokenEnum.Mail);
                            }
                            continue;
                        }
                        
                        var includePrivate = group.IncludePrivateMail;

                        if (group.Filters.Values.All(a => a.FilterSenders.Count == 0 && a.FilterLabels.Count == 0 && a.FilterMailList.Count == 0))
                        {
                            await LogHelper.LogWarning($"Mail feed for user {charId} has no labels, lists or senders configured!", Category);
                            continue;
                        }

                        var labelsData = await APIHelper.ESIAPI.GetMailLabels(Reason, charId.ToString(), token);
                        var searchLabels = labelsData?.labels.Where(a => a.name?.ToLower() != "sent" && a.name?.ToLower() != "received").ToList() ??
                                           new List<JsonClasses.MailLabel>();
                        var mailLists = await APIHelper.ESIAPI.GetMailLists(Reason, charId, token);

                        var etag = _tags.GetOrNull(charId);
                        var result = await APIHelper.ESIAPI.GetMailHeaders(Reason, charId.ToString(), token, 0, etag);
                        _tags.AddOrUpdateEx(charId, result.Data.ETag);
                        if(result.Data.IsNotModified) continue;

                        var mailsHeaders = result.Result;

                        var lastMailId = await DbHelper.GetLastMailId(charId);
                        var prevMailId = lastMailId;

                        if (lastMailId > 0)
                            mailsHeaders = mailsHeaders.Where(a => a.mail_id > lastMailId).OrderBy(a => a.mail_id).ToList();
                        else
                        {
                            lastMailId = mailsHeaders.OrderBy(a => a.mail_id).LastOrDefault()?.mail_id ?? 0;
                            mailsHeaders.Clear();
                        }

                        foreach (var mailHeader in mailsHeaders)
                        {
                            try
                            {
                                if (mailHeader.mail_id <= lastMailId) continue;
                                lastMailId = mailHeader.mail_id;
                                if (!includePrivate && (mailHeader.recipients.Count(a => a.recipient_id == charId) > 0)) continue;

                                foreach (var filter in group.Filters.Values)
                                {
                                    //filter by senders
                                    if (filter.FilterSenders.Count > 0 && !filter.FilterSenders.Contains(mailHeader.from))
                                        continue;
                                    //filter by labels
                                    var labelIds = searchLabels.Where(a => filter.FilterLabels.Contains(a.name)).Select(a => a.label_id).ToList();
                                    if (labelIds.Count > 0 && !mailHeader.labels.Any(a => labelIds.Contains(a)))
                                        continue;
                                    //filter by mail lists
                                    var mailListIds = filter.FilterMailList.Count > 0
                                        ? mailLists.Where(a => filter.FilterMailList.Any(b => a.name.Equals(b, StringComparison.OrdinalIgnoreCase))).Select(a => a.mailing_list_id)
                                            .ToList()
                                        : new List<long>();
                                    if (mailListIds.Count > 0 && !mailHeader.recipients.Where(a => a.recipient_type == "mailing_list")
                                            .Any(a => mailListIds.Contains(a.recipient_id)))
                                        continue;

                                    var mail = await APIHelper.ESIAPI.GetMail(Reason, charId.ToString(), token, mailHeader.mail_id);
                                    // var labelNames = string.Join(",", mail.labels.Select(a => searchLabels.FirstOrDefault(l => l.label_id == a)?.name)).Trim(',');
                                    var sender = await APIHelper.ESIAPI.GetCharacterData(Reason, mail.from);
                                    var from = sender?.name;
                                    var ml = mailHeader.recipients.FirstOrDefault(a => a.recipient_type == "mailing_list" && mailListIds.Contains(a.recipient_id));
                                    if (ml != null)
                                        from = $"{sender?.name}[{mailLists.First(a => a.mailing_list_id == ml.recipient_id).name}]";
                                    var channel = filter.FeedChannel > 0 ? filter.FeedChannel : defaultChannel;
                                    await SendMailNotification(channel, mail, LM.Get("mailMsgTitle", from), group.DefaultMention, filter.DisplayDetailsSummary);
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                await LogHelper.LogEx($"MailCheck: {mailHeader?.mail_id} {mailHeader?.subject}", ex);
                            }
                        }

                        if (prevMailId != lastMailId || lastMailId == 0)
                            await DbHelper.UpdateMail(charId, lastMailId);
                    }
                }

                // await LogHelper.LogModule("Completed", Category);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
               // await LogHelper.LogModule("Completed", Category);
            }
            finally
            {
                IsRunning = false; 
            }
        }

        private static async Task SendMailNotification(ulong channel, JsonClasses.Mail mail, string from,
            string mention, bool displaySummary)
        {
            if (mail == null || channel == 0) return;

            var sList = await PrepareBodyMessage(mail.body);
            var body = sList != null && sList.Any() ? sList[0] : (mail?.body ?? "");
            var fits = sList != null && sList.Any() ? sList[1] : null;
            var urls = sList != null && sList.Any() ? sList[2] : null;
            var fields = string.IsNullOrWhiteSpace(body) ? new List<string>() : body.SplitToLines(1923);

            var ch = APIHelper.DiscordAPI.GetChannel(channel);
            await APIHelper.DiscordAPI.SendMessageAsync(ch, $"{mention} {from}");
            foreach (var field in fields)
                await APIHelper.DiscordAPI.SendMessageAsync(ch, $".\r\n{field}");
            if (displaySummary && !string.IsNullOrEmpty(fits))
            {
                var list = fits.SplitToLines(1950, "</a>", true).ToList();
                for (var i = 0; i < list.Count; i++)
                {
                    var res = list[i];
                    if (i != list.Count - 1 && !res.EndsWith("```"))
                        res += "```";
                    if (!res.StartsWith("```"))
                        res = res.Insert(0, "```");
                    await APIHelper.DiscordAPI.SendMessageAsync(ch, $".\r\n{res}");
                }
            }

            if (displaySummary && !string.IsNullOrEmpty(urls))
            {
                var list = urls.SplitToLines(1950);
                foreach (var s in list)
                    await APIHelper.DiscordAPI.SendMessageAsync(ch, $".\r\n{s}");
            }
        }

        public static async Task<string[]> PrepareBodyMessage(string input, bool forWeb = false, bool skipValidation = false)
        {
            if (string.IsNullOrEmpty(input)) return new [] {" ", null, null, null};

            var doc = new HtmlDocument();
            doc.LoadHtml(input);
            if (!skipValidation && doc.ParseErrors.Any())
                return new [] {input, null, null, null};

            string body;
            if (!forWeb)
                body = input.Replace("<br>", Environment.NewLine)
                    .Replace("<b>", "**").Replace("</b>", "**")
                    .Replace("<u>", "__").Replace("</u>", "__")
                    .Replace("</font>", null)
                    .Replace("<loc>", null).Replace("</loc>", null);
            else 
                body = input.Replace("<loc>", null).Replace("</loc>", null).Replace("</font>", null);

            var fitList = new List<string>();
            var urlsList = new List<string>();
            var channelList = new List<string>();
            try
            {
                while (true)
                {
                    var index = body.IndexOf("<font");
                    if (index == -1) break;
                    var lst = body.IndexOf('>', index + 1);
                    if (lst == -1) break;
                    body = index != 0 ? $"{body.Substring(0, index)}{body.Substring(lst + 1, body.Length - lst - 1)}" : $"{body.Substring(lst + 1, body.Length - lst - 1)}";
                }


                var prevIndex = -1;
                while (true)
                {
                    var index = body.IndexOf("<a", prevIndex+1);
                    prevIndex = index;
                    if (index == -1) break;
                    var urlStart = body.IndexOf('\"', index) + 1;
                    var urlEnd = body.IndexOf('\"', urlStart) - 1;
                    var lst = body.IndexOf('>', index + 1);
                    var endTagStart = body.IndexOf('<', lst + 1);


                    string url = null;
                    string text = null;

                    try
                    {
                        url = body.Substring(urlStart, urlEnd - urlStart + 1);
                        text = body.Substring(lst + 1, endTagStart - lst - 1);
                    }
                    catch
                    {
                        // ignored
                    }

                    //parse data
                    var data = forWeb ? $"<a href=\"{url}\">{text}</a>" : $"{text}({url})";
                    var furl = $"<a href=\"{url}\">{text}</a>";
                    bool isEmpty = false;
                    try
                    {
                        if (url.StartsWith("http"))
                        {
                            data = forWeb ? data : url;
                        }
                        else if (url.StartsWith("joinChannel"))
                        {
                            data = forWeb ? text : $"__{text}__";//$"[{text}](<url={url}>{text}</url>)";
                            channelList.Add(furl);
                        }
                        else if (url.StartsWith("fitting"))
                        {
                            data = forWeb ? text : $"***{text}***";
                            fitList.Add(furl);
                        }
                        else if (url.StartsWith("showinfo"))
                        {
                            if (!url.Contains("//"))
                            {
                                var value = url.Split(":")[1];
                                data = forWeb
                                    ? $"<a href=\"{APIHelper.GetItemTypeUrl(value)}\">{text}</a>"
                                    : $"***{text}***";
                                fitList.Add(furl);
                            }else data = text;
                        }
                        else if (url.StartsWith("killReport"))
                        {
                            var id = url.Substring(11, url.Length - 11);
                            data = forWeb ? $"<a href=\"https://zkillboard.com/kill/{id}\">{text}</a>" : text; //$"[{text}](https://zkillboard.com/kill/{id})";
                            urlsList.Add($"{text}: https://zkillboard.com/kill/{id}");
                        }
                        else if (url.StartsWith("bookmarkFolder"))
                        {
                            //var id = url.Substring(15);
                            data = $"BOOKMARKFOLDER";
                        }
                        else if (url.StartsWith("overviewPreset"))
                        {
                            data = $"OVERVIEW_PRESET";
                        }
                        else if (url.StartsWith("hyperNet"))
                        {
                            data = $"HYPERNET_OFFER";
                        }
                        else if (url.StartsWith("fleet"))
                        {
                            data = $"FLEET";
                        }
                        else if (url.StartsWith("http"))
                        {
                            data = url;
                        }
                        else
                        {
                            var mid1 = url.Split(':');
                            var mid2 = mid1.Length > 1 ? mid1[1].Split("//") : null;
                            var type = Convert.ToInt32(mid2[0]);
                            var id = Convert.ToInt64(mid2[1]);
                            var newUrl = string.Empty;
                            var addon = string.Empty;
                            switch (type)
                            {
                                case 2:
                                    newUrl = $"https://zkillboard.com/corporation/{id}/";
                                    addon = $"[(who)](https://evewho.com/corp/{HttpUtility.UrlPathEncode(text)})";
                                    break;
                                case 16159:
                                    newUrl = $"https://zkillboard.com/alliance/{id}/";
                                    addon = $"[(who)](https://evewho.com/alli/{HttpUtility.UrlPathEncode(text)})";
                                    break;
                                case 5:
                                    newUrl = $"https://zkillboard.com/system/{id}/";
                                    addon = $"[(dotlan)](http://evemaps.dotlan.net/system/{id})";
                                    break;
                                case 3:
                                    newUrl = $"https://zkillboard.com/region/{id}/";
                                    addon = $"[(dotlan)](http://evemaps.dotlan.net/map/{id})";
                                    break;
                                case 4:
                                    newUrl = $"https://zkillboard.com/constellation/{id}/";
                                    break;
                                case var s when s >= 1373 && s <= 1386:
                                    newUrl = $"https://zkillboard.com/character/{id}/";
                                    addon = $"[(who)](https://evewho.com/pilot/{HttpUtility.UrlPathEncode(text)})";
                                    break;
                                default:
                                    isEmpty = true;
                                    break;
                            }

                            if (isEmpty)
                            {
                                data = text;
                            }
                            else if (!string.IsNullOrEmpty(newUrl))
                            {
                                data = forWeb ? $"<a href=\"{newUrl}\">{text}</a>" : text;//$"[{text}]({newUrl}) {addon}";
                               urlsList.Add($"{text}: {newUrl}");
                            }
                        }
                    }
                    catch
                    {

                    }

                    body = index != 0
                        ? $"{body.Substring(0, index)}{data}{body.Substring(endTagStart, body.Length - endTagStart)}"
                        : $"{data}{body.Substring(endTagStart, body.Length - endTagStart)}";
                }

                if(!forWeb)
                    body = body.Replace("</a>", null);

                //data parsing

            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, LogCat.Mail);
                return new [] {input, forWeb ? null : GenerateSpyDetails(fitList, true), forWeb ? null : GenerateSpyDetails(channelList, true), forWeb ? null : GenerateSpyDetails(urlsList, false)};
            }

            //prepare


            return new[] {body, forWeb ? null : GenerateSpyDetails(fitList, true), forWeb ? null : GenerateSpyDetails(channelList, true), forWeb ? null : GenerateSpyDetails(urlsList, false)};
        }

        private static string GenerateSpyDetails(List<string> list, bool wrap)
        {
            var sb = new StringBuilder();
            if (list.Any())
            {
                if(wrap) sb.AppendLine("```");
                list.ForEach(a => sb.AppendLine(a));
                if(wrap) sb.AppendLine("```");
            }
            return sb.ToString();
        }


        private static readonly List<long> LastSpyMailIds = new List<long>();

        public static async Task FeedSpyMail(IEnumerable<ThdAuthUser> users, ulong feedChannel)
        {
            var reason = LogCat.HRM.ToString();
            //preload initial mail IDs to suppress possible dupes on start
            foreach (var user in users.Where(a=> a.DataView.LastSpyMailId > 0))
            {
                if(!LastSpyMailIds.Contains(user.DataView.LastSpyMailId))
                    LastSpyMailIds.Add(user.DataView.LastSpyMailId);
            }
            //processing
            foreach (var user in users)
            {
                var corp = user.DataView.CorporationId;
                var ally= user.DataView.AllianceId;
                var filter = SettingsManager.Settings.HRMModule.SpyFilters.FirstOrDefault(a => a.Value.CorpIds.ContainsValue(corp)).Value;
                if (filter != null)
                    feedChannel = filter.MailFeedChannelId;
                else
                {
                    if (ally > 0)
                    {
                        filter = SettingsManager.Settings.HRMModule.SpyFilters.FirstOrDefault(a => a.Value.AllianceIds.ContainsValue(ally)).Value;
                        if (filter != null)
                            feedChannel = filter.MailFeedChannelId;
                    }
                }
                if(feedChannel == 0) continue;

                var displaySummary = filter?.DisplayMailDetailsSummary ?? true;

                try
                {
                    if (!SettingsManager.HasReadMailScope(user.DataView.PermissionsList))
                        continue;

                    var token = (await APIHelper.ESIAPI.GetAccessToken(user.GetGeneralToken(),
                        $"From Mail | Char ID: {user.CharacterId} | Char name: {user.DataView.CharacterName}"))?.Result;

                    if (string.IsNullOrEmpty(token))
                        continue;
                    var mailHeaders = (await APIHelper.ESIAPI.GetMailHeaders(reason, user.CharacterId.ToString(), token, 0, null))?.Result;

                    if (mailHeaders == null || !mailHeaders.Any()) continue;

                    if (user.DataView.LastSpyMailId > 0)
                    {
                        foreach (var mailHeader in mailHeaders.Where(a => a.mail_id > user.DataView.LastSpyMailId))
                        {
                            if(LastSpyMailIds.Contains(mailHeader.mail_id)) continue;
                            LastSpyMailIds.Add(mailHeader.mail_id);
                            if(LastSpyMailIds.Count > 100)
                                LastSpyMailIds.RemoveRange(0, 20);
                            var mail = await APIHelper.ESIAPI.GetMail(reason, user.CharacterId, token, mailHeader.mail_id);
                            var sender = await APIHelper.ESIAPI.GetCharacterData(reason, mail.from);
                            mailHeader.ToName = await GetRecepientNames(reason, mailHeader.recipients, user.CharacterId, token);
                            var from = $"{user.DataView.CharacterName}[{user.DataView.AllianceTicker ?? user.DataView.CorporationTicker}]";
                            await SendMailNotification(feedChannel, mail, $"**{LM.Get("hrmSpyFeedFrom")} {from}**\n__{LM.Get("hrmSpyMsgFrom",sender?.name, mailHeader.ToName)}__", " ", displaySummary);
                        }
                    }

                    user.DataView.LastSpyMailId = mailHeaders.Max(a => a.mail_id);
                    await DbHelper.SaveAuthUser(user);
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(ex.Message, ex, LogCat.HRM);
                }
            }
        }

        public static async Task<string> GetRecepientNames(string reason, JsonClasses.MailRecipient[] entryRecipients, long inspectCharId, string token)
        {
            var rcp = new StringBuilder();
            foreach (var r in entryRecipients)
            {
                switch (r.recipient_type)
                {
                    case "character":
                        var sch = await APIHelper.ESIAPI.GetCharacterData(reason, r.recipient_id);
                        rcp.Append(string.IsNullOrEmpty(sch?.name) ? "???" : sch.name);
                        rcp.Append(",");
                        break;
                    case "corporation":
                        var scorp = await APIHelper.ESIAPI.GetCorporationData(reason, r.recipient_id);
                        rcp.Append(string.IsNullOrEmpty(scorp?.name) ? "???" : scorp.name);
                        rcp.Append(",");
                        break;
                    case "alliance":
                        var sal = await APIHelper.ESIAPI.GetAllianceData(reason, r.recipient_id);
                        rcp.Append(string.IsNullOrEmpty(sal?.name) ? "???" : sal.name);
                        rcp.Append(",");
                        break;
                    case "mailing_list":
                        var mls = await APIHelper.ESIAPI.GetMailLists(reason, inspectCharId, token);
                        var ml = mls?.FirstOrDefault(a => a.mailing_list_id == r.recipient_id);
                        rcp.Append(ml == null ? LM.Get("mailUnkList") : ml.name);
                        rcp.Append(",");
                        break;
                }
            }

            if (rcp.Length > 0)
                rcp.Remove(rcp.Length - 1, 1);
            return rcp.ToString();
        }

        public static bool HasAuthAccess(in long id)
        {
            if (!SettingsManager.Settings.Config.ModuleMail) return false;
            var m = TickManager.GetModule<MailModule>();
            return m?.GetAllParsedCharacters().Contains(id) ?? false;
        }
    }
}
