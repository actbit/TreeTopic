using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models
{
    [MultiTenant]
    public class ShareItemFile : BaseModel
    {
        [ForeignKey(nameof(ShareItem))]
        public Guid ShareItemId { get; set; }
        public ShareItem ShareItem { get; set; }

        [ForeignKey(nameof(File))]
        public Guid FileId { get; set; }
        public File File { get; set; }

        public bool IsCurrent { get; set; } = true;
    }
}

