using System;

namespace ThunderED.Thd
{
    public class ThdHistoryNotification
    {
        public long Id { get; set; }
        public long SenderId { get; set; }
        public string SenderType { get; set; }
        public string Type { get; set; }
        public DateTime ReceiveDate { get; set; }
        public string Data { get; set; }
        public long FeederId { get; set; }
        public long SenderCorporationId { get; set; }
        public long? SenderAllianceId { get; set; }
        public CharacterSnapshot SenderSnapshot { get; set; }

        public virtual ThdAuthUser User { get; set; }
    }
}
