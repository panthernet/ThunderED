using System;
using System.Collections.Generic;
using System.Text;

namespace ThunderED.Json.Internal
{
    public class TimerItem
    {
        public int id;
        public int timerType;
        public int timerStage;
        public string timerLocation;
        public string timerOwner;
        public string timerET;
        public string timerNotes;
        public string timerChar;
        public int announce;

        public string GetModeName()
        {
            switch (timerType)
            {
                case 1:
                    return LM.Get("timerOffensive");
                case 2:
                    return LM.Get("timerDefensive");
                default:
                    return null;
            }
        }

        public DateTime? GetDateTime()
        {
            if (!DateTime.TryParse(timerET, out var result))
                return null;
            return result;
        }

        public string GetStageName()
        {
            switch (timerStage)
            {
                case 1:
                    return LM.Get("timerHull");
                case 2:
                    return LM.Get("timerArmor");
                case 3:
                    return LM.Get("timerShield");
                case 4:
                    return LM.Get("timerOther");
                default:
                    return null;
            }
        }

        public string GetRemains()
        {
            var d = GetDateTime();
            if(!d.HasValue) return null;
            var dif = (d.Value - DateTime.UtcNow);
            return LM.Get("timerRemains", dif.Days, dif.Hours, dif.Minutes);

        }

        public Dictionary<string, object> GetDictionary()
        {
            return new Dictionary<string, object>
            {
                {nameof(timerType), timerType},
                {nameof(timerStage), timerStage},
                {nameof(timerLocation), timerLocation},
                {nameof(timerOwner), timerOwner},
                {nameof(timerET), GetDateTime()},
                {nameof(timerNotes), timerNotes},
                {nameof(timerChar), timerChar},
                {nameof(announce), announce},
            };
        }
    }
}
