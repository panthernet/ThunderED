# ThunderED - EVE Online Discord Bot
[Reach us on Discord](https://discord.gg/UsnY6UR)

![](https://ci.appveyor.com/api/projects/status/67i3q6v804sjyse6?svg=true)

# Advantages
* .NET Core powered multi-platform support (Win, Linux, Mac, etc.)
* Standalone build with no additional software or framework requirements
* 100% ESI API
* Multiple language support
* Web templates and rich settings
* Effective caching logic for less network and memory load
* Highly customizable Message templates for killmails without any source-code modifications
* Modular design for new extensions such as templates, modules and DB support

# Supported Modules
* Web Auth - authenticate EVE characters in Discord using built-in web server
* Auth Check - check users access rights and strip permissions when char leaves your corp or ally
* Live Kill Feed - feed live EVE killmails using ZKill RedisQ into multiple channels 
* Reliable Kill Feed - feed killmails for selected corps or alliances in a reliable way using ZKill API
* Radius Kill Feed - feed live killmails in a radius around selected systems, constellations and regions into multiple channels
* Notifications Feed - feed EVE notifications from different characters into multiple channels
* Jabber Integration - connect with jabbers for cross messaging support
* Char & corp search - fetch information about characters and corps using special commands
* EVE Time - get EVE Online time
* Price Check - check relevant prices on item in all major trade hubs using special commands
* Ally & Corp Stats - get KM stats for selected alliances or corporations by day, month or year
* FleetUP integration - announces and reminders for FleetUp ops
* (BETA) Timers - built-in web server for important timers and events. Auto add timers for reinforced structure events!
* (WIP) Mail Feeder - feed mail by key phrases from authenticated characters 
* (?) Channel MOTD integration - could be done by request

# Build Requirements
* Visual Studio 2017 Community Edition
* .NET Core 2.0
