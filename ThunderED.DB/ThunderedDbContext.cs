using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThunderED.Thd;

namespace ThunderED
{
    public class ThunderedDbContext : DbContext
    {
        public DbSet<ThdAuthUser> Users { get; set; }
        public DbSet<ThdToken> Tokens { get; set; }
        //public DbSet<JsonClasses.SystemName> Systems { get; set; }
        //public DbSet<JsonClasses.ConstellationData> Constellations { get; set; }
        //public DbSet<JsonClasses.RegionData> Regions { get; set; }

        public ThunderedDbContext()
        {
            //Database.EnsureCreated();
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        public override ValueTask DisposeAsync()
        {
            return base.DisposeAsync();

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

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

            //modelBuilder.Entity<ThdToken>().HasIndex(new string[] { "character_id", "type" }, "tokens_character_id_type_uindex").IsUnique();
            //modelBuilder.Entity<JsonClasses.SystemName>().HasIndex(u => u.system_id );
            //modelBuilder.Entity<JsonClasses.ConstellationData>().HasIndex(u => u.constellation_id );
            //modelBuilder.Entity<JsonClasses.RegionData>().HasIndex(u => u.DB_id);

            SetupRelations(modelBuilder);
            // CreateTestData(modelBuilder);
        }


        private void SetupRelations(ModelBuilder modelBuilder)
        {

            /*modelBuilder.Entity<DogmaAttributeData>().HasNoKey();
            // modelBuilder.Entity<DogmaAttributeData>().HasKey(
            //     t => new { t.TypeId, t.AttributeId });

            //secretuser
            modelBuilder.Entity<SecretUserGeneralData>()
                .HasOne(p => p.User)
                .WithOne(t => t.GeneralData)
                .HasForeignKey<SecretUserGeneralData>(p => p.CharacterId);

            modelBuilder.Entity<IpData>()
                .HasOne(p => p.User)
                .WithOne(t => t.IpData)
                .HasForeignKey<IpData>(p => p.CharacterId);

            modelBuilder.Entity<AuthUserData>()
                .HasOne(p => p.User)
                .WithOne(t => t.Data)
                .HasForeignKey<AuthUserData>(p => p.CharacterId);

            modelBuilder.Entity<ApiKeyData>()
                .HasOne(p => p.User)
                .WithMany(t => t.ApiKeys)
                .HasForeignKey(p => p.CharacterId);

            modelBuilder.Entity<CorporationHistoryData>()
                .HasOne(p => p.User)
                .WithMany(t => t.CorpHistory)
                .HasForeignKey(p => p.CharacterId);

            //wallet journal
            modelBuilder.Entity<WalletJournalData>()
                .HasOne(p => p.User)
                .WithMany(t => t.WalletJournal)
                .HasForeignKey(p => p.CharacterId);


            //wallet transactions
            modelBuilder.Entity<WalletTransactionData>()
                .HasOne(p => p.User)
                .WithMany(t => t.WalletTransactions)
                .HasForeignKey(p => p.CharacterId);
            modelBuilder.Entity<WalletTransactionData>()
                .HasOne(p => p.Type)
                .WithMany(p=> p.WalletTransactions)
                .HasForeignKey(p => p.TypeId);

            //apikey
            modelBuilder.Entity<EtagData>()
                .HasOne(p => p.User)
                .WithMany(t => t.Etags)
                .HasForeignKey(p => p.CharacterId);

            //static
            modelBuilder.Entity<JsonClasses.SystemName>()
                .HasOne(p => p.Constellation)
                .WithMany(t => t.Systems)
                .HasForeignKey(p => p.constellation_id);

            modelBuilder.Entity<JsonClasses.ConstellationData>()
                .HasOne(p => p.Region)
                .WithMany(t => t.Constellations)
                .HasForeignKey(p => p.region_id);

            modelBuilder.Entity<TypeIdData>()
                .HasOne(p => p.Group)
                .WithMany(t => t.Types)
                .HasForeignKey(p => p.GroupId);

            //mail
            modelBuilder.Entity<MailData>()
                .HasOne(p => p.User)
                .WithMany(t => t.Mails)
                .HasForeignKey(p => p.CharacterId);

            //mail recipients
            modelBuilder.Entity<MailRecipientData>()
                .HasOne(p => p.Mail)
                .WithMany(t => t.Recipients)
                .HasForeignKey(p => p.MailId);

            //mail body
            modelBuilder.Entity<MailBodyData>()
                .HasOne(p => p.Mail)
                .WithOne(p => p.Body)
                .HasForeignKey<MailBodyData>(p => p.Id);


            //assets
            modelBuilder.Entity<AssetData>()
                .HasOne(p => p.User)
                .WithMany(p => p.Assets)
                .HasForeignKey(p => p.CharacterId);
            modelBuilder.Entity<AssetData>()
                .HasOne(p => p.Type)
                .WithMany(p=> p.Assets)
                .HasForeignKey(p => p.TypeId);

            //skills
            modelBuilder.Entity<SkillData>()
                .HasOne(p => p.User)
                .WithMany(p => p.Skills)
                .HasForeignKey(p => p.CharacterId);
            
            modelBuilder.Entity<SkillData>()
                .HasOne(p => p.SkillInfo)
                .WithMany(p=> p.Skills)
                .HasForeignKey(p => p.SkillId);

            //contracts
            modelBuilder.Entity<ContractData>()
                .HasOne(p => p.User)
                .WithMany(p => p.Contracts)
                .HasForeignKey(p => p.CharacterId);*/
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            switch (DbSettingsManager.Settings.Database.DatabaseProvider.ToLower())
            {
                case "sqlite":
                    optionsBuilder.UseSqlite($"Data Source={Path.Combine(DbSettingsManager.DataDirectory, DbSettingsManager.Settings.Database.DatabaseFile)}");
                    break;
                case "mysql":
                    optionsBuilder.UseMySQL($"server={DbSettingsManager.Settings.Database.ServerAddress};UserId={DbSettingsManager.Settings.Database.UserId};Password={DbSettingsManager.Settings.Database.Password};database={DbSettingsManager.Settings.Database.DatabaseName};");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

       /* protected static string CreateConnectString(bool skipDatabase = false)
        {
            var sb = new StringBuilder();
            sb.Append($"Server={SettingsManager.Settings.Database.ServerAddress};");
            if (SettingsManager.Settings.Database.ServerPort > 0)
                sb.Append($"Port={SettingsManager.Settings.Database.ServerPort};");
            if (!skipDatabase)
                sb.Append($"Database={SettingsManager.Settings.Database.DatabaseName};");
            sb.Append($"Uid={SettingsManager.Settings.Database.UserId};");
            if (!string.IsNullOrEmpty(SettingsManager.Settings.Database.Password))
                sb.Append($"Pwd={SettingsManager.Settings.Database.Password};");
            return sb.ToString();
        }*/
    }


}
