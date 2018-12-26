using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Json.Internal;
using ThunderED.Modules.OnDemand;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules
{
    internal class TimersModule: AppModuleBase
    {
        public override LogCat Category => LogCat.Timers;

        public TimersModule()
        {
            LogHelper.LogModule("Initializing Timers module...", Category).GetAwaiter().GetResult();
            WebServerModule.ModuleConnectors.Add(Reason, OnDisplayTimers);
        }

        private async Task<bool> OnDisplayTimers(HttpListenerRequestEventArgs context)
        {
            if (!Settings.Config.ModuleTimers) return false;

            var request = context.Request;
            var response = context.Response;

            try
            {
                var extPort = Settings.WebServerModule.WebExternalPort;
                var port = Settings.WebServerModule.WebListenPort;

                if (request.HttpMethod == HttpMethod.Get.ToString())
                {
                    if (request.Url.LocalPath == "/callback.php" || request.Url.LocalPath == $"{extPort}/callback.php" || request.Url.LocalPath == $"{port}/callback.php")
                    {
                        var clientID = Settings.WebServerModule.CcpAppClientId;
                        var secret = Settings.WebServerModule.CcpAppSecret;

                        var prms = request.Url.Query.TrimStart('?').Split('&');
                        var code = prms[0].Split('=')[1];
                        var state = prms.Length > 1 ? prms[1].Split('=')[1] : null;

                        if (state != "11") return false;

                        //state = 11 && have code
                        var result = await WebAuthModule.GetCharacterIdFromCode(code, clientID, secret);
                        if (result == null)
                        {
                            //TODO invalid auth
                            await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                            return true;
                        }
                        var characterId = Convert.ToInt64(result[0]);
                        await SQLHelper.SQLiteDataInsertOrUpdate("timersAuth", new Dictionary<string, object> {{"id", result[0]}, {"time", DateTime.Now}});
                        //redirect to timers
                        var iid = Convert.ToBase64String(Encoding.UTF8.GetBytes(characterId.ToString()));
                        iid = HttpUtility.UrlEncode(iid);
                        var url = $"{WebServerModule.GetTimersURL()}?data=0&id={iid}&state=11";
                        await response.RedirectAsync(new Uri(url));
                        return true;
                    }

                    if (request.Url.LocalPath == "/timers.php" || request.Url.LocalPath == $"{extPort}/timers.php" || request.Url.LocalPath == $"{port}/timers.php")
                    {
                        if (string.IsNullOrWhiteSpace(request.Url.Query))
                        {
                            //redirect to auth
                            await response.RedirectAsync(new Uri(WebServerModule.GetTimersAuthURL()));
                            return true;
                        }

                        var prms = request.Url.Query.TrimStart('?').Split('&');
                        if (prms.Length != 3)
                        {
                            await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                            return true;
                        }

                        var data = prms[0].Split('=')[1];
                        var inputId = prms[1].Split('=')[1];
                        var state = prms[2].Split('=')[1];
                        if (state != "11")
                        {
                            await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                            return true;
                        }
                        var characterId = Convert.ToInt64(Encoding.UTF8.GetString(Convert.FromBase64String(HttpUtility.UrlDecode(inputId))));

                        var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterId, true);
                        if (rChar == null)
                        {
                            await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                            return true;
                        }
                        
                        //have charId - had to check it
                        //check in db
                        var timeout = Settings.TimersModule.AuthTimeoutInMinutes;
                        if (timeout != 0)
                        {
                            var result = await SQLHelper.SQLiteDataQuery<string>("timersAuth", "time", "id", characterId.ToString());
                            if (result == null || (DateTime.Now - DateTime.Parse(result)).TotalMinutes > timeout)
                            {
                                //redirect to auth
                                await response.RedirectAsync(new Uri(WebServerModule.GetTimersAuthURL()));
                                return true;
                            }
                        }

                        if (!CheckAccess(characterId, rChar, out var isEditor))
                        {
                            await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("Timers Module", LM.Get("accessDenied")), response);
                            return true;
                        }

                        if (isEditor && data.StartsWith("delete"))
                        {
                            data = data.Substring(6, data.Length - 6);
                            await SQLHelper.SQLiteDataDelete("timers", "id", data);
                            var x = HttpUtility.ParseQueryString(request.Url.Query);
                            x.Set("data", "0");
                            await response.RedirectAsync(new Uri($"{request.Url.ToString().Split('?')[0]}?{x}"));
                            return true;
                        }

                        await WriteCorrectResponce(response, isEditor, characterId);
                        return true;
                    }
                }
                else if (request.HttpMethod == HttpMethod.Post.ToString())
                {
                    var prms = request.Url.Query.TrimStart('?').Split('&');
                    if (prms.Length != 3)
                    {
                        //await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                        return false;
                    }
                    var inputId = prms[1].Split('=')[1];
                    var state = prms[2].Split('=')[1];
                    if (state != "11")
                    {
                       // await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                        return false;
                    }

                    var characterId = Convert.ToInt64(Encoding.UTF8.GetString(Convert.FromBase64String(HttpUtility.UrlDecode(inputId))));

                    var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterId, true);
                    if (rChar == null)
                    {
                       // await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                        return false;
                    }

                    if(!CheckAccess(characterId, rChar, out var isEditor))
                        return true;

                    var data = await request.ReadContentAsStringAsync();

                    if (isEditor && data != null)
                    {
                        if (data.StartsWith("delete"))
                        {
                            data = data.Substring(6, data.Length - 6);
                            await SQLHelper.SQLiteDataDelete("timers", "id", data);
                        }
                        else
                        {
                            TimerItem entry = null;
                            try
                            {
                                entry = JsonConvert.DeserializeObject<TimerItem>(data);
                            }
                            catch
                            {
                                //ignore
                            }

                            if (entry == null)
                            {
                                await response.WriteContentAsync(LM.Get("invalidInputData"));
                                return true;
                            }

                            var iDate = entry.GetDateTime();
                            if (iDate == null)
                            {
                                await response.WriteContentAsync(LM.Get("invalidTimeFormat"));
                                return true;
                            }

                            if (iDate < DateTime.Now)
                            {
                                await response.WriteContentAsync(LM.Get("passedTimeValue"));
                                return true;
                            }

                            //save
                            entry.timerChar = rChar.name;
                            await SQLHelper.SQLiteDataInsertOrUpdate("timers", entry.GetDictionary());
                        }
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

        private async Task WriteCorrectResponce(HttpListenerResponse response, bool isEditor, long characterId)
        {
            var baseCharId = Convert.ToBase64String(Encoding.UTF8.GetBytes(characterId.ToString()));
            var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterId, true);

            if (rChar == null)
            {
                await response.WriteContentAsync("ERROR: Probably EVE ESI is shut down at the moment. Please try again later.");
                return;
            }

            var text = File.ReadAllText(SettingsManager.FileTemplateTimersPage).Replace("{header}", LM.Get("timersTemplateHeader"))
                .Replace("{loggedInAs}", LM.Get("loggedInAs", rChar.name))
                .Replace("{charId}", baseCharId )
                .Replace("{body}", await GenerateTimersHtml(isEditor, baseCharId))
                .Replace("{isEditorElement}", isEditor ? null : "d-none")
                .Replace("{addNewTimerHeader}", LM.Get("timersAddHeader"))
                .Replace("{addNewRfTimerHeader}", LM.Get("timersAddRfHeader"))
                .Replace("{timersType}",LM.Get("timersType"))
                .Replace("{timersStage}",LM.Get("timersStage"))
                .Replace("{timersLocation}",LM.Get("timersLocation"))
                .Replace("{timersOwner}",LM.Get("timersOwner"))
                .Replace("{timersET}",LM.Get("timersET"))
                .Replace("{timersRfET}",LM.Get("timersRfET"))
                .Replace("{timersNotes}",LM.Get("timersNotes"))
                .Replace("{Add}",LM.Get("Add"))
                .Replace("{Cancel}",LM.Get("Cancel"))
                .Replace("{timerOffensive}",LM.Get("timerOffensive"))
                .Replace("{timerDefensive}",LM.Get("timerDefensive"))
                .Replace("{timerHull}",LM.Get("timerHull"))
                .Replace("{timerArmor}",LM.Get("timerArmor"))
                .Replace("{timerShield}",LM.Get("timerShield"))
                .Replace("{timerOther}",LM.Get("timerOther"))
                .Replace("{LogOutUrl}",WebServerModule.GetWebSiteUrl())
                    .Replace("{LogOut}",LM.Get("LogOut"))
                    .Replace("{timerTooltipLocation}",LM.Get("timerTooltipLocation"))
                    .Replace("{timerTooltipOwner}",LM.Get("timerTooltipOwner"))
                    .Replace("{timerTooltipET}",LM.Get("timerTooltipET"))
                    .Replace("{locale}",LM.Locale)
                    .Replace("{dateformat}", Settings.TimersModule.TimeInputFormat)
                ;
            await WebServerModule.WriteResponce(text, response);
        }

        private bool CheckAccess(long characterId, JsonClasses.CharacterData rChar, out bool isEditor)
        {
            var authgroups = Settings.TimersModule.AccessList;
            var accessCorps = new List<long>();
            var accessAlliance = new List<long>();
            var accessChars = new List<long>();
            isEditor = false;
            bool skip = false;

            if (authgroups.Count == 0 || authgroups.Values.All(a => !a.AllianceIDs.Any() && !a.CorporationIDs.Any() && !a.CharacterIDs.Any()))
            {
                skip = true;
            }
            else
            {
                foreach (var config in authgroups)
                {
                    accessChars.AddRange(config.Value.CharacterIDs.Where(a=> a > 0));
                    accessAlliance.AddRange(config.Value.AllianceIDs.Where(a=> a > 0));
                    accessCorps.AddRange(config.Value.CorporationIDs.Where(a=> a > 0));
                }
            }

            authgroups = Settings.TimersModule.EditList;
            var editCorps = new List<long>();
            var editAlliance = new List<long>();
            var editChars = new List<long>();
            bool skip2 = false;

            //check for Discord admins
            if (Settings.TimersModule.GrantEditRolesToDiscordAdmins)
            {
                var roles = SQLHelper.SQLiteDataQuery<string>("authUsers", "role", "characterID", characterId.ToString()).GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(roles))
                {
                    var exemptRoles = Settings.Config.DiscordAdminRoles;
                    skip2 = roles.Replace("&br;", "\"").Split(',').Any(role => exemptRoles.Contains(role));
                }
            }

            if (authgroups.Count == 0 ||  authgroups.Values.All(a => !a.AllianceIDs.Any() && !a.CorporationIDs.Any() && !a.CharacterIDs.Any()))
            {
                skip2 = true;
            }
            else
            {
                foreach (var config in authgroups)
                {
                    editChars.AddRange(config.Value.CharacterIDs.Where(a=> a > 0));
                    editAlliance.AddRange(config.Value.AllianceIDs.Where(a=> a > 0));
                    editCorps.AddRange(config.Value.CorporationIDs.Where(a=> a > 0));
                }
            }



            if (!skip && !skip2 && !accessCorps.Contains(rChar.corporation_id) && !editCorps.Contains(rChar.corporation_id) &&
                (!rChar.alliance_id.HasValue || !(rChar.alliance_id > 0) || (!accessAlliance.Contains(
                                                                                    rChar.alliance_id
                                                                                        .Value) && !editAlliance.Contains(
                                                                                    rChar.alliance_id.Value))))
            {
                if (!editChars.Contains(characterId) && !accessChars.Contains(characterId))
                {
                    return false;
                }
            }

            isEditor = skip2 || editCorps.Contains(rChar.corporation_id) || (rChar.alliance_id.HasValue && rChar.alliance_id.Value > 0 && editAlliance.Contains(rChar.alliance_id.Value))
                || editChars.Contains(characterId);

            return true;
        }

        public async Task<string> GenerateTimersHtml(bool isEditor, string baseCharId)
        {
            var timers = await SQLHelper.SQLiteDataSelectTimers();
            int counter = 1;
            var sb = new StringBuilder();

            sb.AppendLine("<thead>");
            sb.AppendLine("<tr>");
            sb.AppendLine($"<th scope=\"col-md-auto\">#</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("timersType")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("timersStage")}</th>");
            sb.AppendLine($"<th scope=\"col\">{LM.Get("timersLocation")}</th>");
            sb.AppendLine($"<th scope=\"col\">{LM.Get("timersOwner")}</th>");
            sb.AppendLine($"<th scope=\"col\" class=\"fixed150\">{LM.Get("timersET")}</th>");
            sb.AppendLine($"<th scope=\"col\" class=\"fixed150\">{LM.Get("timersRemaining")}</th>");
            sb.AppendLine($"<th scope=\"col\">{LM.Get("timersNotes")}</th>");
            sb.AppendLine($"<th scope=\"col\">{LM.Get("timersUser")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\" class=\"{(isEditor ? null : "d-none")}\"></th");
            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");

            var timeFormat = Settings.Config.ShortTimeFormat;
            timers.OrderBy(a=> a.GetDateTime()).ToList().ForEach(timer =>
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"  <th scope=\"row\">{counter++}</th>");
                sb.AppendLine($"  <td>{timer.GetModeName()}</td>");
                sb.AppendLine($"  <td>{timer.GetStageName()}</td>");
                sb.AppendLine($"  <td>{HttpUtility.HtmlEncode(timer.timerLocation)}</td>");
                sb.AppendLine($"  <td>{HttpUtility.HtmlEncode(timer.timerOwner)}</td>");
                sb.AppendLine($"  <td>{timer.GetDateTime()?.ToString(timeFormat)}</td>");
                sb.AppendLine($"  <td>{timer.GetRemains()}</td>");
                sb.AppendLine($"  <td>{HttpUtility.HtmlEncode(timer.timerNotes)}</td>");
                sb.AppendLine($"  <td>{HttpUtility.HtmlEncode(timer.timerChar)}</td>");
                if(isEditor)
                    sb.AppendLine($"<td><a class=\"btn btn-danger\" href=\"{WebServerModule.GetTimersURL()}?data=delete{timer.id}&id={HttpUtility.UrlEncode(baseCharId)}&state=11\" role=\"button\" data-toggle=\"confirmation\" data-title=\"{LM.Get("ConfirmDelete")}?\">X</a></td>");
                sb.AppendLine("</tr>");
            });
            sb.AppendLine("</tbody>");
            return sb.ToString();
        }

        private DateTime? _lastTimersCheck;

        public override async Task Run(object prm)
        {
            await ProcessTimers();
        }

        private async Task ProcessTimers()
        {
            if(IsRunning) return;
            IsRunning = true;
            try
            {
                if (_lastTimersCheck != null && (DateTime.Now - _lastTimersCheck.Value).TotalMinutes <= 1) return;
                _lastTimersCheck = DateTime.Now;

                await LogHelper.LogModule("Running timers check...", Category);
                var timers = await SQLHelper.SQLiteDataSelectTimers();
                timers?.ForEach(async timer =>
                {
                    var channel = Settings.TimersModule.AnnounceChannel;
                    var dt = timer.GetDateTime();
                    if (dt != null && (dt.Value - DateTime.UtcNow).TotalMinutes <= 0)
                    {
                        if (channel != 0)
                            await SendNotification(timer, channel);
                        await SQLHelper.SQLiteDataDelete("timers", "id", timer.id);
                        return;
                    }

                    if (channel == 0) return;

                    var announces = Settings.TimersModule.Announces.OrderByDescending(a => a).ToList();
                    if (announces.Count == 0) return;

                    //if we don;t have any lesser announce times
                    if (timer.announce != 0 && announces.Min() >= timer.announce) return;

                    if (timer.announce == 0)
                    {
                        var left = (timer.GetDateTime().Value - DateTime.UtcNow).TotalMinutes;
                        if (left <= announces.Max())
                        {
                            var value = announces.Where(a => a < left).OrderByDescending(a => a).FirstOrDefault();
                            value = value == 0 ? announces.Min() : value;
                            //announce
                            await SendNotification(timer, channel);
                            await SQLHelper.SQLiteDataUpdate("timers", "announce", value, "id", timer.id);
                        }
                    }
                    else
                    {
                        var aList = announces.Where(a => a < timer.announce).OrderByDescending(a => a).ToList();
                        if (aList.Count == 0) return;

                        var an = aList.First();
                        if ((timer.GetDateTime().Value - DateTime.UtcNow).TotalMinutes <= an)
                        {
                            //announce
                            await SendNotification(timer, channel);
                            await SQLHelper.SQLiteDataUpdate("timers", "announce", an, "id", timer.id);
                        }
                    }
                });
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

        private async Task SendNotification(TimerItem timer, ulong channel)
        {
            try
            {
                var remains = timer.GetRemains();
                var stage = timer.GetStageName();
                var mode = timer.GetModeName();
                var embed = new EmbedBuilder()
                    .WithTitle(LM.Get("timerNotifyTitle", string.IsNullOrEmpty(timer.timerLocation) ? "-" : timer.timerLocation))
                    .AddInlineField(LM.Get("timersType"), string.IsNullOrEmpty(mode) ? "-" : mode)
                    .AddInlineField(LM.Get("timersStage"), string.IsNullOrEmpty(stage) ? "-" : stage)
                    .AddInlineField(LM.Get("timersOwner"), string.IsNullOrEmpty(timer.timerOwner) ? "-" : timer.timerOwner)
                    .AddInlineField(LM.Get("timersRemaining"), string.IsNullOrEmpty(remains) ? "-" : remains)
                    .AddField(LM.Get("timersNotes"), string.IsNullOrEmpty(timer.timerNotes) ? "-" : timer.timerNotes);
                if (!string.IsNullOrEmpty(Settings.Resources.ImgTimerAlert))
                    embed.WithThumbnailUrl(Settings.Resources.ImgTimerAlert);

                await APIHelper.DiscordAPI.SendMessageAsync(APIHelper.DiscordAPI.GetChannel(channel), Settings.TimersModule.DefaultMention ?? "", embed.Build()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
        }

        public static async Task AddTimer(TimerItem entry)
        {
            await SQLHelper.SQLiteDataInsertOrUpdate("timers", entry.GetDictionary());

        }

        public static async Task<string> GetUpcomingTimersString(int count = 5)
        {
            var timers = await SQLHelper.SQLiteDataSelectTimers();
            var sb = new StringBuilder();
            if (timers.Count > 0)
            {
                for (int i = 0; i < timers.Count && i < count; i++)
                {
                    var timer = timers[i];
                    sb.Append(
                        $"[{timer.GetModeName()}][{timer.GetStageName()}] {timer.timerLocation} - {timer.GetRemains(true)} ({timer.GetDateTime().Value.ToString(SettingsManager.Settings.Config.ShortTimeFormat)} ET)\n");
                }
            }
            else
            {
                sb.Append(LM.Get("timers_none"));
            }

            return sb.ToString();
        }
    }
}
