
export interface PermissionDefinition {
  name: string;
  scope: string;
}

export interface AvailablePermissions {
  tenant: PermissionDefinition[];
  topic: PermissionDefinition[];
  room: PermissionDefinition[];
}

export interface RolePermissionsResponse {
  roleName: string;
  permissions: string[];
}

export interface AssignPermissionRequest {
  permissionName: string;
}

export interface Role {
  id: string;
  name: string;
  description?: string;
}

export interface RoomRolePermissionDto {
  id: string;
  roomRoleId: string;
  roleName: string;
  permissionName: string;
}

export interface TopicUserPermissionDto {
  id: string;
  topicId: string;
  roomUserId: string;
  userName: string | null;
  displayName: string | null;
  name: string;
}

export interface TopicRolePermissionDto {
  id: string;
  topicId: string;
  roomRoleId: string;
  roleName: string | null;
  roleDescription: string | null;
  name: string;
}

export interface AddTopicPermissionToUserRequest {
  roomUserId: string;
  permissionName: string;
  applyToDescendants?: boolean;
}

export interface AddTopicRolePermissionRequest {
  roleName: string;
  permissionName: string;
  applyToDescendants?: boolean;
}

export interface UserPermissionsResponse {
  permissions: string[];
}

export interface RoomPermissionsResponse extends UserPermissionsResponse {}

export interface TopicPermissionsResponse extends UserPermissionsResponse {}
