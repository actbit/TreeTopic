namespace TreeTopic.Services;

/// <summary>
/// 共有アイテム情報DTO
/// </summary>
public record ShareItemDto(
    string Id,
    string RoomId,
    string? TopicId,
    string Kind,
    string? BoardId,
    string Title,
    string FileName,
    string MimeType,
    long Size,
    string Url,
    DateTime CreatedAt,
    ShareItemUserDto? CreatedByUser,
    string CreatedByName,
    ShareItemMessageDto? SourceMessage,
    ShareItemFileDto? SourceFile,
    ShareItemShareDto? SourceShareItem);

/// <summary>
/// 共有アイテム作成ユーザー情報DTO
/// </summary>
public record ShareItemUserDto(
    string Id,
    string? Name,
    string? DisplayName);

/// <summary>
/// 共有アイテム元メッセージ情報DTO
/// </summary>
public record ShareItemMessageDto(
    string Id,
    string Header);

/// <summary>
/// 共有アイテム元ファイル情報DTO
/// </summary>
public record ShareItemFileDto(
    string Id,
    string FileName);

/// <summary>
/// 共有アイテム元共有アイテム情報DTO
/// </summary>
public record ShareItemShareDto(
    string Id,
    string Title);