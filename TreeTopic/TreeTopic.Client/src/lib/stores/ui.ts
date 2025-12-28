import { writable, derived } from 'svelte/store';
import type { ViewMode, ModalConfig, Notification, DragState } from '$lib/types/ui';

/**
 * Global UI state
 */
export interface UIStateData {
  viewMode: ViewMode;
  sidebarCollapsed: boolean;
  subpanelCollapsed: boolean;
  activeModals: ModalConfig[];
  notifications: Notification[];
  isDragDropActive: boolean;
  dragState: DragState | null;
  contextMenuOpen: boolean;
  contextMenuPosition: { x: number; y: number } | null;
  selectedItems: Set<string>;
  isMobile: boolean;
  isTablet: boolean;
  isDesktop: boolean;
}

/**
 * Create UI store
 */
function createUIStore() {
  const { subscribe, set, update } = writable<UIStateData>({
    viewMode: 'default',
    sidebarCollapsed: false,
    subpanelCollapsed: false,
    activeModals: [],
    notifications: [],
    isDragDropActive: false,
    dragState: null,
    contextMenuOpen: false,
    contextMenuPosition: null,
    selectedItems: new Set(),
    isMobile: false,
    isTablet: false,
    isDesktop: true,
  });

  return {
    subscribe,
    /**
     * Set view mode
     */
    setViewMode: (mode: ViewMode) => {
      update((state) => ({ ...state, viewMode: mode }));
      localStorage.setItem('ui_view_mode', mode);
    },
    /**
     * Toggle sidebar
     */
    toggleSidebar: () => {
      update((state) => ({ ...state, sidebarCollapsed: !state.sidebarCollapsed }));
      update((state) => {
        localStorage.setItem('ui_sidebar_collapsed', state.sidebarCollapsed.toString());
        return state;
      });
    },
    /**
     * Set sidebar collapsed state
     */
    setSidebarCollapsed: (collapsed: boolean) => {
      update((state) => ({ ...state, sidebarCollapsed: collapsed }));
      localStorage.setItem('ui_sidebar_collapsed', collapsed.toString());
    },
    /**
     * Toggle subpanel
     */
    toggleSubpanel: () => {
      update((state) => ({ ...state, subpanelCollapsed: !state.subpanelCollapsed }));
      update((state) => {
        localStorage.setItem('ui_subpanel_collapsed', state.subpanelCollapsed.toString());
        return state;
      });
    },
    /**
     * Set subpanel collapsed state
     */
    setSubpanelCollapsed: (collapsed: boolean) => {
      update((state) => ({ ...state, subpanelCollapsed: collapsed }));
      localStorage.setItem('ui_subpanel_collapsed', collapsed.toString());
    },
    /**
     * Open modal
     */
    openModal: (modal: ModalConfig) => {
      update((state) => ({
        ...state,
        activeModals: [...state.activeModals, modal],
      }));
    },
    /**
     * Close modal
     */
    closeModal: (modalId: string) => {
      update((state) => ({
        ...state,
        activeModals: state.activeModals.filter((m) => m.id !== modalId),
      }));
    },
    /**
     * Close all modals
     */
    closeAllModals: () => {
      update((state) => ({
        ...state,
        activeModals: [],
      }));
    },
    /**
     * Add notification
     */
    addNotification: (notification: Omit<Notification, 'id' | 'createdAt'>) => {
      const newNotification: Notification = {
        ...notification,
        id: `notif_${Date.now()}_${Math.random()}`,
        createdAt: Date.now(),
      };

      update((state) => ({
        ...state,
        notifications: [...state.notifications, newNotification],
      }));

      // Auto-remove notifications after duration
      if (notification.duration !== 0) {
        setTimeout(() => {
          ui.removeNotification(newNotification.id);
        }, notification.duration ?? 3000);
      }

      return newNotification.id;
    },
    /**
     * Remove notification
     */
    removeNotification: (notificationId: string) => {
      update((state) => ({
        ...state,
        notifications: state.notifications.filter((n) => n.id !== notificationId),
      }));
    },
    /**
     * Clear all notifications
     */
    clearNotifications: () => {
      update((state) => ({
        ...state,
        notifications: [],
      }));
    },
    /**
     * Start drag operation
     */
    startDrag: (dragState: DragState) => {
      update((state) => ({
        ...state,
        isDragDropActive: true,
        dragState,
      }));
    },
    /**
     * Update drag state
     */
    updateDragState: (dragState: Partial<DragState>) => {
      update((state) => ({
        ...state,
        dragState: state.dragState ? { ...state.dragState, ...dragState } : null,
      }));
    },
    /**
     * End drag operation
     */
    endDrag: () => {
      update((state) => ({
        ...state,
        isDragDropActive: false,
        dragState: null,
      }));
    },
    /**
     * Show context menu
     */
    showContextMenu: (x: number, y: number) => {
      update((state) => ({
        ...state,
        contextMenuOpen: true,
        contextMenuPosition: { x, y },
      }));
    },
    /**
     * Hide context menu
     */
    hideContextMenu: () => {
      update((state) => ({
        ...state,
        contextMenuOpen: false,
        contextMenuPosition: null,
      }));
    },
    /**
     * Select item
     */
    selectItem: (itemId: string) => {
      update((state) => {
        const selected = new Set(state.selectedItems);
        selected.add(itemId);
        return { ...state, selectedItems: selected };
      });
    },
    /**
     * Deselect item
     */
    deselectItem: (itemId: string) => {
      update((state) => {
        const selected = new Set(state.selectedItems);
        selected.delete(itemId);
        return { ...state, selectedItems: selected };
      });
    },
    /**
     * Toggle item selection
     */
    toggleItemSelection: (itemId: string) => {
      update((state) => {
        const selected = new Set(state.selectedItems);
        if (selected.has(itemId)) {
          selected.delete(itemId);
        } else {
          selected.add(itemId);
        }
        return { ...state, selectedItems: selected };
      });
    },
    /**
     * Clear selection
     */
    clearSelection: () => {
      update((state) => ({
        ...state,
        selectedItems: new Set(),
      }));
    },
    /**
     * Set viewport size
     */
    setViewportSize: (width: number, height: number) => {
      update((state) => {
        const isMobile = width < 768;
        const isTablet = width >= 768 && width < 1024;
        const isDesktop = width >= 1024;

        return {
          ...state,
          isMobile,
          isTablet,
          isDesktop,
        };
      });
    },
    /**
     * Restore UI state from localStorage
     */
    restoreState: () => {
      const savedViewMode = localStorage.getItem('ui_view_mode');
      const savedSidebarCollapsed = localStorage.getItem('ui_sidebar_collapsed');
      const savedSubpanelCollapsed = localStorage.getItem('ui_subpanel_collapsed');

      update((state) => ({
        ...state,
        viewMode: (savedViewMode as ViewMode) ?? state.viewMode,
        sidebarCollapsed: savedSidebarCollapsed === 'true',
        subpanelCollapsed: savedSubpanelCollapsed === 'true',
      }));
    },
  };
}

