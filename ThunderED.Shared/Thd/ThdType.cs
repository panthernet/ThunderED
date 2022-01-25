using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using ThunderED.Json;

namespace ThunderED.Thd
{
    public class ThdType
    {
        public long Id { get; set; }
        public long GroupId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public float? Mass { get; set; }
        public float? Volume { get; set; }
        public float? Capacity { get; set; }
        public int? PortionSize { get; set; }
        public long? RaceId { get; set; }
        public decimal? BasePrice { get; set; }
        public bool? Published { get; set; }
        public long? MarketGroupId { get; set; }
        public long? IconId { get; set; }
        public long? SoundId { get; set; }
        public long? GraphicId { get; set; }

        [JsonIgnore]
        [NotMapped]
        public List<JsonClasses.Dogma_Attributes> Attributes { get; set; } = new List<JsonClasses.Dogma_Attributes>();

        //public List<ThdInvCustomScheme> Schemes { get; set; }
        public static ThdType FromJson(JsonClasses.Type_id input)
        {
            return new ThdType
            {
                Id = input.type_id,
                Name = input.name,
                IconId = 0,
                Capacity = input.capacity,
                Description = input.description,
                Published = input.published,
                GraphicId = input.graphic_id,
                PortionSize = input.portion_size,
                GroupId = input.group_id,
                Mass = input.mass,
                Volume = input.volume,
                Attributes = input.dogma_attributes == null ? new List<JsonClasses.Dogma_Attributes>() : input.dogma_attributes.ToList()
            };
        }
    }
}
