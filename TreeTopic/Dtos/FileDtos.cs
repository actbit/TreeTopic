using System.ComponentModel.DataAnnotations;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public class FileDto : BaseDto
{
    public MaskedGuid? SourceFileId { get; set; }

    public MaskedGuid? MessageId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string SaveFileName { get; set; } = string.Empty;

    // MIME type (e.g. image/png, application/pdf)
    public string FileType { get; set; } = string.Empty;

    // Convenience fields for UI (may be empty for some endpoints)
    public long Size { get; set; }

    public string Url { get; set; } = string.Empty;

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

    public MaskedGuid? SourceFileId { get; set; }

    public MaskedGuid? MessageId { get; set; }
}

public class UpdateFileRequest : BaseUpdateRequest
{
    [StringLength(255)]
    public string? FileName { get; set; }

    [StringLength(100)]
    public string? FileType { get; set; }
}
