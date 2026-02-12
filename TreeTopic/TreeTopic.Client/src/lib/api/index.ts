/**
 * API エクスポート
 */

export { api, assignUserRole, removeUserRole } from './client';
export {
  permissionsApi,
  userPermissionsApi,
  tenantRolePermissionsApi,
  roomRolePermissionsApi,
  topicPermissionsApi,
} from './permissions';
