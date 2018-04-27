namespace ThunderED.Json.ZKill
{
    internal partial class JsonZKill
    {
         //zKill Classes

        public class ZKillboard
        {
            public Package package { get; set; }
        }


        public class Trophies
        {
            public int levels { get; set; }
            public int max { get; set; }
        }

        public class Activepvp
        {
            public Ships ships { get; set; }
            public Systems systems { get; set; }
            public Regions regions { get; set; }
            public Kills kills { get; set; }
        }

        public class Ships
        {
            public string type { get; set; }
            public int count { get; set; }
        }

        public class Systems
        {
            public string type { get; set; }
            public int count { get; set; }
        }

        public class Regions
        {
            public string type { get; set; }
            public int count { get; set; }
        }

        public class Kills
        {
            public string type { get; set; }
            public int count { get; set; }
        }

        public class _1
        {
            public int allianceID { get; set; }
        }

        public class Lastapiupdate
        {
            public int sec { get; set; }
            public int usec { get; set; }
        }

        public class Topalltime
        {
            public string type { get; set; }
            public Datum2[] data { get; set; }
        }

        public class Toplist
        {
            public string type { get; set; }
            public string title { get; set; }
            public Value[] values { get; set; }
        }
    }
}
