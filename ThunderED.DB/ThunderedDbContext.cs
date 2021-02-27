using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
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
        public DbSet<ThdNotificationListEntry> NotificationsList { get; set; }
        public DbSet<ThdMoonTableEntry> MoonTable { get; set; }
        //public DbSet<JsonClasses.SystemName> Systems { get; set; }
        //public DbSet<JsonClasses.ConstellationData> Constellations { get; set; }
        //public DbSet<JsonClasses.RegionData> Regions { get; set; }

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
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.RefreshToken).HasColumnName("refreshToken");
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.AuthState).HasColumnName("authState");
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.Data).HasColumnName("data");
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.RegCode).HasColumnName("reg_code");
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.CreateDate).HasColumnName("reg_date");
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.DumpDate).HasColumnName("dump_date");
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.MainCharacterId).HasColumnName("main_character_id");
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.LastCheck).HasColumnName("last_check");
            modelBuilder.Entity<ThdAuthUser>().Property(a => a.Ip).HasColumnName("ip");

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

            #region ThdNotificationListEntry
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
