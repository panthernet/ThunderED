using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Matrix.Xmpp.Chatstates;
using Matrix.Xmpp.Client;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    internal class JabberModule: AppModuleBase
    {
        public override LogCat Category => LogCat.Jabber;

        public override async Task Run(object prm)
        {
            var username = Settings.JabberModule.Username;
            var password = Settings.JabberModule.Password;
            var domain = Settings.JabberModule.Domain;

            if (!IsRunning)
            {
                try
                {
                    var xmppWrapper = new ReconnectXmppWrapper(domain, username, password);
                    xmppWrapper.Connect(null);
                    IsRunning = true;
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(ex.Message, ex, Category);
                    IsRunning = false;
                }
            }

        }
    }

    internal class ReconnectXmppWrapper
    {
        private readonly XmppClient _xmppClient;
        private bool _onLogin;
        private readonly Timer _connectTimer;

        public ReconnectXmppWrapper(string xmppdomain, string username, string password)
        {
            try
            {
                _xmppClient = new XmppClient
                {
                    XmppDomain = xmppdomain,
                    Username = username,
                    Password = password
                };

                _xmppClient.OnMessage += OnMessage;
                _xmppClient.OnClose += OnClose;
                _xmppClient.OnBind += OnBind;
                _xmppClient.OnBindError += XmppClientOnOnBindError;
                _xmppClient.OnAuthError += OnAuthError;
                _xmppClient.OnError += OnError;
                _xmppClient.OnStreamError += OnStreamError;
                _xmppClient.OnXmlError += OnXmlError;
                _xmppClient.OnLogin += OnLogin;
                _xmppClient.OnReceiveXml += XmppClient_OnReceiveXml;
                _xmppClient.OnSendXml += XmppClient_OnSendXml;

                _connectTimer = new Timer(Connect, null, 5000, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        internal static async void OnMessage(object sender, MessageEventArgs e)
        {
            var filtered = false;
            if (e.Message.Chatstate != Chatstate.Composing && !string.IsNullOrWhiteSpace(e.Message.Value))
            {
                if (SettingsManager.Settings.JabberModule.Filter)
                {
                    foreach (var filter in SettingsManager.Settings.JabberModule.Filters)
                    {
                        if (e.Message.Value.ToLower().Contains(filter.Key.ToLower()))
                        {
                            var prepend = SettingsManager.Settings.JabberModule.Prepend;
                            var channel = APIHelper.DiscordAPI.Client.GetGuild(SettingsManager.Settings.Config.DiscordGuildId).GetTextChannel(Convert.ToUInt64(filter.Value));
                            filtered = true;
                            await APIHelper.DiscordAPI.SendMessageAsync(channel, $"{prepend + Environment.NewLine}{LM.Get("From")}: {e.Message.From.User} {Environment.NewLine} {LM.Get("Message")}: ```{e.Message.Value}```").ConfigureAwait(false);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(e.Message.Value) && !filtered)
                {
                    var prepend = SettingsManager.Settings.JabberModule.Prepend;
                    var channel = APIHelper.DiscordAPI.Client.GetGuild(SettingsManager.Settings.Config.DiscordGuildId).GetTextChannel(SettingsManager.Settings.JabberModule.DefChan);
                    await APIHelper.DiscordAPI.SendMessageAsync(channel, $"{prepend + Environment.NewLine}{LM.Get("From")}: {e.Message.From.User} {Environment.NewLine} {LM.Get("Message")}: ```{e.Message.Value}```").ConfigureAwait(false);
                }
            }
        }

        private void XmppClient_OnSendXml(object sender, Matrix.TextEventArgs e)
        {
            LogHelper.LogDebug($"JabberClient Sent {e.Text}", LogCat.Jabber).GetAwaiter().GetResult();
        }

        private void XmppClient_OnReceiveXml(object sender, Matrix.TextEventArgs e)
        {
            if (SettingsManager.Settings.JabberModule.Debug)
                LogHelper.LogDebug($"JabberClient Rcv {e.Text}", LogCat.Jabber).GetAwaiter().GetResult();
        }


        #region xmpp error handlers

        private void XmppClientOnOnBindError(object sender, IqEventArgs iqEventArgs)
        {
            Console.WriteLine("OnBindError");
            _xmppClient.Close();
        }

        private static void OnStreamError(object sender, Matrix.StreamErrorEventArgs e)
        {
            Console.WriteLine("OnStreamError: error condition {0}", e.Error.Condition.ToString());
        }

        private static void OnXmlError(object sender, Matrix.ExceptionEventArgs e)
        {
            Console.WriteLine("OnXmlError");
            Console.WriteLine(e.Exception.Message);
            Console.WriteLine(e.Exception.StackTrace);
        }

        private static void OnAuthError(object sender, Matrix.Xmpp.Sasl.SaslEventArgs e)
        {
            Console.WriteLine("OnAuthError");
        }

        private void OnError(object sender, Matrix.ExceptionEventArgs e)
        {
            var msg = e != null ? (e.Exception != null ? e.Exception.Message : "") : "";
            Console.WriteLine("OnError: " + msg);

            if (!_onLogin)
                StartConnectTimer();
        }
        #endregion

        #region << XMPP handlers >>

        private void OnLogin(object sender, Matrix.EventArgs e)
        {
            Console.WriteLine("OnLogin");
            _onLogin = true;
        }

        private void OnBind(object sender, Matrix.JidEventArgs e)
        {
            Console.WriteLine("OnBind: XMPP connected. JID: " + e.Jid);
        }

        private void OnClose(object sender, Matrix.EventArgs e)
        {
            Console.WriteLine("OnClose: XMPP connection closed");
            StartConnectTimer();
        }
        #endregion


        private void StartConnectTimer()
        {
            Console.WriteLine("starting reconnect timer...");
            _connectTimer.Change(5000, Timeout.Infinite);
        }

        public void Connect(object obj)
        {
            if (!_xmppClient.StreamActive)
            {
                Console.WriteLine("StreamActive=" + _xmppClient.StreamActive);
                Console.WriteLine("connect: XMPP connecting.... ");
                _onLogin = false;
                _xmppClient.Open();
            }
        }
    }
}
