using System;
using System.Collections.Generic;
using System.Text;

namespace ThunderED.Classes.Entities
{
    public class PendingUserEntity
    {
        public long Id { get; set; }
        public long CharacterId { get; set; }
        public long CorporationId { get; set; }
        public long AllianceId { get; set; }
        public string Groups { get; set; }
        public string AuthString { get; set; }
        public bool Active { get; set; }
        public DateTime CreateDate { get; set; }
        public long DiscordId { get; set; }
    }
}
