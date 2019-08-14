using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using TED_ChatRelay.Classes;

namespace ThunderED
{
    internal partial class Program
    {
        private static RelaySetttings _settings;
        private static Timer _timer;

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine($"TED Chat Relay v{VERSION} for EVE Online is starting...");
                try
                {
                    _settings = RelaySetttings.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "relaysettings.json"));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error loading config file! Make sure it has correct syntax.");
                    Console.WriteLine(ex.ToString());
                    Console.WriteLine(ex.InnerException?.ToString());
                    Console.ReadKey();
                    return;
                }

                if (!Directory.Exists(_settings.EveLogsFolder))
                {
                    Console.WriteLine("ERROR: EVE chatlogs folder not found!");
                    Console.ReadKey();
                    return;
                }


                Console.WriteLine("OK! Configured relays:");
                _settings.RelayChannels.ForEach(a=> Console.WriteLine($"  * {a.EveChannelName}"));

                _timer = new Timer(Tick, new AutoResetEvent(true), 100, 1000);
                while (true)
                {
                    var command = Console.ReadLine();
                    switch (command?.Split(" ")[0])
                    {
                        case "quit":
                            return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.ReadKey();
            }
        }

        private static volatile bool _isRunning;

        private static readonly object Locker = new object();

        private static void Tick(object state)
        {
            if(_isRunning) return;
            _isRunning = true;
            try
            {
                Parallel.ForEach(_settings.RelayChannels, relay =>
                {
                    var file = Directory
                        .EnumerateFiles(_settings.EveLogsFolder, $"{relay.EveChannelName}*",
                            SearchOption.TopDirectoryOnly).Aggregate((agg, next) =>
                            Directory.GetLastWriteTime(next) > Directory.GetLastWriteTime(agg) ? next : agg);
                    if (string.IsNullOrEmpty(file)) return;

                    if (relay.Pool.Count == 0)
                    {
                        using (var reader = new ReverseTextReader(file, Encoding.Unicode))
                        {
                            relay.Pool.AddRange(reader.Take(5));
                        }
                        return;
                    }

                    using (var reader = new ReverseTextReader(file, Encoding.Unicode))
                    {
                        foreach( var line in reader.Take(5))
                        {
                            if (!line.StartsWith('[') || relay.Pool.Contains(line)) continue;

                            

                            var newLine = line;
                            //change time
                            try
                            {
                                var end = line.IndexOf(']', 1);
                                var dt = DateTime.Parse(line.Substring(1, end - 2).Trim());
                                var msg = line.Substring(end + 1, line.Length - end - 1).Trim();
                                //startswith
                                if(relay.RelayStartsWithText.Count > 0 && !relay.RelayStartsWithText.Any(a=> msg.ToLower().StartsWith(a.ToLower()))) continue;
                                if(relay.RelayContainsText.Count > 0 && !relay.RelayContainsText.Any(a=> msg.ToLower().Contains(a.ToLower()))) continue;
                                if(relay.FilterChatContainsText.Any(a=> msg.ToLower().Contains(a.ToLower()))) continue;

                                newLine = $"[{dt.ToString(relay.DateFormat)}] {msg}";
                            }
                            catch
                            {
                                //ignore
                            }

                            Console.WriteLine($"SEND->[{relay.EveChannelName}]: {newLine}");
                            lock (Locker)
                            {
                                relay.Pool.Add(line);
                                if (relay.Pool.Count > 10)
                                    relay.Pool.RemoveAt(0);
                            }

                            if (!SendMessage(relay, newLine).GetAwaiter().GetResult())
                            {
                                Console.WriteLine("");
                            }
                        }
                    }
                });
            }
            finally
            {
                _isRunning = false;
            }
        }

        private static async Task<bool> SendMessage(RelayChannel relay, string line)
        {
            try
            {
                var handler = new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    
                };
                using (var httpClient = new HttpClient(handler))
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
                    httpClient.DefaultRequestHeaders.Clear();
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "TED_ChatRelay");
                    httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip");

                    var responceMessage =
                        await httpClient.PostAsync($"{relay.Endpoint}?msg={EncodeParam(line)}&code={EncodeCode(relay.Code)}&ch={EncodeParam(relay.EveChannelName)}", null);
                    var r = await responceMessage.Content.ReadAsStringAsync();
                    if (!responceMessage.IsSuccessStatusCode)
                    {
                        //if(responceMessage.StatusCode == )
                        Console.WriteLine("ERROR: Bad client request!");
                        return false;
                    }

                    if (r != "OK" && r != "DUPE")
                    {
                        if(r.StartsWith("ERROR"))
                            Console.WriteLine($"REPONCE -> {r}");
                        else Console.WriteLine("ERROR: Server not configured!");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                return false;
            }
        }

        private static string EncodeParam(string value)
        {
            return HttpUtility.UrlEncode(value);
        }

        private static string EncodeCode(string code)
        {
            return HttpUtility.UrlEncode(Convert.ToBase64String(Encoding.UTF8.GetBytes(code.Replace('+', '-').Replace('/', '_'))));
        }
    }
}
