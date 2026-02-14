import { writable, derived } from 'svelte/store';
import { isCacheValid } from '$lib/utils/store';

const MESSAGES_CACHE_TTL = 30 * 1000; // 30秒 - メッセージは頻繁に変更される

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
  childTopicId?: string;
  childTopicTitle?: string;
}

export interface MessagesState {
  messages: Message[];
  messagesByTopic: Map<string, string[]>; // topicId -> message IDs
  sortedMessages: Message[];
  isLoading: boolean;
  error: string | null;
  lastUpdated: number | null;
  currentTopicId: string | null;
  cacheExpiry: number;
}

function createMessagesStore() {
  const { subscribe, set, update } = writable<MessagesState>({
    messages: [],
    messagesByTopic: new Map(),
    sortedMessages: [],
    isLoading: false,
    error: null,
    lastUpdated: null,
    currentTopicId: null,
    cacheExpiry: 0,
  });

  return {
    subscribe,
    setMessages: (topicId: string, messages: Message[]) => {
      update((state) => {
        const messagesByTopic = new Map(state.messagesByTopic);
        messagesByTopic.set(topicId, messages.map((m) => m.id));

        // メッセージをマージ - 置換されなかったものは保持
        const messagesMap = new Map(state.messages.map((m) => [m.id, m]));
        messages.forEach((m) => messagesMap.set(m.id, m));

        const allMessages = Array.from(messagesMap.values());
        const sorted = [...allMessages].sort((a, b) => {
          const aTime = a.createdAt?.getTime() ?? 0;
          const bTime = b.createdAt?.getTime() ?? 0;
          return aTime - bTime;
        });

        return {
          ...state,
          messages: allMessages,
          sortedMessages: sorted,
          messagesByTopic,
          currentTopicId: topicId,
          error: null,
          lastUpdated: Date.now(),
          cacheExpiry: Date.now() + MESSAGES_CACHE_TTL,
        };
      });
    },
    addMessage: (message: Message) => {
      update((state) => {
        // 重複チェック - 同じIDのメッセージが既に存在する場合は追加しない
        const existingMessage = state.messages.find((m) => m.id === message.id);
        if (existingMessage) {
          console.warn(`Message with ID ${message.id} already exists, skipping duplicate`);
          return state;
        }

        const messagesByTopic = new Map(state.messagesByTopic);
        const topicMessages = messagesByTopic.get(message.topicId) || [];
        const existingTopicMessageIds = new Set(topicMessages);
        if (!existingTopicMessageIds.has(message.id)) {
          messagesByTopic.set(message.topicId, [...topicMessages, message.id]);
        }

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
    setLoading: (isLoading: boolean) => {
      update((state) => ({ ...state, isLoading }));
    },
    setError: (error: string | null) => {
      update((state) => ({ ...state, error }));
    },
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
    clear: () => {
      set({
        messages: [],
        messagesByTopic: new Map(),
        sortedMessages: [],
        isLoading: false,
        error: null,
        lastUpdated: null,
        currentTopicId: null,
        cacheExpiry: 0,
      });
    },
  };
}

export const messages = createMessagesStore();

export const messageList = derived(messages, ($messages) => $messages?.messages ?? []);
export const messagesLoading = derived(messages, ($messages) => $messages.isLoading);
export const messagesError = derived(messages, ($messages) => $messages.error);
export const currentTopicId = derived(
  messages,
  ($messages) => $messages.currentTopicId
);

export const getMessagesByTopic = (topicId: string) =>
  derived(messageList, ($messages) =>
    $messages.filter((m) => m.topicId === topicId)
  );

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

export const getThreadedMessages = (topicId: string) =>
  derived(getMessagesByTopic(topicId), ($messages) => {
    const parentMessages: Message[] = [];
    const childrenMap = new Map<string, Message[]>();
    const validParentIds = new Set($messages.map(m => m.id));

    $messages.forEach((msg) => {
      if (!msg.replyToId) {
        parentMessages.push(msg);
      } else {
        if (validParentIds.has(msg.replyToId)) {
          if (!childrenMap.has(msg.replyToId)) {
            childrenMap.set(msg.replyToId, []);
          }
          childrenMap.get(msg.replyToId)!.push(msg);
        } else {
          console.warn(`Message ${msg.id} has invalid replyToId ${msg.replyToId}, treating as parent`);
          parentMessages.push(msg);
        }
      }
    });

    return {
      parentMessages,
      childrenMap,
    };
  });

export const getMessageById = (messageId: string) =>
  derived(messageList, ($messages) => $messages.find((m) => m.id === messageId));

export const replyTargetId = writable<string | null>(null);
export const replyTarget = derived(
  [replyTargetId, messageList],
  ([$replyTargetId, $messages]) =>
    $replyTargetId ? $messages.find((m) => m.id === $replyTargetId) ?? null : null
);

export const unreadMessagesCount = derived(messageList, ($messages) => {
  // 既読レシートに基づいて計算する必要がある
  // 現在はプレースホルダー
  return $messages.length;
});

export const recentMessages = (limit: number = 10) =>
  derived(messageList, ($messages) =>
    $messages.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()).slice(0, limit)
  );

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
