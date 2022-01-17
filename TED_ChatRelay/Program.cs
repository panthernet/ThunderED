using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Web;
using TED_ChatRelay.Classes;
using ThunderED.Classes;

using Timer = System.Timers.Timer;

namespace ThunderED
{
    internal partial class Program
    {
        private static RelaySettings _settings;
        private static Timer _timer;
        private static Timer _sendTimer;

        static void Main(string[] args)
        {
            try
            {
                AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
                {
                    Console.WriteLine(eventArgs.ExceptionObject.ToString());
                };

                Console.WriteLine($"TED Chat Relay v{VERSION} for EVE Online is starting...");
                try
                {
                    _settings = RelaySettings.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "relaysettings.json"));
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
                if (!_settings.RelayChannels.Any())
                {
                    Console.WriteLine("ERROR: RelayChannels not set. Please add one!");
                    Console.ReadKey();
                    return;
                }
                _settings.RelayChannels.ForEach(a=> Console.WriteLine($"  * {a.EveChannelName}"));

                _timer = new Timer(1000);
                _timer.Elapsed += Tick;
                _sendTimer = new Timer(_settings.SendInterval);
                _sendTimer.Elapsed += SendTick;
                _timer.Start();
                _sendTimer.Start();
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

        private static void Tick(object sender, ElapsedEventArgs args)
        {
            if(_isRunning) return;
            _isRunning = true;
            try
            {
                Parallel.ForEach(_settings.RelayChannels, relay =>
                {
                    try
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
                            foreach (var line in reader.Take(5))
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
                                    if (relay.RelayStartsWithText.Count > 0 && !relay.RelayStartsWithText.Any(a => msg.ToLower().StartsWith(a.ToLower()))) continue;
                                    if (relay.RelayContainsText.Count > 0 && !relay.RelayContainsText.Any(a => msg.ToLower().Contains(a.ToLower()))) continue;
                                    if (relay.FilterChatContainsText.Any(a => msg.ToLower().Contains(a.ToLower()))) continue;

                                    newLine = $"[{dt.ToString(relay.DateFormat)}] {msg}";
                                }
                                catch
                                {
                                    //ignore
                                }

                                lock (Locker)
                                {
                                    relay.Pool.Add(line);
                                    if (relay.Pool.Count > 10)
                                        relay.Pool.RemoveAt(0);
                                }

                                SendMessage(relay, newLine);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERROR: {ex.Message}");
                    }
                });
            }
            finally
            {
                _isRunning = false;
            }
        }

        private static void SendMessage(RelayChannel relay, string message)
        {
            try
            {
                Console.WriteLine($"ENQUEUE->[{relay.EveChannelName}]: {message}");

                if(message.Length > MAX_MESSAGE_LENGTH)
                    foreach (var line in message.SplitToLines(MAX_MESSAGE_LENGTH))
                        Package.Enqueue(new Tuple<RelayChannel, string>(relay, line));
                else Package.Enqueue(new Tuple<RelayChannel, string>(relay, message));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }

        private static readonly ConcurrentQueue<Tuple<RelayChannel, string>> Package = new ConcurrentQueue<Tuple<RelayChannel, string>>();
        private const int MAX_MESSAGE_LENGTH = 1999;

        private static async void SendTick(object sender, ElapsedEventArgs args)
        {
            _sendTimer.Stop();
            try
            {
                if (Package.Count > 0)
                {
                    Console.WriteLine($"PACK SEND");
                    var groups = Package.GroupBy(a => a.Item1.EveChannelName).Select(a=> new { Key = a.FirstOrDefault().Item1, Value = a.Select(p =>p.Item2).ToArray()}).ToList();
                    Package.Clear();
                    foreach (var @group in groups)
                    {
                        var message = string.Join(Environment.NewLine, group.Value);
                        if(message.Length > MAX_MESSAGE_LENGTH)
                            foreach (var line in message.SplitToLines(MAX_MESSAGE_LENGTH))
                                await SendWeb(group.Key, line);
                        else await SendWeb(group.Key, message);
                    }
                }
            }
            catch
            {
                //ignore
            }
            finally
            {
                _sendTimer.Start();
            }
        }

        private static async Task SendWeb(RelayChannel relay, string message)
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

                var token = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                using (var responseMessage =
                    await httpClient.PostAsync($"{relay.Endpoint}?msg={EncodeParam(message)}&code={EncodeCode(relay.Code)}&ch={EncodeParam(relay.EveChannelName)}", null, token.Token))
                {
                    var r = await responseMessage.Content.ReadAsStringAsync(token.Token);
                    if (!responseMessage.IsSuccessStatusCode)
                    {
                        //if(responceMessage.StatusCode == )
                        Console.WriteLine("ERROR: Bad client request!");
                    }

                    if (r != "OK" && r != "DUPE")
                    {
                        Console.WriteLine(r.StartsWith("ERROR") ? $"RESPONSE -> {r}" : "ERROR: Server not configured!");
                    }
                }
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
