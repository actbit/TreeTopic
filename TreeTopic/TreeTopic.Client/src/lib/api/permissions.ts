import type {
  AvailablePermissions,
  RolePermissionsResponse,
  AssignPermissionRequest,
} from '$lib/types/permissions';
import { api } from './client';

/**
 * Permissions API
 */
export const permissionsApi = {
  /**
   * 利用可能な全権限を取得
   */
  getAvailablePermissions: (tenant: string) =>
    api.get<AvailablePermissions>(`/${tenant}/api/permissions/available`),
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
    api.post(`/${tenant}/api/tenantroles/${roleName}/permissions`, request),

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
    api.post(`/${tenant}/api/roomroles/${roleName}/permissions`, request),

  /**
   * ルームロールから権限を削除
   */
  removePermission: (tenant: string, roleName: string, permissionName: string) =>
    api.delete(
      `/${tenant}/api/roomroles/${roleName}/permissions/${encodeURIComponent(permissionName)}`
    ),
};

/**
 * Topic Permissions API
 */
export const topicPermissionsApi = {
  /**
   * トピックのユーザー権限一覧を取得
   */
  getUserPermissions: (tenant: string, topicId: string) =>
    api.get<any[]>(`/${tenant}/api/topics/${topicId}/permissions/users`),

  /**
   * トピックのロール権限一覧を取得
   */
  getRolePermissions: (tenant: string, topicId: string) =>
    api.get<any[]>(`/${tenant}/api/topics/${topicId}/permissions/role-permissions`),

  /**
   * トピックのユーザーに権限を追加
   */
  addUserPermission: (
    tenant: string,
    topicId: string,
    request: { roomUserId: string; permissionName: string }
  ) =>
    api.post(`/${tenant}/api/topics/${topicId}/permissions/users`, request),

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
   * トピックのロールに権限を追加
   */
  addRolePermission: (
    tenant: string,
    topicId: string,
    request: { roleName: string; permissionName: string }
  ) =>
    api.post(`/${tenant}/api/topics/${topicId}/permissions/role-permissions`, request),

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
};
