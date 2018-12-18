using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Helpers;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules
{
    public class AuthStandingsModule: AppModuleBase
    {
        public override LogCat Category => LogCat.AuthStandings;

        public async Task<bool> Auth(HttpListenerRequestEventArgs context)
        {
            if (!Settings.Config.ModuleAuthStandings) return false;

            var request = context.Request;
            var response = context.Response;
            var extPort = Settings.WebServerModule.WebExternalPort;
            var port = Settings.WebServerModule.WebListenPort;
            try
            {
                if (request.HttpMethod != HttpMethod.Get.ToString())
                    return false;
                if ((request.Url.LocalPath == "/callback.php" || request.Url.LocalPath == $"{extPort}/callback.php" || request.Url.LocalPath == $"{port}/callback.php")
                    && request.Url.Query.Contains("&state=authst"))
                {
                    var prms = request.Url.Query.TrimStart('?').Split('&');
                    var code = prms[0].Split('=')[1];

                    var result = await WebAuthModule.GetCharacterIdFromCode(code, Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret);
                    if (result == null)
                    {
                        var message = LM.Get("ESIFailure");
                        await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", message)
                            .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{backText}", LM.Get("backText")), response);
                        return true;
                    }

                    var characterID = result[0];
                    var numericCharId = Convert.ToInt64(characterID);

                    if (string.IsNullOrEmpty(characterID))
                    {
                        await LogHelper.LogWarning("Bad or outdated stand auth request!");
                        await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuthNotifyFail)
                            .Replace("{message}", LM.Get("authTokenBadRequest"))
                            .Replace("{header}", LM.Get("authTokenHeader")).Replace("{body}", LM.Get("authTokenBodyFail")).Replace("{backText}", LM.Get("backText")), response);
                        return true;
                    }

                    if (Settings.AuthStandingsModule.AuthGroups.Values.All(g => !g.CharacterIDs.Contains(numericCharId)))
                    {
                        await LogHelper.LogWarning($"Unathorized auth stands feed request from {characterID}");
                        await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuthNotifyFail)
                            .Replace("{message}", LM.Get("authTokenInvalid"))
                            .Replace("{header}", LM.Get("authTokenHeader")).Replace("{body}", LM.Get("authTokenBodyFail")).Replace("{backText}", LM.Get("backText")), response);
                        return true;
                    }

                    var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterID, true);

                    await SQLHelper.DeleteAuthStands(numericCharId);
                    var data = new AuthStandsEntity {CharacterID = numericCharId, Token = result[1]};

                    var token = await APIHelper.ESIAPI.RefreshToken(data.Token, Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret);

                    await RefreshStandings(data, token);
                    await SQLHelper.SaveAuthStands(data);
                    
                    await LogHelper.LogInfo($"Auth stands feed added for character: {characterID}({rChar.name})", LogCat.AuthWeb);
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

        private async Task RefreshStandings(AuthStandsEntity data, string token)
        {
            data.PersonalStands = await APIHelper.ESIAPI.GetCharacterContacts(Reason, data.CharacterID, token);
            data.CorpStands = await APIHelper.ESIAPI.GetCharacterContacts(Reason, data.CharacterID, token);
            data.PersonalStands = await APIHelper.ESIAPI.GetCharacterContacts(Reason, data.CharacterID, token);
        }

        public override async Task Run(object prm)
        {



        }
    }
}
