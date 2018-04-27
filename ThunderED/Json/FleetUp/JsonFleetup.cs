using System;

namespace ThunderED.Json.FleetUp
{
    public static class JsonFleetup
    {
        //Fleetup

        public class Opperations
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public DateTime CachedUntilUTC { get; set; }
            public int Code { get; set; }
            public Datum[] Data { get; set; }
        }

        public class Datum
        {
            public int Id { get; set; }
            public int OperationId { get; set; }
            public string Subject { get; set; }
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public string Location { get; set; }
            public int LocationId { get; set; }
            public string Details { get; set; }
            public string Url { get; set; }
            public string Organizer { get; set; }
            public string Category { get; set; }
            public string Group { get; set; }
            public int GroupId { get; set; }
            public Doctrine[] Doctrines { get; set; }
        }

        public class Doctrine
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}
