<script lang="ts">
  import { onMount } from 'svelte';
  import { HubConnectionBuilder, HubConnectionState, type HubConnection } from '@microsoft/signalr';
  import { page } from '$app/stores';
  import { goto } from '$app/navigation';
  import { auth, isAuthenticated } from '$lib/stores/auth';
  import { currentRoom, setRooms, setCurrentRoom, addRoom, updateRoom, deleteRoom, roomList } from '$lib/stores/rooms';
  import { rooms } from '$lib/stores/rooms';
  import type { Room } from '$lib/stores/rooms';
  import {
  selectedTopic,
  setSelectedTopic,
  setTopics,
  addTopic,
  topicList,
  updateTopic,
  deleteTopic,
  expandedTopics,
  toggleTopicExpansion,
  moveTopicParent,
  } from '$lib/stores/topics';
  import type { Topic } from '$lib/stores/topics';
  import { addMessage, deleteMessage, messageList, messages, setMessages, updateMessage, type Message } from '$lib/stores/messages';
  import { setFiles } from '$lib/stores/files';
  import { push } from '$lib/stores/push';
  import AppLayout from '$lib/components/layout/AppLayout.svelte';
  import RoomSelector from '$lib/components/rooms/RoomSelector.svelte';
  import RoomCreateModal from '$lib/components/rooms/RoomCreateModal.svelte';
  import RoomSettingsModal from '$lib/components/rooms/RoomSettingsModal.svelte';
  import RoomUserJoinModal from '$lib/components/rooms/RoomUserJoinModal.svelte';
  import TopicTree from '$lib/components/topics/TopicTree.svelte';
  import TopicCreateModal from '$lib/components/topics/TopicCreateModal.svelte';
  import TopicEditModal from '$lib/components/topics/TopicEditModal.svelte';
  import TopicDeleteModal from '$lib/components/topics/TopicDeleteModal.svelte';
  import MessageInput from '$lib/components/messages/MessageInput.svelte';
  import MessagesView from '$lib/components/messages/MessagesView.svelte';
  import MessageEditModal from '$lib/components/messages/MessageEditModal.svelte';
  import MessageDeleteModal from '$lib/components/messages/MessageDeleteModal.svelte';
  import ViewModeSelector from '$lib/components/messages/ViewModeSelector.svelte';
  import ShareList from '$lib/components/shares/ShareList.svelte';
  import FileUploadModal from '$lib/components/files/FileUploadModal.svelte';
  import PdfViewerModal from '$lib/components/documents/PdfViewerModal.svelte';
  import ImageEditorModal from '$lib/components/images/ImageEditorModal.svelte';
  import UserSettingModal from '$lib/components/user/UserSettingModal.svelte';
  import { ui } from '$lib/stores/ui';
  import { api, getApiBaseUrl, getCurrentTenant } from '$lib/api/client';

  let isLoading = $state(true);
  let loadError = $state<string | null>(null);
  let loadedTopicId = $state<string | null>(null);
  let loadRequestId = $state(0);
  let loadedRoomFilesId = $state<string | null>(null);
  let filesLoadRequestId = $state(0);
  let lastAppliedUrlTopicId = $state<string | null>(null);
  let checkedRoomUserId = $state<string | null>(null);
  let signalRStarted = $state(false);  // SignalR接続開始済みフラグ
  let messageHub: HubConnection | null = null;
  let messageHubTenant: string | null = null;
  let messageHubTopicId: string | null = null;
  let messageSyncTimer: ReturnType<typeof setTimeout> | null = null;
  let messageHubConnected = $state(false);
  let roomTopicHub: HubConnection | null = null;
  let roomTopicHubTenant: string | null = null;
  let roomTopicHubRoomId: string | null = null;
  let roomTopicHubConnected = $state(false);
  let roomUserSyncHub: HubConnection | null = null;
  let roomUserSyncHubTenant: string | null = null;
  let roomUserSyncHubRoomId: string | null = null;
  let roomUserSyncHubUserId: string | null = null;
  let roomUserSyncHubConnected = $state(false);

  let urlTopicId = $derived.by(() => ($page.params as any)?.topicId ?? null);
  let legacyQueryTopicId = $derived.by(() => $page.url.searchParams.get('topicId'));

  function buildMessageHubUrl(tenant: string) {
    const baseUrl = getApiBaseUrl();
    const normalizedBaseUrl = baseUrl?.endsWith('/') ? baseUrl.slice(0, -1) : baseUrl;
    return normalizedBaseUrl ? `${normalizedBaseUrl}/${tenant}/hubs/messages` : `/${tenant}/hubs/messages`;
  }

  function buildRoomTopicHubUrl(tenant: string) {
    const baseUrl = getApiBaseUrl();
    const normalizedBaseUrl = baseUrl?.endsWith('/') ? baseUrl.slice(0, -1) : baseUrl;
    return normalizedBaseUrl ? `${normalizedBaseUrl}/${tenant}/hubs/rooms` : `/${tenant}/hubs/rooms`;
  }

  function buildRoomUserSyncHubUrl(tenant: string) {
    const baseUrl = getApiBaseUrl();
    const normalizedBaseUrl = baseUrl?.endsWith('/') ? baseUrl.slice(0, -1) : baseUrl;
    return normalizedBaseUrl ? `${normalizedBaseUrl}/${tenant}/hubs/roomusersync` : `/${tenant}/hubs/roomusersync`;
  }

  async function startMessageHub(tenant: string) {
    if (messageHub && messageHubTenant === tenant) return;

    await stopMessageHub();

    const connection = new HubConnectionBuilder()
      .withUrl(buildMessageHubUrl(tenant), { withCredentials: true })
      .withAutomaticReconnect()
      .build();

    connection.on('MessageCreated', (raw: any) => {
      try {
        const normalized = normalizeMessage(raw);
        const exists = $messageList.some((m) => m.id === normalized.id);

        if (exists) {
          // 既に存在する場合は更新
          updateMessage(normalized.id, normalized);
        } else {
          // 直近の10件を取得して整合性チェック
          syncLatestMessages(normalized.topicId, normalized);
        }
        // SignalR受信時に即座に既読マークを付ける
        if (normalized.topicId) {
          void markTopicAsRead(normalized.topicId);
          scheduleMessageSync(normalized.topicId);
        }
      } catch (error) {
        console.error('Failed to process MessageCreated event:', error, raw);
      }
    });

    connection.on('MessageUpdated', (raw: any) => {
      const normalized = normalizeMessage(raw);
      updateMessage(normalized.id, normalized);
      if (normalized.topicId) scheduleMessageSync(normalized.topicId);
    });

    connection.on('MessageDeleted', (raw: any) => {
      const messageId = raw?.messageId ?? raw?.MessageId ?? '';
      if (!messageId) return;
      deleteMessage(messageId);
      const topicId = raw?.topicId ?? raw?.TopicId ?? '';
      if (topicId) scheduleMessageSync(topicId);
    });

    connection.onreconnected(async () => {
      messageHubConnected = true;
      if (!messageHubTopicId) return;
      try {
        await connection.invoke('JoinTopic', messageHubTopicId);
      } catch (err) {
        console.error('Failed to rejoin message hub topic:', err);
      }
    });
    connection.onclose(() => {
      messageHubConnected = false;
    });

    try {
      await connection.start();
      messageHub = connection;
      messageHubTenant = tenant;
      messageHubConnected = true;
    } catch (err) {
      console.error('Failed to start message hub:', err);
    }
  }

  async function startRoomTopicHub(tenant: string) {
    if (roomTopicHub && roomTopicHubTenant === tenant) return;

    await stopRoomTopicHub();

    const connection = new HubConnectionBuilder()
      .withUrl(buildRoomTopicHubUrl(tenant), { withCredentials: true })
      .withAutomaticReconnect()
      .build();

    connection.on('RoomCreated', (raw: any) => {
      const normalized = normalizeRoom(raw);
      const exists = $roomList.some((r) => r.id === normalized.id);
      if (exists) {
        updateRoom(normalized.id, normalized);
      } else {
        addRoom(normalized);
      }
    });

    connection.on('RoomUpdated', (raw: any) => {
      const normalized = normalizeRoom(raw);
      updateRoom(normalized.id, normalized);
    });

    connection.on('RoomDeleted', (raw: any) => {
      const roomId = raw?.roomId ?? raw?.RoomId ?? '';
      if (!roomId) return;
      deleteRoom(roomId);
    });

    connection.on('TopicCreated', async (raw: any) => {
      const normalized = normalizeTopic(raw);
      if (!$currentRoom || normalized.roomId !== $currentRoom.id) return;

      const exists = $topicList.some((t) => t.id === normalized.id);
      if (exists) {
        // 既存の場合はトピックを更新
        updateTopic(normalized.id, normalized);
        return;
      }

      // 親トピックのID
      const parentId = normalized.parentId ?? null;

      // 親がtopicListにない場合は先に親を取得
      if (parentId && !$topicList.some((t) => t.id === parentId)) {
        try {
          const tenant = messageHubTenant ?? $page.params.tenant ?? getCurrentTenant();
          if (tenant) {
            const parentRaw = await api.get<any>(`/${tenant}/api/Topic/${parentId}`);
            const parentNormalized = normalizeTopic(parentRaw);
            // 親を追加
            if (!$topicList.some((t) => t.id === parentNormalized.id)) {
              addTopic(parentNormalized);
            }
          }
        } catch (err) {
          console.error('Failed to fetch parent topic:', err);
          // 親の取得に失敗しても子は追加する
        }
      }

      // 親トピックのhasChildrenを更新
      if (parentId) {
        updateTopic(parentId, { hasChildren: true });
      }

      addTopic(normalized);
    });

    connection.on('TopicUpdated', (raw: any) => {
      const normalized = normalizeTopic(raw);
      if (!$currentRoom || normalized.roomId !== $currentRoom.id) {
        const existing = $topicList.find((t) => t.id === normalized.id);
        if (existing) deleteTopic(normalized.id);
        return;
      }

      const existing = $topicList.find((t) => t.id === normalized.id);
      if (!existing) {
        addTopic(normalized);
        return;
      }

      const normalizedParentId = normalized.parentId ?? null;
      const previousParentId = existing.parentId ?? null;
      if (previousParentId !== normalizedParentId) {
        moveTopicParent(normalized.id, normalizedParentId);
      }

      updateTopic(normalized.id, {
        parentId: normalized.parentId,
        roomId: normalized.roomId,
        title: normalized.title,
        description: normalized.description,
        creatorId: normalized.creatorId,
        messageCount: normalized.messageCount,
        unreadCount: normalized.unreadCount,
        userPermission: normalized.userPermission,
        permissions: normalized.permissions,
        isArchived: normalized.isArchived,
        tags: normalized.tags,
        hasChildren: normalized.hasChildren,
        createdAt: normalized.createdAt,
        updatedAt: normalized.updatedAt,
      });
    });

    connection.on('TopicDeleted', (raw: any) => {
      const topicId = raw?.topicId ?? raw?.TopicId ?? '';
      const roomId = raw?.roomId ?? raw?.RoomId ?? '';
      if (!topicId) return;
      if ($currentRoom && roomId && roomId !== $currentRoom.id) return;

      // 削除するトピックの情報を取得
      const deletedTopic = $topicList.find(t => t.id === topicId);
      const parentId = deletedTopic?.parentId;

      deleteTopic(topicId);

      // 親トピックがあればhasChildrenを更新
      // TODO: if (parentId) {
      //   topics.refreshHasChildren(parentId);
      // }
    });

    connection.onreconnected(async () => {
      roomTopicHubConnected = true;
      try {
        await connection.invoke('JoinTenant', tenant);
      } catch (err) {
        console.error('Failed to rejoin room hub tenant:', err);
      }

      if (!roomTopicHubRoomId) return;
      try {
        await connection.invoke('JoinRoom', roomTopicHubRoomId);
      } catch (err) {
        console.error('Failed to rejoin room hub room:', err);
      }
    });

    connection.onclose(() => {
      roomTopicHubConnected = false;
    });

    try {
      await connection.start();
      roomTopicHub = connection;
      roomTopicHubTenant = tenant;
      roomTopicHubConnected = true;
      await connection.invoke('JoinTenant', tenant);
    } catch (err) {
      console.error('Failed to start room hub:', err);
    }
  }

  async function startRoomUserSyncHub(tenant: string, roomId: string, userId: string) {
    if (roomUserSyncHub && roomUserSyncHubTenant === tenant && roomUserSyncHubRoomId === roomId && roomUserSyncHubUserId === userId) return;

    await stopRoomUserSyncHub();

    const connection = new HubConnectionBuilder()
      .withUrl(buildRoomUserSyncHubUrl(tenant), { withCredentials: true })
      .withAutomaticReconnect()
      .build();

    connection.on('TopicUnreadUpdated', async (raw: any) => {
      const topicId = raw?.topicId ?? raw?.TopicId ?? '';
      if (!topicId) return;

      // トピック情報を再取得して未読数を更新
      try {
        const currentTenant = getCurrentTenant() ?? tenant;
        const updated = await api.get<any>(`/${currentTenant}/api/Topic/${topicId}`);
        if (updated) {
          const normalized = normalizeTopic(updated);
          if (normalized.id) {
            updateTopic(normalized.id, { unreadCount: normalized.unreadCount });
          }
        }
      } catch (err) {
        console.error('Failed to fetch topic unread count:', err);
      }
    });

    connection.onreconnected(async () => {
      roomUserSyncHubConnected = true;
      try {
        await connection.invoke('JoinRoomUserGroup', roomId, userId);
      } catch (err) {
        console.error('Failed to rejoin room user sync hub:', err);
      }
    });

    connection.onclose(() => {
      roomUserSyncHubConnected = false;
    });

    try {
      await connection.start();
      roomUserSyncHub = connection;
      roomUserSyncHubTenant = tenant;
      roomUserSyncHubRoomId = roomId;
      roomUserSyncHubUserId = userId;
      roomUserSyncHubConnected = true;
      await connection.invoke('JoinRoomUserGroup', roomId, userId);
    } catch (err) {
      console.error('Failed to start room user sync hub:', err);
    }
  }

  async function stopMessageHub() {
    if (!messageHub) return;
    try {
      await messageHub.stop();
    } catch (err) {
      console.error('Failed to stop message hub:', err);
    } finally {
      messageHub = null;
      messageHubTenant = null;
      messageHubTopicId = null;
      messageHubConnected = false;
    }
  }

  async function stopRoomTopicHub() {
    if (!roomTopicHub) return;
    try {
      await roomTopicHub.stop();
    } catch (err) {
      console.error('Failed to stop room hub:', err);
    } finally {
      roomTopicHub = null;
      roomTopicHubTenant = null;
      roomTopicHubRoomId = null;
      roomTopicHubConnected = false;
    }
  }

  async function stopRoomUserSyncHub() {
    if (!roomUserSyncHub) return;
    try {
      await roomUserSyncHub.stop();
    } catch (err) {
      console.error('Failed to stop room user sync hub:', err);
    } finally {
      roomUserSyncHub = null;
      roomUserSyncHubTenant = null;
      roomUserSyncHubRoomId = null;
      roomUserSyncHubUserId = null;
      roomUserSyncHubConnected = false;
    }
  }

  async function ensureMessageHubTopic(topicId: string | null) {
    if (!messageHub || messageHub.state !== HubConnectionState.Connected) return;

    if (messageHubTopicId && messageHubTopicId !== topicId) {
      try {
        await messageHub.invoke('LeaveTopic', messageHubTopicId);
      } catch (err) {
        console.error('Failed to leave message hub topic:', err);
      }
      messageHubTopicId = null;
    }

    if (topicId && messageHubTopicId !== topicId) {
      try {
        await messageHub.invoke('JoinTopic', topicId);
        messageHubTopicId = topicId;
      } catch (err) {
        console.error('Failed to join message hub topic:', err);
      }
    }
  }

  async function ensureRoomTopicHubRoom(roomId: string | null) {
    if (!roomTopicHub || roomTopicHub.state !== HubConnectionState.Connected) return;

    if (roomTopicHubRoomId && roomTopicHubRoomId !== roomId) {
      try {
        await roomTopicHub.invoke('LeaveRoom', roomTopicHubRoomId);
      } catch (err) {
        console.error('Failed to leave room hub room:', err);
      }
      roomTopicHubRoomId = null;
    }

    if (roomId && roomTopicHubRoomId !== roomId) {
      try {
        await roomTopicHub.invoke('JoinRoom', roomId);
        roomTopicHubRoomId = roomId;
      } catch (err) {
        console.error('Failed to join room hub room:', err);
      }
    }
  }

  function normalizeRoom(raw: any) {
    const id = raw?.id ?? raw?.Id ?? '';
    const name = raw?.name ?? raw?.Name ?? '';
    const createdAt = raw?.createdAt ?? raw?.CreatedAt ?? null;
    const updatedAt = raw?.updatedAt ?? raw?.UpdatedAt ?? null;

    return {
      id,
      name,
      description: raw?.description ?? raw?.Description,
      avatar: raw?.avatar ?? raw?.Avatar,
      createdAt: createdAt ? new Date(createdAt) : new Date(),
      updatedAt: updatedAt ? new Date(updatedAt) : new Date(),
      ownerId: raw?.ownerId ?? raw?.OwnerId ?? raw?.createdUserId ?? raw?.CreatedUserId ?? '',
      memberCount: raw?.memberCount ?? raw?.MemberCount ?? 0,
      unreadCount: raw?.unreadCount ?? raw?.UnreadCount ?? 0,
      isArchived: raw?.isArchived ?? raw?.IsArchived ?? false,
      settings: raw?.settings ?? raw?.Settings,
    };
  }

  function normalizeTopic(raw: any) {
    const id = raw?.id ?? raw?.Id ?? '';
    const createdAt = raw?.createdAt ?? raw?.CreatedAt ?? null;
    const updatedAt = raw?.updatedAt ?? raw?.UpdatedAt ?? null;

    return {
      id,
      roomId: raw?.roomId ?? raw?.RoomId ?? '',
      title: raw?.title ?? raw?.Title ?? '',
      description: raw?.description ?? raw?.Description,
      parentId: raw?.parentId ?? raw?.ParentId ?? null,
      sourceMessageId: raw?.sourceMessageId ?? raw?.SourceMessageId ?? null,
      childIds: raw?.childIds ?? raw?.ChildIds ?? [],
      createdAt: createdAt ? new Date(createdAt) : new Date(),
      updatedAt: updatedAt ? new Date(updatedAt) : new Date(),
      creatorId: raw?.creatorId ?? raw?.CreatorId ?? '',
      messageCount: raw?.messageCount ?? raw?.MessageCount ?? 0,
      unreadCount: raw?.unreadCount ?? raw?.UnreadCount ?? 0,
      userPermission: raw?.userPermission ?? raw?.UserPermission ?? 'read',
      permissions: raw?.permissions ?? raw?.Permissions ?? [],
      isArchived: raw?.isArchived ?? raw?.IsArchived ?? false,
      tags: raw?.tags ?? raw?.Tags ?? [],
      hasChildren: raw?.hasChildren ?? raw?.HasChildren ?? false,
    };
  }

  async function ensureTopicPathLoaded(tenant: string, topicId: string) {
    const chain: any[] = [];
    let cursorId: string | null = topicId;
    const visited = new Set<string>();

    while (cursorId && !visited.has(cursorId)) {
      visited.add(cursorId);
      const raw = await api.get<any>(`/${tenant}/api/Topic/${cursorId}`);
      const normalized = normalizeTopic(raw);
      chain.push(normalized);
      cursorId = normalized.parentId ?? null;
    }

    chain.reverse(); // root -> leaf

    for (const t of chain) {
      const existing = $topicList.find((x) => x.id === t.id);
      if (!existing) {
        addTopic(t);
      } else {
        updateTopic(t.id, {
          title: t.title,
          description: t.description,
          parentId: t.parentId,
          roomId: t.roomId,
          hasChildren: t.hasChildren,
          updatedAt: t.updatedAt,
          sourceMessageId: t.sourceMessageId ?? null,
        });
      }
    }

    // Expand ancestors (not the leaf) so the selected topic is visible.
    for (let i = 0; i < chain.length - 1; i++) {
      const id = chain[i].id;
      if (!$expandedTopics.has(id)) toggleTopicExpansion(id);
    }

    return chain[chain.length - 1] ?? null;
  }

  async function selectTopicFromUrl(tenant: string, roomToUse?: Room | null): Promise<Topic | null> {
    const room = roomToUse ?? $currentRoom;
    if (!room) return null;
    if (!urlTopicId) {
      if ($selectedTopic) setSelectedTopic(null);
      return null;
    }

    if ($selectedTopic?.id === urlTopicId) return $selectedTopic;

    const existing = $topicList.find((t) => t.id === urlTopicId) ?? null;
    if (existing) {
      if (existing.roomId === room.id) setSelectedTopic(existing);
      return existing;
    }

    try {
      const loaded = await ensureTopicPathLoaded(tenant, urlTopicId);
      if (loaded && loaded.roomId === room.id) {
        setSelectedTopic(loaded);
        return loaded;
      }
    } catch {
      // ignore
    }
    return null;
  }

  function normalizeMessage(raw: any) {
    const id = raw?.id ?? raw?.Id ?? '';

    // IDが空文字列の場合はエラーとして扱う（デバッグ用）
    if (!id) {
      console.error('Message ID is empty:', raw);
      throw new Error('Invalid message ID: ID is empty');
    }

    const createdAt = raw?.createdAt ?? raw?.CreatedAt ?? null;
    const updatedAt = raw?.updatedAt ?? raw?.UpdatedAt ?? null;

    function getAttachmentKind(fileName: string, mimeType: string): 'image' | 'pdf' | 'document' | 'other' {
      if (mimeType?.startsWith('image/')) return 'image';
      const ext = (fileName?.split('.').pop() ?? '').toLowerCase();
      if (ext === 'pdf') return 'pdf';
      const docExts = new Set(['doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx', 'txt', 'md', 'rtf', 'csv']);
      if (docExts.has(ext)) return 'document';
      return 'other';
    }

    const rawFiles = raw?.files ?? raw?.Files ?? [];
    const attachments =
      Array.isArray(rawFiles)
        ? rawFiles.map((f: any) => {
            const fid = f?.id ?? f?.Id ?? '';
            const fileName = f?.fileName ?? f?.FileName ?? '';
            const mimeType = f?.fileType ?? f?.FileType ?? 'application/octet-stream';
            const size = f?.size ?? f?.Size ?? 0;
            const url = f?.url ?? f?.Url ?? '';
            const uploadedAt = f?.createdAt ?? f?.CreatedAt ?? null;
            return {
              id: fid,
              fileName,
              mimeType,
              size,
              url,
              fileType: getAttachmentKind(fileName, mimeType),
              uploadedAt: uploadedAt ? new Date(uploadedAt) : new Date(),
              uploadedBy:
                f?.uploadedBy ??
                f?.UploadedBy ??
                raw?.roomUserId ??
                raw?.RoomUserId ??
                raw?.applicationUserId ??
                raw?.ApplicationUserId ??
                '',
            };
          })
        : [];

    return {
      id,
      topicId: raw?.topicId ?? raw?.TopicId ?? '',
      userId:
        raw?.roomUserId ??
        raw?.RoomUserId ??
        raw?.applicationUserId ??
        raw?.ApplicationUserId ??
        raw?.userId ??
        raw?.UserId ??
        '',
      userName: raw?.userName ?? raw?.UserName ?? '',
      userDisplayName:
        raw?.userDisplayName ?? raw?.UserDisplayName ?? raw?.userName ?? raw?.UserName ?? '',
      userAvatar: raw?.userAvatar ?? raw?.UserAvatar ?? undefined,
      subject: raw?.subject ?? raw?.Subject ?? raw?.header ?? raw?.Header ?? '',
      content: raw?.content ?? raw?.Content ?? raw?.body ?? raw?.Body ?? '',
      replyToId: raw?.replyToId ?? raw?.ReplyToId ?? raw?.replyId ?? raw?.ReplyId ?? undefined,
      createdAt: createdAt ? new Date(createdAt) : new Date(),
      updatedAt: updatedAt ? new Date(updatedAt) : undefined,
      attachments,
      isOwner: false,
      canEdit: true,
      canDelete: true,
      childTopicId: (raw?.childTopicId || raw?.ChildTopicId) || null,
      childTopicTitle: (raw?.childTopicTitle || raw?.ChildTopicTitle) || null,
    };
  }

  function mergeMessagesForTopic(topicId: string, incoming: ReturnType<typeof normalizeMessage>[]) {
    const existing = $messageList.filter((m) => m.topicId === topicId);
    const map = new Map<string, typeof existing[number]>();
    existing.forEach((m) => map.set(m.id, m));
    incoming.forEach((m) => map.set(m.id, { ...map.get(m.id), ...m }));

    const merged = Array.from(map.values())
      .filter((m) => m.topicId === topicId)
      .sort((a, b) => {
        const at = new Date(a.createdAt).getTime();
        const bt = new Date(b.createdAt).getTime();
        if (at !== bt) return at - bt;
        return a.id.localeCompare(b.id);
      });

    setMessages(topicId, merged);
  }

  async function markTopicAsRead(topicId: string) {
    const tenant = $page.params.tenant ?? getCurrentTenant();
    if (!tenant) return;
    try {
      await api.post(`/${tenant}/api/Message/topic/${topicId}/markAsRead`);
    } catch (err) {
      console.error('Failed to mark topic as read:', err);
    }
  }

  function getAnchorIdForTopic(topicId: string, backCount: number) {
    const topicMessages = $messageList
      .filter((m) => m.topicId === topicId)
      .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());

    if (topicMessages.length === 0) return null;
    const index = Math.max(topicMessages.length - backCount, 0);
    return topicMessages[index]?.id ?? topicMessages[0]?.id ?? null;
  }

  function syncLatestMessages(topicId: string, newMessage: Message) {
    // 直近10件のメッセージを取得
    const topicMessages = $messageList
      .filter((m) => m.topicId === topicId)
      .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime())
      .slice(-10);

    if (topicMessages.length === 0) {
      // メッセージがなければ直接追加
      addMessage(newMessage);
      return;
    }

    // 最も古いメッセージより新しいメッセージのみを取得
    const oldestMessage = topicMessages[0];
    const newerMessages = topicMessages.filter(m => new Date(m.createdAt) >= new Date(oldestMessage.createdAt));

    // 古いメッセージを削除
    newerMessages.forEach(m => {
      deleteMessage(m.id);
    });

    // 新しいメッセージを追加（新しい順）
    const sortedNewMessages = [...newerMessages, newMessage]
      .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());

    // すべてを再追加
    sortedNewMessages.forEach(m => {
      addMessage(m);
    });
  }

  function scheduleMessageSync(topicId: string) {
    if (messageSyncTimer) {
      clearTimeout(messageSyncTimer);
    }

    messageSyncTimer = setTimeout(async () => {
      const tenant = $page.params.tenant ?? getCurrentTenant();
      if (!tenant) return;

      const anchorId = getAnchorIdForTopic(topicId, 10);
      if (!anchorId) return;

      try {
        const response = await api.get<any[]>(
          `/${tenant}/api/Message/topic/${topicId}/after/${anchorId}`,
          { params: { take: 50 } }
        );
        const list = Array.isArray(response) ? response.map(normalizeMessage) : [];
        mergeMessagesForTopic(topicId, list);
      } catch (err) {
        console.error('Failed to sync messages:', err);
      }
    }, 300);
  }

  function normalizeMaterial(raw: any) {
    const id = raw?.id ?? raw?.Id ?? '';
    const createdAt = raw?.uploadedAt ?? raw?.UploadedAt ?? raw?.createdAt ?? raw?.CreatedAt ?? null;

    return {
      id,
      roomId: raw?.roomId ?? raw?.RoomId ?? '',
      messageId: raw?.messageId ?? raw?.MessageId ?? undefined,
      fileName: raw?.fileName ?? raw?.FileName ?? raw?.originalFileName ?? raw?.OriginalFileName ?? '',
      originalFileName: raw?.originalFileName ?? raw?.OriginalFileName ?? raw?.fileName ?? raw?.FileName ?? '',
      mimeType: raw?.mimeType ?? raw?.MimeType ?? 'application/octet-stream',
      size: raw?.size ?? raw?.Size ?? 0,
      url: raw?.url ?? raw?.Url ?? '',
      fileType: raw?.fileType ?? raw?.FileType ?? 'other',
      uploadedAt: createdAt ?? new Date().toISOString(),
      uploadedBy: raw?.uploadedBy ?? raw?.UploadedBy ?? '',
      uploadedByName: raw?.uploadedByName ?? raw?.UploadedByName ?? raw?.uploadedBy ?? raw?.UploadedBy ?? 'Unknown',
      versions: raw?.versions ?? raw?.Versions ?? [],
      isArchived: raw?.isArchived ?? raw?.IsArchived ?? false,
      tags: raw?.tags ?? raw?.Tags ?? undefined,
      description: raw?.description ?? raw?.Description ?? undefined,
    };
  }

  async function loadTenantData() {
    isLoading = true;
    loadError = null;
    let tenant: string | null = null;

    try {
      tenant = $page.params.tenant ?? getCurrentTenant();
      if (!tenant) throw new Error('Tenant not found in URL');

      api.configureApiClient(tenant);

      // 認証確認
      await auth.fetchCurrentUser(tenant);

      // 認証完了後すぐにSignalR接続を開始（並列化）
      signalRStarted = true;
      void startMessageHub(tenant);
      void startRoomTopicHub(tenant);
      void startRoomUserSyncHub(tenant, $page.params.roomId ?? '', $auth?.user?.id ?? '');

      // Room取得
      const response = await api.get<any[]>(`/${tenant}/api/Room`);
      const rooms = Array.isArray(response) ? response.map(normalizeRoom) : [];
      setRooms(rooms);

      const roomId = $page.params.roomId;
      console.log('[RoomPage] URL roomId:', roomId, 'Type:', typeof roomId);
      console.log('[RoomPage] Loaded rooms:', rooms.length, rooms);
      console.log('[RoomPage] First room id:', rooms[0]?.id, 'Type:', typeof rooms[0]?.id);

      const initialRoom = rooms.find((room) => room.id === roomId) ?? rooms[0] ?? null;
      console.log('[RoomPage] Selected initialRoom:', initialRoom);
      setCurrentRoom(initialRoom);

      // ここでローディング解除 - Roomが表示される
      isLoading = false;

      if (initialRoom) {
        // バックグラウンドでTopics, Files, RoomUserを順次読み込み
        const [topicsResponse] = await Promise.all([
          api.get<any[]>(`/${tenant}/api/Topic/room/${initialRoom.id}/root`).catch(err => {
            console.error('Failed to load root topics:', err);
            return [];
          }),
          loadRoomFiles(tenant, initialRoom.id)
        ]);

        console.log('[RoomPage] Topics API response:', topicsResponse);
        const topics = Array.isArray(topicsResponse) ? topicsResponse.map(normalizeTopic) : [];
        console.log('[RoomPage] Normalized topics:', topics);
        setTopics(topics);

        // RoomUser読み込み（401でリダイレクトされる可能性があるため、分離）
        await loadRoomUser(tenant, initialRoom.id);

        // RoomUser読み込み成功後にSignalR接続
        if ($auth?.user?.id) {
          console.log('[RoomPage] Starting room user sync hub...');
          void startRoomUserSyncHub(tenant, initialRoom.id, $auth.user.id);
        }

        // Topics読み込み完了後にURLベースのトピックを選択（initialRoomを直接渡す）
        const selected = await selectTopicFromUrl(tenant, initialRoom);
        // $effectでの再呼び出しを防ぐためにフラグを設定
        lastAppliedUrlTopicId = urlTopicId ?? null;

        // トピック選択後にメッセージ読み込みを開始（並列化）
        if (selected) {
          loadedTopicId = selected.id;
          messages.setLoading(true);
          messages.setError(null);

          const requestId = ++loadRequestId;
          api.get<any[]>(`/${tenant}/api/Message/topic/${selected.id}`)
            .then((response) => {
              if (requestId !== loadRequestId) return;
              const list = Array.isArray(response) ? response.map(normalizeMessage) : [];
              setMessages(selected.id, list);
            })
            .catch((err: unknown) => {
              if (requestId !== loadRequestId) return;
              const msg = err instanceof Error ? err.message : 'Failed to load messages';
              messages.setError(msg);
            })
            .finally(() => {
              if (requestId !== loadRequestId) return;
              messages.setLoading(false);
            });
        }
      }

      if (initialRoom && initialRoom.id !== roomId) {
        // goto()による再遷移をやめ、履歴を追加せずにURLのみを更新
        const search = $page.url.search;
        const maybeTopic = urlTopicId ? `/topic/${urlTopicId}` : '';
        const newUrl = `/${tenant}/room/${initialRoom.id}${maybeTopic}${search}`;
        window.history.replaceState({}, '', newUrl);
        // setCurrentRoomは既に呼ばれているので、これ以上何もしない
      }
    } catch (error) {
      const resolvedTenant = tenant ?? ($page.params.tenant ?? getCurrentTenant());
      if (
        resolvedTenant &&
        error instanceof api.ApiError &&
        (error.status === 401 || error.status === 403)
      ) {
        isLoading = false;
        auth.logout();
        redirectToTenantLogin(resolvedTenant);
        return;
      }

      loadError = error instanceof Error ? error.message : 'Failed to load tenant data';
      isLoading = false;
    }
  }

  async function loadRoomFiles(tenant: string, roomId: string): Promise<void> {
    if (loadedRoomFilesId === roomId) return;
    loadedRoomFilesId = roomId;

    try {
      const response = await api.get<any[]>(`/${tenant}/api/File/room/${roomId}`);
      const list = Array.isArray(response) ? response.map(normalizeMaterial) : [];
      setFiles(list);
    } catch {
      setFiles([]);
    }
  }

  async function loadRoomUser(tenant: string, roomId: string): Promise<void> {
    if (checkedRoomUserId === roomId) return;
    checkedRoomUserId = roomId;

    try {
      const roomUserData = await api.get<any>(`/${tenant}/api/RoomUsers/room/${roomId}/me`);
      if (roomUserData) {
        const roomUser = {
          id: roomUserData.id ?? roomUserData.Id ?? '',
          displayName: roomUserData.displayName ?? roomUserData.DisplayName ?? '',
          iconUrl: roomUserData.iconUrl ?? roomUserData.IconUrl,
          useMainIcon: roomUserData.useMainIcon ?? roomUserData.UseMainIcon ?? false,
        };
        rooms.setCurrentRoomUser(roomUser);
      }
    } catch (err: unknown) {
      if (err instanceof api.ApiError) {
        if (err.status === 401) {
          // 未ログイン: ログインページにリダイレクト
          redirectToTenantLogin(tenant);
          return;
        }
        if (err.status === 404) {
          // 未登録: 参加モーダルを表示
          void handleRoomUserNotFound(tenant, roomId);
          return;
        }
      }
      console.error('Failed to fetch RoomUser:', err);
    }
  }

  onMount(() => {
    loadTenantData();
    // プッシュ通知の初期化と購読
    push.init().then(async () => {
      if (Notification.permission === 'default') {
        const granted = await push.requestPermission();
        if (granted) {
          console.log('[RoomPage] Push notification permission granted, subscribing...');
          try {
            await push.subscribePush();
            console.log('[RoomPage] Push notification subscription successful');
          } catch (err) {
            console.error('[RoomPage] Failed to subscribe to push notifications:', err);
          }
        }
      } else if (Notification.permission === 'granted') {
        // 既に許可されている場合は購読のみ
        try {
          await push.subscribePush();
          console.log('[RoomPage] Push notification subscription successful');
        } catch (err) {
          console.error('[RoomPage] Failed to subscribe to push notifications:', err);
        }
      }
    });
    return () => {
      void stopMessageHub();
      void stopRoomTopicHub();
    };
  });

  // Backward compatibility: convert old `?topicId=...` to the new page URL.
  $effect(() => {
    const tenant = $page.params.tenant ?? getCurrentTenant();
    const roomId = $page.params.roomId;
    if (!tenant || !roomId) return;
    if (urlTopicId) return;
    if (!legacyQueryTopicId) return;

    goto(`/${tenant}/room/${roomId}/topic/${legacyQueryTopicId}`, {
      replaceState: true,
      keepFocus: true,
      noScroll: true,
    });
  });

  function buildReturnUrl(): string {
    if (typeof window === 'undefined') return '/';
    const { pathname, search, hash } = window.location;
    return `${pathname}${search}${hash}`;
  }

  function redirectToTenantLogin(tenant: string): void {
    if (!tenant || typeof window === 'undefined') return;
    const returnUrl = buildReturnUrl();
    window.location.href = `/${tenant}/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
  }

  async function handleRoomUserNotFound(tenant: string, roomId: string): Promise<void> {
    try {
      await auth.fetchCurrentUser(tenant);
      ui.openModal({
        id: 'room-user-join',
        title: 'Set your name',
        type: 'custom',
        data: { roomId },
      });
    } catch (error: unknown) {
      if (error instanceof api.ApiError && error.status === 404) {
        auth.logout();
        redirectToTenantLogin(tenant);
        return;
      }

      console.error('Failed to refresh ApplicationUser after missing RoomUser:', error);
    }
  }

  // If URL changes (back/forward) reflect it into selected topic.
  $effect(() => {
    const tenant = $page.params.tenant ?? getCurrentTenant();
    if (!tenant) return;
    if (!isLoading) {
      if (lastAppliedUrlTopicId === urlTopicId) return;
      lastAppliedUrlTopicId = urlTopicId ?? null;
      selectTopicFromUrl(tenant);
    }
  });

  $effect(() => {
    if (!$currentRoom || !$selectedTopic) return;

    // 既に読み込まれている場合はスキップ（loadTenantData内で実行済みの場合）
    if (loadedTopicId === $selectedTopic.id) return;
    loadedTopicId = $selectedTopic.id;

    const tenant = $page.params.tenant ?? getCurrentTenant();
    if (!tenant) return;

    const requestId = ++loadRequestId;
    messages.setLoading(true);
    messages.setError(null);

    api.get<any[]>(`/${tenant}/api/Message/topic/${$selectedTopic.id}`)
      .then((response) => {
        if (requestId !== loadRequestId) return;
        const list = Array.isArray(response) ? response.map(normalizeMessage) : [];
        setMessages($selectedTopic.id, list);
      })
      .catch((err: unknown) => {
        if (requestId !== loadRequestId) return;
        const msg = err instanceof Error ? err.message : 'Failed to load messages';
        messages.setError(msg);
      })
      .finally(() => {
        if (requestId !== loadRequestId) return;
        messages.setLoading(false);
      });
  });

  $effect(() => {
    const tenant = $page.params.tenant ?? getCurrentTenant();
    if (!tenant) return;
    if (!$isAuthenticated) {
      void stopMessageHub();
      void stopRoomTopicHub();
      void stopRoomUserSyncHub();
      signalRStarted = false;
      return;
    }

    // loadTenantData()で既にSignalR接続を開始済みの場合はスキップ
    if (signalRStarted) return;

    void startMessageHub(tenant).then(() => {
      void ensureMessageHubTopic($selectedTopic?.id ?? null);
    });

    void startRoomTopicHub(tenant).then(() => {
      void ensureRoomTopicHubRoom($currentRoom?.id ?? null);
    });

    const userId = $auth?.user?.id ?? null;
    if ($currentRoom?.id && userId) {
      void startRoomUserSyncHub(tenant, $currentRoom.id, userId);
    }

    signalRStarted = true;
  });

  $effect(() => {
    if (!messageHubConnected) return;
    void ensureMessageHubTopic($selectedTopic?.id ?? null);
  });

  $effect(() => {
    if (!roomTopicHubConnected) return;
    void ensureRoomTopicHubRoom($currentRoom?.id ?? null);
  });

  $effect(() => {
    const tenant = $page.params.tenant ?? getCurrentTenant();
    const roomId = $currentRoom?.id ?? null;
    const userId = $auth?.user?.id ?? null;
    if (!tenant || !roomId || !userId) {
      void stopRoomUserSyncHub();
      return;
    }
    void startRoomUserSyncHub(tenant, roomId, userId);
  });
</script>

<svelte:head>
  <title>TreeTopic - Collaborative Discussion</title>
</svelte:head>

{#if isLoading}
  <div class="flex items-center justify-center h-screen bg-gradient-to-br from-primary to-secondary">
    <div class="text-center text-white">
      <h1 class="text-4xl font-bold mb-4">TreeTopic</h1>
      <p>Loading...</p>
    </div>
  </div>
{:else if $isAuthenticated}
  <AppLayout subPanelTitle="Shared">
    {#snippet headerContent()}
      <RoomSelector />
    {/snippet}

    {#snippet sidebarContent()}
      {#if $currentRoom}
        <div class="panel-header">
          <h3 class="panel-title">Top</h3>
          <span class="text-small text-light">Topics</span>
        </div>
        <TopicTree />
      {:else}
        <div class="p-4 text-center text-text-light">
          <p class="text-sm">Select a room to view topics</p>
        </div>
      {/if}
    {/snippet}

    {#snippet mainContent()}
      {#if $currentRoom && $selectedTopic}
        <div class="room-main">
          <div class="border-b border-border room-topic-header">
            <div>
              <h2 class="text-lg font-semibold text-text">{$selectedTopic.title}</h2>
              {#if $selectedTopic.description}
                <p class="text-sm text-text-light mt-1">{$selectedTopic.description}</p>
              {/if}
            </div>
            <div class="pt-2 border-t border-border">
              <ViewModeSelector />
            </div>
          </div>
          <div class="room-messages-container">
            <MessagesView />
          </div>
          <div class="message-input-wrapper">
            <MessageInput />
          </div>
        </div>
      {:else if $currentRoom}
        <div class="flex items-center justify-center h-full text-center">
          <div>
            <h2 class="text-2xl font-bold text-text mb-2">{$currentRoom.name}</h2>
            <p class="text-text-secondary">Select a topic to view messages</p>
          </div>
        </div>
      {:else}
        <div class="flex items-center justify-center h-full text-center">
          <div>
            <h2 class="text-2xl font-bold text-text mb-2">Welcome to TreeTopic</h2>
            <p class="text-text-secondary">Select a room to get started</p>
            <div class="mt-4">
              <button class="button button-primary" onclick={() => ui.openModal({ id: 'room-create', title: 'Create Room', type: 'custom' })}>
                Create your first room
              </button>
            </div>
          </div>
        </div>
      {/if}
    {/snippet}

    {#snippet subPanelContent()}
      <ShareList />
    {/snippet}
  </AppLayout>

  <RoomCreateModal />
  <RoomSettingsModal />
  <RoomUserJoinModal />
  <TopicCreateModal />
  <TopicEditModal />
  <TopicDeleteModal />
  <FileUploadModal />
  <PdfViewerModal />
  <ImageEditorModal />
  <MessageEditModal />
  <MessageDeleteModal />
  <UserSettingModal roomId={$currentRoom?.id?.toString() ?? ''} />
{:else}
  <div class="flex items-center justify-center h-screen bg-gradient-to-br from-primary to-secondary">
    <div class="text-center text-white">
      <h1 class="text-4xl font-bold mb-4">TreeTopic</h1>
      {#if loadError}
        <p class="mb-4">{loadError}</p>
      {:else}
        <p class="mb-4">Not authenticated</p>
      {/if}
      <button
        class="button button-secondary"
        onclick={() => {
          const tenant = $page.params.tenant ?? getCurrentTenant();
          const returnUrl = `${window.location.pathname}${window.location.search}${window.location.hash}`;
          window.location.href = `/${tenant}/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
        }}
      >
        Go to login
      </button>
    </div>
  </div>
{/if}

<style>
.room-topic-header {
  padding: var(--spacing-sm) var(--spacing-md);
}

  .room-main {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-height: 0;
    overflow: hidden;
  }

  .room-messages-container {
    flex: 1;
    min-height: 0;
    overflow: hidden;
    display: flex;
    flex-direction: column;
  }

  .message-input-wrapper {
    flex: 0 0 auto;
  }

  @media (max-width: 768px) {
    .room-topic-header {
      padding: var(--spacing-sm);
    }
  }
</style>
