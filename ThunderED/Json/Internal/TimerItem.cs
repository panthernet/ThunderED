using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ThunderED.Classes;

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
        public int timerRfDay;
        public int timerRfHour;
        public int timerRfMin;

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
            if (int.TryParse(timerET, out var iValue))
            {
                var x = DateTimeOffset.FromUnixTimeSeconds(iValue).UtcDateTime;
                return x;
            }

            if (DateTime.TryParse(timerET, out var result)) return result;
            if (!string.IsNullOrEmpty(SettingsManager.Settings.TimersModule.TimeInputFormat))
            {
                var format = SettingsManager.Settings.TimersModule.TimeInputFormat.Replace("D", "d").Replace("Y", "y");
                if (DateTime.TryParseExact(timerET, format, null, DateTimeStyles.None, out result))
                    return result;
            }

            if (timerRfDay == 0 && timerRfHour == 0 && timerRfMin == 0) return null;
            var now = DateTime.UtcNow;
            return now.AddDays(timerRfDay).AddHours(timerRfHour).AddMinutes(timerRfMin);
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

        public string GetRemains(bool addWord = false)
        {
            var d = GetDateTime();
            if(!d.HasValue) return null;
            var dif = (d.Value - DateTime.UtcNow);
            return $"{(addWord ? $"{LM.Get("Remains")} " : null)}{LM.Get("timerRemains", dif.Days, dif.Hours, dif.Minutes)}";

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
