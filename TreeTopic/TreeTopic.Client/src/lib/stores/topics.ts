import { writable, derived } from 'svelte/store';
import type { TopicTreeNode } from '$lib/types/ui';

/**
 * Topic permission levels
 */
export type PermissionLevel = 'none' | 'read' | 'write' | 'admin';

/**
 * Topic permission information
 */
export interface TopicPermission {
  userId: string;
  userName: string;
  level: PermissionLevel;
}

/**
 * Topic information
 */
export interface Topic {
  id: string;
  roomId: string;
  title: string;
  description?: string;
  parentId: string | null;
  childIds: string[];
  createdAt: Date;
  updatedAt: Date;
  creatorId: string;
  messageCount: number;
  unreadCount: number;
  userPermission: PermissionLevel;
  permissions?: TopicPermission[];
  isArchived: boolean;
  tags?: string[];
  hasChildren: boolean;
}

/**
 * Topics store state
 */
export interface TopicsState {
  topics: Topic[];
  selectedTopicId: string | null;
  selectedTopic: Topic | null;
  expandedTopics: Set<string>;
  isLoading: boolean;
  error: string | null;
  lastUpdated: number | null;
}

/**
 * Create topics store
 */
function createTopicsStore() {
  const { subscribe, set, update } = writable<TopicsState>({
    topics: [],
    selectedTopicId: null,
    selectedTopic: null,
    expandedTopics: new Set(),
    isLoading: false,
    error: null,
    lastUpdated: null,
  });

  return {
    subscribe,
    /**
     * Set all topics
     */
    setTopics: (topics: Topic[]) => {
      update((state) => ({
        ...state,
        topics,
        error: null,
        lastUpdated: Date.now(),
      }));
    },
    /**
     * Set selected topic
     */
    setSelectedTopic: (topic: Topic | null) => {
      update((state) => ({
        ...state,
        selectedTopic: topic,
        selectedTopicId: topic?.id ?? null,
        error: null,
      }));
      if (topic) {
        localStorage.setItem('selected_topic', topic.id);
      }
    },
    /**
     * Add a new topic
     */
    addTopic: (topic: Topic) => {
      update((state) => {
        const updatedTopics = [...state.topics, topic];

        // If the new topic has a parent, add it to the parent's childIds
        if (topic.parentId) {
          const parentIndex = updatedTopics.findIndex((t) => t.id === topic.parentId);
          if (parentIndex !== -1) {
            const parent = updatedTopics[parentIndex];
            if (!parent.childIds.includes(topic.id)) {
              updatedTopics[parentIndex] = {
                ...parent,
                childIds: [...parent.childIds, topic.id],
              };
            }
          }
        }

        return {
          ...state,
          topics: updatedTopics,
        };
      });
    },
    /**
     * Update topic
     */
    updateTopic: (topicId: string, updates: Partial<Topic>) => {
      update((state) => ({
        ...state,
        topics: state.topics.map((t) =>
          t.id === topicId ? { ...t, ...updates } : t
        ),
        selectedTopic:
          state.selectedTopic?.id === topicId
            ? { ...state.selectedTopic, ...updates }
            : state.selectedTopic,
      }));
    },
    /**
     * Move topic to a new parent (or root if null)
     */
    moveTopicParent: (topicId: string, newParentId: string | null) => {
      update((state) => {
        const moving = state.topics.find((t) => t.id === topicId);
        if (!moving) return state;

        const oldParentId = moving.parentId;
        const now = new Date();

        const topics = state.topics.map((t) => ({ ...t }));

        const movingIndex = topics.findIndex((t) => t.id === topicId);
        if (movingIndex === -1) return state;
        topics[movingIndex] = {
          ...topics[movingIndex],
          parentId: newParentId,
          updatedAt: now,
        };

        if (oldParentId) {
          const oldParentIndex = topics.findIndex((t) => t.id === oldParentId);
          if (oldParentIndex !== -1) {
            const oldParent = topics[oldParentIndex];
            const nextChildIds = (oldParent.childIds ?? []).filter((id) => id !== topicId);
            topics[oldParentIndex] = {
              ...oldParent,
              childIds: nextChildIds,
              // Avoid incorrectly flipping to false when children aren't fully loaded.
              hasChildren: nextChildIds.length > 0 ? true : oldParent.hasChildren,
              updatedAt: now,
            };
          }
        }

        if (newParentId) {
          const newParentIndex = topics.findIndex((t) => t.id === newParentId);
          if (newParentIndex !== -1) {
            const newParent = topics[newParentIndex];
            const nextChildIds = newParent.childIds?.includes(topicId)
              ? newParent.childIds
              : [...(newParent.childIds ?? []), topicId];
            topics[newParentIndex] = {
              ...newParent,
              childIds: nextChildIds,
              hasChildren: true,
              updatedAt: now,
            };
          }
        }

        return {
          ...state,
          topics,
          selectedTopic:
            state.selectedTopic?.id === topicId
              ? { ...state.selectedTopic, parentId: newParentId, updatedAt: now }
              : state.selectedTopic,
        };
      });
    },
    /**
     * Delete topic
     */
    deleteTopic: (topicId: string) => {
      update((state) => ({
        ...state,
        topics: state.topics.filter((t) => t.id !== topicId),
        selectedTopic:
          state.selectedTopic?.id === topicId ? null : state.selectedTopic,
      }));
    },
    /**
     * Toggle topic expansion
     */
    toggleTopicExpansion: (topicId: string) => {
      update((state) => {
        const expanded = new Set(state.expandedTopics);
        if (expanded.has(topicId)) {
          expanded.delete(topicId);
        } else {
          expanded.add(topicId);
        }
        return { ...state, expandedTopics: expanded };
      });
    },
    /**
     * Expand all topics
     */
    expandAll: () => {
      update((state) => ({
        ...state,
        expandedTopics: new Set(state.topics.map((t) => t.id)),
      }));
    },
    /**
     * Collapse all topics
     */
    collapseAll: () => {
      update((state) => ({
        ...state,
        expandedTopics: new Set(),
      }));
    },
    /**
     * Update topic permissions
     */
    updateTopicPermissions: (topicId: string, permissions: TopicPermission[]) => {
      update((state) => ({
        ...state,
        topics: state.topics.map((t) =>
          t.id === topicId ? { ...t, permissions } : t
        ),
        selectedTopic:
          state.selectedTopic?.id === topicId
            ? { ...state.selectedTopic, permissions }
            : state.selectedTopic,
      }));
    },
    /**
     * Update topic unread count
     */
    updateUnreadCount: (topicId: string, count: number) => {
      update((state) => ({
        ...state,
        topics: state.topics.map((t) =>
          t.id === topicId ? { ...t, unreadCount: count } : t
        ),
      }));
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
     * Clear all topics
     */
    clear: () => {
      set({
        topics: [],
        selectedTopicId: null,
        selectedTopic: null,
        expandedTopics: new Set(),
        isLoading: false,
        error: null,
        lastUpdated: null,
      });
      localStorage.removeItem('selected_topic');
    },
  };
}

export const topics = createTopicsStore();

/**
 * Derived stores
 */
export const topicList = derived(topics, ($topics) => $topics.topics);
export const selectedTopic = derived(topics, ($topics) => $topics.selectedTopic);
export const topicsLoading = derived(topics, ($topics) => $topics.isLoading);
export const topicsError = derived(topics, ($topics) => $topics.error);
export const expandedTopics = derived(topics, ($topics) => $topics.expandedTopics);

/**
 * Get topic by ID
 */
export const getTopicById = (topicId: string) =>
  derived(topicList, ($topics) => $topics.find((t) => t.id === topicId));

/**
 * Build topic tree structure
 */
export const topicTree = derived([topicList, expandedTopics], ([$topics, $expandedTopics]) => {
  const buildTree = (): TopicTreeNode[] => {
    const topicMap = new Map($topics.map((t) => [t.id, t]));
    const roots: TopicTreeNode[] = [];
    const processed = new Set<string>();

    const buildNode = (topic: Topic, level: number = 0): TopicTreeNode => {
      const node: TopicTreeNode = {
        id: topic.id,
        title: topic.title,
        level,
        parentId: topic.parentId,
        children: [],
        unreadCount: topic.unreadCount,
        isSelected: false,
        isExpanded: $expandedTopics.has(topic.id),
        hasChildren: topic.hasChildren,
        canRead: topic.userPermission !== 'none',
        canWrite: topic.userPermission === 'write' || topic.userPermission === 'admin',
        canDelete: topic.userPermission === 'admin',
        canManagePermissions: topic.userPermission === 'admin',
      };

      // Add child topics
      topic.childIds.forEach((childId) => {
        if (!processed.has(childId)) {
          const childTopic = topicMap.get(childId);
          if (childTopic) {
            processed.add(childId);
            node.children.push(buildNode(childTopic, level + 1));
          }
        }
      });

      return node;
    };

    // Build tree starting from root topics (no parent)
    $topics.forEach((topic) => {
      if (!topic.parentId && !processed.has(topic.id)) {
        processed.add(topic.id);
        roots.push(buildNode(topic));
      }
    });

    return roots;
  };

  return buildTree();
});

/**
 * Get unread topics
 */
export const unreadTopics = derived(topicList, ($topics) =>
  $topics.filter((t) => t.unreadCount > 0)
);

/**
 * Get total unread count
 */
export const totalTopicUnreadCount = derived(topicList, ($topics) =>
  $topics.reduce((sum, topic) => sum + topic.unreadCount, 0)
);

/**
 * Get topic with write permission
 */
export const writableTopics = derived(topicList, ($topics) =>
  $topics.filter((t) => t.userPermission === 'write' || t.userPermission === 'admin')
);

/**
 * Get child topics of a specific topic
 */
export const getChildTopics = (parentId: string) =>
  derived(topicList, ($topics) =>
    $topics.filter((t) => t.parentId === parentId)
  );

/**
 * Get parent topic
 */
export const getParentTopic = (topicId: string) =>
  derived(topicList, ($topics) => {
    const topic = $topics.find((t) => t.id === topicId);
    return topic ? $topics.find((t) => t.id === topic.parentId) : null;
  });

/**
 * Helper functions to interact with topics store
 */
export function addTopic(topic: Topic) {
  topics.addTopic(topic);
}

export function updateTopic(topicId: string, updates: Partial<Topic>) {
  topics.updateTopic(topicId, updates);
}

export function moveTopicParent(topicId: string, newParentId: string | null) {
  topics.moveTopicParent(topicId, newParentId);
}

export function deleteTopic(topicId: string) {
  topics.deleteTopic(topicId);
}

export function setSelectedTopic(topic: Topic | null) {
  topics.setSelectedTopic(topic);
}

export function toggleTopicExpansion(topicId: string) {
  topics.toggleTopicExpansion(topicId);
}

export function setTopics(topicsList: Topic[]) {
  topics.setTopics(topicsList);
}

/**
 * Store for managing parent topic selection in topic creation modal
 */
export const createTopicParentId = writable<string | null>(null);
