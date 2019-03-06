using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Discord;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Modules.OnDemand;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules
{
    public class MailModule: AppModuleBase
    {
        public override LogCat Category => LogCat.Mail;

        private readonly int _checkInterval;
        private DateTime _lastCheckTime = DateTime.MinValue;

        private readonly ConcurrentDictionary<long, string> _tags = new ConcurrentDictionary<long, string>();

        public MailModule()
        {
            _checkInterval = Settings.MailModule.CheckIntervalInMinutes;
            if (_checkInterval == 0)
                _checkInterval = 1;
            WebServerModule.ModuleConnectors.Add(Reason, OnAuthRequest);
        }

        private async Task<bool> OnAuthRequest(HttpListenerRequestEventArgs context)
        {
            if (!Settings.Config.ModuleMail) return false;

            var request = context.Request;
            var response = context.Response;

            try
            {
                var extPort = Settings.WebServerModule.WebExternalPort;
                var port = Settings.WebServerModule.WebExternalPort;

                if (request.HttpMethod == HttpMethod.Get.ToString())
                {
                    if (request.Url.LocalPath == "/callback.php" || request.Url.LocalPath == $"{extPort}/callback.php" || request.Url.LocalPath == $"{port}/callback.php")
                    {
                        var clientID = Settings.WebServerModule.CcpAppClientId;
                        var secret = Settings.WebServerModule.CcpAppSecret;

                        var prms = request.Url.Query.TrimStart('?').Split('&');
                        var code = prms[0].Split('=')[1];
                        var state = prms.Length > 1 ? prms[1].Split('=')[1] : null;

                        if (state != "12") return false;

                        //state = 12 && have code
                        var result = await WebAuthModule.GetCharacterIdFromCode(code, clientID, secret);
                        if (result == null)
                        {
                            await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("Mail Module", LM.Get("accessDenied"), WebServerModule.GetAuthPageUrl()), response);
                            return true;
                        }

                        var lCharId = Convert.ToInt64(result[0]);

                        if (Settings.MailModule.AuthGroups.Values.All(a => !a.Id.Contains(lCharId)))
                        {
                            await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("Mail Module", LM.Get("accessDenied"), WebServerModule.GetAuthPageUrl()), response);
                            return true;
                        }

                        await SQLHelper.InsertOrUpdateTokens("", result[0], result[1], "");
                        await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateMailAuthSuccess)
                            .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                            .Replace("{header}", "authTemplateHeader")
                            .Replace("{body}", LM.Get("mailAuthSuccessHeader"))
                            .Replace("{body2}", LM.Get("mailAuthSuccessBody"))
                            .Replace("{backText}", LM.Get("backText")), response
                        );
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }

            return false;
        }

        public override async Task Run(object prm)
        {
            if (IsRunning) return;
            IsRunning = true;
            try
            {
                if((DateTime.Now - _lastCheckTime).TotalMinutes < _checkInterval) return;
                _lastCheckTime = DateTime.Now;
                await LogHelper.LogModule("Running Mail module check...", Category);

                foreach (var groupPair in Settings.MailModule.AuthGroups)
                {
                    var group = groupPair.Value;
                    if (group.DefaultChannel == 0)
                        continue;
                    var defaultChannel = group.DefaultChannel;

                    foreach (var charId in group.Id)
                    {
                        if (charId == 0) continue;

                        var rToken = await SQLHelper.GetRefreshTokenMail(charId);
                        if (string.IsNullOrEmpty(rToken))
                        {
                            await SendOneTimeWarning(charId, $"Mail feed token for character {charId} not found! User is not authenticated.");
                            continue;
                        }

                        var token = await APIHelper.ESIAPI.RefreshToken(rToken, Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret);
                        if (string.IsNullOrEmpty(token))
                        {
                            await LogHelper.LogWarning($"Unable to get contracts token for character {charId}. Refresh token might be outdated or no more valid.", Category);
                            continue;
                        }


                        var includePrivate = group.IncludePrivateMail;

                        if (group.Filters.Values.All(a => a.FilterSenders.Count == 0 && a.FilterLabels.Count == 0 && a.FilterMailList.Count == 0))
                        {
                            await LogHelper.LogWarning($"Mail feed for user {charId} has no labels, lists or senders configured!", Category);
                            continue;
                        }

                        var labelsData = await APIHelper.ESIAPI.GetMailLabels(Reason, charId.ToString(), token);
                        var searchLabels = labelsData?.labels.Where(a => a.name.ToLower() != "sent" && a.name.ToLower() != "received").ToList() ??
                                           new List<JsonClasses.MailLabel>();
                        var mailLists = await APIHelper.ESIAPI.GetMailLists(Reason, charId, token);

                        var etag = _tags.GetOrNull(charId);
                        var result = await APIHelper.ESIAPI.GetMailHeaders(Reason, charId.ToString(), token, 0, etag);
                        _tags.AddOrUpdateEx(charId, result.Data.ETag);
                        if(result.Data.IsNotModified) continue;

                        var mailsHeaders = result.Result;

                        var lastMailId = await SQLHelper.GetLastMailId(charId);
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
                                    await SendMailNotification(channel, mail, LM.Get("mailMsgTitle", from), group.DefaultMention);
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                await LogHelper.LogEx($"MailCheck: {mailHeader?.mail_id} {mailHeader?.subject}", ex);
                            }
                        }

                        if (prevMailId != lastMailId || lastMailId == 0)
                            await SQLHelper.UpdateMail(charId, lastMailId);
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

        private static async Task SendMailNotification(ulong channel, JsonClasses.Mail mail, string from, string mention)
        {
           // var stamp = DateTime.Parse(mail.timestamp).ToString(SettingsManager.Settings.Config.ShortTimeFormat);
            var body = await PrepareBodyMessage(mail.body);
            var fields = body.Split(1023);

           /* var embed = new EmbedBuilder()
                .WithThumbnailUrl(SettingsManager.Settings.Resources.ImgMail);
            var cnt = 0;
            foreach (var field in fields)
            {
                if (cnt == 0)
                    embed.AddField($"{LM.Get("mailSubject")} {mail.subject}", string.IsNullOrWhiteSpace(field) ? "---" : field);
                else
                    embed.AddField($"-", string.IsNullOrWhiteSpace(field) ? "---" : field);
                cnt++;
            }
            embed.WithFooter($"{LM.Get("mailDate")} {stamp}");*/
            var ch = APIHelper.DiscordAPI.GetChannel(channel);
            await APIHelper.DiscordAPI.SendMessageAsync(ch, $"{mention} {from}");
            foreach (var field in fields)
                await APIHelper.DiscordAPI.SendMessageAsync(ch, field);
        }

        public static async Task<string> PrepareBodyMessage(string input, bool forWeb = false)
        {
            if (string.IsNullOrEmpty(input)) return " ";

            string body;
            if (!forWeb)
                body = input.Replace("<br>", Environment.NewLine)
                    .Replace("<b>", "**").Replace("</b>", "**")
                    .Replace("<u>", "__").Replace("</u>", "__")
                    .Replace("</font>", null)
                    .Replace("<loc>", null).Replace("</loc>", null);
            else 
                body = input.Replace("<loc>", null).Replace("</loc>", null).Replace("</font>", null);

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

                var prevIndex = 0;
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
                    bool isEmpty = false;
                    try
                    {
                        if (url.StartsWith("http"))
                        {
                            data = forWeb ? data : url;
                        }
                        else if (url.StartsWith("joinChannel"))
                        {
                            data = forWeb ? text : $"[{text}](<url={url}>{text}</url>)";
                        }
                        else if (url.StartsWith("fitting"))
                        {
                            data = text;
                        }
                        else if (url.StartsWith("killReport"))
                        {
                            var id = url.Substring(11, url.Length - 11);
                            data = forWeb ? $"<a href=\"https://zkillboard.com/kill/{id}\">{text}</a>" : $"[{text}](https://zkillboard.com/kill/{id})";
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
                                data = forWeb ? $"<a href=\"{newUrl}\">{text}</a>" : $"[{text}]({newUrl}) {addon}";
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
                return " ";
            }

            return body;
        }


        public static async Task FeedSpyMail(IEnumerable<AuthUserEntity> users, ulong feedChannel)
        {
            if(feedChannel == 0) return;
            var reason = LogCat.HRM.ToString();
            foreach (var user in users)
            {
                try
                {
                    if (!SettingsManager.HasReadMailScope(user.Data.PermissionsList))
                        continue;

                    var token = await APIHelper.ESIAPI.RefreshToken(user.RefreshToken, SettingsManager.Settings.WebServerModule.CcpAppClientId,
                        SettingsManager.Settings.WebServerModule.CcpAppSecret);

                    if (string.IsNullOrEmpty(token))
                        continue;
                    var mailHeaders = (await APIHelper.ESIAPI.GetMailHeaders(reason, user.CharacterId.ToString(), token, 0, null))?.Result;

                    if (mailHeaders == null || !mailHeaders.Any()) continue;

                    if (user.Data.LastSpyMailId > 0)
                    {
                        foreach (var mailHeader in mailHeaders.Where(a => a.mail_id > user.Data.LastSpyMailId))
                        {
                            var mail = await APIHelper.ESIAPI.GetMail(reason, user.CharacterId, token, mailHeader.mail_id);
                            var sender = await APIHelper.ESIAPI.GetCharacterData(reason, mail.from);
                            mailHeader.ToName = await GetRecepientNames(reason, mailHeader.recipients, user.CharacterId, token);
                            var from = $"{user.Data.CharacterName}[{user.Data.AllianceTicker ?? user.Data.CorporationTicker}]";
                            await SendMailNotification(feedChannel, mail, $"{LM.Get("hrmSpyFeedFrom")} {from}\n{LM.Get("hrmSpyMsgFrom",sender?.name, mailHeader.ToName)}", " ");
                        }
                    }

                    user.Data.LastSpyMailId = mailHeaders.Max(a => a.mail_id);
                    await SQLHelper.SaveAuthUser(user);
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(ex.Message, ex, LogCat.HRM);
                }
            }
        }

        public static async Task<string> SearchRelated(long searchCharId, HRMModule.SearchMailItem item, string authCode)
        {
            var isGlobal = searchCharId == 0;
            var users = new List<AuthUserEntity>();
            if (isGlobal)
            {
                users = await SQLHelper.GetAuthUsersWithPerms();
                switch (item.smAuthType)
                {
                    case 1:
                        users = users.Where(a => a.IsAuthed).ToList();
                        break;
                    case 2:
                        users = users.Where(a => a.IsPending).ToList();
                        break;
                    default:
                        return null;
                }
            }
            else
            {
                var u = await SQLHelper.GetAuthUserByCharacterId(searchCharId);
                if (u == null)
                    return LM.Get("hrmSearchMailErrorSourceNotFound");
                users.Add(u);
            }

            if (users.Count == 0) return LM.Get("hrmSearchMailNoUsersToCheck");
            var Reason = "HRM";

            long charId = 0;
            long corpId = 0;
            long allyId = 0;
            //JsonClasses.CharacterData rChar = null;
            switch (item.smSearchType)
            {
                case 1:
                    var charIdLookup = await APIHelper.ESIAPI.SearchCharacterId(Reason, item.smText);
                    charId = charIdLookup?.character?.FirstOrDefault() ?? 0;
                   // rChar = charIdLookup?.character == null || charIdLookup.character.Length == 0 ? null : await APIHelper.ESIAPI.GetCharacterData(Reason, charIdLookup.character.FirstOrDefault());
                    if (charId == 0)
                        return LM.Get("hrmSearchMailErrorCharNotFound");
                    break;
                case 2:
                    var corpIdLookup = await APIHelper.ESIAPI.SearchCorporationId(Reason, item.smText);
                    corpId = corpIdLookup?.corporation == null || corpIdLookup.corporation.Length == 0 ? 0 : corpIdLookup.corporation.FirstOrDefault();
                    if (corpId == 0)
                        return LM.Get("hrmSearchMailErrorCorpNotFound");
                    break;
                case 3:
                    var allyIdLookup = await APIHelper.ESIAPI.SearchAllianceId(Reason, item.smText);
                    allyId = allyIdLookup?.alliance == null || allyIdLookup.alliance.Length == 0 ? 0 : allyIdLookup.alliance.FirstOrDefault();
                    if (allyId == 0)
                        return LM.Get("hrmSearchMailErrorAllianceNotFound");
                    break;
            }

           /* JsonClasses.CorporationData sCorp = null;
            if (!isGlobal && corpId > 0)
                sCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, corpId);
            JsonClasses.AllianceData sAlly = null;
            if (!isGlobal && allyId > 0)
                sAlly = await APIHelper.ESIAPI.GetAllianceData(Reason, allyId);
                */
            var sb = new StringBuilder();
            foreach (var user in users)
            {
                if (!SettingsManager.HasReadMailScope(user.Data.PermissionsList))
                    continue;

                var token = await APIHelper.ESIAPI.RefreshToken(user.RefreshToken, SettingsManager.Settings.WebServerModule.CcpAppClientId,
                    SettingsManager.Settings.WebServerModule.CcpAppSecret);

                var mailHeaders = (await APIHelper.ESIAPI.GetMailHeaders(Reason, user.CharacterId.ToString(), token, 0, null))?.Result;

                //filter
                switch (item.smSearchType)
                {
                    case 1:
                    {
                        var newList = new List<JsonClasses.MailHeader>();
                        newList.AddRange(mailHeaders.Where(a => a.@from == charId).ToList());
                        foreach (var header in newList)
                        {
                            header.ToName = isGlobal ? user.Data.CharacterName : (await GetRecepientNames(Reason, header.recipients, user.CharacterId, token));
                            header.FromName = item.smText;
                        }

                        var tmp = mailHeaders.Where(a => a.recipients.Any(b => b.recipient_id == charId)).ToList();
                        foreach (var header in tmp)
                        {
                            header.ToName = isGlobal ? item.smText : (await GetRecepientNames(Reason, header.recipients, user.CharacterId, token));
                            header.FromName = user.Data.CharacterName;
                        }

                        mailHeaders = newList;
                        mailHeaders.AddRange(tmp);
                    }
                        break;
                    case 2: //corp
                    {
                        var newList = new List<JsonClasses.MailHeader>();
                        foreach (var header in mailHeaders)
                        {
                            var ch = await APIHelper.ESIAPI.GetCharacterData(Reason, header.@from);
                            if (ch == null) continue;
                            if (ch.corporation_id == corpId)
                            {
                                header.ToName = isGlobal ? user.Data.CharacterName : (await GetRecepientNames(Reason, header.recipients, user.CharacterId, token));
                                header.FromName = item.smText;
                                newList.Add(header);
                                continue;
                            }

                            if (isGlobal)
                            {
                                if (header.recipients.Any(b => b.recipient_id == corpId))
                                {
                                    header.ToName = item.smText;
                                    header.FromName = user.Data.CharacterName;
                                    newList.Add(header);
                                    continue;
                                }
                            }
                            else //detailed personal search
                            {
                                foreach (var recipient in header.recipients)
                                {
                                    if (recipient.recipient_type == "character")
                                    {
                                        var r = await APIHelper.ESIAPI.GetCharacterData(Reason, recipient.recipient_id);
                                        if(r == null) continue;
                                        if (r.corporation_id == corpId)
                                        {
                                            header.ToName = await GetRecepientNames(Reason, header.recipients, user.CharacterId, token);
                                            header.FromName = user.Data.CharacterName;
                                            newList.Add(header);
                                            break;
                                        }
                                    }else if (recipient.recipient_type == "corporation")
                                    {
                                        if(recipient.recipient_id == corpId)
                                        {
                                            header.ToName = await GetRecepientNames(Reason, header.recipients, user.CharacterId, token);
                                            header.FromName = user.Data.CharacterName;
                                            newList.Add(header);
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        mailHeaders = newList.ToList();
                    }
                        break;
                    case 3: //ally
                    {
                        var newList = new List<JsonClasses.MailHeader>();
                        foreach (var header in mailHeaders)
                        {
                            var ch = await APIHelper.ESIAPI.GetCharacterData(Reason, header.@from);
                            if (ch?.alliance_id != null && (ch.alliance_id == allyId || header.@from == allyId))
                            {
                                header.ToName = isGlobal ? user.Data.CharacterName : (await GetRecepientNames(Reason, header.recipients, user.CharacterId, token));
                                header.FromName = item.smText;
                                newList.Add(header);
                                continue;
                            }

                            if (isGlobal)
                            {
                                if (ch?.alliance_id != null && header.recipients.Any(b => b.recipient_id == allyId))
                                {
                                    header.ToName = item.smText;
                                    header.FromName = user.Data.CharacterName;
                                    newList.Add(header);
                                    continue;
                                }
                            }
                            else
                            {
                                foreach (var recipient in header.recipients)
                                {
                                    if (recipient.recipient_type == "character")
                                    {
                                        var r = await APIHelper.ESIAPI.GetCharacterData(Reason, recipient.recipient_id);
                                        if(r == null) continue;
                                        if (r.alliance_id.HasValue && r.alliance_id == allyId)
                                        {
                                            header.ToName = await GetRecepientNames(Reason, header.recipients, user.CharacterId, token);
                                            header.FromName = user.Data.CharacterName;
                                            newList.Add(header);
                                            break;
                                        }

                                    }else if (recipient.recipient_type == "corporation")
                                    {
                                        var corp = await APIHelper.ESIAPI.GetCorporationData(Reason, recipient.recipient_id);
                                        if(corp == null) continue;
                                        if (corp.alliance_id.HasValue && corp.alliance_id == allyId)
                                        {
                                            header.ToName = await GetRecepientNames(Reason, header.recipients, user.CharacterId, token);
                                            header.FromName = user.Data.CharacterName;
                                            newList.Add(header);
                                            break;
                                        }
                                    }else if (recipient.recipient_type == "alliance")
                                    {
                                        if(recipient.recipient_id == allyId)
                                        {
                                            header.ToName = await GetRecepientNames(Reason, header.recipients, user.CharacterId, token);
                                            header.FromName = user.Data.CharacterName;
                                            newList.Add(header);
                                            break;
                                        }
                                    }
                                }
                                continue;
                            }
                        }

                        mailHeaders = newList.ToList();
                    }
                        break;
                    case 4: //header text
                    {
                        mailHeaders = mailHeaders.Where(a => a.subject.Contains(item.smText, StringComparison.OrdinalIgnoreCase)).ToList();
                    }
                        break;
                }

                foreach (var entry in mailHeaders)
                {
                    var mailBodyUrl = WebServerModule.GetHRM_AjaxMailURL(entry.mail_id, user.CharacterId, authCode);

                    sb.AppendLine("<tr>");
                    sb.AppendLine($"  <td><a href=\"#\" onclick=\"openMailDialog('{mailBodyUrl}')\">{entry.subject}</td>");
                    sb.AppendLine($"  <td>{entry.FromName ?? LM.Get("Unknown")}</td>");
                    sb.AppendLine($"  <td>{entry.ToName ?? LM.Get("Unknown")}</td>");
                    sb.AppendLine($"  <td>{entry.Date.ToShortDateString()}</td>");
                    sb.AppendLine("</tr>");
                }
            }

            if (sb.Length > 0)
            {
                var sbFinal = new StringBuilder();
                sbFinal.AppendLine("<thead>");
                sbFinal.AppendLine("<tr>");
                sbFinal.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("mailSubjectHeader")}</th>");
                sbFinal.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("mailFromHeader")}</th>");
                sbFinal.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("mailToHeader")}</th>");
                sbFinal.AppendLine($"<th scope=\"col\">{LM.Get("mailDateHeader")}</th>");
                sbFinal.AppendLine("</tr>");
                sbFinal.AppendLine("</thead>");
                sbFinal.AppendLine("<tbody>");
                sbFinal.Append(sb.ToString());
                sbFinal.AppendLine("</tbody>");
                return sbFinal.ToString();
            }

            return "No results";
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
    }
}
