using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ThunderED.Classes;
using ThunderED.Modules;

namespace ThunderED.Json.Internal
{
    public class TimerItem: IIdentifiable
    {
        public long Id { get; set; }
        public int timerType { get; set; }
        public int timerStage { get; set; }
        public string timerLocation { get; set; }
        public string timerOwner { get; set; }
        public string timerET { get; set; }
        public string timerNotes { get; set; }
        public string timerChar { get; set; }
        public int announce { get; set; }
        public int timerRfDay;
        public int timerRfHour;
        public int timerRfMin;


        public string DisplayType => GetModeName();
        public string DisplayStage => GetStageName();

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
            if(!Date.HasValue) return null;
            var dif = (Date.Value - DateTime.UtcNow);
            return $"{(addWord ? $"{LM.Get("Remains")} " : null)}{LM.Get("timerRemains", dif.Days, dif.Hours, dif.Minutes)}";

        }

        public Dictionary<string, object> GetDictionary()
        {
            var dic =  new Dictionary<string, object>
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
            if(Id != 0)
                dic.Insert(nameof(Id), Id);
            return dic;
        }

        public static TimerItem FromWebTimerData(WebTimerData data, WebAuthUserData user)
        {
            var ti =  new TimerItem
            {
                timerLocation = data.Location,
                timerType = data.Type,
                timerStage = data.Stage,
                timerOwner = data.Owner,
                timerET = ((int)(data.Date.Subtract(new DateTime(1970, 1, 1))).TotalSeconds).ToString(),
                timerNotes = data.Notes,
                timerChar = user.Name,
                Id = data.Id,
            };

            ti.Date = ti.GetDateTime();
            return ti;
        }

        public DateTime? Date { get; set; }
    }
}
