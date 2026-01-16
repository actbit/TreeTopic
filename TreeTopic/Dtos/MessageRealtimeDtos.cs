namespace TreeTopic.Dtos;

public record MessageDeletedEvent(string MessageId, string TopicId);

public class MessageRealtimeDto
{
    public string Id { get; set; } = string.Empty;
    public string TopicId { get; set; } = string.Empty;
    public string RoomUserId { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? UserAvatar { get; set; }
    public string Header { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ReplyId { get; set; }
    public string? ChildTopicId { get; set; }
    public string? ChildTopicTitle { get; set; }
    public List<FileRealtimeDto>? Files { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class FileRealtimeDto
{
    public string Id { get; set; } = string.Empty;
    public string? SourceFileId { get; set; }
    public string? MessageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string SaveFileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Url { get; set; } = string.Empty;
    public bool IsLatest { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
