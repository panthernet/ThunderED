using System;
using Newtonsoft.Json;
using ThunderED.Thd;

namespace ThunderED
{
    public static class EntityExtensions
    {
        public static void PackData(this ThdAuthUser user)
        {
            user.Data = JsonConvert.SerializeObject(user.DataView);
        }

        public static DateTime GetDateTime(this ThdTimerRf entry)
        {
            if (entry.IntDay != 0 || entry.IntHour != 0 || entry.IntMinute == 0)
            {
                var now = DateTime.UtcNow;
                return now.AddDays(entry.IntDay).AddHours(entry.IntHour).AddMinutes(entry.IntMinute);
            }
            return DateTime.MinValue;
        }
    }
}