export const ui = createUIStore();

/**
 * Derived stores
 */
export const viewMode = derived(ui, ($ui) => $ui.viewMode);
export const sidebarCollapsed = derived(ui, ($ui) => $ui.sidebarCollapsed);
export const subpanelCollapsed = derived(ui, ($ui) => $ui.subpanelCollapsed);
export const activeModals = derived(ui, ($ui) => $ui.activeModals);
export const notifications = derived(ui, ($ui) => $ui.notifications);
export const isDragDropActive = derived(ui, ($ui) => $ui.isDragDropActive);
export const dragState = derived(ui, ($ui) => $ui.dragState);
export const contextMenuOpen = derived(ui, ($ui) => $ui.contextMenuOpen);
export const contextMenuPosition = derived(ui, ($ui) => $ui.contextMenuPosition);
export const selectedItems = derived(ui, ($ui) => $ui.selectedItems);
export const isMobile = derived(ui, ($ui) => $ui.isMobile);
export const isTablet = derived(ui, ($ui) => $ui.isTablet);
export const isDesktop = derived(ui, ($ui) => $ui.isDesktop);

/**
 * Check if there are active modals
 */
export const hasActiveModals = derived(activeModals, ($modals) => $modals.length > 0);

/**
 * Get notifications count
 */
export const notificationsCount = derived(
  notifications,
  ($notifs) => $notifs.length
);

/**
 * Get selection count
 */
export const selectionCount = derived(
  selectedItems,
  ($selected) => $selected.size
);
