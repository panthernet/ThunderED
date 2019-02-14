using System;
using System.Diagnostics;
using System.Threading;
using ThunderED.API;
using ThunderED.Classes;

namespace ThunderED.Tests.Fixtures
{
    public class DiscordFixture: IDisposable
    {
        private const string TEST_CONFIG_FILE = @"D:\Projects\EVE\ThunderED\ThunderED_git\ThunderED\bin\Debug\netcoreapp2.1\settings.json";
        public DiscordAPI API;

        public DiscordFixture()
        {
            SettingsManager.Prepare(TEST_CONFIG_FILE);
            API = new DiscordAPI();
            API.Start().GetAwaiter().GetResult();
            while (!API.IsAvailable)
            {
                Thread.Sleep(100);
            }
            Debug.WriteLine("DiscordFixture ONLINE!");
        }

        public void Dispose()
        {
            API.Stop();
        }
    }
}