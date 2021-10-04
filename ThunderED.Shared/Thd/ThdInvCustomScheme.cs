using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ThunderED.Thd
{
    public class ThdInvCustomScheme
    {
        public long Id { get; set; }
        public long ItemId { get; set; }
        public int Quantity { get; set; }

        //[NotMapped]
        //public string Name => Type?.Name;

        //public ThdType Type { get; set; }
    }

    public class ThdType
    {
        public long Id { get; set; }
        public long GroupId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public float Mass { get; set; }
        public float Volume { get; set; }
        public float Capacity { get; set; }
        public int PortionSize { get; set; }
        public long RaceId { get; set; }
        public decimal BasePrice { get; set; }
        public bool Published { get; set; }
        public long MarketGroupId { get; set; }
        public long IconId { get; set; }
        public long SoundId { get; set; }
        public long GraphicId { get; set; }

        //public List<ThdInvCustomScheme> Schemes { get; set; }
    }
}
