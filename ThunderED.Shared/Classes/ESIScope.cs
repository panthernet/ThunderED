using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace ThunderED.Classes
{
    public class ESIScope
    {
        public readonly List<string> Scopes = new();

        protected string Merge()
        {
            if (!Scopes.Any()) return null;
            return string.Join(' ', Scopes);
        }

        public override string ToString()
        {
            return Merge();
        }
    }

    public static class ESIEXTENSIONS
    {
        public static ESIScope AddScope(this ESIScope item, string scope)
        {
            if (!ESIScopes.Contains(scope))
                throw new Exception($"Unknown ESI scope {scope}");
            if(!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static ESIScope AddLocation(this ESIScope item)
        {
            const string scope = "esi-location.read_location.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static ESIScope AddShip(this ESIScope item)
        {
            const string scope = "esi-location.read_ship_type.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static ESIScope AddUniverseStructure(this ESIScope item)
        {
            const string scope = "esi-universe.read_structures.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static ESIScope AddCorpStructure(this ESIScope item)
        {
            const string scope = "esi-corporations.read_structures.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static ESIScope AddSkills(this ESIScope item)
        {
            const string scope = "esi-skills.read_skills.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }
        public static ESIScope AddSkillQueue(this ESIScope item)
        {
            const string scope = "esi-skills.read_skillqueue.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }
        public static ESIScope AddCharWallet(this ESIScope item)
        {
            const string scope = "esi-wallet.read_character_wallet.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static ESIScope AddCorpWallet(this ESIScope item)
        {
            const string scope = "esi-wallet.read_corporation_wallets.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static ESIScope AddCharAssets(this ESIScope item)
        {
            const string scope = "esi-assets.read_assets.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static ESIScope AddCorpAssets(this ESIScope item)
        {
            const string scope = "esi-assets.read_corporation_assets.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }



        public static ESIScope AddCharContracts(this ESIScope item)
        {
            const string scope = "esi-contracts.read_character_contracts.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static ESIScope AddCorpContracts(this ESIScope item)
        {
            const string scope = "esi-contracts.read_corporation_contracts.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static ESIScope AddCharReadMail(this ESIScope item)
        {
            const string scope = "esi-mail.read_mail.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static ESIScope AddCorpMining(this ESIScope item)
        {
            const string scope = "esi-industry.read_corporation_mining.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static ESIScope AddNotifications(this ESIScope item)
        {
            const string scope = "esi-characters.read_notifications.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static ESIScope AddSearch(this ESIScope item)
        {
            const string scope = "esi-search.search_structures.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }
        

        public static ESIScope AddCharStandings(this ESIScope item)
        {
            const string scope = "esi-characters.read_standings.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static ESIScope AddCharContacts(this ESIScope item)
        {
            const string scope = "esi-characters.read_contacts.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static ESIScope AddCorpContacts(this ESIScope item)
        {
            const string scope = "esi-corporations.read_contacts.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static ESIScope AddAllyContacts(this ESIScope item)
        {
            const string scope = "esi-alliances.read_contacts.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static ESIScope AddCharIndustry(this ESIScope item)
        {
            const string scope = "esi-industry.read_character_jobs.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static ESIScope AddCorpIndustry(this ESIScope item)
        {
            const string scope = "esi-industry.read_corporation_jobs.v1";
            if (!item.Scopes.Contains(scope))
                item.Scopes.Add(scope);
            return item;
        }

        public static List<string> ESIScopes = new List<string>
        {
            "esi-calendar.respond_calendar_events.v1",
            "esi-calendar.read_calendar_events.v1",
            "esi-mail.organize_mail.v1",
            "esi-mail.send_mail.v1",
            "esi-wallet.read_corporation_wallet.v1",
            "esi-search.search_structures.v1",
            "esi-planets.manage_planets.v1",
            "esi-fleets.write_fleet.v1",
            "esi-ui.open_window.v1",
            "esi-ui.write_waypoint.v1",
            "esi-fittings.read_fittings.v1",
            "esi-fittings.write_fittings.v1",
            "esi-markets.structure_markets.v1",
            "esi-characters.read_medals.v1",
            "esi-location.read_location.v1",
            "esi-location.read_ship_type.v1",
            "esi-mail.read_mail.v1",
            "esi-skills.read_skills.v1",
            "esi-skills.read_skillqueue.v1",
            "esi-wallet.read_character_wallet.v1",
            "esi-clones.read_clones.v1",
            "esi-characters.read_contacts.v1",
            "esi-universe.read_structures.v1",
            "esi-bookmarks.read_character_bookmarks.v1",
            "esi-killmails.read_killmails.v1",
            "esi-assets.read_assets.v1",
            "esi-fleets.read_fleet.v1",
            "esi-characters.write_contacts.v1",
            "esi-characters.read_loyalty.v1",
            "esi-characters.read_opportunities.v1",
            "esi-characters.read_chat_channels.v1",
            "esi-characters.read_standings.v1",
            "esi-characters.read_agents_research.v1",
            "esi-industry.read_character_jobs.v1",
            "esi-markets.read_character_orders.v1",
            "esi-characters.read_blueprints.v1",
            "esi-characters.read_corporation_roles.v1",
            "esi-location.read_online.v1",
            "esi-contracts.read_character_contracts.v1",
            "esi-clones.read_implants.v1",
            "esi-characters.read_fatigue.v1",
            "esi-characters.read_notifications.v1",
            "esi-contracts.read_corporation_contracts.v1",
            "esi-industry.read_character_mining.v1",
            "esi-characters.read_titles.v1",
            "esi-characters.read_fw_stats.v1",
            "esi-characterstats.read.v1",
            "esi-corporations.read_corporation_membership.v1",
            "esi-corporations.read_structures.v1",
            "esi-killmails.read_corporation_killmails.v1",
            "esi-corporations.track_members.v1",
            "esi-wallet.read_corporation_wallets.v1",
            "esi-corporations.read_divisions.v1",
            "esi-corporations.read_contacts.v1",
            "esi-assets.read_corporation_assets.v1",
            "esi-corporations.read_titles.v1",
            "esi-corporations.read_blueprints.v1",
            "esi-bookmarks.read_corporation_bookmarks.v1",
            "esi-corporations.read_standings.v1",
            "esi-corporations.read_starbases.v1",
            "esi-industry.read_corporation_jobs.v1",
            "esi-markets.read_corporation_orders.v1",
            "esi-corporations.read_container_logs.v1",
            "esi-industry.read_corporation_mining.v1",
            "esi-planets.read_customs_offices.v1",
            "esi-corporations.read_facilities.v1",
            "esi-corporations.read_medals.v1",
            "esi-alliances.read_contacts.v1",
            "esi-corporations.read_fw_stats.v1",
        };
    }
}
