using System;

namespace ThunderED.Json
{
    public class MiningLedgerJson
    {
        public DateTime last_updated;
        public long observer_id;
        public ObserverEnum observer_type;
    }

    public enum ObserverEnum
    {
        structure
    }
}
