using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace ThunderED.Classes.Entities
{
    public class SimplifiedAuthEntity: IIdentifiable
    {
        public long Id { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string Group { get; set; }
        [Required]
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
