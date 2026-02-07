using System.ComponentModel.DataAnnotations;

namespace TreeTopic.Dtos;

/// <summary>
/// Room権限割り当てリクエスト
/// </summary>
public record AddRoomPermissionRequest(string PermissionName);

/// <summary>
/// Roomユーザー権限割り当てリクエスト
/// </summary>
public record AddRoomUserPermissionRequest(string PermissionName);

/// <summary>
/// Tenant権限割り当てリクエスト
/// </summary>
public record AddTenantPermissionRequest(string PermissionName);

/// <summary>
/// Topicユーザー権限割り当てリクエスト
/// </summary>
public record AddTopicPermissionToUserRequest(Guid RoomUserId, string PermissionName, bool ApplyToDescendants = false);

/// <summary>
/// TopicRolePermission割り当てリクエスト
/// </summary>
public record AddTopicRolePermissionRequest(string RoleName, string PermissionName, bool ApplyToDescendants = false);

/// <summary>
/// ユーザーロール一括設定リクエスト
/// </summary>
public record SetUserRolesRequest(List<string> RoleNames);
