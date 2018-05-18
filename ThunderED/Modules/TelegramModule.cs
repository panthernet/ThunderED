using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using ThunderED.Classes;
using ThunderED.Classes.Telegram;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public class TelegramModule: AppModuleBase, IDiscordRelayModule
    {
        public override LogCat Category => LogCat.Telegram;
        private User _me;
        private TelegramBotClient _client;
        private TelegramSettings Settings { get; set; }
        private readonly List<string> _messagePool = new List<string>();
        public event Action<string, ulong> RelayMessage;

        public override async Task Run(object prm)
        {
            if (IsRunning || _me != null) return;
            IsRunning = true;
            try
            {
                Settings = TelegramSettings.Load(SettingsManager.FileSettingsPath);
                if (Settings.TelegramModule == null || string.IsNullOrEmpty(Settings.TelegramModule.Token))
                {
                    await LogHelper.LogError("Token is not set for Telegram module!", Category);
                    return;
                }
                if (Settings.TelegramModule.RelayChannels.Count == 0 || Settings.TelegramModule.RelayChannels.All(a=> a.Telegram == 0)
                                                                     || Settings.TelegramModule.RelayChannels.All(a=> a.Discord == 0))
                {
                    await LogHelper.LogError("No relay channels set for Telegram module!", Category);
                    return;
                }
                _client = new TelegramBotClient(Settings.TelegramModule.Token);
                _client.OnMessage += BotClient_OnMessage;
                if (!await _client.TestApiAsync())
                {
                    await LogHelper.LogError("API ERROR!", Category);
                    return;
                }
                _client.OnReceiveError += _client_OnReceiveError;
                _client.OnReceiveGeneralError += _client_OnReceiveGeneralError;
                _me = await _client.GetMeAsync();
                _client.StartReceiving();
                await LogHelper.LogInfo("Telegram bot connected!", Category);

            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private void _client_OnReceiveGeneralError(object sender, Telegram.Bot.Args.ReceiveGeneralErrorEventArgs e)
        {
            LogHelper.LogEx($"General Error: {e.Exception.Message}", e.Exception, Category).ConfigureAwait(false);
        }

        private void _client_OnReceiveError(object sender, Telegram.Bot.Args.ReceiveErrorEventArgs e)
        {
            LogHelper.LogEx($"API Error: {e.ApiRequestException.Message}", e.ApiRequestException, Category).ConfigureAwait(false);

        }

        private void BotClient_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            if(e.Message.Type != MessageType.Text || e.Message.Chat.Type == ChatType.Private) return;

            var relay = Settings.TelegramModule.RelayChannels.FirstOrDefault(a=> a.Telegram == e.Message.Chat.Id);
            if (relay == null) return;
            if(relay.Discord == 0 || IsMessagePooled(e.Message.Text) || relay.TelegramFilters.Any(e.Message.Text.Contains) || relay.TelegramFiltersStartsWith.Any(e.Message.Text.StartsWith)) return;

            var from = string.IsNullOrEmpty(e.Message.From.Username) ? $"{e.Message.From.FirstName} {e.Message.From.LastName}" : e.Message.From.Username;
            if(relay.TelegramUsers.Count > 0 && !relay.TelegramUsers.Contains(from)) return;

            var msg = $"[TM][{from}]: {e.Message.Text}";
            UpdatePool(msg);
            RelayMessage?.Invoke(msg, relay.Discord);
        }

        public async Task SendMessage(ulong channel, ulong authorId, string user, string message)
        {
            if(_me == null) return;
            var relay = Settings.TelegramModule.RelayChannels.FirstOrDefault(a => a.Discord == channel);
            if(relay == null) return;
            if(relay.Telegram == 0 || IsMessagePooled(message) || relay.DiscordFilters.Any(message.Contains) || relay.DiscordFiltersStartsWith.Any(message.StartsWith)) return;
            //check if we relay only bot messages
            if (relay.RelayFromDiscordBotOnly)
            {
                var u = APIHelper.DiscordAPI.Client.GetUser(authorId);
                if(APIHelper.DiscordAPI.Client.CurrentUser.Id != u.Id) return;
            }

            var msg = $"[DISCORD][{user}]: {message}";
            UpdatePool(msg);
            await _client.SendTextMessageAsync(relay.Telegram, msg);
        }

        #region Pooling
        private bool IsMessagePooled(string message)
        {
            return !string.IsNullOrEmpty(_messagePool.FirstOrDefault(a => a == message));
        }

        private void UpdatePool(string message)
        {
            _messagePool.Add(message);
            if(_messagePool.Count > 10)
                _messagePool.RemoveAt(0);
        }
        #endregion
    }
}
