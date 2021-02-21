using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Json;
using ThunderED.Json.Internal;
using ThunderED.Json.ZKill;
using ThunderED.Thd;

namespace ThunderED.Helpers
{
    public static class EntityExtensions
    {
        

        public static void PackData(this ThdAuthUser user)
        {
            user.Data = JsonConvert.SerializeObject(user.DataView);
        }

        

        public static DateTime? GetDateTime(this TimerItem entry)
        {
            if (int.TryParse(entry.timerET, out var iValue))
            {
                var x = DateTimeOffset.FromUnixTimeSeconds(iValue).UtcDateTime;
                return x;
            }

            if (DateTime.TryParse(entry.timerET, out var result)) return result;
            if (!string.IsNullOrEmpty(SettingsManager.Settings.TimersModule.TimeInputFormat))
            {
                var format = SettingsManager.Settings.TimersModule.TimeInputFormat.Replace("D", "d").Replace("Y", "y");
                if (DateTime.TryParseExact(entry.timerET, format, null, DateTimeStyles.None, out result))
                    return result;
            }

            if (entry.timerRfDay == 0 && entry.timerRfHour == 0 && entry.timerRfMin == 0) return null;
            var now = DateTime.UtcNow;
            return now.AddDays(entry.timerRfDay).AddHours(entry.timerRfHour).AddMinutes(entry.timerRfMin);
        }

       

        public static Dictionary<string, object> GetDictionary(this TimerItem entry)
        {
            var dic = new Dictionary<string, object>
            {
                {nameof(entry.timerType), entry.timerType},
                {nameof(entry.timerStage), entry.timerStage},
                {nameof(entry.timerLocation), entry.timerLocation},
                {nameof(entry.timerOwner), entry.timerOwner},
                {nameof(entry.timerET), entry.GetDateTime()},
                {nameof(entry.timerNotes), entry.timerNotes},
                {nameof(entry.timerChar), entry.timerChar},
                {nameof(entry.announce), entry.announce},
            };
            if (entry.Id != 0)
                dic.Add("id", entry.Id);
            return dic;
        }

        
    }
}
