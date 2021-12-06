using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using ThunderED.Json;
using ThunderED.Thd;

namespace ThunderED
{
    public class ThunderedDbContext : DbContext
    {
        public DbSet<ThdAuthUser> Users { get; set; }
        public DbSet<ThdToken> Tokens { get; set; }
        public DbSet<ThdMiningNotification> MiningNotifications { get; set; }
        public DbSet<ThdMiningLedger> MiningLedgers { get; set; }
        public DbSet<ThdCacheEntry> Cache { get; set; }
        public DbSet<ThdCacheDataEntry> CacheData { get; set; }
        public DbSet<ThdNotificationListEntry> NotificationsList { get; set; }
        public DbSet<ThdMoonTableEntry> MoonTable { get; set; }
        public DbSet<ThdStorageConsoleEntry> StorageConsole { get; set; }
        public DbSet<ThdInvCustomScheme> CustomSchemes { get; set; }
        public DbSet<ThdStandsAuth> StandsAuth { get; set; }
        public DbSet<ThdContract> Contracts { get; set; }
        public DbSet<ThdNullCampaign> NullCampaigns { get; set; }
        public DbSet<ThdIncursion> Incursions { get; set; }
        public DbSet<ThdMail> Mails { get; set; }
        public DbSet<ThdSovIndexTracker> SovIndexTrackers { get; set; }
        public DbSet<ThdIndustryJob> IndustryJobs { get; set; }
        public DbSet<ThdStarSystem> StarSystems { get; set; }
        public DbSet<ThdStarRegion> StarRegions{ get; set; }
        public DbSet<ThdStarConstellation> StarConstellations { get; set; }
        public DbSet<ThdType> Types { get; set; }
        public DbSet<ThdGroup> Groups { get; set; }

        public DbSet<ThdTimer> Timers { get; set; }

