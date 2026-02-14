
export interface RawRoom {
  id?: string;
  Id?: string;
  name?: string;
  Name?: string;
  description?: string;
  Description?: string;
  joinPolicy?: number;
  JoinPolicy?: number;
  avatar?: string;
  Avatar?: string;
  createdAt?: string;
  CreatedAt?: string;
  updatedAt?: string;
  UpdatedAt?: string;
  ownerId?: string;
  OwnerId?: string;
  createdUserId?: string;
  CreatedUserId?: string;
  memberCount?: number;
  MemberCount?: number;
  unreadCount?: number;
  UnreadCount?: number;
  isArchived?: boolean;
  IsArchived?: boolean;
  canEdit?: boolean;
  CanEdit?: boolean;
  canDelete?: boolean;
  CanDelete?: boolean;
  canJoin?: boolean;
  CanJoin?: boolean;
  isJoined?: boolean;
  IsJoined?: boolean;
  settings?: Record<string, unknown>;
  Settings?: Record<string, unknown>;
  [key: string]: unknown;
}

export interface RawTopic {
  id?: string;
  Id?: string;
  roomId?: string;
  RoomId?: string;
  title?: string;
  Title?: string;
  description?: string;
  Description?: string;
  parentId?: string;
  ParentId?: string;
  sourceMessageId?: string;
  SourceMessageId?: string;
  childIds?: string[];
  ChildIds?: string[];
  createdAt?: string;
  CreatedAt?: string;
  updatedAt?: string;
  UpdatedAt?: string;
  creatorId?: string;
  CreatorId?: string;
  messageCount?: number;
  MessageCount?: number;
  unreadCount?: number;
  UnreadCount?: number;
  userPermission?: string;
  UserPermission?: string;
  permissions?: string[];
  Permissions?: string[];
  hasChildren?: boolean;
  HasChildren?: boolean;
  [key: string]: unknown;
}

export interface RawMaterial {
  id?: string;
  Id?: string;
  roomId?: string;
  RoomId?: string;
  messageId?: string;
  MessageId?: string;
  fileName?: string;
  FileName?: string;
  originalFileName?: string;
  OriginalFileName?: string;
  mimeType?: string;
  MimeType?: string;
  fileType?: string;
  FileType?: string;
  size?: number;
  Size?: number;
  url?: string;
  Url?: string;
  uploadedAt?: string;
  UploadedAt?: string;
  createdAt?: string;
  CreatedAt?: string;
  uploadedBy?: string;
  UploadedBy?: string;
  uploadedByName?: string;
  UploadedByName?: string;
  versions?: RawMaterial[];
  Versions?: RawMaterial[];
  [key: string]: unknown;
}

export interface RawRoomUser {
  id?: string;
  Id?: string;
  displayName?: string;
  DisplayName?: string;
  iconUrl?: string;
  IconUrl?: string;
  useMainIcon?: boolean;
  UseMainIcon?: boolean;
  [key: string]: unknown;
}

export interface RawMessage {
  id?: string;
  Id?: string;
  topicId?: string;
  TopicId?: string;
  content?: string;
  Content?: string;
  body?: string;
  Body?: string;
  header?: string;
  Header?: string;
  subject?: string;
  Subject?: string;
  createdAt?: string;
  CreatedAt?: string;
  updatedAt?: string;
  UpdatedAt?: string;
  roomUserId?: string;
  RoomUserId?: string;
  applicationUserId?: string;
  ApplicationUserId?: string;
  userId?: string;
  UserId?: string;
  userName?: string;
  UserName?: string;
  userDisplayName?: string;
  UserDisplayName?: string;
  userAvatar?: string;
  UserAvatar?: string;
  parentId?: string;
  ParentId?: string;
  replyId?: string;
  ReplyId?: string;
  replyToId?: string;
  ReplyToId?: string;
  childTopicId?: string;
  ChildTopicId?: string;
  childTopicTitle?: string;
  ChildTopicTitle?: string;
  sortOrder?: number;
  SortOrder?: number;
  readBy?: string[];
  ReadBy?: string[];
  files?: RawMaterial[];
  Files?: RawMaterial[];
}

export interface MessageCreatedEvent {
  id: string;
  Id?: string;
  topicId: string;
  TopicId?: string;
  content?: string;
  Content?: string;
  userId?: string;
  UserId?: string;
  userName?: string;
  UserName?: string;
  userIconUrl?: string;
  UserIconUrl?: string;
  createdAt?: string;
  CreatedAt?: string;
  updatedAt?: string;
  UpdatedAt?: string;
  parentId?: string;
  ParentId?: string;
  materials?: unknown[];
  Materials?: unknown[];
  reactions?: unknown[];
  Reactions?: unknown[];
  [key: string]: unknown;
}

export interface MessageUpdatedEvent extends MessageCreatedEvent {}

export interface MessageDeletedEvent {
  messageId: string;
  MessageId?: string;
  topicId?: string;
  TopicId?: string;
  [key: string]: unknown;
}

export interface RoomCreatedEvent {
  id: string;
  Id?: string;
  name: string;
  Name?: string;
  description?: string;
  Description?: string;
  joinPolicy?: number;
  JoinPolicy?: number;
  createdAt?: string;
  CreatedAt?: string;
  [key: string]: unknown;
}

export interface RoomUpdatedEvent extends RoomCreatedEvent {}

export interface RoomDeletedEvent {
  roomId: string;
  RoomId?: string;
  [key: string]: unknown;
}

export interface TopicCreatedEvent {
  id: string;
  Id?: string;
  roomId: string;
  RoomId?: string;
  title?: string;
  Title?: string;
  description?: string;
  Description?: string;
  parentId?: string;
  ParentId?: string;
  position?: number;
  Position?: number;
  createdAt?: string;
  CreatedAt?: string;
  [key: string]: unknown;
}

export interface TopicUpdatedEvent extends TopicCreatedEvent {}

export interface TopicDeletedEvent {
  topicId: string;
  TopicId?: string;
  roomId?: string;
  RoomId?: string;
  parentId?: string;
  ParentId?: string;
  [key: string]: unknown;
}

export interface TopicUnreadUpdatedEvent {
  topicId: string;
  TopicId?: string;
  unreadCount: number;
  UnreadCount?: number;
  [key: string]: unknown;
}

export interface RoomUserJoinedEvent {
  roomUserId: string;
  RoomUserId?: string;
  roomId: string;
  RoomId?: string;
  userId?: string;
  UserId?: string;
  userName?: string;
  UserName?: string;
  [key: string]: unknown;
}

export interface RoomUserLeftEvent {
  roomUserId: string;
  RoomUserId?: string;
  roomId: string;
  RoomId?: string;
  [key: string]: unknown;
}

export interface RoomUserUpdatedEvent {
  roomUserId: string;
  RoomUserId?: string;
  roomId: string;
  RoomId?: string;
  roles?: string[];
  [key: string]: unknown;
}

export interface RoomUserRoleAddedEvent {
  roomUserId: string;
  RoomUserId?: string;
  roleName: string;
  RoleName?: string;
  [key: string]: unknown;
}

export interface RoomUserRoleRemovedEvent {
  roomUserId: string;
  RoomUserId?: string;
  roleName: string;
  RoleName?: string;
  [key: string]: unknown;
}
