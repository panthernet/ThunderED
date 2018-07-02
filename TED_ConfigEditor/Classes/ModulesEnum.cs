using System.ComponentModel;

namespace TED_ConfigEditor.Classes
{
    public enum ModulesEnum
    {
        [Description("webServerModule")]
        ModuleWebServer,
        [Description("")]
        ModuleAuthCheck,
        [Description("WebAuthModule")]
        ModuleAuthWeb,
        [Description("")]
        ModuleCharCorp,
        [Description("liveKillFeedModule")]
        ModuleLiveKillFeed,
        [Description("radiusKillFeedModule")]
        ModuleRadiusKillFeed,
        [Description("")]
        ModulePriceCheck,
        [Description("")]
        ModuleTime,
        [Description("fleetupModule")]
        ModuleFleetup,
        [Description("jabberModule")]
        ModuleJabber,
        [Description("notificationFeedModule")]
        ModuleNotificationFeed,
        [Description("statsModule")]
        ModuleStats,
        [Description("timersModule")]
        ModuleTimers,
        [Description("mailModule")]
        ModuleMail,
        [Description("ircModule")]
        ModuleIRC,
        [Description("telegramModule")]
        ModuleTelegram,
        [Description("chatRelayModule")]
        ModuleChatRelay,
        [Description("incursionNotificationModule")]
        ModuleIncursionNotify,
    }
}
