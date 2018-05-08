namespace ThunderED.Json
{
    public partial class JsonClasses
    {
        //ESI Classes
        public class CharacterID
        {
            public int[] character { get; set; }
        }

        public class SearchInventoryType
        {
            public int[] inventory_type { get; set; }
        }


        public class CorpIDLookup
        {
            public int[] corporation { get; set; }
        }

        public class IDLookUp
        {
            public int[] idList;
        }


        public class SystemIDSearch
        {
            public int[] solar_system { get; set; }
        }

        public class SearchName
        {
            public int id { get; set; }
            public string name { get; set; }
            public string category { get; set; }
        }


        public class Position
        {
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }
        }

        public class Planet
        {
            public int planet_id { get; set; }
            public int[] moons { get; set; }
        }

        public class Dogma_Attributes
        {
            public int attribute_id { get; set; }
            public float value { get; set; }
        }

        public class Dogma_Effects
        {
            public int effect_id { get; set; }
            public bool is_default { get; set; }
        }

        internal class NotificationSearch
        {
            public Notification[] list;
        }

        internal class Notification
        {
            public int notification_id;
            public string type;
            public int sender_id;
            public string sender_type;
            public string timestamp;
            public bool is_read;
            public string text;
        }

        internal class StructureData
        {
            public string name;
            public int solar_system_id;
        }

        internal class ConstellationData
        {
            public int constellation_id;
            public string name;
            public int region_id;
        }

        internal class RegionData
        {
            public string name;
        }

        public class MailRecipient
        {
            public int recipient_id;
            public string recipient_type;
        }

        public class MailHeader
        {
            public int from;
            public bool is_read;
            public int[] labels;
            public int mail_id;
            public MailRecipient[] recipients;
            public string subject;
            public string timestamp;
        }

        public class Mail
        {
            public string body;
            public int from;
            public int[] labels;
            public bool read;
            public string subject;
            public string timestamp;
        }

        public class MailLabelData
        {
            public MailLabel[] labels;
            public int total_unread_count;
        }

        public class MailLabel
        {
            public string color;
            public int label_id;
            public string name;
            public int unread_count;
        }
    }
}