using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public record MessageDeletedEvent(MaskedGuid MessageId, MaskedGuid TopicId);

public class MessageRealtimeDto
{
    public MaskedGuid Id { get; set; }
    public MaskedGuid TopicId { get; set; }
    public MaskedGuid RoomUserId { get; set; }
    public string? UserName { get; set; }
    public string? UserAvatar { get; set; }
    public string Header { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public MaskedGuid? ReplyId { get; set; }
    public MaskedGuid? ChildTopicId { get; set; }
    public string? ChildTopicTitle { get; set; }
    public List<FileRealtimeDto>? Files { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class FileRealtimeDto
{
    public MaskedGuid Id { get; set; }
    public MaskedGuid? SourceFileId { get; set; }
    public MaskedGuid? MessageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string SaveFileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Url { get; set; } = string.Empty;
    public bool IsLatest { get; set; }
    public MaskedGuid? UploadedBy { get; set; }
    public string? UploadedByName { get; set; }
    public List<FileRealtimeDto>? Versions { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
