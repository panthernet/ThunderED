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
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace ThunderED.Classes.IRC
{
    public class AutoResponseInfo
    {
        public List<string> Messages { get; set; } = new List<string>();

        public List<string> Responses { get; set; } = new List<string>();

        public IRCAutoResponseType Type { get; set; } = IRCAutoResponseType.Contains;

        private readonly Stopwatch _lastMatchTimer = new Stopwatch();

        public AutoResponseInfo(List<string> message, List<string> response, IRCAutoResponseType type)
        {
            Messages = message;
            Responses = response;
            Type = type;
        }

        public bool IsMatch(string message, string nick, string mynick)
        {
            bool isMatch = Messages.Select(m => m.Replace("$nick", nick).Replace("$mynick", mynick)).Any(x =>
            {
                switch (Type)
                {
                    default:
                    case IRCAutoResponseType.Contains:
                        return Regex.IsMatch(message, $"\\b{Regex.Escape(x)}\\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                    case IRCAutoResponseType.StartsWith:
                        return message.StartsWith(x, StringComparison.InvariantCultureIgnoreCase);
                    case IRCAutoResponseType.ExactMatch:
                        return message.Equals(x, StringComparison.InvariantCultureIgnoreCase);
                }
            });

            if (isMatch)
            {
                _lastMatchTimer.Restart();
            }

            return isMatch;
        }

        public bool CheckLastMatchTimer(int milliseconds)
        {
            return milliseconds <= 0 || !_lastMatchTimer.IsRunning || _lastMatchTimer.Elapsed.TotalMilliseconds > milliseconds;
        }

        public string RandomResponse(string nick, string mynick)
        {
            int index = Helpers.Helpers.Random(Responses.Count - 1);
            return Responses.Select(r => r.Replace("$nick", nick).Replace("$mynick", mynick)).ElementAt(index);
        }

        public override string ToString()
        {
            if (Messages != null && Messages.Count > 0)
            {
                return string.Join(", ", Messages);
            }

            return "...";
        }
    }
}