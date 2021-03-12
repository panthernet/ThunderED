using System.Collections.Generic;
using System.Linq;

namespace ThunderED
{
    public class SimplifiedAuthEntity: IIdentifiable
    {
        public long Id { get; set; }
        [Classes.Required]
        public string Name { get; set; }
        [Classes.Required]
        public string Group { get; set; }
        [Classes.Required]
        public IEnumerable<string> RolesList;// = new List<string>();
        public string Roles { get; set; }

        public bool Validate()
        {
            return !string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(Group) &&
                   (!string.IsNullOrEmpty(Roles) || RolesList.Any());
        }

        public void UpdateFrom(SimplifiedAuthEntity value)
        {
            if (value != null)
            {
                Name = value.Name;
                Group = value.Group;
                Roles = value.Roles;
                RolesList = value.RolesList;
            }
        }

        public SimplifiedAuthEntity Clone()
        {
            return new SimplifiedAuthEntity
            {
                Id = Id,
                Group = Group,
                RolesList = RolesList,
                Roles = Roles,
                Name = Name
            };
        }
    }
}
