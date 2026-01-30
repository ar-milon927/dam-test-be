using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NinjaDAM.Entity.Entities
{

 
        public class Users : IdentityUser
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public bool IsApproved { get; set; } = false;
            public bool IsActive { get; set; } = false;
            public bool IsFirstLogin { get; set; } = true;
            public Guid? CompanyId { get; set; }
            public  Company? Company { get; set; }
            public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
            public ICollection<UserGroup> UserGroups { get; set; } = new List<UserGroup>();
        }
}


