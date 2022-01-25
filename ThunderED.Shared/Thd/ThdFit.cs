using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ThunderED.Thd
{
    public class ThdFit
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string ShipName { get; set; }
        public string GroupName { get; set; }
        public string FitText { get; set; }
        public List<FitSkillEntry> Skills { get; set; } = new();

        [NotMapped]
        public string WebName => $"{Name} ({ShipName})";
    }
}
