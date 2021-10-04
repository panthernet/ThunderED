using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ProtectedBrowserStorage;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Helpers;
using ThunderED.Modules;
using ThunderED.Thd;

namespace THDWebServer.Authentication
{
    public class CustomAuthenticationStateProvider : AuthenticationStateProvider
    {
        public const string ROLE_WEB_SETTINGS = "web_settings";
        public const string ROLE_TIMERS = "timers";
        public const string ROLE_HRM = "hrm";
        public const string ROLE_FEED_AUTH = "feed_auth";
        public const string ROLE_MINING_SCHEDULE = "ms";
        public const string ROLE_STRUCTURES = "struct";
        public const string ROLE_MOON_TABLE = "moon_table";

        public ProtectedSessionStorage ProtectedSessionStore { get; set; }

        public static async Task<bool> HasAuth(AuthenticationStateProvider provider)
        {
            var u = (await ((CustomAuthenticationStateProvider)provider).GetAuthenticationStateAsync())?.User;
            return u?.Identity != null;
        }

        public CustomAuthenticationStateProvider(ProtectedSessionStorage storage)
        {
            ProtectedSessionStore = storage;
        }

        private static readonly ConcurrentDictionary<long, AuthState> Auth = new ConcurrentDictionary<long, AuthState>();

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var usr = await ProtectedSessionStore.GetAsync<WebAuthUserData>("user");

#if NOREG
            usr ??= new WebAuthUserData(new AuthUserEntity {CharacterId = 1731524545}, "1");
            usr.Name = "Ves Na";
            Auth.TryAdd(usr.Id, new AuthState());
#endif

            if (usr == null || usr.Id == 0 || string.IsNullOrEmpty(usr.Code) || !Auth.ContainsKey(usr.Id) || Auth[usr.Id].IsExpired(30))
            {
                return new AuthenticationState(new ClaimsPrincipal());
            }

#if !NOREG
            Auth[usr.Id].Prolong();
#endif

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, usr.Name, ClaimValueTypes.String),
                new Claim(ClaimTypes.NameIdentifier, usr.Id.ToString(), ClaimValueTypes.Integer64),
                new Claim(ClaimTypes.Authentication, "true", ClaimValueTypes.Boolean),
            };

            if (WebSettingsModule.HasWebAccess(usr.Id))
            {
                claims.Add(new Claim(ClaimTypes.Role, ROLE_WEB_SETTINGS));
            }
            if (HRMModule.HasWebAccess(usr.Id))
            {
                claims.Add(new Claim(ClaimTypes.Role, ROLE_HRM));
            }
            if (TimersModule.HasWebAccess(usr.Id, usr.CorpId, usr.AllianceId))
            {
                claims.Add(new Claim(ClaimTypes.Role, ROLE_TIMERS));
            }
            if (MiningScheduleModule.HasViewAccess(usr))
            {
                claims.Add(new Claim(ClaimTypes.Role, ROLE_MINING_SCHEDULE));
            }

            if (StructureManagementModule.HasViewAccess(usr) || StructureManagementModule.HasManageAccess(usr, out _))
            {
                claims.Add(new Claim(ClaimTypes.Role, ROLE_STRUCTURES));
            }

            if (MoonInfoModule.HasAccess(usr))
            {
                claims.Add(new Claim(ClaimTypes.Role, ROLE_MOON_TABLE));
            }

            if (ContractNotificationsModule.HasAuthAccess(usr.Id) || IndustrialJobsModule.HasAuthAccess(usr.Id) ||
                MailModule.HasAuthAccess(usr.Id) || NotificationModule.HasAuthAccess(usr.Id) || WebAuthModule.HasAuthAccess(usr.Id)
                || MiningScheduleModule.HasAuthAccess(usr))
            {
                claims.Add(new Claim(ClaimTypes.Role, ROLE_FEED_AUTH));
            }


            var identity = new ClaimsIdentity(claims, "General Auth");
            var user = new ClaimsPrincipal(identity);

            return new AuthenticationState(user);
        }

        public async Task SaveAuth(ThdAuthUser user)
        {
            var auth = new AuthState();
            Auth.AddOrUpdate(user.CharacterId,auth);
            await ProtectedSessionStore.SetAsync("user", new WebAuthUserData(user, auth.Code));
        }

        private class AuthState
        {
            public readonly string Code;
            private DateTime _time;

            public AuthState()
            {
                Code = Guid.NewGuid().ToString("N");
                Prolong();
            }

            public void Prolong()
            {
                _time = DateTime.Now;
            }

            public bool IsExpired(int timeInMinutes)
            {
                return (DateTime.Now - _time).TotalMinutes > timeInMinutes;
            }
        }

        public async Task Logout()
        {
            var id = (await ProtectedSessionStore.GetAsync<WebAuthUserData>("user"))?.Id ?? 0;
            if (Auth.ContainsKey(id))
            {
                Auth.Remove(id);
            }
        }
    }

}
