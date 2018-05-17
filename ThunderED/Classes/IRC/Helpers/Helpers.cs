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
using System.IO;
using System.Threading.Tasks;

namespace ThunderED.Classes.IRC.Helpers
{
    public static class Helpers
    {
        private static readonly object randomLock = new object();
        private static readonly Random random = new Random();

        public static int Random(int max)
        {
            lock (randomLock)
            {
                return random.Next(max + 1);
            }
        }

        public static int Random(int min, int max)
        {
            lock (randomLock)
            {
                return random.Next(min, max + 1);
            }
        }

        public static DateTime UnixToDateTime(long unix)
        {
            long timeInTicks = unix * TimeSpan.TicksPerSecond;
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddTicks(timeInTicks);
        }

        public static void OpenURL(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                Task.Run(() =>
                {
                    try
                    {
                        Process.Start(url);

                        Console.WriteLine("URL opened: " + url);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("OpenURL failed: {0}{1}{2}", url, Environment.NewLine, e);
                    }
                });
            }
        }

        public static void CreateDirectoryFromDirectoryPath(string path)
        {
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        public static void CreateDirectoryFromFilePath(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                CreateDirectoryFromDirectoryPath(Path.GetDirectoryName(path));
            }
        }

        public static List<string> SplitCommand(string command, int partCount)
        {
            List<string> commands = new List<string>();

            if (!string.IsNullOrEmpty(command))
            {
                if (partCount <= 1)
                {
                    commands.Add(command);
                }
                else
                {
                    for (int i = 1; i < partCount; i++)
                    {
                        command = command.TrimStart(' ');

                        if (command.Length > 0)
                        {
                            int index = command.IndexOf(' ');

                            if (index > 0 && index < command.Length - 1)
                            {
                                commands.Add(command.Remove(index));
                                command = command.Substring(index + 1);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }

                    commands.Add(command);
                }
            }

            if (commands.Count == partCount)
            {
                return commands;
            }

            return null;
        }
    }
}