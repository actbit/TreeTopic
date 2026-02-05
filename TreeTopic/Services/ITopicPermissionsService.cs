using TreeTopic.Common;

namespace TreeTopic.Services;

/// <summary>
/// Topic権限管理サービスインターフェース
/// </summary>
public interface ITopicPermissionsService
{
    /// <summary>
    /// Topicのユーザー権限一覧を取得
    /// </summary>
    Task<Result<List<TopicUserPermissionDto>>> GetTopicUserPermissionsAsync(
        Guid topicId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 特定ユーザーのTopic権限を取得
    /// </summary>
    Task<Result<List<string>>> GetUserTopicPermissionsAsync(
        Guid topicId,
        Guid roomUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ユーザーにTopic権限を割り当て
    /// </summary>
    Task<Result<TopicUserPermissionDto>> AddPermissionToUserAsync(
        Guid topicId,
        Guid roomUserId,
        string permissionName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ユーザーからTopic権限を削除
    /// </summary>
    Task<Result> RemovePermissionFromUserAsync(
        Guid topicId,
        Guid roomUserId,
        string permissionName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ユーザーのTopic権限をクリア
    /// </summary>
    Task<Result> ClearUserPermissionsAsync(
        Guid topicId,
        Guid roomUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// TopicのRoomRole権限一覧を取得
    /// </summary>
    Task<Result<List<TopicRolePermissionDto>>> GetTopicRolePermissionsAsync(
        Guid topicId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// TopicにRoomRole権限を割り当て
    /// </summary>
    Task<Result<TopicRolePermissionDto>> AddTopicRolePermissionAsync(
        Guid topicId,
        string roleName,
        string permissionName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// TopicからRoomRole権限を削除
    /// </summary>
    Task<Result> RemoveTopicRolePermissionAsync(
        Guid topicId,
        string roleName,
        string permissionName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Topicの全RoomRole権限をクリア
    /// </summary>
    Task<Result> ClearRolePermissionsAsync(
        Guid topicId,
        CancellationToken cancellationToken = default);
}