using System;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using ThunderED.Classes.Entities;

namespace ThunderED.Thd
{
    public class ThdAuthUser
    {
        public long Id;
        public long CharacterId;
        public ulong DiscordId;
        public string GroupName;
        public string RefreshToken;
        public int AuthState;
        public string Data;
        public string RegCode;
        public DateTime CreateDate;
        public DateTime? DumpDate;
        public long? MainCharacterId;
        public DateTime? LastCheck;
        public string Ip;

        [NotMapped] public AuthUserData DataView = new AuthUserData();
        [NotMapped] public MiscUserData MiscData = new MiscUserData();
        [NotMapped] public string CharacterName => DataView?.CharacterName;

        [NotMapped] public bool HasToken => !string.IsNullOrEmpty(RefreshToken);

        public void UnpackData()
        {
            if(string.IsNullOrEmpty(Data)) return;
            DataView = JsonConvert.DeserializeObject<AuthUserData>(Data);
        }
    }
}
