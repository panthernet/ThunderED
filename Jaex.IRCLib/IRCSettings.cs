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
using System.ComponentModel;

namespace Jaex.IRCLib
{
    public class IRCSettingsInternal
    {
        [Category("\t\tServer info"), Description("IRC server address. Example: chat.freenode.net"), DefaultValue("chat.freenode.net")]
        public string Server { get; set; } = "chat.freenode.net";

        [Category("\t\tServer info"), Description("IRC server port. Default: 6667. Default SSL: 6697"), DefaultValue(IRC.DEFAULT_PORT)]
        public int Port { get; set; } = IRC.DEFAULT_PORT;

        [Category("\t\tServer info"), Description("Will use SSL connection. You must use correct SSL port of the IRC server."), DefaultValue(false)]
        public bool UseSSL { get; set; } = false;

        [Category("\t\tServer info"), Description("IRC server password. Can be used to identify in some servers."), PasswordPropertyText(true)]
        public string Password { get; set; }

        [Category("\tUser info"), Description("Nickname."), DefaultValue("UserXXX")]
        public string Nickname { get; set; }

        [Category("\tUser info"), Description("Alternative nickname in case nickname is already in use. If it is empty then _ character will be added to the end of nickname.")]
        public string Nickname2 { get; set; }

        [Category("\tUser info"), Description("Username. This info is visible to everyone in the WHOIS result."), DefaultValue("username")]
        public string Username { get; set; } 

        [Category("\tUser info"), Description("Realname. This info is visible to everyone in WHOIS result."), DefaultValue("realname")]
        public string Realname { get; set; } = "realname";

        [Category("\tUser info"), Description("IRC invisible mode."), DefaultValue(true)]
        public bool Invisible { get; set; } = true;

        [Category("Options"), Description("When disconnected from server auto reconnect."), DefaultValue(true)]
        public bool AutoReconnect { get; set; } = true;

        [Category("Options"), Description("Wait specific milliseconds before reconnecting."), DefaultValue(5000)]
        public int AutoReconnectDelay { get; set; } = 5000;

        [Category("Options"), Description("Auto rejoin when got kicked out from the channel."), DefaultValue(false)]
        public bool AutoRejoinOnKick { get; set; }

        [Category("Options"), Description("Message to show others when you disconnect from the server."), DefaultValue("Leaving")]
        public string QuitReason { get; set; } = "Leaving";

        [Category("Options"), Description("Don't show 'Message of the day' texts in output."), DefaultValue(false)]
        public bool SuppressMOTD { get; set; } = false;

        [Category("Options"), Description("Don't show 'PING' and 'PONG' texts in output."), DefaultValue(false)]
        public bool SuppressPing { get; set; } = false;

        [Category("Options"), Description("When connected these commands will automatically execute.")]
        public List<string> ConnectCommands { get; set; } = new List<string>();

        [Category("Options"), Description("When connected automatically join these channels.")]
        public List<IRCRelayItem> RelayChannels { get; set; } = new List<IRCRelayItem>();

        [Category("Options"), Description("Wait for identify confirmation before auto join channels. Currently only works in Freenode server because each server sends different response after identify."), DefaultValue(false)]
        public bool AutoJoinWaitIdentify { get; set; }

        [Category("Options"), Description("Enable/Disable auto response system which using AutoResponseList."), DefaultValue(false)]
        public bool AutoResponse { get; set; }

        [Category("Options"), Description("When specific message written in channel automatically respond with your message.")]
        public List<AutoResponseInfo> AutoResponseList { get; set; } = new List<AutoResponseInfo>();

        [Category("Options"), Description("After successful auto response match how many milliseconds to wait for next auto response. Delay independant per response."), DefaultValue(10000)]
        public int AutoResponseDelay { get; set; } = 10000;

        public string GetAutoResponses()
        {
            List<string> messages = new List<string>();

            foreach (AutoResponseInfo autoResponseInfo in AutoResponseList)
            {
                if (autoResponseInfo.Messages.Count > 1)
                {
                    messages.Add($"[{string.Join(", ", autoResponseInfo.Messages)}]");
                }
                else if (autoResponseInfo.Messages.Count == 1)
                {
                    messages.Add(autoResponseInfo.Messages[0]);
                }
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

        public IRCSettings()
        {
           // Save("settigs.json");
        }
    }
}