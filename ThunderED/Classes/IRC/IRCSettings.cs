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

using System.Collections.Generic;
namespace ThunderED.Classes.IRC
{
    public class IRCSettingsInternal
    {
        public string Server { get; set; } = "chat.freenode.net";

        public int Port { get; set; } = IRC.DEFAULT_PORT;

        public bool UseSSL { get; set; } = false;

        public string Password { get; set; }

        public string Nickname { get; set; } = "DefaultUser-TH";

        public string Nickname2 { get; set; }

        public string Username { get; set; } = "username";

        public string Realname { get; set; } = "realname";

        public bool Invisible { get; set; } = true;

        public bool AutoReconnect { get; set; } = true;

        public int AutoReconnectDelay { get; set; } = 5000;

        public bool AutoRejoinOnKick { get; set; }

        public string QuitReason { get; set; } = "Leaving";

        public bool SuppressMOTD { get; set; } = false;

        public bool SuppressPing { get; set; } = false;

        public List<string> ConnectCommands { get; set; } = new List<string>();

        public List<IRCRelayItem> RelayChannels { get; set; } = new List<IRCRelayItem>();

        public bool AutoJoinWaitIdentify { get; set; }

        public bool AutoResponse { get; set; }

        public List<AutoResponseInfo> AutoResponseList { get; set; } = new List<AutoResponseInfo>();

        public int AutoResponseDelay { get; set; } = 10000;

        public string GetAutoResponses()
        {
            var messages = new List<string>();

            foreach (var autoResponseInfo in AutoResponseList)
            {
                if (autoResponseInfo.Messages.Count > 1)
                    messages.Add($"[{string.Join(", ", autoResponseInfo.Messages)}]");
                else if (autoResponseInfo.Messages.Count == 1)
                    messages.Add(autoResponseInfo.Messages[0]);
            }

            return string.Join(", ", messages);
        }
    }

    public class IRCRelayItem
    {
        public string IRC { get; set; }
        public ulong Discord { get; set; }
        public List<string> DiscordFilters { get; set; } = new List<string>();
        public List<string> IRCFilters { get; set; } = new List<string>();
    }

    public class IRCSettings : SettingsBase<IRCSettings>
    {
        public IRCSettingsInternal ircModule;
    }
}