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
  roleId: string;
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
