import type {
  AvailablePermissions,
  RolePermissionsResponse,
  AssignPermissionRequest,
  RoomRolePermissionDto,
  TopicUserPermissionDto,
  TopicRolePermissionDto,
  AddTopicPermissionToUserRequest,
  AddTopicRolePermissionRequest,
  RoomPermissionsResponse,
  TopicPermissionsResponse,
} from '$lib/types/permissions';
import { api } from './client';

/**
 * Permissions API
 * 利用可能な権限一覧の取得
 */
export const permissionsApi = {
  /**
   * 利用可能な全権限を取得
   */
  getAvailablePermissions: (tenant: string) =>
    api.get<AvailablePermissions>(`/${tenant}/api/permissions/available`),
};

/**
 * User Permissions API
 * 各リソースごとの現在のユーザー権限を取得
 */
export const userPermissionsApi = {
  /**
   * 現在のユーザーのテナント権限を取得
   * GET /{tenant}/api/tenant/my/permissions
   */
  getMyTenantPermissions: (tenant: string) =>
    api.get<{ permissions: string[] }>(`/${tenant}/api/tenant/my/permissions`),

  /**
   * 現在のユーザーのルーム権限を取得
   * GET /{tenant}/api/room/{roomId}/my/permissions
   */
  getMyRoomPermissions: (tenant: string, roomId: string) =>
    api.get<RoomPermissionsResponse>(`/${tenant}/api/room/${roomId}/my/permissions`),

  /**
   * 現在のユーザーのトピック権限を取得
   * GET /{tenant}/api/topic/{topicId}/my/permissions
   */
  getMyTopicPermissions: (tenant: string, topicId: string) =>
    api.get<TopicPermissionsResponse>(`/${tenant}/api/topic/${topicId}/my/permissions`),
};

/**
 * Tenant Role Permissions API
 */
export const tenantRolePermissionsApi = {
  /**
   * テナントロールの権限一覧を取得
   */
  getRolePermissions: (tenant: string, roleName: string) =>
    api.get<RolePermissionsResponse>(
      `/${tenant}/api/tenantroles/${roleName}/permissions`
    ),

  /**
   * テナントロールに権限を追加
   */
  addPermission: (
    tenant: string,
    roleName: string,
    request: AssignPermissionRequest
  ) =>
    api.post<{ permissionId: string; name: string }>(
      `/${tenant}/api/tenantroles/${roleName}/permissions`,
      request
    ),

  /**
   * テナントロールから権限を削除
   */
  removePermission: (tenant: string, roleName: string, permissionName: string) =>
    api.delete(
      `/${tenant}/api/tenantroles/${roleName}/permissions/${encodeURIComponent(permissionName)}`
    ),
};

/**
 * Room Role Permissions API
 */
export const roomRolePermissionsApi = {
  /**
   * ルームロールの権限一覧を取得
   */
  getRolePermissions: (tenant: string, roleName: string) =>
    api.get<RolePermissionsResponse>(
      `/${tenant}/api/roomroles/${roleName}/permissions`
    ),

  /**
   * ルームロールに権限を追加
   */
  addPermission: (
    tenant: string,
    roleName: string,
    request: AssignPermissionRequest
  ) =>
    api.post<{ permissionId: string; name: string }>(
      `/${tenant}/api/roomroles/${roleName}/permissions`,
      request
    ),

  /**
   * ルームロールから権限を削除
   */
  removePermission: (tenant: string, roleName: string, permissionName: string) =>
    api.delete(
      `/${tenant}/api/roomroles/${roleName}/permissions/${encodeURIComponent(permissionName)}`
    ),

  /**
   * ルームロールの権限をすべてクリア
   */
  clearPermissions: (tenant: string, roleName: string) =>
    api.delete(`/${tenant}/api/roomroles/${roleName}/permissions`),
};

/**
 * Topic Permissions API
 */
export const topicPermissionsApi = {
  /**
   * トピックのユーザー権限一覧を取得
   */
  getUserPermissions: (tenant: string, topicId: string) =>
    api.get<TopicUserPermissionDto[]>(
      `/${tenant}/api/topics/${topicId}/permissions/users`
    ),

  /**
   * 特定ユーザーのトピック権限を取得
   */
  getUserTopicPermissions: (tenant: string, topicId: string, roomUserId: string) =>
    api.get<{ topicId: string; roomUserId: string; permissions: string[] }>(
      `/${tenant}/api/topics/${topicId}/permissions/users/${roomUserId}`
    ),

  /**
   * トピックのユーザーに権限を追加
   */
  addUserPermission: (
    tenant: string,
    topicId: string,
    request: AddTopicPermissionToUserRequest
  ) =>
    api.post<{ permissionId: string; name: string }>(
      `/${tenant}/api/topics/${topicId}/permissions/users`,
      request
    ),

  /**
   * トピックのユーザーから権限を削除
   */
  removeUserPermission: (
    tenant: string,
    topicId: string,
    roomUserId: string,
    permissionName: string
  ) =>
    api.delete(
      `/${tenant}/api/topics/${topicId}/permissions/users/${roomUserId}/${encodeURIComponent(permissionName)}`
    ),

  /**
   * ユーザーのトピック権限をすべてクリア
   */
  clearUserPermissions: (tenant: string, topicId: string, roomUserId: string) =>
    api.delete(`/${tenant}/api/topics/${topicId}/permissions/users/${roomUserId}`),

  /**
   * トピックのロール権限一覧を取得
   */
  getRolePermissions: (tenant: string, topicId: string) =>
    api.get<TopicRolePermissionDto[]>(
      `/${tenant}/api/topics/${topicId}/permissions/role-permissions`
    ),

  /**
   * トピックのロールに権限を追加
   */
  addRolePermission: (
    tenant: string,
    topicId: string,
    request: AddTopicRolePermissionRequest
  ) =>
    api.post<{ permissionId: string; name: string }>(
      `/${tenant}/api/topics/${topicId}/permissions/role-permissions`,
      request
    ),

  /**
   * トピックのロールから権限を削除
   */
  removeRolePermission: (
    tenant: string,
    topicId: string,
    roleName: string,
    permissionName: string
  ) =>
    api.delete(
      `/${tenant}/api/topics/${topicId}/permissions/role-permissions/${roleName}/${encodeURIComponent(permissionName)}`
    ),

  /**
   * トピックの全ロール権限をクリア
   */
  clearRolePermissions: (tenant: string, topicId: string) =>
    api.delete(`/${tenant}/api/topics/${topicId}/permissions/role-permissions`),
};
