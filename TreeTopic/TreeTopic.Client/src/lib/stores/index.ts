// Export all stores from a single entry point

// Authentication store
export { auth, currentUser, isAuthenticated, isLoading, authError, userRoles, hasRole } from './auth';
export type { User, AuthContext } from './auth';

// Tenant store
export {
  tenant,
  currentTenant,
  tenantList,
  tenantLoading,
  tenantError,
  getTenantByIdentifier,
  isTenantMember,
} from './tenant';
export type { Tenant, TenantState } from './tenant';

// Rooms store
export {
  rooms,
  roomList,
  currentRoom,
  selectedRoomId,
  roomsLoading,
  roomsError,
  getRoomById,
  unreadRooms,
  totalUnreadCount,
  activeRooms,
  archivedRooms,
} from './rooms';
export type { Room, RoomMember, RoomsState } from './rooms';

// Topics store
export {
  topics,
  topicList,
  selectedTopic,
  topicsLoading,
  topicsError,
  expandedTopics,
  getTopicById,
  topicTree,
  unreadTopics,
  totalTopicUnreadCount,
  writableTopics,
  getChildTopics,
  getParentTopic,
} from './topics';
export type { Topic, TopicPermission, PermissionLevel, TopicsState } from './topics';

// Messages store
export {
  messages,
  messageList,
  messagesLoading,
  messagesError,
  currentTopicId,
  getMessagesByTopic,
  messagesGroupedByTopic,
  getThreadedMessages,
  getMessageById,
  unreadMessagesCount,
  recentMessages,
} from './messages';
export type { Message, Attachment, MessagesState } from './messages';

// UI store
export {
  ui,
  viewMode,
  sidebarCollapsed,
  subpanelCollapsed,
  activeModals,
  notifications,
  isDragDropActive,
  dragState,
  contextMenuOpen,
  contextMenuPosition,
  selectedItems,
  isMobile,
  isTablet,
  isDesktop,
  hasActiveModals,
  notificationsCount,
  selectionCount,
} from './ui';
export type { UIStateData } from './ui';

// Files store
export {
  files,
  fileList,
  filesLoading,
  filesError,
  uploads,
  imageFiles,
  pdfFiles,
  documentFiles,
  getFilesByRoom,
  getFilesByMessage,
  getFileById,
  getUploadProgress,
  activeUploads,
  uploadProgress,
} from './files';
export type { Material, FileVersion, FileUploadProgress, FilesState } from './files';

// Brainstorm store
export {
  brainstorm,
  boardList,
  currentBoard,
  brainstormLoading,
  brainstormError,
  editingIdeaId,
  getBoardById,
  currentBoardIdeas,
  getBoardIdeasCount,
} from './brainstorm';
export type { BrainstormBoard, BrainIdea, BrainIdeaVote, BrainstormState } from './brainstorm';
