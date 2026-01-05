/* UI-specific type definitions */

/**
 * Available view modes for displaying messages and topics
 */
export type ViewMode =
  | 'default'      // Topic tree with messages
  | 'timeline'     // Chronological message view
  | 'user'         // Grouped by user
  | 'document'     // Grouped by document/attachment
  | 'image'        // Gallery view of images
  | 'search'       // Search results
  | 'topic';       // Messages by topic

/**
 * UI state management
 */
export interface UIState {
  viewMode: ViewMode;
  sidebarCollapsed: boolean;
  subpanelCollapsed: boolean;
  activeModals: string[];
  showContextMenu: boolean;
  contextMenuPosition: { x: number; y: number } | null;
  selectedItems: Set<string>;
}

/**
 * Drag and drop state
 */
export interface DragState {
  isDragging: boolean;
  draggedItemId: string | null;
  draggedItemType: 'message' | 'topic' | 'idea' | null;
  dropTarget: string | null;
  dropTargetType: 'message' | 'topic' | 'idea' | null;
  offset: { x: number; y: number } | null;
}

/**
 * Modal configuration
 */
export interface ModalConfig {
  id: string;
  title: string;
  type: 'alert' | 'confirm' | 'prompt' | 'custom';
  message?: string;
  onConfirm?: () => void | Promise<void>;
  onCancel?: () => void | Promise<void>;
  isLoading?: boolean;
  isDangerous?: boolean; // For delete confirmations
}

/**
 * Context menu item configuration
 */
export interface ContextMenuItem {
  id: string;
  label: string;
  icon?: string;
  action: () => void | Promise<void>;
  isDangerous?: boolean;
  isVisible?: boolean;
  isDisabled?: boolean;
  subItems?: ContextMenuItem[];
}

/**
 * Pagination state
 */
export interface PaginationState {
  currentPage: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

/**
 * Sort configuration
 */
export interface SortConfig {
  field: string;
  direction: 'asc' | 'desc';
}

/**
 * Filter configuration
 */
export interface FilterConfig {
  [key: string]: string | number | boolean | null;
}

/**
 * Search state
 */
export interface SearchState {
  query: string;
  filters: FilterConfig;
  sort: SortConfig;
  pagination: PaginationState;
  isLoading: boolean;
  results: MessageViewItem[];
  error: string | null;
}

/**
 * Notification state
 */
export interface Notification {
  id: string;
  type: 'success' | 'error' | 'warning' | 'info';
  message: string;
  duration?: number; // milliseconds, 0 = persistent
  action?: {
    label: string;
    handler: () => void;
  };
  createdAt: number;
}

/**
 * Loading state
 */
export interface LoadingState {
  isLoading: boolean;
  error: Error | null;
  progress?: number; // 0-100 for multi-step operations
}

/**
 * Form state
 */
export interface FormState<T> {
  values: T;
  errors: Record<keyof T, string | null>;
  touched: Record<keyof T, boolean>;
  isDirty: boolean;
  isValid: boolean;
  isSubmitting: boolean;
  submitError: string | null;
}

/**
 * Topic tree node for hierarchical display
 */
export interface TopicTreeNode {
  id: string;
  title: string;
  level: number;
  parentId: string | null;
  children: TopicTreeNode[];
  unreadCount: number;
  isSelected: boolean;
  isExpanded: boolean;
  hasChildren: boolean;
  canRead: boolean;
  canWrite: boolean;
  canDelete: boolean;
  canManagePermissions: boolean;
}

/**
 * Message view item with computed properties
 */
export interface MessageViewItem {
  id: string;
  userId: string;
  userName: string;
  userAvatar?: string;
  topicId: string;
  subject: string;
  content: string;
  createdAt: Date;
  updatedAt?: Date;
  replyTo?: string; // parent message ID
  attachments: AttachmentView[];
  reactions: { emoji: string; userIds: string[] }[];
  isOwner: boolean;
  canEdit: boolean;
  canDelete: boolean;
}

/**
 * Attachment view with metadata
 */
export interface AttachmentView {
  id: string;
  fileName: string;
  fileType: 'image' | 'pdf' | 'document' | 'other';
  fileSize: number;
  url: string;
  preview?: string;
  uploadedAt: Date;
  uploadedBy: string;
}

/**
 * Brainstorm idea card
 */
export interface IdeaCardView {
  id: string;
  boardId: string;
  text: string;
  x: number;
  y: number;
  width?: number;
  height?: number;
  userId?: string;
  userName?: string;
  isAnonymous: boolean;
  votes: {
    circle: number;
    square: number;
    triangle: number;
    cross: number;
  };
  userVotes: {
    circle: boolean;
    square: boolean;
    triangle: boolean;
    cross: boolean;
  };
  linkedMessageId?: string;
  color?: string;
  createdAt: Date;
  isEditing: boolean;
}

/**
 * Room view for display
 */
export interface RoomView {
  id: string;
  name: string;
  description?: string;
  avatar?: string;
  memberCount: number;
  unreadCount: number;
  isSelected: boolean;
  lastMessageAt?: Date;
  canEdit: boolean;
  canDelete: boolean;
  canManageMembers: boolean;
}

/**
 * User presence status
 */
export type PresenceStatus = 'online' | 'away' | 'offline' | 'idle';

/**
 * User presence view
 */
export interface UserPresence {
  userId: string;
  userName: string;
  status: PresenceStatus;
  lastSeenAt: Date;
  currentActivity?: string;
}

/**
 * Keyboard shortcut configuration
 */
export interface KeyboardShortcut {
  key: string;
  ctrl?: boolean;
  shift?: boolean;
  alt?: boolean;
  meta?: boolean;
  action: () => void;
  description: string;
}

/**
 * Breadcrumb item for navigation
 */
export interface BreadcrumbItem {
  label: string;
  href?: string;
  onClick?: () => void;
}

/**
 * Permission display information
 */
export interface PermissionInfo {
  roleId: string;
  roleName: string;
  canRead: boolean;
  canWrite: boolean;
  canDelete: boolean;
  canManagePermissions: boolean;
  description?: string;
}

/**
 * File upload progress
 */
export interface FileUploadProgress {
  fileId: string;
  fileName: string;
  progress: number; // 0-100
  status: 'pending' | 'uploading' | 'completed' | 'failed';
  error?: string;
  size: number;
  uploadedBytes: number;
}

/**
 * Viewport dimensions
 */
export interface ViewportSize {
  width: number;
  height: number;
  isMobile: boolean;
  isTablet: boolean;
  isDesktop: boolean;
}

/**
 * Animation state for components
 */
export interface AnimationState {
  isAnimating: boolean;
  animationType?: 'slideIn' | 'fadeIn' | 'scaleIn';
  duration?: number;
}
