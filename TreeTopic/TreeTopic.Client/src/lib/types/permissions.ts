/**
 * 権限定義
 */
export interface PermissionDefinition {
  name: string;
  scope: string;
}

/**
 * 利用可能な権限一覧（カテゴリ別）
 */
export interface AvailablePermissions {
  tenant: PermissionDefinition[];
  topic: PermissionDefinition[];
  room: PermissionDefinition[];
}

/**
 * ロール権限レスポンス
 */
export interface RolePermissionsResponse {
  roleName: string;
  permissions: string[];
}

/**
 * 権限割り当てリクエスト
 */
export interface AssignPermissionRequest {
  permissionName: string;
}

/**
 * ロール情報
 */
export interface Role {
  id: string;
  name: string;
  description?: string;
}

/**
 * RoomRole権限情報DTO
 */
export interface RoomRolePermissionDto {
  id: string;
  roomRoleId: string;
  roleName: string;
  permissionName: string;
}

/**
 * Topicユーザー権限情報DTO
 */
export interface TopicUserPermissionDto {
  id: string;
  topicId: string;
  roomUserId: string;
  userName: string | null;
  displayName: string | null;
  name: string;
}

/**
 * Topicロール権限情報DTO
 */
export interface TopicRolePermissionDto {
  id: string;
  topicId: string;
  roomRoleId: string;
  roleName: string | null;
  roleDescription: string | null;
  name: string;
}

/**
 * Topicユーザー権限割り当てリクエスト
 */
export interface AddTopicPermissionToUserRequest {
  roomUserId: string;
  permissionName: string;
  applyToDescendants?: boolean;
}

/**
 * Topicロール権限割り当てリクエスト
 */
export interface AddTopicRolePermissionRequest {
  roleName: string;
  permissionName: string;
  applyToDescendants?: boolean;
}

/**
 * ユーザー権限レスポンス（基本）
 */
export interface UserPermissionsResponse {
  permissions: string[];
}

/**
 * ルーム権限レスポンス
 */
export interface RoomPermissionsResponse extends UserPermissionsResponse {}

/**
 * トピック権限レスポンス
 */
export interface TopicPermissionsResponse extends UserPermissionsResponse {}
