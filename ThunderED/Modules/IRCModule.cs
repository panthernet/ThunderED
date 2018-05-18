#region License Information (GPL v3)

/*
    Copyright (c) Jaex

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThunderED.Classes.IRC;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public class IRCModule: AppModuleBase, IDisposable, IDiscordRelayModule
    {
        public IRC IRC { get; private set; }
        public IRCSettings Settings { get; private set; }

        public event Action<string, ulong> RelayMessage;

        private readonly List<string> _messagePool = new List<string>();

        private async Task IRC_ErrorOutput(Exception e)
        {
            await LogHelper.LogEx($"IRC error: {e.Message}", e, Category);
        }

        private async Task IRC_UserJoined(UserInfo userInfo, string channel)
        {
            if (userInfo.UserType == IRCUserType.Me)
                await LogHelper.LogInfo($"Joined channel {channel}", Category);
        }
    
        private void IRC_Disconnected()
        {
            Disconnect();
        }

        public override async Task Run(object prm)
        {
            await Connect();
        }

        public async Task Connect()
        {
            if(IsRunning) return;
            IsRunning = true;

            if(IRC?.IsWorking ?? false) return;
            try
            {
                Settings = IRCSettings.Load(AppDomain.CurrentDomain.BaseDirectory + "settings.json");

                IRC = new IRC(Settings);
                IRC.Message += IRC_Message;
                IRC.ErrorOutput += IRC_ErrorOutput;
                IRC.UserJoined += IRC_UserJoined;
                IRC.Disconnected += IRC_Disconnected;
                _messagePool.Clear();
                await IRC.Connect().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx($"IRC connect error: {ex.Message}", ex, Category);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private void IRC_Message(UserInfo userinfo, string channel, string message)
        {
            var relay = Settings.ircModule.RelayChannels.FirstOrDefault(a => a.IRC == channel);
            if (relay == null) return;
            if(relay.Discord == 0 || IsMessagePooled(message) || relay.IRCFilters.Any(message.Contains) || relay.IRCFiltersStartsWith.Any(message.StartsWith)) return;
            if(relay.IRCUsers.Count > 0 && !relay.IRCUsers.Contains(userinfo.Nickname)) return;

            var msg = $"[IRC][{userinfo.Nickname}]: {message}";
            UpdatePool(msg);
            RelayMessage?.Invoke(msg, relay.Discord);
        }

        #region Pooling
        private void UpdatePool(string message)
        {
            _messagePool.Add(message);
            if(_messagePool.Count > 10)
                _messagePool.RemoveAt(0);
        }

        private bool IsMessagePooled(string message)
        {
            return !string.IsNullOrEmpty(_messagePool.FirstOrDefault(a => a == message));
        }
        #endregion

        public async Task SendMessage(ulong channel, ulong authorId, string user, string message)
        {
            if(IRC == null || !IRC.IsWorking) return;
            var relay = Settings.ircModule.RelayChannels.FirstOrDefault(a => a.Discord == channel);
            if(relay == null) return;
            if(string.IsNullOrEmpty(relay.IRC) || IsMessagePooled(message) || relay.DiscordFilters.Any(message.Contains) || relay.DiscordFiltersStartsWith.Any(message.StartsWith)) return;
            //check if we relay only bot messages
            if (relay.RelayFromDiscordBotOnly)
            {
                var u = APIHelper.DiscordAPI.Client.GetUser(authorId);
                if(APIHelper.DiscordAPI.Client.CurrentUser.Id != u.Id) return;
            }

            var msg = $"[DISCORD][{user}]: {message}";
            UpdatePool(msg);
            await IRC.SendMessage(msg, relay.IRC);
        }

        public void Disconnect()
        {
            IRC?.Disconnect();
            IRC?.Dispose();
        }

        public void Dispose()
        {
            IRC?.Dispose();
        }

        public override LogCat Category => LogCat.IRC;
    }
}