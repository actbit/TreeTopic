
export type ViewMode =
  | 'default'
  | 'user'
  | 'document'
  | 'image'
  | 'topic'
  | 'search';


export interface UIState {
  viewMode: ViewMode;
  sidebarCollapsed: boolean;
  subpanelCollapsed: boolean;
  activeModals: string[];
  showContextMenu: boolean;
  contextMenuPosition: { x: number; y: number } | null;
  selectedItems: Set<string>;
}


export interface DragState {
  isDragging: boolean;
  draggedItemId: string | null;
  draggedItemType: 'message' | 'topic' | 'idea' | null;
  dropTarget: string | null;
  dropTargetType: 'message' | 'topic' | 'idea' | null;
  offset: { x: number; y: number } | null;
}

export interface ModalConfig {
  id: string;
  title: string;
  type: 'alert' | 'confirm' | 'prompt' | 'custom';
  message?: string;
  data?: Record<string, unknown>;
  onConfirm?: () => void | Promise<void>;
  onCancel?: () => void | Promise<void>;
  isLoading?: boolean;
  isDangerous?: boolean; 
}

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


export interface PaginationState {
  currentPage: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface SortConfig {
  field: string;
  direction: 'asc' | 'desc';
}

export interface FilterConfig {
  [key: string]: string | number | boolean | null;
}

export interface SearchState {
  query: string;
  filters: FilterConfig;
  sort: SortConfig;
  pagination: PaginationState;
  isLoading: boolean;
  results: MessageViewItem[];
  error: string | null;
}

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

export interface LoadingState {
  isLoading: boolean;
  error: Error | null;
  progress?: number; // 0-100 for multi-step operations
}

export interface FormState<T> {
  values: T;
  errors: Record<keyof T, string | null>;
  touched: Record<keyof T, boolean>;
  isDirty: boolean;
  isValid: boolean;
  isSubmitting: boolean;
  submitError: string | null;
}

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

export type PresenceStatus = 'online' | 'away' | 'offline' | 'idle';

export interface UserPresence {
  userId: string;
  userName: string;
  status: PresenceStatus;
  lastSeenAt: Date;
  currentActivity?: string;
}

export interface KeyboardShortcut {
  key: string;
  ctrl?: boolean;
  shift?: boolean;
  alt?: boolean;
  meta?: boolean;
  action: () => void;
  description: string;
}

export interface BreadcrumbItem {
  label: string;
  href?: string;
  onClick?: () => void;
}

export interface PermissionInfo {
  roleId: string;
  roleName: string;
  canRead: boolean;
  canWrite: boolean;
  canDelete: boolean;
  canManagePermissions: boolean;
  description?: string;
}

export interface ViewportSize {
  width: number;
  height: number;
  isMobile: boolean;
  isTablet: boolean;
  isDesktop: boolean;
}

export interface AnimationState {
  isAnimating: boolean;
  animationType?: 'slideIn' | 'fadeIn' | 'scaleIn';
  duration?: number;
}

export interface ApplicationUser {
  id: string;
  tenantId: string;
  userName: string;
  normalizedUserName: string;
  email: string;
  normalizedEmail: string;
  emailConfirmed: boolean;
  passwordHash: string;
  securityStamp: string;
  concurrencyStamp: string;
  phoneNumber: string | null;
  phoneNumberConfirmed: boolean;
  twoFactorEnabled: boolean;
  lockoutEnd: Date | null;
  lockoutEnabled: boolean;
  accessFailedCount: number;
  displayName: string;
  iconFileName: string | null;
  sub: string | null;
}

export interface ApplicationUserDto {
  id: string;
  userName: string;
  email: string;
  displayName: string;
  iconFileName: string | null;
}
