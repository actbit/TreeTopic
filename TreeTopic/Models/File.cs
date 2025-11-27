using Finbuckle.MultiTenant;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models
{
    [MultiTenant]
    public class File : BaseModel
    {
        [ForeignKey(nameof(SourceFile))]
        public required Guid? SourceFileId { get; set; }
        public required File? SourceFile { get; set; }

        [ForeignKey(nameof(Message))]
        public Guid? MessageId { get; set; }
        public Message? Message { get; set; }
        public required string FileName { get; set; }
        public required string SaveFileName { get; set; }
        public required string FileType { get; set; }
        
        public bool IsLatast { get; set; } = true;
    }
}