        public ThunderedDbContext()
        {
            //Database.EnsureCreated();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            #region ThdAuthUser
            modelBuilder.Entity<ThdAuthUser>().HasIndex(u => u.CharacterId).IsUnique();
            modelBuilder.Entity<ThdAuthUser>().HasKey(u => u.Id);
            modelBuilder.Entity<ThdAuthUser>().ToTable("auth_users");

            modelBuilder.Entity<ThdAuthUser>().Property(a => a.Id).HasColumnName("Id").ValueGeneratedOnAdd();
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.CharacterId).HasColumnName("characterID");
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.DiscordId).HasColumnName("discordID");
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.GroupName).HasColumnName("groupName");
            //modelBuilder.Entity<ThdAuthUser>().Property(a => a.RefreshToken).HasColumnName("refreshToken");
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.AuthState).HasColumnName("authState");
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.Data).HasColumnName("data");
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.RegCode).HasColumnName("reg_code");
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.CreateDate).HasColumnName("reg_date");
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.DumpDate).HasColumnName("dump_date");
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.MainCharacterId).HasColumnName("main_character_id");
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.LastCheck).HasColumnName("last_check");
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.Ip).HasColumnName("ip");

            #endregion

            #region ThdStandsAuth
            modelBuilder.Entity<ThdStandsAuth>().HasKey(u => u.CharacterId);
            modelBuilder.Entity<ThdStandsAuth>().ToTable("stand_auth");

            modelBuilder.Entity<ThdStandsAuth>().Property(a => a.CharacterId).HasColumnName("characterID").ValueGeneratedNever();
            modelBuilder.Entity<ThdStandsAuth>().Property(a => a.Token).HasColumnName("token");
            modelBuilder.Entity<ThdStandsAuth>().Property(a => a.PersonalStands).HasColumnName("personalStands").HasConversion(v=> JsonConvert.SerializeObject(v), v=> JsonConvert.DeserializeObject<List<JsonClasses.Contact>>(v));
            modelBuilder.Entity<ThdStandsAuth>().Property(a => a.CorpStands).HasColumnName("corpStands").HasConversion(v => JsonConvert.SerializeObject(v), v => JsonConvert.DeserializeObject<List<JsonClasses.Contact>>(v));
            modelBuilder.Entity<ThdStandsAuth>().Property(a => a.AllianceStands).HasColumnName("allianceStands").HasConversion(v => JsonConvert.SerializeObject(v), v => JsonConvert.DeserializeObject<List<JsonClasses.Contact>>(v));

            #endregion

            #region ThdTimer
            modelBuilder.Entity<ThdTimer>().HasKey(a=> a.Id);
            modelBuilder.Entity<ThdTimer>().ToTable("timers");

            modelBuilder.Entity<ThdTimer>().Property(a => a.Id).HasColumnName("id");
            modelBuilder.Entity<ThdTimer>().Property(a => a.Type).HasColumnName("timerType");
            modelBuilder.Entity<ThdTimer>().Property(a => a.Stage).HasColumnName("timerStage");
            modelBuilder.Entity<ThdTimer>().Property(a => a.Location).HasColumnName("timerLocation");
            modelBuilder.Entity<ThdTimer>().Property(a => a.Owner).HasColumnName("timerOwner");
            modelBuilder.Entity<ThdTimer>().Property(a => a.Date).HasColumnName("timerET");
            modelBuilder.Entity<ThdTimer>().Property(a => a.Notes).HasColumnName("timerNotes");
            modelBuilder.Entity<ThdTimer>().Property(a => a.TimerChar).HasColumnName("timerChar");
            modelBuilder.Entity<ThdTimer>().Property(a => a.Announce).HasColumnName("announce");

            #endregion

            #region ThdContract
            modelBuilder.Entity<ThdContract>().HasKey(u => u.CharacterId);
            modelBuilder.Entity<ThdContract>().ToTable("contracts");

            modelBuilder.Entity<ThdContract>().Property(a => a.CharacterId).HasColumnName("characterID").ValueGeneratedNever();
            modelBuilder.Entity<ThdContract>().Property(a => a.Data).HasColumnName("data").HasConversion(v => JsonConvert.SerializeObject(v), v => JsonConvert.DeserializeObject<List<JsonClasses.Contract>>(v).OrderByDescending(a=> a.contract_id).ToList());
            modelBuilder.Entity<ThdContract>().Property(a => a.CorpData).HasColumnName("corpdata").HasConversion(v => JsonConvert.SerializeObject(v), v => JsonConvert.DeserializeObject<List<JsonClasses.Contract>>(v).OrderByDescending(a => a.contract_id).ToList());

            #endregion

            #region ThdNullCampaign

            modelBuilder.Entity<ThdNullCampaign>().HasKey("GroupKey", "CampaignId");

            modelBuilder.Entity<ThdNullCampaign>().HasIndex(u => u.GroupKey);
            modelBuilder.Entity<ThdNullCampaign>().HasIndex("GroupKey","CampaignId");

            modelBuilder.Entity<ThdNullCampaign>().ToTable("null_campaigns");
            modelBuilder.Entity<ThdNullCampaign>().Property(a => a.GroupKey).HasColumnName("groupKey");
            modelBuilder.Entity<ThdNullCampaign>().Property(a => a.CampaignId).HasColumnName("campaignId");
            modelBuilder.Entity<ThdNullCampaign>().Property(a => a.Time).HasColumnName("time");
            modelBuilder.Entity<ThdNullCampaign>().Property(a => a.Data).HasColumnName("data").HasConversion(v => JsonConvert.SerializeObject(v), v => JsonConvert.DeserializeObject<JsonClasses.NullCampaignItem>(v));
            modelBuilder.Entity<ThdNullCampaign>().Property(a => a.LastAnnounce).HasColumnName("lastAnnounce");
            #endregion

            #region ThdIncursion
            modelBuilder.Entity<ThdIncursion>().HasKey(u => u.ConstId);
            modelBuilder.Entity<ThdIncursion>().HasIndex(u => u.ConstId);

            modelBuilder.Entity<ThdIncursion>().ToTable("incursions");
            modelBuilder.Entity<ThdIncursion>().Property(a => a.ConstId).HasColumnName("constId");
            modelBuilder.Entity<ThdIncursion>().Property(a => a.Time).HasColumnName("time");
            #endregion

            #region ThdMail
            modelBuilder.Entity<ThdMail>().HasKey(u => u.Id);
            modelBuilder.Entity<ThdMail>().HasIndex(u => u.Id);

            modelBuilder.Entity<ThdMail>().ToTable("mail");
            modelBuilder.Entity<ThdMail>().Property(a => a.Id).HasColumnName("id");
            modelBuilder.Entity<ThdMail>().Property(a => a.MailId).HasColumnName("mailId");
            #endregion

            #region ThdSovIndexTracker
            modelBuilder.Entity<ThdSovIndexTracker>().HasKey(u => u.GroupName);
            modelBuilder.Entity<ThdSovIndexTracker>().HasIndex(u => u.GroupName);

            modelBuilder.Entity<ThdSovIndexTracker>().ToTable("sovIndexTracker");
            modelBuilder.Entity<ThdSovIndexTracker>().Property(a => a.GroupName).HasColumnName("groupName");
            modelBuilder.Entity<ThdSovIndexTracker>().Property(a => a.Data).HasColumnName("data").HasConversion(v => JsonConvert.SerializeObject(v), v => JsonConvert.DeserializeObject< List<JsonClasses.SovStructureData>>(v));
            #endregion

            #region ThdIndustryJob
            modelBuilder.Entity<ThdIndustryJob>().HasKey(u => u.CharacterId);
            modelBuilder.Entity<ThdIndustryJob>().HasIndex(u => u.CharacterId);

            modelBuilder.Entity<ThdIndustryJob>().ToTable("industry_jobs");
            modelBuilder.Entity<ThdIndustryJob>().Property(a => a.CharacterId).HasColumnName("character_id");
            modelBuilder.Entity<ThdIndustryJob>().Property(a => a.PersonalJobs).HasColumnName("personal_jobs").HasConversion(v => JsonConvert.SerializeObject(v), v => JsonConvert.DeserializeObject<List<JsonClasses.IndustryJob>>(v));
            modelBuilder.Entity<ThdIndustryJob>().Property(a => a.CorporateJobs).HasColumnName("corporate_jobs").HasConversion(v => JsonConvert.SerializeObject(v), v => JsonConvert.DeserializeObject<List<JsonClasses.IndustryJob>>(v));
            #endregion

            #region ThdToken
            modelBuilder.Entity<ThdToken>().HasIndex(u => u.Id).IsUnique();
            modelBuilder.Entity<ThdToken>().HasKey(u => u.Id);
            modelBuilder.Entity<ThdToken>().HasIndex(u => u.CharacterId);
            modelBuilder.Entity<ThdToken>().ToTable("tokens");

            modelBuilder.Entity<ThdToken>().Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
            modelBuilder.Entity<ThdToken>().Property(a => a.CharacterId).HasColumnName("character_id");
            modelBuilder.Entity<ThdToken>().Property(a => a.Token).HasColumnName("token");
            modelBuilder.Entity<ThdToken>().Property(a => a.Type).HasColumnName("type");
            modelBuilder.Entity<ThdToken>().Property(a => a.Roles).HasColumnName("roles");
            modelBuilder.Entity<ThdToken>().Property(a => a.Scopes).HasColumnName("scopes");
            modelBuilder.Entity<ThdToken>().HasOne(a => a.User).WithMany(a => a.Tokens)
                .HasForeignKey(a => a.CharacterId).HasPrincipalKey(a => a.CharacterId);
            #endregion

            #region ThdMiningNotification

            modelBuilder.Entity<ThdMiningNotification>().HasIndex(u => u.CitadelId).IsUnique();
            modelBuilder.Entity<ThdMiningNotification>().HasKey(u => u.CitadelId);
            modelBuilder.Entity<ThdMiningNotification>().ToTable("mining_notifications");

            modelBuilder.Entity<ThdMiningNotification>().Property(a => a.CitadelId).HasColumnName("citadel_id").ValueGeneratedNever();
            modelBuilder.Entity<ThdMiningNotification>().Property(a => a.OreComposition).HasColumnName("ore_composition");
            modelBuilder.Entity<ThdMiningNotification>().Property(a => a.Operator).HasColumnName("operator");
            modelBuilder.Entity<ThdMiningNotification>().Property(a => a.Date).HasColumnName("date");

            #endregion

            #region ThdMiningLedger
            modelBuilder.Entity<ThdMiningLedger>().HasIndex(u => u.Id).IsUnique();
            modelBuilder.Entity<ThdMiningLedger>().HasKey(u => u.Id);
            modelBuilder.Entity<ThdMiningLedger>().ToTable("mining_ledger");
          
            modelBuilder.Entity<ThdMiningLedger>().Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
            modelBuilder.Entity<ThdMiningLedger>().Property(a => a.CitadelId).HasColumnName("citadel_id");
            modelBuilder.Entity<ThdMiningLedger>().Property(a => a.Date).HasColumnName("date");
            modelBuilder.Entity<ThdMiningLedger>().Property(a => a.OreJson).HasColumnName("ore_json");
            modelBuilder.Entity<ThdMiningLedger>().Property(a => a.Stats).HasColumnName("stats");
            #endregion

            #region Cache
            modelBuilder.Entity<ThdCacheEntry>().HasIndex(u => u.Id);

            modelBuilder.Entity<ThdCacheEntry>().ToTable("cache");

            modelBuilder.Entity<ThdCacheEntry>().Property(a => a.Id).HasColumnName("id");
            modelBuilder.Entity<ThdCacheEntry>().Property(a => a.Type).HasColumnName("type");
            modelBuilder.Entity<ThdCacheEntry>().Property(a => a.LastAccess).HasColumnName("lastAccess");
            modelBuilder.Entity<ThdCacheEntry>().Property(a => a.LastUpdate).HasColumnName("lastUpdate");
            modelBuilder.Entity<ThdCacheEntry>().Property(a => a.Content).HasColumnName("text");
            modelBuilder.Entity<ThdCacheEntry>().Property(a => a.Days).HasColumnName("days");
            #endregion

            #region CacheData
            modelBuilder.Entity<ThdCacheDataEntry>().HasKey(a=> a.Name);
            modelBuilder.Entity<ThdCacheDataEntry>().ToTable("cache_data");
            modelBuilder.Entity<ThdCacheDataEntry>().HasIndex(u => u.Name);

            modelBuilder.Entity<ThdCacheDataEntry>().Property(a => a.Name).HasColumnName("name");
            modelBuilder.Entity<ThdCacheDataEntry>().Property(a => a.Data).HasColumnName("data");
            #endregion

            #region ThdNotificationListEntry
            modelBuilder.Entity<ThdNotificationListEntry>().HasKey("GroupName", "FilterName", "Id");
            modelBuilder.Entity<ThdNotificationListEntry>().HasIndex(u => u.GroupName);
            modelBuilder.Entity<ThdNotificationListEntry>().ToTable("notifications_list");

            modelBuilder.Entity<ThdNotificationListEntry>().Property(a => a.Id).HasColumnName("id");
            modelBuilder.Entity<ThdNotificationListEntry>().Property(a => a.GroupName).HasColumnName("groupName");
            modelBuilder.Entity<ThdNotificationListEntry>().Property(a => a.FilterName).HasColumnName("filterName");
            modelBuilder.Entity<ThdNotificationListEntry>().Property(a => a.Time).HasColumnName("time");
            #endregion

            #region ThdMoonTableEntry
            modelBuilder.Entity<ThdMoonTableEntry>().HasIndex(u => u.Id).IsUnique();
            modelBuilder.Entity<ThdMoonTableEntry>().HasIndex(u => u.SystemId);
            modelBuilder.Entity<ThdMoonTableEntry>().HasIndex(u => u.OreId);
            modelBuilder.Entity<ThdMoonTableEntry>().ToTable("moon_table");

            modelBuilder.Entity<ThdMoonTableEntry>().Property(a => a.Id).HasColumnName("id");
            modelBuilder.Entity<ThdMoonTableEntry>().Property(a => a.MoonId).HasColumnName("moon_id");
            modelBuilder.Entity<ThdMoonTableEntry>().Property(a => a.OreId).HasColumnName("ore_id");
            modelBuilder.Entity<ThdMoonTableEntry>().Property(a => a.OreQuantity).HasColumnName("ore_quantity");
            modelBuilder.Entity<ThdMoonTableEntry>().Property(a => a.PlanetId).HasColumnName("planet_id");
            modelBuilder.Entity<ThdMoonTableEntry>().Property(a => a.SystemId).HasColumnName("system_id");
            modelBuilder.Entity<ThdMoonTableEntry>().Property(a => a.RegionId).HasColumnName("region_id");
            modelBuilder.Entity<ThdMoonTableEntry>().Property(a => a.OreName).HasColumnName("ore_name");
            modelBuilder.Entity<ThdMoonTableEntry>().Property(a => a.MoonName).HasColumnName("moon_name");
            #endregion

            #region ThdStorageConsoleEntry
            modelBuilder.Entity<ThdStorageConsoleEntry>().HasIndex(u => u.Id).IsUnique();
            modelBuilder.Entity<ThdStorageConsoleEntry>().HasIndex(u => u.Name).IsUnique();
            modelBuilder.Entity<ThdStorageConsoleEntry>().HasKey(u => u.Id);
            modelBuilder.Entity<ThdStorageConsoleEntry>().ToTable("storage_console");

            modelBuilder.Entity<ThdStorageConsoleEntry>().Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
            modelBuilder.Entity<ThdStorageConsoleEntry>().Property(a => a.Name).HasColumnName("name");
            modelBuilder.Entity<ThdStorageConsoleEntry>().Property(a => a.Value).HasColumnName("value");
            #endregion

            #region ThdInvCustomScheme
            modelBuilder.Entity<ThdInvCustomScheme>().HasIndex(u => u.Id);
            modelBuilder.Entity<ThdInvCustomScheme>().ToTable("inv_custom_scheme");

            modelBuilder.Entity<ThdInvCustomScheme>().Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
            modelBuilder.Entity<ThdInvCustomScheme>().Property(a => a.ItemId).HasColumnName("item_id");
            modelBuilder.Entity<ThdInvCustomScheme>().Property(a => a.Quantity).HasColumnName("quantity");
            //modelBuilder.Entity<ThdInvCustomScheme>().HasOne(a => a.Type).WithMany(a => a.Schemes)
            //    .HasPrincipalKey(a => a.Id);

            #endregion

            #region ThdType
             modelBuilder.Entity<ThdType>().HasKey(u => u.Id);
             modelBuilder.Entity<ThdType>().HasIndex(u => u.Id);
             modelBuilder.Entity<ThdType>().ToTable("inv_types");

             modelBuilder.Entity<ThdType>().Property(a => a.Id).HasColumnName("typeID").ValueGeneratedNever();
             modelBuilder.Entity<ThdType>().Property(a => a.GroupId).HasColumnName("groupID");
             modelBuilder.Entity<ThdType>().Property(a => a.Name).HasColumnName("typeName");
             modelBuilder.Entity<ThdType>().Property(a => a.Description).HasColumnName("description");
             modelBuilder.Entity<ThdType>().Property(a => a.Mass).HasColumnName("mass");
             modelBuilder.Entity<ThdType>().Property(a => a.Volume).HasColumnName("volume");
             modelBuilder.Entity<ThdType>().Property(a => a.Capacity).HasColumnName("capacity");
             modelBuilder.Entity<ThdType>().Property(a => a.PortionSize).HasColumnName("portionSize");
             modelBuilder.Entity<ThdType>().Property(a => a.RaceId).HasColumnName("raceID");
             modelBuilder.Entity<ThdType>().Property(a => a.BasePrice).HasColumnName("basePrice");
             modelBuilder.Entity<ThdType>().Property(a => a.Published).HasColumnName("published");
             modelBuilder.Entity<ThdType>().Property(a => a.MarketGroupId).HasColumnName("marketGroupID");
             modelBuilder.Entity<ThdType>().Property(a => a.IconId).HasColumnName("iconID");
             modelBuilder.Entity<ThdType>().Property(a => a.SoundId).HasColumnName("soundID");
             modelBuilder.Entity<ThdType>().Property(a => a.GraphicId).HasColumnName("graphicID");

            #endregion

            #region ThdGroup
            modelBuilder.Entity<ThdGroup>().HasKey(u => u.GroupId);
            modelBuilder.Entity<ThdGroup>().HasIndex(u => u.CategoryId);
            modelBuilder.Entity<ThdGroup>().ToTable("inv_groups");

            modelBuilder.Entity<ThdGroup>().Property(a => a.CategoryId).HasColumnName("categoryID");
            modelBuilder.Entity<ThdGroup>().Property(a => a.GroupId).HasColumnName("groupID").ValueGeneratedNever();
            modelBuilder.Entity<ThdGroup>().Property(a => a.GroupName).HasColumnName("groupName");
            modelBuilder.Entity<ThdGroup>().Property(a => a.IconId).HasColumnName("iconID");
            modelBuilder.Entity<ThdGroup>().Property(a => a.UseBasePrice).HasColumnName("useBasePrice");
            modelBuilder.Entity<ThdGroup>().Property(a => a.Anchored).HasColumnName("anchored");
            modelBuilder.Entity<ThdGroup>().Property(a => a.Anchorable).HasColumnName("anchorable");
            modelBuilder.Entity<ThdGroup>().Property(a => a.Fittable).HasColumnName("fittableNonSingleton");
            modelBuilder.Entity<ThdGroup>().Property(a => a.Published).HasColumnName("published");

            #endregion

            #region ThdStarSystem
            modelBuilder.Entity<ThdStarSystem>().HasKey(u => u.SolarSystemId);
            modelBuilder.Entity<ThdStarSystem>().HasIndex(u => u.SolarSystemId);
            modelBuilder.Entity<ThdStarSystem>().HasIndex(u => u.ConstellationId);
            modelBuilder.Entity<ThdStarSystem>().HasIndex(u => u.RegionId);
            modelBuilder.Entity<ThdStarSystem>().HasIndex(u => u.Security);

            modelBuilder.Entity<ThdStarSystem>().ToTable("map_solar_systems");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.RegionId).HasColumnName("regionID");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.ConstellationId).HasColumnName("constellationID");

            modelBuilder.Entity<ThdStarSystem>().Property(a => a.SolarSystemId).HasColumnName("solarSystemID");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.SolarSystemName).HasColumnName("solarSystemName");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.X).HasColumnName("x");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.Y).HasColumnName("y");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.Z).HasColumnName("z");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.XMin).HasColumnName("xMin");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.YMin).HasColumnName("yMin");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.ZMin).HasColumnName("zMin");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.XMax).HasColumnName("xMax");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.YMax).HasColumnName("yMax");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.ZMax).HasColumnName("zMax");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.Luminosity).HasColumnName("luminosity");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.Border).HasColumnName("border");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.Fringe).HasColumnName("fringe");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.Corridor).HasColumnName("corridor");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.Hub).HasColumnName("hub");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.International).HasColumnName("international");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.Regional).HasColumnName("regional");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.Constellation).HasColumnName("constellation");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.Security).HasColumnName("security");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.FactionId).HasColumnName("factionID");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.Radius).HasColumnName("radius");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.SunTypeId).HasColumnName("sunTypeID");
            modelBuilder.Entity<ThdStarSystem>().Property(a => a.SecurityClass).HasColumnName("securityClass");
            #endregion

            #region ThdStarRegion
            modelBuilder.Entity<ThdStarRegion>().HasKey(u => u.RegionId);
            modelBuilder.Entity<ThdStarRegion>().ToTable("map_regions");
            modelBuilder.Entity<ThdStarRegion>().Property(a => a.RegionName).HasColumnName("regionName");
            modelBuilder.Entity<ThdStarRegion>().Property(a => a.RegionId).HasColumnName("regionID");
            modelBuilder.Entity<ThdStarRegion>().Property(a => a.X).HasColumnName("x");
            modelBuilder.Entity<ThdStarRegion>().Property(a => a.Y).HasColumnName("y");
            modelBuilder.Entity<ThdStarRegion>().Property(a => a.Z).HasColumnName("z");
            modelBuilder.Entity<ThdStarRegion>().Property(a => a.XMin).HasColumnName("xMin");
            modelBuilder.Entity<ThdStarRegion>().Property(a => a.YMin).HasColumnName("yMin");
            modelBuilder.Entity<ThdStarRegion>().Property(a => a.ZMin).HasColumnName("zMin");
            modelBuilder.Entity<ThdStarRegion>().Property(a => a.XMax).HasColumnName("xMax");
            modelBuilder.Entity<ThdStarRegion>().Property(a => a.YMax).HasColumnName("yMax");
            modelBuilder.Entity<ThdStarRegion>().Property(a => a.ZMax).HasColumnName("zMax");
            modelBuilder.Entity<ThdStarRegion>().Property(a => a.FactionId).HasColumnName("factionID");
            modelBuilder.Entity<ThdStarRegion>().Property(a => a.Radius).HasColumnName("radius");
            #endregion

            #region ThdStarConstellation
            modelBuilder.Entity<ThdStarConstellation>().HasKey(u => u.ConstellationId);
            modelBuilder.Entity<ThdStarConstellation>().ToTable("map_constellations");
            modelBuilder.Entity<ThdStarConstellation>().Property(a => a.ConstellationName).HasColumnName("constellationName");
            modelBuilder.Entity<ThdStarConstellation>().Property(a => a.ConstellationId).HasColumnName("constellationID");
            modelBuilder.Entity<ThdStarConstellation>().Property(a => a.RegionId).HasColumnName("regionID");
            modelBuilder.Entity<ThdStarConstellation>().Property(a => a.X).HasColumnName("x");
            modelBuilder.Entity<ThdStarConstellation>().Property(a => a.Y).HasColumnName("y");
            modelBuilder.Entity<ThdStarConstellation>().Property(a => a.Z).HasColumnName("z");
            modelBuilder.Entity<ThdStarConstellation>().Property(a => a.XMin).HasColumnName("xMin");
            modelBuilder.Entity<ThdStarConstellation>().Property(a => a.YMin).HasColumnName("yMin");
            modelBuilder.Entity<ThdStarConstellation>().Property(a => a.ZMin).HasColumnName("zMin");
            modelBuilder.Entity<ThdStarConstellation>().Property(a => a.XMax).HasColumnName("xMax");
            modelBuilder.Entity<ThdStarConstellation>().Property(a => a.YMax).HasColumnName("yMax");
            modelBuilder.Entity<ThdStarConstellation>().Property(a => a.ZMax).HasColumnName("zMax");
            modelBuilder.Entity<ThdStarConstellation>().Property(a => a.FactionId).HasColumnName("factionID");
            modelBuilder.Entity<ThdStarConstellation>().Property(a => a.Radius).HasColumnName("radius");
            #endregion
        }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            switch (DbSettingsManager.Settings.Database.DatabaseProvider.ToLower())
            {
                case "sqlite":
                    optionsBuilder.UseSqlite($"Data Source={Path.Combine(DbSettingsManager.DataDirectory, DbSettingsManager.Settings.Database.DatabaseFile)}");
                    break;
                case "mysql":
                    var cstring =
                        $"server={DbSettingsManager.Settings.Database.ServerAddress};UserId={DbSettingsManager.Settings.Database.UserId};Password={DbSettingsManager.Settings.Database.Password};database={DbSettingsManager.Settings.Database.DatabaseName};";
                    var v = ServerVersion.AutoDetect(cstring);
                    optionsBuilder.UseMySql(cstring, v);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

}
