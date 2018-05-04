using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json.FleetUp;

namespace ThunderED.API
{
    public class FleetUpAPI
    {
        public async Task<JsonFleetup.Opperations> GetOperations(string reason, string userId, string apiCode, string appKey, string groupID)
        {
            return await RequestWrapper<JsonFleetup.Opperations>($"http://api.fleet-up.com/Api.svc/{appKey}/{userId}/{apiCode}/Operations/{groupID}", reason);
        }

        private async Task<string> RequestWrapperString(string request, string reason)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var responceMessage = await httpClient.GetAsync(request);
                    var data = await responceMessage.Content.ReadAsStringAsync();
                    if (!responceMessage.IsSuccessStatusCode || data == null)
                    {
                        await LogHelper.LogError($"[{reason}] Potential {responceMessage.StatusCode} FleetUp Failure: {request}");
                        return null;
                    }

                    return data;
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(request, ex, LogCat.ESI);
                return null;
            }
        }

        private async Task<T> RequestWrapper<T>(string request, string reason, string auth = null)
            where T: class
        {
            string raw = null;
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Clear();
                    httpClient.DefaultRequestHeaders.Add("User-Agent", SettingsManager.DefaultUserAgent);
                    if(!string.IsNullOrEmpty(auth))
                        httpClient.DefaultRequestHeaders.Add("Authorization", auth);
                
                    var responceMessage = await httpClient.GetAsync(request);
                    raw = await responceMessage.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<T>(raw);
                    if (!responceMessage.IsSuccessStatusCode || data == null)
                    {
                        if(responceMessage.StatusCode != HttpStatusCode.NotFound && responceMessage.StatusCode != HttpStatusCode.Forbidden)
                            await LogHelper.LogError($"[{reason}] Potential {responceMessage.StatusCode} ESI Failure: {request}", LogCat.ESI, false);
                        return null;
                    }
                    return data;                
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(request, ex, LogCat.ESI);
                await LogHelper.LogInfo($"RESPONCE: {raw}", LogCat.ESI);
                return null;
            }
        }
    }
}
