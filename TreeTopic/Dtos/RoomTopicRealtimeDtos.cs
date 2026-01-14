namespace TreeTopic.Dtos;

public record RoomRealtimeDto(
    string Id,
    string Name,
    string? CreatedUserId,
    string? CreatedUserName,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record RoomDeletedEvent(string RoomId);

public record TopicRealtimeDto(
    string Id,
    string RoomId,
    string? ParentId,
    string Title,
    string? Description,
    bool HasChildren,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record TopicDeletedEvent(string TopicId, string RoomId, string? ParentId);
