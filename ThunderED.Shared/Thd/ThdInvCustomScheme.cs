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

}
