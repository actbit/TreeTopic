using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public record RoomRealtimeDto(
    MaskedGuid Id,
    string Name,
    string? Description,
    int JoinPolicy,
    MaskedGuid? CreatedUserId,
    string? CreatedUserName,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record RoomDeletedEvent(MaskedGuid RoomId);

public record TopicRealtimeDto(
    MaskedGuid Id,
    MaskedGuid RoomId,
    MaskedGuid? ParentId,
    string Title,
    string? Description,
    bool HasChildren,
    MaskedGuid? SourceMessageId,
    int UnreadCount,
    int MessageCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record TopicDeletedEvent(MaskedGuid TopicId, MaskedGuid RoomId, MaskedGuid? ParentId);
