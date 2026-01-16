import { writable, derived } from 'svelte/store';

/**
 * Message attachment
 */
export interface Attachment {
  id: string;
  fileName: string;
  mimeType: string;
  size: number;
  url: string;
  fileType?: 'image' | 'pdf' | 'document' | 'other';
  uploadedAt: Date;
  uploadedBy: string;
}

/**
 * Message information
 */
export interface Message {
  id: string;
  topicId: string;
  userId: string;
  userName: string;
  userDisplayName: string;
  userAvatar?: string;
  subject: string;
  content: string;
  replyToId?: string; // parent message ID
  createdAt: Date;
  updatedAt?: Date;
  attachments: Attachment[];
  isOwner: boolean;
  canEdit: boolean;
  canDelete: boolean;
  reactions?: { emoji: string; userIds: string[] }[];
  readBy?: string[]; // user IDs
  sortOrder?: number; // for custom ordering
  childTopicId?: string;
  childTopicTitle?: string;
}

/**
 * Messages store state
 */
export interface MessagesState {
  messages: Message[];
  messagesByTopic: Map<string, string[]>; // topicId -> message IDs
  sortedMessages: Message[];
  isLoading: boolean;
  error: string | null;
  lastUpdated: number | null;
  currentTopicId: string | null;
}

/**
 * Create messages store
 */
function createMessagesStore() {
  const { subscribe, set, update } = writable<MessagesState>({
    messages: [],
    messagesByTopic: new Map(),
    sortedMessages: [],
    isLoading: false,
    error: null,
    lastUpdated: null,
    currentTopicId: null,
  });

  return {
    subscribe,
    /**
     * Set messages for a topic
     */
    setMessages: (topicId: string, messages: Message[]) => {
      update((state) => {
        const messagesByTopic = new Map(state.messagesByTopic);
        messagesByTopic.set(topicId, messages.map((m) => m.id));

        // Merge messages - keep existing if not replaced
        const messagesMap = new Map(state.messages.map((m) => [m.id, m]));
        messages.forEach((m) => messagesMap.set(m.id, m));

        const allMessages = Array.from(messagesMap.values());
        const sorted = [...allMessages].sort((a, b) => a.createdAt.getTime() - b.createdAt.getTime());

        return {
          ...state,
          messages: allMessages,
          sortedMessages: sorted,
          messagesByTopic,
          currentTopicId: topicId,
          error: null,
          lastUpdated: Date.now(),
        };
      });
    },
    /**
     * Add a new message
     */
    addMessage: (message: Message) => {
      update((state) => {
        const messagesByTopic = new Map(state.messagesByTopic);
        const topicMessages = messagesByTopic.get(message.topicId) || [];
        messagesByTopic.set(message.topicId, [...topicMessages, message.id]);

        const newMessages = [...state.messages, message];
        const sorted = [...newMessages].sort((a, b) => a.createdAt.getTime() - b.createdAt.getTime());

        return {
          ...state,
          messages: newMessages,
          sortedMessages: sorted,
          messagesByTopic,
        };
      });
    },
    /**
     * Update message
     */
    updateMessage: (messageId: string, updates: Partial<Message>) => {
      update((state) => ({
        ...state,
        messages: state.messages.map((m) =>
          m.id === messageId ? { ...m, ...updates } : m
        ),
        sortedMessages: state.sortedMessages.map((m) =>
          m.id === messageId ? { ...m, ...updates } : m
        ),
      }));
    },
    /**
     * Delete message
     */
    deleteMessage: (messageId: string) => {
      update((state) => {
        const message = state.messages.find((m) => m.id === messageId);
        const messagesByTopic = new Map(state.messagesByTopic);

        if (message) {
          const topicMessages = messagesByTopic.get(message.topicId) || [];
          messagesByTopic.set(
            message.topicId,
            topicMessages.filter((id) => id !== messageId)
          );
        }

        const filtered = state.messages.filter((m) => m.id !== messageId);
        const sorted = [...filtered].sort((a, b) => a.createdAt.getTime() - b.createdAt.getTime());

        return {
          ...state,
          messages: filtered,
          sortedMessages: sorted,
          messagesByTopic,
        };
      });
    },
    /**
     * Add reaction to message
     */
    addReaction: (messageId: string, emoji: string, userId: string) => {
      update((state) => ({
        ...state,
        messages: state.messages.map((m) => {
          if (m.id === messageId) {
            const reactions = [...(m.reactions || [])];
            const reaction = reactions.find((r) => r.emoji === emoji);

            if (reaction) {
              reaction.userIds = [...new Set([...reaction.userIds, userId])];
            } else {
              reactions.push({ emoji, userIds: [userId] });
            }

            return { ...m, reactions };
          }
          return m;
        }),
      }));
    },
    /**
     * Remove reaction from message
     */
    removeReaction: (messageId: string, emoji: string, userId: string) => {
      update((state) => ({
        ...state,
        messages: state.messages.map((m) => {
          if (m.id === messageId) {
            const reactions = (m.reactions || []).map((r) => {
              if (r.emoji === emoji) {
                return {
                  ...r,
                  userIds: r.userIds.filter((id) => id !== userId),
                };
              }
              return r;
            });

            return {
              ...m,
              reactions: reactions.filter((r) => r.userIds.length > 0),
            };
          }
          return m;
        }),
      }));
    },
    /**
     * Update message sort order
     */
    updateMessageOrder: (messageId: string, sortOrder: number) => {
      update((state) => ({
        ...state,
        messages: state.messages.map((m) =>
          m.id === messageId ? { ...m, sortOrder } : m
        ),
      }));
    },
    /**
     * Reorder messages
     */
    reorderMessages: (messageOrders: { messageId: string; sortOrder: number }[]) => {
      update((state) => {
        const orderMap = new Map(
          messageOrders.map((m) => [m.messageId, m.sortOrder])
        );

        return {
          ...state,
          messages: state.messages.map((m) => ({
            ...m,
            sortOrder: orderMap.get(m.id) ?? m.sortOrder,
          })),
        };
      });
    },
    /**
     * Set loading state
     */
    setLoading: (isLoading: boolean) => {
      update((state) => ({ ...state, isLoading }));
    },
    /**
     * Set error
     */
    setError: (error: string | null) => {
      update((state) => ({ ...state, error }));
    },
    /**
     * Clear messages for a topic
     */
    clearTopicMessages: (topicId: string) => {
      update((state) => {
        const messagesByTopic = new Map(state.messagesByTopic);
        const topicMessageIds = messagesByTopic.get(topicId) || [];
        messagesByTopic.delete(topicId);

        return {
          ...state,
          messages: state.messages.filter(
            (m) => !topicMessageIds.includes(m.id)
          ),
          messagesByTopic,
        };
      });
    },
    /**
     * Clear all messages
     */
    clear: () => {
      set({
        messages: [],
        messagesByTopic: new Map(),
        sortedMessages: [],
        isLoading: false,
        error: null,
        lastUpdated: null,
        currentTopicId: null,
      });
    },
  };
}

