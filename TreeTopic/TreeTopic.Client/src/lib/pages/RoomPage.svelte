<script lang="ts">
  import { onMount } from 'svelte';
  import { HubConnectionBuilder, HubConnectionState, type HubConnection } from '@microsoft/signalr';
  import { page } from '$app/stores';
  import { goto } from '$app/navigation';
  import { auth, isAuthenticated } from '$lib/stores/auth';
  import { currentRoom, setRooms, setCurrentRoom, addRoom, updateRoom, deleteRoom, roomList, rooms, rooms as roomsStore, currentRoomUser } from '$lib/stores/rooms';
  import type { Room } from '$lib/stores/rooms';
  import type { CurrentRoomUser } from '$lib/stores/rooms';
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
  import type { Topic, PermissionLevel, TopicPermission } from '$lib/stores/topics';
  import type { RawTopic, RawRoom, RawMessage, RawMaterial, RawRoomUser } from '$lib/types/signalr';
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
  import UserSettingModal from '$lib/components/user/UserSettingModal.svelte';
  import { ui } from '$lib/stores/ui';
  import { api, getApiBaseUrl, getCurrentTenant } from '$lib/api/client';
  import type {
    MessageCreatedEvent,
    MessageUpdatedEvent,
    MessageDeletedEvent,
    RoomCreatedEvent,
    RoomUpdatedEvent,
    RoomDeletedEvent,
    TopicCreatedEvent,
    TopicUpdatedEvent,
    TopicDeletedEvent,
    TopicUnreadUpdatedEvent,
    RoomUserJoinedEvent,
    RoomUserLeftEvent,
    RoomUserUpdatedEvent,
    RoomUserRoleAddedEvent,
    RoomUserRoleRemovedEvent,
  } from '$lib/types/signalr';

  let isLoading = $state(true);
  let loadError = $state<string | null>(null);
  let loadedTopicId = $state<string | null>(null);
  let loadRequestId = $state(0);
  let loadedRoomFilesId = $state<string | null>(null);
  let filesLoadRequestId = $state(0);
  let lastAppliedUrlTopicId = $state<string | null>(null);
  let checkedRoomUserId = $state<string | null>(null);
  let signalRStarted = $state(false);  // SignalR接続開始済みフラグ

  // Tree描画完了フラグ
  let isTreeRendered = $state(false);
  let treeRenderTimeout: ReturnType<typeof setTimeout> | null = null;
  let hasScrolledToMessages = $state(false);
  let scrollTimeout: ReturnType<typeof setTimeout> | null = null;
  let messageScrollListenerCleanup: (() => void) | null = null;
  let messageHub: HubConnection | null = null;
  let messageHubTenant: string | null = null;
  let messageHubTopicId: string | null = null;
  let messageSyncTimer: ReturnType<typeof setTimeout> | null = null;
  let markAsReadTimer: ReturnType<typeof setTimeout> | null = null;
  let messageHubConnected = $state(false);

  // Mark as read deduplication - prevent concurrent calls for same topic
  const pendingMarkAsRead = new Map<string, Promise<number | null>>();
  let roomTopicHub: HubConnection | null = null;
  let roomTopicHubTenant: string | null = null;
  let roomTopicHubRoomId: string | null = null;
  let roomTopicHubConnected = $state(false);
  let roomUserSyncHub: HubConnection | null = null;
  let roomUserSyncHubTenant: string | null = null;
  let roomUserSyncHubRoomId: string | null = null;
  let roomUserSyncHubUserId: string | null = null;
  let roomUserSyncHubConnected = $state(false);

  // RoomUser参加完了イベントハンドラー
  async function handleRoomUserJoined(event: Event) {
    const customEvent = event as CustomEvent;
    console.log('[RoomPage] RoomUser joined event received:', customEvent.detail);

    // イベント発生時の値をキャプチャ（stale closure防止）
    const tenant = $page.params.tenant ?? getCurrentTenant();
    let room = $currentRoom;

    // $currentRoom が設定されていない場合、roomId からRoomを探す
    const eventRoomId = customEvent.detail?.roomId;
    if (!room && eventRoomId) {
      room = $roomList.find(r => r.id === eventRoomId) ?? null;
      if (room) {
        setCurrentRoom(room);
      }
    }

    if (!tenant || !room) {
      console.error('[RoomPage] Tenant or Room not found, cannot reload data');
      ui.addNotification({ type: 'error', message: 'Failed to reload room data: Missing context' });
      return;
    }

    // 既に読み込み済みの場合はスキップ（二重読み込み防止）
    if (room.id === lastLoadedRoomId) {
      console.log('[RoomPage] Room already loaded, skipping');
      ui.addNotification({ type: 'success', message: 'Room joined successfully' });
      return;
    }
    lastLoadedRoomId = room.id;

    try {
      // 並列でTopics、Files、子孫ロードを実行
      await Promise.all([
        // Topicsの再取得
        api.get<any[]>(`/${tenant}/api/topic/room/${room.id}/root-with-unread`)
          .then(async (topicsResponse) => {
            const topics = Array.isArray(topicsResponse) ? topicsResponse.map(normalizeTopic) : [];
            const topicsWithUnread = topics.map(topic => ({
              ...topic,
              unreadCount: topic.unreadCount || 0
            }));
            setTopics(topicsWithUnread);
          })
          .catch(err => {
            console.error('Failed to load root topics with unread:', err);
            // フォールバック
            return api.get<any[]>(`/${tenant}/api/topic/room/${room.id}/root`)
              .then(topicsResponse => {
                const topics = Array.isArray(topicsResponse) ? topicsResponse.map(normalizeTopic) : [];
                setTopics(topics);
              });
          }),
        // Filesの再取得
        loadRoomFiles(tenant, room.id),
        // 子孫ロード
        loadDescendantsForExpandedTopics(tenant),
      ]);

      console.log('[RoomPage] Data reloaded after RoomUser joined');
      ui.addNotification({ type: 'success', message: 'Room joined successfully' });
    } catch (err) {
      console.error('[RoomPage] Failed to reload data after RoomUser joined:', err);
      ui.addNotification({ type: 'error', message: 'Failed to reload room data after joining' });
    }
  }

  // Topic fetch deduplication map
  const pendingTopicFetches = new Map<string, Promise<any>>();
  type PendingTopicPathLoad = { topicId: string; roomId: string; expandAncestors: boolean };
  type PendingHasChildrenRefresh = { topicId: string; roomId: string };
  const pendingTopicPathLoads = new Map<string, PendingTopicPathLoad>();
  const pendingHasChildrenRefreshes = new Map<string, PendingHasChildrenRefresh>();
  const TOPIC_EVENT_BATCH_DELAY_MS = 150;
  const TOPIC_FALLBACK_SYNC_MIN_INTERVAL_MS = 5000;
  let topicEventBatchTimer: ReturnType<typeof setTimeout> | null = null;
  let isTopicEventBatchRunning = false;
  const lastTopicFallbackSyncAtByRoom = new Map<string, number>();

  // Lazy-loaded heavy modal components (PDF/Image editors)
  let PdfViewerModalComponent = $state<any | null>(null);
  let PdfEditorModalComponent = $state<any | null>(null);
  let ImageEditorModalComponent = $state<any | null>(null);
  let isLoadingPdfViewerModal = false;
  let isLoadingPdfEditorModal = false;
  let isLoadingImageEditorModal = false;

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

  function scheduleTopicEventBatch() {
    if (topicEventBatchTimer || isTopicEventBatchRunning) return;
    topicEventBatchTimer = setTimeout(() => {
      topicEventBatchTimer = null;
      void flushTopicEventBatch();
    }, TOPIC_EVENT_BATCH_DELAY_MS);
  }

  function queueTopicPathLoad(topicId: string, roomId: string, expandAncestors = false) {
    if (!topicId || !roomId) return;
    const key = `${roomId}:${topicId}`;
    const existing = pendingTopicPathLoads.get(key);
    pendingTopicPathLoads.set(key, {
      topicId,
      roomId,
      expandAncestors: (existing?.expandAncestors ?? false) || expandAncestors,
    });
    scheduleTopicEventBatch();
  }

  function queueHasChildrenRefresh(topicId: string | null, roomId: string | null) {
    if (!topicId || !roomId) return;
    const key = `${roomId}:${topicId}`;
    pendingHasChildrenRefreshes.set(key, { topicId, roomId });
    scheduleTopicEventBatch();
  }

  async function flushTopicEventBatch() {
    if (isTopicEventBatchRunning) {
      scheduleTopicEventBatch();
      return;
    }

    const tenant = messageHubTenant ?? $page.params.tenant ?? getCurrentTenant();
    if (!tenant) {
      return;
    }
    const activeRoomId = $currentRoom?.id ?? null;
    if (!activeRoomId) {
      return;
    }

    isTopicEventBatchRunning = true;
    const queuedPathRequests = Array.from(pendingTopicPathLoads.values());
    const queuedHasChildrenRequests = Array.from(pendingHasChildrenRefreshes.values());
    pendingTopicPathLoads.clear();
    pendingHasChildrenRefreshes.clear();

    const pathLoadRequests = queuedPathRequests.filter((request) => request.roomId === activeRoomId);
    const hasChildrenRequests = queuedHasChildrenRequests.filter((request) => request.roomId === activeRoomId);

    let failedPathTopicIds: string[] = [];

    try {
      if (pathLoadRequests.length > 0) {
        const loadResults = await Promise.all(
          pathLoadRequests.map(async ({ topicId, expandAncestors }) => {
            try {
              const loaded = await ensureTopicPathLoaded(tenant, topicId, { expandAncestors });
              return { topicId, loaded: loaded !== null };
            } catch (err) {
              console.error('Failed to load topic path in batch:', { topicId, err });
              return { topicId, loaded: false };
            }
          })
        );
        failedPathTopicIds = loadResults
          .filter((result) => !result.loaded)
          .map((result) => result.topicId);
      }

      if (failedPathTopicIds.length > 0 && $currentRoom) {
        const now = Date.now();
        const includesActiveTopic = failedPathTopicIds.some((id) => id === loadedTopicId || id === urlTopicId);
        const includesExpandedTopic = failedPathTopicIds.some((id) => $expandedTopics.has(id));
        const shouldFallbackSync = includesActiveTopic || includesExpandedTopic;
        const lastSyncedAt = lastTopicFallbackSyncAtByRoom.get(activeRoomId) ?? 0;
        const cooldownElapsed = now - lastSyncedAt >= TOPIC_FALLBACK_SYNC_MIN_INTERVAL_MS;
        if (shouldFallbackSync && cooldownElapsed) {
          lastTopicFallbackSyncAtByRoom.set(activeRoomId, now);
          await loadDescendantsForExpandedTopics(tenant);
        }
      }

      if (hasChildrenRequests.length > 0) {
        const uniqueHasChildrenIds = Array.from(new Set(hasChildrenRequests.map((request) => request.topicId)));
        await Promise.allSettled(
          uniqueHasChildrenIds.map((topicId) => refreshTopicHasChildren(topicId, tenant))
        );
      }
    } finally {
      isTopicEventBatchRunning = false;
      if (pendingTopicPathLoads.size > 0 || pendingHasChildrenRefreshes.size > 0) {
        scheduleTopicEventBatch();
      }
    }
  }

  $effect(() => {
    const activeRoomId = $currentRoom?.id ?? null;
    if (!activeRoomId) return;
    if (pendingTopicPathLoads.size === 0 && pendingHasChildrenRefreshes.size === 0) return;
    scheduleTopicEventBatch();
  });

  async function ensureLazyModalsLoaded(modalIds: Set<string>) {
    if (modalIds.has('pdf-viewer') && !PdfViewerModalComponent && !isLoadingPdfViewerModal) {
      isLoadingPdfViewerModal = true;
      try {
        const mod = await import('$lib/components/documents/PdfViewerModal.svelte');
        PdfViewerModalComponent = mod.default;
      } finally {
        isLoadingPdfViewerModal = false;
      }
    }

    if (modalIds.has('pdf-editor') && !PdfEditorModalComponent && !isLoadingPdfEditorModal) {
      isLoadingPdfEditorModal = true;
      try {
        const mod = await import('$lib/components/documents/PdfEditorModal.svelte');
        PdfEditorModalComponent = mod.default;
      } finally {
        isLoadingPdfEditorModal = false;
      }
    }

    if (modalIds.has('image-editor') && !ImageEditorModalComponent && !isLoadingImageEditorModal) {
      isLoadingImageEditorModal = true;
      try {
        const mod = await import('$lib/components/images/ImageEditorModal.svelte');
        ImageEditorModalComponent = mod.default;
      } finally {
        isLoadingImageEditorModal = false;
      }
    }
  }

  $effect(() => {
    const activeModalIds = new Set(($ui.activeModals ?? []).map((modal) => modal.id));
    if (activeModalIds.size === 0) return;
    void ensureLazyModalsLoaded(activeModalIds);
  });

  async function startMessageHub(tenant: string) {
    if (messageHub && messageHubTenant === tenant) return;

    await stopMessageHub();

    const connection = new HubConnectionBuilder()
      .withUrl(buildMessageHubUrl(tenant), { withCredentials: true })
      .withAutomaticReconnect()
      .build();

    connection.on('MessageCreated', (raw: MessageCreatedEvent) => {
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

        // SignalR受信時の処理
        if (normalized.topicId) {
          // 新しいメッセージで、現在選択中のトピックの場合は即座に既読にする
          const isSelectedTopic = loadedTopicId === normalized.topicId;

          if (!exists && isSelectedTopic && !document.hidden) {
            console.log('[MessageCreated] New message for selected topic, marking as read immediately:', {
              topicId: normalized.topicId,
              loadedTopicId,
              messageId: normalized.id
            });
            // 既読APIを呼び、レスポンスの未読数で更新
            void markTopicAsRead(normalized.topicId).then(unreadCount => {
              if (unreadCount !== null) {
                updateTopic(normalized.topicId, { unreadCount });
              }
            });
          } else if (!exists) {
            console.log('[MessageCreated] New message for different topic or page hidden:', {
              topicId: normalized.topicId,
              loadedTopicId,
              isSelectedTopic,
              pageHidden: document.hidden
            });
          }
        }
      } catch (error) {
        console.error('Failed to process MessageCreated event:', error, raw);
      }
    });

    connection.on('MessageUpdated', (raw: MessageUpdatedEvent) => {
      const normalized = normalizeMessage(raw);
      updateMessage(normalized.id, normalized);
      if (normalized.topicId) scheduleMessageSync(normalized.topicId);
    });

    connection.on('MessageDeleted', (raw: MessageDeletedEvent) => {
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

    connection.on('RoomCreated', (raw: RoomCreatedEvent) => {
      const normalized = normalizeRoom(raw);
      const exists = $roomList.some((r) => r.id === normalized.id);
      if (exists) {
        updateRoom(normalized.id, normalized);
      } else {
        addRoom(normalized);
      }
    });

    connection.on('RoomUpdated', (raw: RoomUpdatedEvent) => {
      const normalized = normalizeRoom(raw);
      updateRoom(normalized.id, normalized);
    });

    connection.on('RoomDeleted', (raw: RoomDeletedEvent) => {
      const roomId = raw?.roomId ?? raw?.RoomId ?? '';
      if (!roomId) return;
      deleteRoom(roomId);
    });

    connection.on('TopicCreated', (raw: TopicCreatedEvent) => {
      const normalized = normalizeTopic(raw);
      if (!$currentRoom || normalized.roomId !== $currentRoom.id) return;

      const existing = $topicList.find((t) => t.id === normalized.id);
      const exists = !!existing;
      if (exists) {
        // TopicRealtimeDto is partial for per-user fields; keep local unread/permission state.
        updateTopic(normalized.id, {
          parentId: normalized.parentId,
          roomId: normalized.roomId,
          title: normalized.title,
          description: normalized.description,
          sourceMessageId: normalized.sourceMessageId ?? null,
          messageCount: normalized.messageCount,
          hasChildren: normalized.hasChildren,
          createdAt: normalized.createdAt,
          updatedAt: normalized.updatedAt,
        });
      } else {
        addTopic(normalized);
      }

      const parentId = normalized.parentId ?? null;
      if (parentId) {
        // Optimistic UI update first; authoritative value is refreshed in batch.
        updateTopic(parentId, { hasChildren: true });
        queueHasChildrenRefresh(parentId, normalized.roomId);
      }

      // Remote events should not force UI expansion.
      queueTopicPathLoad(normalized.id, normalized.roomId, false);
    });

    connection.on('TopicUpdated', (raw: TopicUpdatedEvent) => {
      const normalized = normalizeTopic(raw);
      if (!$currentRoom || normalized.roomId !== $currentRoom.id) {
        const existing = $topicList.find((t) => t.id === normalized.id);
        if (existing) deleteTopic(normalized.id);
        return;
      }

      const existing = $topicList.find((t) => t.id === normalized.id);
      if (!existing) {
        addTopic(normalized);
        queueTopicPathLoad(normalized.id, normalized.roomId, false);
      }

      const normalizedParentId = normalized.parentId ?? null;
      const previousParentId = existing?.parentId ?? null;
      if (previousParentId !== normalizedParentId) {
        const previousParentHadChildren = previousParentId
          ? ($topicList.find((t) => t.id === previousParentId)?.hasChildren ?? false)
          : false;
        moveTopicParent(normalized.id, normalizedParentId);

        // Avoid false flicker until authoritative refresh finishes.
        if (previousParentId && previousParentHadChildren) {
          updateTopic(previousParentId, { hasChildren: true });
        }
        if (normalizedParentId) {
          // Optimistic update: moved-in parent gets children immediately.
          updateTopic(normalizedParentId, { hasChildren: true });
        }
        queueHasChildrenRefresh(previousParentId, normalized.roomId);
        queueHasChildrenRefresh(normalizedParentId, normalized.roomId);
      }

      // unreadCount フィールドがイベントに含まれない場合のみ既存値を保持する。
      // RoomTopicHub payload is room-scoped and may not carry accurate per-user unread.
      const unreadCount = existing?.unreadCount ?? normalized.unreadCount;
      const userPermission = (raw?.userPermission ?? raw?.UserPermission) as PermissionLevel | undefined;
      const permissions = (raw?.permissions ?? raw?.Permissions) as string[] | undefined;

      updateTopic(normalized.id, {
        parentId: normalized.parentId,
        roomId: normalized.roomId,
        title: normalized.title,
        description: normalized.description,
        creatorId: normalized.creatorId,
        messageCount: normalized.messageCount,
        unreadCount,
        userPermission,
        permissions,
        hasChildren: normalized.hasChildren,
        createdAt: normalized.createdAt,
        updatedAt: normalized.updatedAt,
      });

      queueTopicPathLoad(normalized.id, normalized.roomId, false);
    });

    connection.on('TopicDeleted', (raw: TopicDeletedEvent) => {
      const topicId = raw?.topicId ?? raw?.TopicId ?? '';
      const roomId = raw?.roomId ?? raw?.RoomId ?? '';
      const parentId = raw?.parentId ?? raw?.ParentId ?? null;
      if (!topicId) return;
      if ($currentRoom && roomId && roomId !== $currentRoom.id) return;

      deleteTopic(topicId);

      if (parentId) {
        const knownChildrenRemain = $topicList.some((t) => t.parentId === parentId);
        // Only apply optimistic true; avoid false flicker before server reconciliation.
        if (knownChildrenRemain) {
          updateTopic(parentId, { hasChildren: true });
        }
        const eventRoomId = roomId || $currentRoom?.id || '';
        queueHasChildrenRefresh(parentId, eventRoomId);
      }
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

    connection.on('TopicUnreadUpdated', async (raw: TopicUnreadUpdatedEvent) => {
      // SignalRはPascalCaseでシリアライズされる
      const topicId = raw?.TopicId ?? raw?.topicId ?? '';
      const unreadCount = raw?.UnreadCount ?? raw?.unreadCount ?? 0;

      if (!topicId) return;

      // SignalRイベントから直接未読数を更新（API再取得をスキップしてタイムラグを回避）
      updateTopic(topicId, { unreadCount });
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

  function normalizeRoom(raw: RawRoom) {
    const id = raw?.id ?? raw?.Id ?? '';
    const name = raw?.name ?? raw?.Name ?? '';
    const createdAt = raw?.createdAt ?? raw?.CreatedAt ?? null;
    const updatedAt = raw?.updatedAt ?? raw?.UpdatedAt ?? null;

    return {
      id,
      name,
      description: raw?.description ?? raw?.Description,
      joinPolicy: raw?.joinPolicy ?? raw?.JoinPolicy ?? 0,
      avatar: raw?.avatar ?? raw?.Avatar,
      createdAt: createdAt ? new Date(createdAt) : new Date(),
      updatedAt: updatedAt ? new Date(updatedAt) : new Date(),
      ownerId: raw?.ownerId ?? raw?.OwnerId ?? raw?.createdUserId ?? raw?.CreatedUserId ?? '',
      memberCount: raw?.memberCount ?? raw?.MemberCount ?? 0,
      unreadCount: raw?.unreadCount ?? raw?.UnreadCount ?? 0,
      isArchived: raw?.isArchived ?? raw?.IsArchived ?? false,
      canEdit: (raw?.canEdit ?? raw?.CanEdit ?? undefined) as boolean | undefined,
      canDelete: (raw?.canDelete ?? raw?.CanDelete ?? undefined) as boolean | undefined,
      canJoin: (raw?.canJoin ?? raw?.CanJoin ?? true) as boolean,
      isJoined: (raw?.isJoined ?? raw?.IsJoined ?? false) as boolean,
      settings: raw?.settings ?? raw?.Settings,
    };
  }

  function normalizeTopic(raw: RawTopic) {
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
      createdAt: createdAt ? new Date(createdAt as string) : new Date(),
      updatedAt: updatedAt ? new Date(updatedAt as string) : new Date(),
      creatorId: raw?.creatorId ?? raw?.CreatorId ?? '',
      messageCount: raw?.messageCount ?? raw?.MessageCount ?? 0,
      unreadCount: raw?.unreadCount ?? raw?.UnreadCount ?? 0,
      userPermission: (raw?.userPermission ?? raw?.UserPermission ?? 'read') as PermissionLevel,
      permissions: raw?.permissions ?? raw?.Permissions ?? [],
      hasChildren: raw?.hasChildren ?? raw?.HasChildren ?? false,
    };
  }

  /**
   * Fetch a topic once with deduplication
   * Prevents duplicate fetches of the same topic
   */
  async function fetchTopicOnce(tenant: string, topicId: string): Promise<Topic | null> {
    const cacheKey = `topic:${tenant}:${topicId}`;
    const cached = pendingTopicFetches.get(cacheKey);
    if (cached) return cached;

    // Create new fetch promise
    const promise = api.get<RawTopic>(`/${tenant}/api/topic/${topicId}`)
      .then(data => {
        pendingTopicFetches.delete(cacheKey);
        if (!data) return null;
        return normalizeTopic(data);
      })
      .catch(err => {
        pendingTopicFetches.delete(cacheKey);
        throw err;
      });

    pendingTopicFetches.set(cacheKey, promise);
    return promise;
  }

  async function ensureTopicPathLoaded(
    tenant: string,
    topicId: string,
    options?: { expandAncestors?: boolean }
  ) {
    const chain: Topic[] = [];
    let cursorId: string | null = topicId;
    const visited = new Set<string>();

    // Fetch all topics in the path (in parallel for better performance)
    const fetchPromises: Map<string, Promise<Topic | null>> = new Map();

    while (cursorId && !visited.has(cursorId)) {
      visited.add(cursorId);
      // Use fetchTopicOnce for deduplication
      fetchPromises.set(cursorId, fetchTopicOnce(tenant, cursorId));
      const topic = await fetchPromises.get(cursorId);
      if (!topic) break;
      chain.push(topic);
      cursorId = topic.parentId ?? null;
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
          unreadCount: t.unreadCount,
          messageCount: t.messageCount,
          updatedAt: t.updatedAt,
          sourceMessageId: t.sourceMessageId ?? null,
        });
      }
    }

    const shouldExpandAncestors = options?.expandAncestors ?? true;
    if (shouldExpandAncestors) {
      for (let i = 0; i < chain.length - 1; i++) {
        const id = chain[i].id;
        if (!$expandedTopics.has(id)) toggleTopicExpansion(id);
      }
    }

    return chain[chain.length - 1] ?? null;
  }

  // 展開されているトピックの子孫を再帰的に取得（並列処理版）
  async function loadDescendantsForExpandedTopics(tenant: string): Promise<void> {
    // 新しいAPI: ルーム内の全トピックを未読カウント付きで一括取得
    if (!$currentRoom) return;

    try {
      const response = await api.get<any[]>(`/${tenant}/api/topic/room/${$currentRoom.id}/all-with-unread`);
      const allTopics = Array.isArray(response) ? response.map(normalizeTopic) : [];

      console.log(`[loadDescendantsForExpandedTopics] Loaded ${allTopics.length} topics from all-with-unread API`);

      // 全トピックをストアに追加または更新
      allTopics.forEach((topic) => {
        const existing = $topicList.find((t) => t.id === topic.id);
        if (!existing) {
          addTopic(topic);
        } else {
          // 既存のトピックの場合は未読カウントなどの情報を更新
          updateTopic(topic.id, {
            unreadCount: topic.unreadCount,
            messageCount: topic.messageCount,
            hasChildren: topic.hasChildren,
          });
        }
      });

      console.log('[loadDescendantsForExpandedTopics] All topics loaded with unread counts');
    } catch (err) {
      console.error('Failed to load all topics with unread:', err);
    }
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
      if (existing.roomId === room.id) {
        setSelectedTopic(existing);
        // トピックを選択したときに既読にする
        if (!document.hidden) {
          console.log('Topic selected from URL, marking as read:', existing.id);
          setTimeout(() => {
            void markTopicAsRead(existing.id).then(unreadCount => {
              if (unreadCount !== null) {
                updateTopic(existing.id, { unreadCount });
              }
            });
          }, 100);
        }
        return existing;
      }
    }

    try {
      const loaded = await ensureTopicPathLoaded(tenant, urlTopicId);
      if (loaded && loaded.roomId === room.id) {
        setSelectedTopic(loaded);
        // トピックを選択したときに既読にする
        if (!document.hidden) {
          console.log('Topic loaded from URL, marking as read:', loaded.id);
          setTimeout(() => {
            void markTopicAsRead(loaded.id).then(unreadCount => {
              if (unreadCount !== null) {
                updateTopic(loaded.id, { unreadCount });
              }
            });
          }, 100);
        }
        return loaded;
      }
    } catch {
      // ignore
    }
    return null;
  }

  /// <summary>
  /// トピックの未読状態をバックエンドから取得して更新する（シンプル版）
  /// </summary>
  async function refreshTopicUnreadStatus(topicId: string, tenant: string) {
    try {
      const updated = await api.get<RawTopic>(`/${tenant}/api/topic/${topicId}`);
      if (updated) {
        const normalized = normalizeTopic(updated);
        if (normalized.id) {
          updateTopic(normalized.id, {
            unreadCount: normalized.unreadCount
          });
        }
      }
    } catch (err) {
      console.error('Failed to refresh topic unread status:', err);
    }
  }

  async function refreshTopicHasChildren(topicId: string, tenant: string) {
    try {
      const updated = await api.get<RawTopic>(`/${tenant}/api/topic/${topicId}`);
      if (!updated) return;
      const normalized = normalizeTopic(updated);
      if (normalized.id) {
        updateTopic(normalized.id, { hasChildren: normalized.hasChildren });
      }
    } catch (err) {
      console.error('Failed to refresh topic hasChildren status:', err);
    }
  }

  function normalizeMessage(raw: RawMessage): Message {
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
        ? rawFiles.map((f: RawMaterial) => {
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
      canEdit: false,
      canDelete: false,
      childTopicId: (raw?.childTopicId || raw?.ChildTopicId) || undefined,
      childTopicTitle: (raw?.childTopicTitle || raw?.ChildTopicTitle) || undefined,
    };
  }

  function mergeMessagesForTopic(topicId: string, incoming: ReturnType<typeof normalizeMessage>[]) {
    const existing = $messageList.filter((m) => m.topicId === topicId);
    const map = new Map<string, typeof existing[number]>();
    existing.forEach((m) => map.set(m.id, m));
    incoming.forEach((m) => {
      const existingMsg = map.get(m.id);
      map.set(m.id, existingMsg ? { ...existingMsg, ...m } : m);
    });

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

  // メッセージ表示領域のスクロールイベントを監視
  function setupMessageScrollListener() {
    if (messageScrollListenerCleanup) {
      messageScrollListenerCleanup();
      messageScrollListenerCleanup = null;
    }

    const messageContainer = document.querySelector('.room-messages-container');
    if (!messageContainer) return;

    const handleScroll = () => {
      if (scrollTimeout) clearTimeout(scrollTimeout);

      const scrollTop = messageContainer.scrollTop;

      // ユーザーがメッセージを表示していることを検出（スクロールが発生した場合）
      if (scrollTop > 0) {
        scrollTimeout = setTimeout(() => {
          if (loadedTopicId) {
            console.log('User scrolled, marking topic as read:', loadedTopicId);
            void markTopicAsRead(loadedTopicId);
          }
        }, 1000); // スクロール停止後1秒で未読更新
      }
    };

    messageContainer.addEventListener('scroll', handleScroll, { passive: true });
    messageScrollListenerCleanup = () => {
      messageContainer.removeEventListener('scroll', handleScroll);
      if (scrollTimeout) {
        clearTimeout(scrollTimeout);
        scrollTimeout = null;
      }
    };

    // スクロールバーが出ていない場合（コンテンツが画面に収まっている場合）、即座に既読にする
    requestAnimationFrame(() => {
      const scrollHeight = messageContainer.scrollHeight;
      const clientHeight = messageContainer.clientHeight;
      if (scrollHeight <= clientHeight && loadedTopicId) {
        console.log('Content fits in viewport, marking topic as read immediately:', loadedTopicId);
        void markTopicAsRead(loadedTopicId);
      }
    });
  }

  async function markTopicAsRead(topicId: string, retryCount = 0): Promise<number | null> {
    const tenant = $page.params.tenant ?? getCurrentTenant();
    if (!tenant) return null;

    // Check if there's already a pending request for this topic
    const existing = pendingMarkAsRead.get(topicId);
    if (existing) {
      console.log(`MarkAsRead already in progress for topic ${topicId}, waiting for existing request`);
      return existing;
    }

    // Create new request promise
    let requestPromise: Promise<number | null> | undefined;
    let cleanupPromise: Promise<number | null> | undefined;

    cleanupPromise = (async () => {
      let attempt = retryCount;
      while (attempt <= 3) {
        try {
          console.log(`Marking topic ${topicId} as read (attempt ${attempt + 1})`);
          const response = await api.post<number>(`/${tenant}/api/message/topic/${topicId}/markAsRead`);
          const unreadCount = typeof response === 'number' ? response : 0;
          console.log(`Mark topic ${topicId} as read, unread count: ${unreadCount}`);
          return unreadCount;
        } catch (err: unknown) {
          const error = err as Error & { status?: number };
          console.error('Failed to mark topic as read:', error);
          console.error('Topic ID:', topicId);
          console.error('User ID:', $auth?.user?.id);

          const canRetry = attempt < 3 && !!error.status && error.status >= 500;
          if (!canRetry) {
            if (error.status && error.status >= 400 && error.status < 500) {
              alert('未読の更新に失敗しました。ページを再読み込みしてください。');
            }
            return null;
          }

          attempt += 1;
          console.log(`Retrying markAsRead for topic ${topicId} (attempt ${attempt})`);
          await new Promise((resolve) => setTimeout(resolve, 1000 * attempt));
        }
      }
      return null;
    })().finally(() => {
      // Clean up pending map when done
      if (pendingMarkAsRead.get(topicId) === cleanupPromise) {
        pendingMarkAsRead.delete(topicId);
      }
    });

    requestPromise = cleanupPromise;
    pendingMarkAsRead.set(topicId, requestPromise);
    return requestPromise;
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
    if (markAsReadTimer) {
      clearTimeout(markAsReadTimer);
      markAsReadTimer = null;
    }

    messageSyncTimer = setTimeout(async () => {
      const tenant = $page.params.tenant ?? getCurrentTenant();
      if (!tenant) return;

      const anchorId = getAnchorIdForTopic(topicId, 10);
      if (!anchorId) return;

      try {
        const response = await api.get<any[]>(
          `/${tenant}/api/message/topic/${topicId}/after/${anchorId}`,
          { params: { take: 50 } }
        );
        const list = Array.isArray(response) ? response.map(normalizeMessage) : [];
        mergeMessagesForTopic(topicId, list);

        // 同期後に未読更新を実行（同期したメッセージが表示されたと判断）
        markAsReadTimer = setTimeout(async () => {
          console.log('Executing markTopicAsRead after message sync for topic:', topicId);
          await markTopicAsRead(topicId);
          markAsReadTimer = null;
        }, 300);
      } catch (err) {
        console.error('Failed to sync messages:', err);
      }
    }, 300);
  }

  function normalizeMaterial(raw: RawMaterial) {
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
      fileType: (raw?.fileType ?? raw?.FileType ?? 'other') as 'image' | 'pdf' | 'document' | 'other',
      uploadedAt: createdAt ? new Date(createdAt) : new Date(),
      uploadedBy: raw?.uploadedBy ?? raw?.UploadedBy ?? '',
      uploadedByName: raw?.uploadedByName ?? raw?.UploadedByName ?? raw?.uploadedBy ?? raw?.UploadedBy ?? 'Unknown',
      versions: [], // RawMaterial doesn't contain versionNumber required for FileVersion
    };
  }

  async function loadTenantData(options?: { skipRoomUserCheck?: boolean; skipTopicSelection?: boolean; showLoading?: boolean }) {
    if (options?.showLoading !== false) {
      isLoading = true;
    }
    loadError = null;
    let tenant: string | null = null;

    try {
      tenant = $page.params.tenant ?? getCurrentTenant();
      if (!tenant) throw new Error('Tenant not found in URL');

      api.configureApiClient(tenant);

      // Step 1: 認証（必ず最初）
      await auth.fetchCurrentUser(tenant);

      // Step 2: 並列実行 - SignalR + Rooms + RoomUserチェック(URLにroomIdがあれば)
      signalRStarted = true;
      const roomIdFromUrl = $page.params.roomId;
      const shouldPrecheckRoomUser = !!roomIdFromUrl && !options?.skipRoomUserCheck;

      const [roomsResponse, , , precheckRoomUser] = await Promise.all([
        api.get<any[]>(`/${tenant}/api/Room`),
        startMessageHub(tenant).catch(err => {
          console.error('Failed to start message hub:', err);
        }),
        startRoomTopicHub(tenant).catch(err => {
          console.error('Failed to start room topic hub:', err);
        }),
        // URLにroomIdがある場合、Rooms取得と並列でRoomUserチェックを開始
        shouldPrecheckRoomUser
          ? api.get<RawRoomUser>(`/${tenant}/api/roomusers/room/${roomIdFromUrl}/me`).catch((err: unknown) => {
              if (err instanceof api.ApiError && err.status === 401) throw err;
              return null;
            })
          : Promise.resolve(undefined as RawRoomUser | null | undefined),
      ]);

      const rooms = Array.isArray(roomsResponse) ? roomsResponse.map(normalizeRoom) : [];
      setRooms(rooms);

      const roomId = roomIdFromUrl;
      console.log('[RoomPage] URL roomId:', roomId, 'Type:', typeof roomId);
      console.log('[RoomPage] Loaded rooms:', rooms.length, rooms);
      console.log('[RoomPage] First room id:', rooms[0]?.id, 'Type:', typeof rooms[0]?.id);

      const initialRoom = rooms.find((room) => room.id === roomId) ?? rooms[0] ?? null;
      console.log('[RoomPage] Selected initialRoom:', initialRoom);

      // Step 3: RoomUserチェック（setCurrentRoomより先に行う）
      if (initialRoom && !options?.skipRoomUserCheck) {
        let roomUserData: CurrentRoomUser | null = null;

        if (precheckRoomUser !== undefined && initialRoom.id === roomIdFromUrl) {
          // 並列チェックの結果を利用（URLのroomIdと一致）
          checkedRoomUserId = initialRoom.id;
          if (precheckRoomUser) {
            roomUserData = {
              id: precheckRoomUser.id ?? (precheckRoomUser as any).Id ?? '',
              displayName: precheckRoomUser.displayName ?? (precheckRoomUser as any).DisplayName ?? '',
              iconUrl: precheckRoomUser.iconUrl ?? (precheckRoomUser as any).IconUrl,
              useMainIcon: precheckRoomUser.useMainIcon ?? (precheckRoomUser as any).UseMainIcon ?? false,
            };
            roomsStore.setCurrentRoomUser(roomUserData);
          } else {
            roomsStore.setCurrentRoomUser(null);
          }
        } else {
          // URLのroomIdと異なるroomが選択された場合、改めてチェック
          checkedRoomUserId = null;
          roomUserData = await loadRoomUser(tenant, initialRoom.id);
        }

        if (!roomUserData) {
          console.log('[RoomPage] RoomUser not found, showing join modal');
          isLoading = false;
          ui.openModal({
            id: 'room-user-join',
            title: 'Join Room',
            type: 'custom',
            data: { roomId: initialRoom.id }
          });
          return;
        }
      }

      setCurrentRoom(initialRoom);

      // ここでローディング解除 - Roomが表示される
      isLoading = false;

      if (initialRoom) {
        // Step 4: RoomUserが存在する場合のみ、Topics + Files + RoomUserSyncHubを並列実行
        const userId = $auth?.user?.id ?? '';
        const [topicsResponse, filesResponse] = await Promise.all([
          api.get<any[]>(`/${tenant}/api/topic/room/${initialRoom.id}/root-with-unread`).catch(err => {
            console.error('Failed to load root topics with unread:', err);
            // フォールバックとして通常のAPIを使用
            return api.get<any[]>(`/${tenant}/api/topic/room/${initialRoom.id}/root`).catch(err => {
              console.error('Failed to load root topics (fallback):', err);
              return [];
            });
          }),
          loadRoomFiles(tenant, initialRoom.id).catch(err => {
            console.error('Failed to load room files:', err);
            return [];
          }),
          // userIdがある場合のみSignalR接続を開始
          userId ? startRoomUserSyncHub(tenant, initialRoom.id, userId).catch(err => {
            console.error('Failed to start room user sync hub:', err);
          }) : Promise.resolve(),
        ]);

        console.log('[RoomPage] Topics API response:', topicsResponse);
        const topics = Array.isArray(topicsResponse) ? topicsResponse.map(normalizeTopic) : [];
        console.log('[RoomPage] Normalized topics:', topics);

        // 新しいAPIではすでにunreadCountが含まれている
        const topicsWithUnread = topics.map(topic => {
          const unreadCount = topic.unreadCount || 0;
          console.log(`[RoomPage] Topic ${topic.id} (${topic.title}): unread count = ${unreadCount}, hasChildren = ${topic.hasChildren}`);
          return topic;
        });

        console.log('[RoomPage] Topics with unread counts:', topicsWithUnread.map(t => ({ id: t.id, title: t.title, unreadCount: t.unreadCount, hasChildren: t.hasChildren })));
        setTopics(topicsWithUnread);

        // Step 5: 並列実行 - トピック選択 + 子孫ロード
        let selected: Topic | null = null;
        if (!options?.skipTopicSelection) {
          [selected] = await Promise.all([
            selectTopicFromUrl(tenant, initialRoom),
            loadDescendantsForExpandedTopics(tenant),
          ]);

          lastAppliedUrlTopicId = urlTopicId ?? null;
        } else {
          // Topic選択をスキップする場合でも、子孫ロードは実行
          await loadDescendantsForExpandedTopics(tenant);
        }

        // Tree描画完了をマーク
        isTreeRendered = true;

        // トピック選択後に未読更新とメッセージ読み込みを開始
        if (selected) {
          loadedTopicId = selected.id;
          messages.setLoading(true);
          messages.setError(null);

          // 即座に未読更新を実行
          console.log('Topic selected, marking as read immediately:', selected.id);
          void markTopicAsRead(selected.id);

          const requestId = ++loadRequestId;
          api.get<any[]>(`/${tenant}/api/message/topic/${selected.id}`)
            .then(async (response) => {
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
              // メッセージ読み込み後、スクロールリスナーを設定
              setupMessageScrollListener();
              // 初回ロード時は即座に未読更新を実行
              if (!hasScrolledToMessages) {
                hasScrolledToMessages = true;
                console.log('Initial load, marking topic as read:', selected.id);
                void markTopicAsRead(selected.id);
              }
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
        error.status === 401
      ) {
        isLoading = false;
        await auth.logout(resolvedTenant);
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

  async function loadRoomUser(tenant: string, roomId: string): Promise<CurrentRoomUser | null> {
    if (checkedRoomUserId === roomId) {
      // 既にチェック済みで、現在のRoomUserを返す
      const currentUser = $currentRoomUser;
      if (currentUser?.id) {
        return currentUser;
      }
      // RoomUserが設定されていない場合はnullを返す
      return null;
    }
    checkedRoomUserId = roomId;

    try {
      const roomUserData = await api.get<RawRoomUser>(`/${tenant}/api/roomusers/room/${roomId}/me`);
      if (roomUserData) {
        const roomUser = {
          id: roomUserData.id ?? roomUserData.Id ?? '',
          displayName: roomUserData.displayName ?? roomUserData.DisplayName ?? '',
          iconUrl: roomUserData.iconUrl ?? roomUserData.IconUrl,
          useMainIcon: roomUserData.useMainIcon ?? roomUserData.UseMainIcon ?? false,
        };
        rooms.setCurrentRoomUser(roomUser);
        return roomUser;
      }
    } catch (err: unknown) {
      if (err instanceof api.ApiError) {
        if (err.status === 401) {
          // 未ログイン: ログインページにリダイレクト
          redirectToTenantLogin(tenant);
          return null;
        }
        if (err.status === 404) {
          // RoomUserが存在しない
          rooms.setCurrentRoomUser(null);
          return null;
        }
      }
      console.error('Failed to fetch RoomUser:', err);
    }
    return null;
  }

  onMount(() => {
    loadTenantData();

    // RoomUser参加完了イベントをリッスン
    window.addEventListener('room-user-joined', handleRoomUserJoined);

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
      // RoomUser参加完了イベントリスナーを削除
      window.removeEventListener('room-user-joined', handleRoomUserJoined);

      if (topicEventBatchTimer) {
        clearTimeout(topicEventBatchTimer);
        topicEventBatchTimer = null;
      }
      if (messageScrollListenerCleanup) {
        messageScrollListenerCleanup();
        messageScrollListenerCleanup = null;
      }
      pendingTopicPathLoads.clear();
      pendingHasChildrenRefreshes.clear();
      lastTopicFallbackSyncAtByRoom.clear();
      void stopMessageHub();
      void stopRoomTopicHub();
      void stopRoomUserSyncHub();
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

    api.get<any[]>(`/${tenant}/api/message/topic/${$selectedTopic.id}`)
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

        // メッセージ読み込み完了後、ページが表示されている場合は既読にする
        if (!document.hidden && loadedTopicId) {
          console.log('[MessageLoad] Messages loaded, marking topic as read:', loadedTopicId);
          void markTopicAsRead(loadedTopicId).then(unreadCount => {
            if (unreadCount !== null && loadedTopicId) {
              updateTopic(loadedTopicId, { unreadCount });
            }
          });
        }
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

  $effect(() => {
    const tenant = $page.params.tenant ?? getCurrentTenant();
    const roomId = $currentRoom?.id ?? null;
    if (!tenant || !roomId || isLoading) return;
    void loadRoomUser(tenant, roomId);
  });

  // Room変更時にTopicsを再読み込み
  let lastLoadedRoomId = $state<string | null>(null);

  $effect(() => {
    const tenant = $page.params.tenant ?? getCurrentTenant();
    const roomId = $currentRoom?.id ?? null;
    if (!tenant || !roomId || isLoading || roomId === lastLoadedRoomId) return;
    // RoomUserが存在しない場合はスキップ（権限エラーを防ぐ）
    if (!$currentRoomUser?.id) return;

    // Roomが変更された場合、Topicsを再読み込み
    console.log('[RoomPage] Room changed, reloading topics for room:', roomId);
    lastLoadedRoomId = roomId;

    (async () => {
      try {
        const [topicsResponse, filesResponse] = await Promise.all([
          api.get<any[]>(`/${tenant}/api/topic/room/${roomId}/root-with-unread`).catch(err => {
            console.error('Failed to load root topics with unread:', err);
            // フォールバックとして通常のAPIを使用
            return api.get<any[]>(`/${tenant}/api/topic/room/${roomId}/root`).catch(err => {
              console.error('Failed to load root topics (fallback):', err);
              return [];
            });
          }),
          loadRoomFiles(tenant, roomId).catch(err => {
            console.error('Failed to load room files:', err);
            return [];
          }),
        ]);

        console.log('[RoomPage] Topics API response:', topicsResponse);
        const topics = Array.isArray(topicsResponse) ? topicsResponse.map(normalizeTopic) : [];
        console.log('[RoomPage] Normalized topics:', topics);

        // 新しいAPIではすでにunreadCountが含まれている
        const topicsWithUnread = topics.map(topic => {
          const unreadCount = topic.unreadCount || 0;
          console.log(`[RoomPage] Topic ${topic.id} (${topic.title}): unread count = ${unreadCount}, hasChildren = ${topic.hasChildren}`);
          return topic;
        });

        console.log('[RoomPage] Topics with unread counts:', topicsWithUnread.map(t => ({ id: t.id, title: t.title, unreadCount: t.unreadCount, hasChildren: t.hasChildren })));
        setTopics(topicsWithUnread);

        // 子孫ロードも実行
        await loadDescendantsForExpandedTopics(tenant);
      } catch (err) {
        console.error('[RoomPage] Failed to reload topics after room change:', err);
      }
    })();
  });

  // document.visibilitychangeイベントを監視
  onMount(() => {
    const handleVisibilityChange = () => {
      if (!document.hidden && loadedTopicId) {
        console.log('[VisibilityChange] Document became visible, marking topic as read:', loadedTopicId);
        void markTopicAsRead(loadedTopicId).then(unreadCount => {
          if (unreadCount !== null && loadedTopicId) {
            updateTopic(loadedTopicId, { unreadCount });
          }
        });
      }
    };

    document.addEventListener('visibilitychange', handleVisibilityChange);

    return () => {
      document.removeEventListener('visibilitychange', handleVisibilityChange);
    };
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
      <div class="flex items-center gap-3">
        <RoomSelector />
        {#if $currentRoom}
          <button
            onclick={() => ui.openModal({
              id: 'room-settings',
              title: 'Room Settings',
              type: 'custom'
            })}
            class="text-text-light hover:text-primary transition-colors text-sm flex items-center gap-1"
            title="Room Settings"
          >
            <span>⚙</span>
            <span>Settings</span>
          </button>
        {/if}
      </div>
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
            <div class="pt-2 border-t border-border flex items-center justify-between gap-4">
              <ViewModeSelector />
              <button
                onclick={() => ui.openModal({
                  id: 'topic-settings',
                  title: 'Topic Settings',
                  type: 'custom',
                  data: {
                    tenant: $page.params.tenant,
                    roomId: $page.params.roomId,
                    topicId: $selectedTopic.id
                  }
                })}
                class="text-text-light hover:text-primary transition-colors text-sm flex items-center gap-1"
                title="Topic Settings"
              >
                <span>⚙</span>
                <span>Settings</span>
              </button>
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
  {#if PdfViewerModalComponent}
    <PdfViewerModalComponent />
  {/if}
  {#if PdfEditorModalComponent}
    <PdfEditorModalComponent />
  {/if}
  {#if ImageEditorModalComponent}
    <ImageEditorModalComponent />
  {/if}
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
