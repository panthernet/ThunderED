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
            return await APIHelper.RequestWrapper<JsonFleetup.Opperations>($"http://api.fleet-up.com/Api.svc/{appKey}/{userId}/{apiCode}/Operations/{groupID}", reason);
        }

    }
}
