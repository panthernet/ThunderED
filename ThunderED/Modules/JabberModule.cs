using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Matrix.Xmpp.Chatstates;
using Matrix.Xmpp.Client;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    internal class JabberModule: AppModuleBase
    {
        public override LogCat Category => LogCat.Jabber;

        public override async Task Initialize()
        {
            await LogHelper.LogModule("Initializing Jabber module...", Category);
        }

        private bool _isJabberRunning;

        public override async Task Run(object prm)
        {
            if (IsRunning) return;
            IsRunning = true;
            try
            {
                if (!_isJabberRunning)
                {
                    var username = Settings.JabberModule.Username;
                    var password = Settings.JabberModule.Password;
                    var domain = Settings.JabberModule.Domain;

                    try
                    {
                        var xmppWrapper = new ReconnectXmppWrapper(domain, username, password);
                        xmppWrapper.Connect(null);
                        _isJabberRunning = true;
                    }
                    catch (Exception ex)
                    {
                        await LogHelper.LogEx(ex.Message, ex, Category);
                        _isJabberRunning = false;
                    }
                }
            }
            finally
            {
                IsRunning = false;
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
            if (!APIHelper.IsDiscordAvailable) return;
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
                            var channelId = Convert.ToUInt64(filter.Value);
                            filtered = true;
                            await APIHelper.DiscordAPI.SendMessageAsync(channelId, $"{prepend + Environment.NewLine}{LM.Get("From")}: {e.Message.From.User} {Environment.NewLine} {LM.Get("Message")}: ```{e.Message.Value}```").ConfigureAwait(false);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(e.Message.Value) && !filtered)
                {
                    var prepend = SettingsManager.Settings.JabberModule.Prepend;
                    if(SettingsManager.Settings.JabberModule.DefChan > 0)
                        await APIHelper.DiscordAPI.SendMessageAsync(SettingsManager.Settings.JabberModule.DefChan, $"{prepend + Environment.NewLine}{LM.Get("From")}: {e.Message.From.User} {Environment.NewLine} {LM.Get("Message")}: ```{e.Message.Value}```").ConfigureAwait(false);
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
            LogHelper.WriteConsole("OnBindError");
            _xmppClient.Close();
        }

        private static void OnStreamError(object sender, Matrix.StreamErrorEventArgs e)
        {
            LogHelper.WriteConsole("OnStreamError: error condition {0}", e.Error.Condition.ToString());
        }

        private static void OnXmlError(object sender, Matrix.ExceptionEventArgs e)
        {
            LogHelper.WriteConsole("OnXmlError");
            LogHelper.WriteConsole(e.Exception.Message);
            LogHelper.WriteConsole(e.Exception.StackTrace);
        }

        private static async void OnAuthError(object sender, Matrix.Xmpp.Sasl.SaslEventArgs e)
        {
            await LogHelper.LogError("Jabber authentication error. See Jabber.log file for details.", LogCat.Jabber);
            await LogHelper.LogError(e.Failure.ToString(), LogCat.Jabber, false);
        }

        private void OnError(object sender, Matrix.ExceptionEventArgs e)
        {
            var msg = e != null ? (e.Exception != null ? e.Exception.Message : "") : "";
            LogHelper.WriteConsole("OnError: " + msg);

            if (!_onLogin)
                StartConnectTimer();
        }
        #endregion

        #region << XMPP handlers >>

        private void OnLogin(object sender, Matrix.EventArgs e)
        {
            LogHelper.WriteConsole("OnLogin");
            _onLogin = true;
        }

        private void OnBind(object sender, Matrix.JidEventArgs e)
        {
            LogHelper.WriteConsole("OnBind: XMPP connected. JID: " + e.Jid);
        }

        private void OnClose(object sender, Matrix.EventArgs e)
        {
            LogHelper.WriteConsole("OnClose: XMPP connection closed");
            StartConnectTimer();
        }
        #endregion


        private void StartConnectTimer()
        {
            LogHelper.WriteConsole("starting reconnect timer...");
            _connectTimer.Change(5000, Timeout.Infinite);
        }

        public void Connect(object obj)
        {
            if (!_xmppClient.StreamActive)
            {
                LogHelper.WriteConsole("StreamActive=" + _xmppClient.StreamActive);
                LogHelper.WriteConsole("connect: XMPP connecting.... ");
                _onLogin = false;
                _xmppClient.Open();
            }
        }
    }
}
