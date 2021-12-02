using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.IdentityModel.JsonWebTokens;

using ThunderED.API;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public class AuthCheckModule: AppModuleBase
    {
        private DateTime _lastAuthCheck = DateTime.MinValue;
        private DateTime _lastTokensCheck = DateTime.MinValue;
        private DateTime _lastDiscordAuthCheck = DateTime.MinValue;
        public override LogCat Category => LogCat.AuthCheck;

        public override async Task Initialize()
        {
            await LogHelper.LogModule("Initializing Auth Check module...", Category);
        }

        public override async Task Run(object prm)
        {
            await RunTokensCheck().ConfigureAwait(false);
            if(IsRunning) return;
            try
            {
                IsRunning = true;

                if (TickManager.IsNoConnection || TickManager.IsESIUnreachable) return;

                if (Settings.Config.ModuleAuthWeb && TickManager.GetModule<WebAuthModule>().IsEntityInitFailed) return;
                if (!Settings.Config.ModuleAuthCheck) return;

                var manual = (bool?)prm ?? false;

                if (Settings.WebAuthModule.AuthCheckUnregisteredDiscordUsers && APIHelper.IsDiscordAvailable)
                    await CheckDiscordUsers(manual);
                await CheckDBUsers(manual);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private volatile bool _tokensCheckRunning;

        private async Task RunTokensCheck()
        {
            if(_tokensCheckRunning) return;
            const bool logConsole = true;
            const bool logFile = true;

            if (DateTime.Now >= _lastTokensCheck)
            {
                _lastTokensCheck = DateTime.Now.AddMinutes(5);

                try
                {
                    _tokensCheckRunning = true;

                    await LogHelper.LogInfo($"Starting token update...", LogCat.TokenUpdate, logConsole, logFile);

                    var tokens = await DbHelper.GetTokensWithoutScopes();
                    if(tokens.Count == 0)
                        return;

                    await LogHelper.LogInfo($"Tokens for update: {tokens.Count}", LogCat.TokenUpdate, logConsole,
                        logFile);
                    foreach (var token in tokens)
                    {
                        await LogHelper.LogInfo($"Tokens {token.CharacterId}", LogCat.TokenUpdate, logConsole, logFile);

                        var result = await APIHelper.ESIAPI.GetAccessToken(token);
                        await LogHelper.LogInfo(
                            $"Result: {result?.Data?.ErrorCode} NoCon: {result?.Data?.IsNoConnection} Msg:{result?.Data?.Message}",
                            LogCat.TokenUpdate, logConsole, logFile);

                        if (result?.Data == null || result.Data.IsNoConnection)
                        {
                            await LogHelper.LogInfo($"Failed badly or no connection", LogCat.TokenUpdate, logConsole,
                                logFile);
                            continue;
                        }

                        if (result.Data.IsFailed)
                        {
                            await LogHelper.LogInfo($"Failed with {result.Data.ErrorCode} {result.Data.Message}",
                                LogCat.TokenUpdate, logConsole, logFile);
                            continue;
                        }

                        await LogHelper.LogInfo($"Passed", LogCat.TokenUpdate, logConsole, logFile);

                        token.Scopes = APIHelper.ESIAPI.GetScopesFromToken(result.Result);
                        await DbHelper.UpdateToken(token.Token, token.CharacterId, token.Type, token.Scopes);
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(ex, Category);
                }
                finally
                {
                    _tokensCheckRunning = false;
                }
            }
        }

        [Flags]
        public enum TokenRoles
        {
            Invalid = 0,
            Notifications = 1,
        }

        private async Task CheckDBUsers(bool manual)
        {
            //Check inactive users are correct
            if (DateTime.Now > _lastAuthCheck.AddMinutes(2) || manual)
            {
                _lastAuthCheck = DateTime.Now;

                await LogHelper.LogModule("Running DB users auth check...", Category);
                if (manual)
                {
                    await DbHelper.ResetAuthUsersLastCheck();
                }
                await WebAuthModule.UpdateAllUserRoles(Settings.WebAuthModule.ExemptDiscordRoles.ToList(), Settings.WebAuthModule.AuthCheckIgnoreRoles, manual);
                await LogHelper.LogModule("DB users auth check complete!", Category);
            }

        }

        private async Task CheckDiscordUsers(bool manual)
        {
            if (DateTime.Now > _lastDiscordAuthCheck.AddMinutes(5) || manual)
            {
                _lastDiscordAuthCheck = DateTime.Now;
                await LogHelper.LogModule("Running Discord users auth check...", Category);
                await WebAuthModule.UpdateAuthUserRolesFromDiscord(Settings.WebAuthModule.ExemptDiscordRoles.ToList(), Settings.WebAuthModule.AuthCheckIgnoreRoles, true);
            }
        }
    }
}
