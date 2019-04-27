using System.Collections.Generic;
using Newtonsoft.Json;

namespace ThunderED.Classes.Entities
{
    public class SimplifiedAuthEntity
    {
        public int Id;
        public string Name;
        public string Group;
        [JsonIgnore]
        public List<string> RolesList = new List<string>();
        public string Roles;
    }
}
