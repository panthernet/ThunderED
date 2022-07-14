using System;
using System.Collections.Generic;

namespace ThunderED.Thd
{
    public class ThdHistoryMail
    {
        public long Id { get; set; }
        public long SenderId { get; set; }
        public string Subject { get; set; }
        public DateTime ReceiveDate { get; set; }
        public string Body { get; set; }
        public string Labels { get; set; }
        public long SenderCorporationId { get; set; }
        public long? SenderAllianceId { get; set; }
        public CharacterSnapshot SenderSnapshot { get; set; }

        public virtual ThdAuthUser User { get; set; }
        public virtual List<ThdHistoryMailRcp> Recipients { get; set; } = new();
    }
}