export const messages = createMessagesStore();

/**
 * Derived stores
 */
export const messageList = derived(messages, ($messages) => $messages.messages);
export const messagesLoading = derived(messages, ($messages) => $messages.isLoading);
export const messagesError = derived(messages, ($messages) => $messages.error);
export const currentTopicId = derived(
  messages,
  ($messages) => $messages.currentTopicId
);

/**
 * Get messages for a specific topic
 */
export const getMessagesByTopic = (topicId: string) =>
  derived(messageList, ($messages) =>
    $messages.filter((m) => m.topicId === topicId)
  );

/**
 * Get messages grouped by topic
 */
export const messagesGroupedByTopic = derived(messageList, ($messages) => {
  const grouped = new Map<string, Message[]>();

  $messages.forEach((message) => {
    if (!grouped.has(message.topicId)) {
      grouped.set(message.topicId, []);
    }
    grouped.get(message.topicId)!.push(message);
  });

  return grouped;
});

/**
 * Get threaded messages for a specific topic
 */
export const getThreadedMessages = (topicId: string) =>
  derived(getMessagesByTopic(topicId), ($messages) => {
    const parentMessages: Message[] = [];
    const childrenMap = new Map<string, Message[]>();

    $messages.forEach((msg) => {
      if (!msg.replyToId) {
        parentMessages.push(msg);
      } else {
        if (!childrenMap.has(msg.replyToId)) {
          childrenMap.set(msg.replyToId, []);
        }
        childrenMap.get(msg.replyToId)!.push(msg);
      }
    });

    return {
      parentMessages,
      childrenMap,
    };
  });

/**
 * Get message by ID
 */
export const getMessageById = (messageId: string) =>
  derived(messageList, ($messages) => $messages.find((m) => m.id === messageId));

/**
 * Reply target (message being replied to)
 */
export const replyTargetId = writable<string | null>(null);
export const replyTarget = derived(
  [replyTargetId, messageList],
  ([$replyTargetId, $messages]) =>
    $replyTargetId ? $messages.find((m) => m.id === $replyTargetId) ?? null : null
);

/**
 * Get unread messages count
 */
export const unreadMessagesCount = derived(messageList, ($messages) => {
  // This would need to be calculated based on read receipts
  // Placeholder for now
  return $messages.length;
});

/**
 * Get recent messages
 */
export const recentMessages = (limit: number = 10) =>
  derived(messageList, ($messages) =>
    $messages.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()).slice(0, limit)
  );

/**
 * Update message order from array of sorted messages
 */
export function updateMessageOrder(sortedMessages: Message[]) {
  const orders = sortedMessages.map((m, index) => ({
    messageId: m.id,
    sortOrder: index,
  }));
  messages.reorderMessages(orders);
}

/**
 * Helper functions to interact with messages store
 */
export function addMessage(message: Message) {
  messages.addMessage(message);
}

export function updateMessage(messageId: string, updates: Partial<Message>) {
  messages.updateMessage(messageId, updates);
}

export function deleteMessage(messageId: string) {
  messages.deleteMessage(messageId);
}

export function setMessages(topicId: string, messagesList: Message[]) {
  messages.setMessages(topicId, messagesList);
}

export function startReply(messageId: string) {
  replyTargetId.set(messageId);
}

export function cancelReply() {
  replyTargetId.set(null);
}
