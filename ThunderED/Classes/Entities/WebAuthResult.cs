using System.Collections.Generic;

namespace ThunderED.Classes.Entities
{
    public class WebAuthResult
    {
        public string GroupName;
        public WebAuthGroup Group;
        public List<AuthRoleEntity> RoleEntities = new List<AuthRoleEntity>();
    }
}
