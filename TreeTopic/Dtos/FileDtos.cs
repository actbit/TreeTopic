using System.ComponentModel.DataAnnotations;
using MaskedUUID.AspNetCore.Attributes;

namespace TreeTopic.Dtos;

public class FileDto : BaseDto
{
    [MaskedUUID]
    public Guid? SourceFileId { get; set; }

    [MaskedUUID]
    public Guid? MessageId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string SaveFileName { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public bool IsLatest { get; set; }

    public List<FileDto>? VersionedFiles { get; set; }
}

public class CreateFileRequest : BaseCreateRequest
{
    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string SaveFileName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string FileType { get; set; } = string.Empty;

    [MaskedUUID]
    public Guid? SourceFileId { get; set; }

    [MaskedUUID]
    public Guid? MessageId { get; set; }
}

public class UpdateFileRequest : BaseUpdateRequest
{
    [StringLength(255)]
    public string? FileName { get; set; }

    [StringLength(100)]
    public string? FileType { get; set; }
}
