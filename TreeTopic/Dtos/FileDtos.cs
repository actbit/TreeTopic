using System.ComponentModel.DataAnnotations;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public class FileDto : BaseDto
{
    public MaskedGuid? SourceFileId { get; set; }

    public MaskedGuid? MessageId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string SaveFileName { get; set; } = string.Empty;

    // MIMEタイプ（例: image/png, application/pdf）
    public string FileType { get; set; } = string.Empty;

    // UI用の利便性フィールド（一部エンドポイントでは空の可能性あり）
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
