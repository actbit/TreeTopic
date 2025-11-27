using Finbuckle.MultiTenant;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models
{
    [MultiTenant]
    public class Permission:BaseModel
    {
        [ForeignKey(nameof(Role))]
        public Guid RoleId { get; set; }
        public ApplicationRole Role { get; set; }
    }
}
