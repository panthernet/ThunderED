using System.Collections.Generic;
using System.Linq;

namespace ThunderED.Thd
{
    public class ThdToken
    {
        public long Id { get; set; }
        public long CharacterId { get; set; }
        public string Token { get; set; }
        public TokenEnum Type { get; set; }
        public long? Roles { get; set; }
        public string Scopes { get; set; }

        public virtual ThdAuthUser User { get; set; }

        public List<string> GetSplitScopes()
        {
            return string.IsNullOrEmpty(Scopes) ? new List<string>() : Scopes.Split(',').ToList();
        }
    }
}
