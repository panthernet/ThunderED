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
            WebServerModule.ModuleConnectors.Add(Reason, OnDisplayTimers);
        }

        private async Task<bool> OnDisplayTimers(HttpListenerRequestEventArgs context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                var extPort = SettingsManager.Get("webServerModule", "webExternalPort");
                var port = SettingsManager.Get("webServerModule", "webListenPort");

                if (request.HttpMethod == HttpMethod.Get.ToString())
                {
                    if (request.Url.LocalPath == "/callback.php" || request.Url.LocalPath == $"{extPort}/callback.php" || request.Url.LocalPath == $"{port}/callback.php")
                    {
                        var clientID = SettingsManager.Get("auth","ccpAppClientId");
                        var secret = SettingsManager.Get("auth","ccpAppSecret");

                        var prms = request.Url.Query.TrimStart('?').Split('&');
                        var code = prms[0].Split('=')[1];
                        var state = prms.Length > 1 ? prms[1].Split('=')[1] : null;

                        if (state != "11") return false;

                        //state = 11 && have code
                        var result = await WebAuthModule.GetCHaracterIdFromCode(code, clientID, secret);
                        if (result == null)
                        {
                            //TODO invalid auth
                            await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                            return true;
                        }
                        var characterId = Convert.ToInt32(result[0]);
                        await SQLiteHelper.SQLiteDataInsertOrUpdate("timersAuth", new Dictionary<string, object> {{"id", result[0]}, {"time", DateTime.Now}});
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
                        var characterId = Convert.ToInt32(Encoding.UTF8.GetString(Convert.FromBase64String(HttpUtility.UrlDecode(inputId))));
                        var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterId, true);
                        if (rChar == null)
                        {
                            await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                            return true;
                        }
                        
                        //have charId - had to check it
                        //check in db
                        var timeout = SettingsManager.GetInt("timersModule", "authTimeoutInMinutes");
                        if (timeout != 0)
                        {
                            var result = await SQLiteHelper.SQLiteDataQuery("timersAuth", "time", "id", characterId.ToString());
                            if (result == null || (DateTime.Now - DateTime.Parse(result)).TotalMinutes > timeout)
                            {
                                //redirect to auth
                                await response.RedirectAsync(new Uri(WebServerModule.GetTimersAuthURL()));
                                return true;
                            }
                        }

                        #region check access
                        var authgroups = SettingsManager.GetSubList("timersModule","accessList");
                        var accessCorps = new List<int>();
                        var accessAlliance = new List<int>();
                        var accessChars = new List<int>();
                        foreach (var config in authgroups)
                        {
                            var configChildren = config.GetChildren().ToList();
                            var id = configChildren.FirstOrDefault(x => x.Key == "id")?.Value ?? "";
                            var isAlliance = Convert.ToBoolean(configChildren.FirstOrDefault(x => x.Key == "isAlliance")?.Value ?? "false");
                            var isChar = Convert.ToBoolean(configChildren.FirstOrDefault(x => x.Key == "isCharacter")?.Value ?? "false");
                            if(isChar)
                                accessChars.Add(Convert.ToInt32(id));
                            else
                            {
                                if (isAlliance)
                                    accessAlliance.Add(Convert.ToInt32(id));
                                else accessCorps.Add(Convert.ToInt32(id));
                            }
                        }
                        authgroups = SettingsManager.GetSubList("timersModule","editList");
                        var editCorps = new List<int>();
                        var editAlliance = new List<int>();
                        var editChars = new List<int>();
                        foreach (var config in authgroups)
                        {
                            var configChildren = config.GetChildren().ToList();
                            var id = configChildren.FirstOrDefault(x => x.Key == "id")?.Value ?? "";
                            var isAlliance = Convert.ToBoolean(configChildren.FirstOrDefault(x => x.Key == "isAlliance")?.Value ?? "false");
                            var isChar = Convert.ToBoolean(configChildren.FirstOrDefault(x => x.Key == "isCharacter")?.Value ?? "false");
                            if(isChar)
                                editChars.Add(Convert.ToInt32(id));
                            else
                            {
                                if (isAlliance)
                                    editAlliance.Add(Convert.ToInt32(id));
                                else editCorps.Add(Convert.ToInt32(id));
                            }
                        }

                        if (!accessCorps.Contains(rChar.corporation_id) && !editCorps.Contains(rChar.corporation_id) &&
                            (!rChar.alliance_id.HasValue || !(rChar.alliance_id > 0) || (!accessAlliance.Contains(
                                                                                             rChar.alliance_id
                                                                                                 .Value) && !editAlliance.Contains(
                                                                                             rChar.alliance_id.Value))))
                        {
                            if (!editChars.Contains(characterId) && !accessChars.Contains(characterId))
                            {
                                //TODO access denied
                                return true;
                            }
                        }

                        var isEditor = editCorps.Contains(rChar.corporation_id) || (rChar.alliance_id.HasValue && rChar.alliance_id.Value > 0 && editAlliance.Contains(rChar.alliance_id.Value))
                            || editChars.Contains(characterId);
                        #endregion

                        if (isEditor && data != "0")
                        {

                            if (data.StartsWith("delete"))
                            {
                                data = data.Substring(6, data.Length - 6);
                                await SQLiteHelper.SQLiteDataDelete("timers", "id", data);
                            }
                            else
                            {

                                var inp = Encoding.UTF8.GetString(Convert.FromBase64String(HttpUtility.UrlDecode(data)));
                                TimerItem entry = null;
                                try
                                {
                                    entry = JsonConvert.DeserializeObject<TimerItem>(inp);
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

                                if (entry.GetDateTime() == null)
                                {
                                    await response.WriteContentAsync(LM.Get("invalidTimeFormat"));
                                    return true;
                                }

                                //save
                                entry.timerChar = rChar.name;
                                await SQLiteHelper.SQLiteDataInsertOrUpdate("timers", entry.GetDictionary());
                                return true;
                            }
                        }

                        var baseCharId = Convert.ToBase64String(Encoding.UTF8.GetBytes(characterId.ToString()));

                        response.Headers.ContentEncoding.Add("utf-8");
                        response.Headers.ContentType.Add("text/html;charset=utf-8");
                        var text = File.ReadAllText(SettingsManager.FileTemplateTimersPage).Replace("{header}", LM.Get("timersTemplateHeader"))
                            .Replace("{loggedInAs}", string.Format(LM.Get("loggedInAs"), rChar.name))
                            .Replace("{charId}", baseCharId )
                            .Replace("{body}", await GenerateTimersHtml(isEditor, baseCharId))
                            .Replace("{isEditorElement}", isEditor ? null : "d-none")
                            .Replace("{addNewTimerHeader}", LM.Get("timersAddHeader"))
                            .Replace("{timersType}",LM.Get("timersType"))
                            .Replace("{timersStage}",LM.Get("timersStage"))
                            .Replace("{timersLocation}",LM.Get("timersLocation"))
                            .Replace("{timersOwner}",LM.Get("timersOwner"))
                            .Replace("{timersET}",LM.Get("timersET"))
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
                            ;
                        await response.WriteContentAsync(text);
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

        public async Task<string> GenerateTimersHtml(bool isEditor, string baseCharId)
        {
            var timers = await SQLiteHelper.SQLiteDataSelectTimers();
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

            var timeFormat = SettingsManager.Get("config", "shortTimeFormat");
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
            if(_lastTimersCheck != null && (DateTime.Now - _lastTimersCheck.Value).TotalMinutes <= 1) return;
            _lastTimersCheck = DateTime.Now;
            
            var timers = await SQLiteHelper.SQLiteDataSelectTimers();
            timers?.ForEach(async timer =>
            {
                var dt = timer.GetDateTime();
                if (dt != null && (dt.Value - DateTime.UtcNow).TotalMinutes < 0)
                {
                    await SQLiteHelper.SQLiteDataDelete("timers", "id", timer.id);
                    return;
                }


                var channel = SettingsManager.GetULong("timersModule", "announceChannel");
                if(channel == 0) return;

                var announces = SettingsManager.GetSubList("timersModule", "announces").Select(a => Convert.ToInt32(a.Value)).OrderByDescending(a => a).ToList();
                if(announces.Count == 0) return;

                //if we don;t have any lesser announce times
                if (timer.announce != 0 && announces.Min() >= timer.announce) return;

                if (timer.announce == 0)
                {

                    var an = announces.First();
                    if ((timer.GetDateTime().Value - DateTime.UtcNow).TotalMinutes <= an)
                    {
                        //announce
                        await SendNotification(timer, channel);
                        await SQLiteHelper.SQLiteDataUpdate("timers", "announce", an, "id", timer.id);
                    }
                }
                else
                {
                    var aList = announces.Where(a => a < timer.announce).OrderByDescending(a => a).ToList();
                    if(aList.Count == 0) return;
                    
                    var an = aList.First();
                    if ((timer.GetDateTime().Value - DateTime.UtcNow).TotalMinutes <= an)
                    {
                        //announce
                        await SendNotification(timer, channel);
                        await SQLiteHelper.SQLiteDataUpdate("timers", "announce", an, "id", timer.id);
                    }
                }
            });
        }

        private async Task SendNotification(TimerItem timer, ulong channel)
        {
            var embed = new EmbedBuilder()
                .WithTitle(string.Format(LM.Get("timerNotifyTitle"), timer.timerLocation))
                .WithThumbnailUrl(SettingsManager.Get("resources", "imgTimerAlert"))
                .AddInlineField(LM.Get("timersType"), timer.GetModeName())
                .AddInlineField(LM.Get("timersStage"), timer.GetStageName())
                .AddInlineField(LM.Get("timersOwner"), timer.timerOwner)
                .AddInlineField(LM.Get("timersRemaining"), timer.GetRemains())
                .AddField(LM.Get("timersNotes"), timer.timerNotes);

            await APIHelper.DiscordAPI.SendMessageAsync(APIHelper.DiscordAPI.GetChannel(channel), "", embed.Build());
        }

        public static async Task AddTimer(TimerItem entry)
        {
            await SQLiteHelper.SQLiteDataInsertOrUpdate("timers", entry.GetDictionary());

        }
    }
}
