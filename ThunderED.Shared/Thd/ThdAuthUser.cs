using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Newtonsoft.Json;

namespace ThunderED.Thd
{
    public class ThdAuthUser
    {
        public long Id;
        public long CharacterId;
        public ulong? DiscordId;
        public string GroupName;
        public int AuthState;
        public string Data;
        public string RegCode;
        public DateTime? CreateDate;
        public DateTime? DumpDate;
        public long? MainCharacterId;
        public DateTime? LastCheck;
        public string Ip;
        //[Obsolete]
        //public string RefreshToken { get; set; }

        [NotMapped] public AuthUserData DataView = new AuthUserData();
        [NotMapped] public MiscUserData MiscData = new MiscUserData();
        [NotMapped] public string CharacterName => DataView?.CharacterName;

        [NotMapped] public bool HasToken => Tokens?.FirstOrDefault(a => a.Type == TokenEnum.General) != null;

        public void UnpackData()
        {
            if(string.IsNullOrEmpty(Data)) return;
            DataView = JsonConvert.DeserializeObject<AuthUserData>(Data);
        }

        public List<ThdToken> Tokens { get; set; }


        public string GetGeneralTokenString()
        {
            return Tokens?.FirstOrDefault(a => a.Type == TokenEnum.General)?.Token;
        }

        public ThdToken GetGeneralToken()
        {
            return Tokens?.FirstOrDefault(a => a.Type == TokenEnum.General);
        }

        public void SetStateDumpster()
        {
            AuthState = 3;
            DumpDate = DateTime.Now;
        }

        public void SetStateSpying()
        {
            AuthState = 4;
            DumpDate = null;
        }

        public void SetStateAwaiting()
        {
            AuthState = 1;
            DumpDate = null;
        }

        public void SetStateAuthed()
        {
            AuthState = 2;
            DumpDate = null;
        }
    }
}
