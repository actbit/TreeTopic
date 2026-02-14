import { writable, derived } from 'svelte/store';
import type { ViewMode, ModalConfig, Notification, DragState } from '$lib/types/ui';

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
  viewportWidth: number;
}

// Store notification timeout IDs for cleanup
const notificationTimers = new Map<string, ReturnType<typeof setTimeout>>();

// Cleanup on page unload
if (typeof window !== 'undefined') {
  window.addEventListener('beforeunload', () => {
    notificationTimers.forEach((timer) => clearTimeout(timer));
    notificationTimers.clear();
  });
}

function createUIStore() {
  const { subscribe, set, update } = writable<UIStateData>({
    viewMode: 'default',
    sidebarCollapsed: false,
    subpanelCollapsed: true,
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
    viewportWidth: 0,
  });

  return {
    subscribe,
    setViewMode: (mode: ViewMode) => {
      update((state) => ({ ...state, viewMode: mode }));
      localStorage.setItem('ui_view_mode', mode);
    },
    toggleSidebar: () => {
      update((state) => ({ ...state, sidebarCollapsed: !state.sidebarCollapsed }));
      update((state) => {
        localStorage.setItem('ui_sidebar_collapsed', state.sidebarCollapsed.toString());
        return state;
      });
    },
    setSidebarCollapsed: (collapsed: boolean) => {
      update((state) => ({ ...state, sidebarCollapsed: collapsed }));
      localStorage.setItem('ui_sidebar_collapsed', collapsed.toString());
    },
    toggleSubpanel: () => {
      update((state) => ({ ...state, subpanelCollapsed: !state.subpanelCollapsed }));
      update((state) => {
        localStorage.setItem('ui_subpanel_collapsed', state.subpanelCollapsed.toString());
        return state;
      });
    },
    setSubpanelCollapsed: (collapsed: boolean) => {
      update((state) => ({ ...state, subpanelCollapsed: collapsed }));
      localStorage.setItem('ui_subpanel_collapsed', collapsed.toString());
    },
    openModal: (modal: ModalConfig) => {
      update((state) => ({
        ...state,
        activeModals: state.activeModals.some((m) => m.id === modal.id)
          ? state.activeModals
          : [...state.activeModals, modal],
      }));
    },
    closeModal: (modalId: string) => {
      update((state) => ({
        ...state,
        activeModals: state.activeModals.filter((m) => m.id !== modalId),
      }));
    },
    closeAllModals: () => {
      update((state) => ({
        ...state,
        activeModals: [],
      }));
    },
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
        const timer = setTimeout(() => {
          ui.removeNotification(newNotification.id);
          notificationTimers.delete(newNotification.id);
        }, notification.duration ?? 3000);
        notificationTimers.set(newNotification.id, timer);
      }

      return newNotification.id;
    },
    removeNotification: (notificationId: string) => {
      // Clear the auto-remove timeout if it exists
      const timer = notificationTimers.get(notificationId);
      if (timer) {
        clearTimeout(timer);
        notificationTimers.delete(notificationId);
      }

      update((state) => ({
        ...state,
        notifications: state.notifications.filter((n) => n.id !== notificationId),
      }));
    },
    clearNotifications: () => {
      // Clear all notification timeouts
      notificationTimers.forEach((timer) => clearTimeout(timer));
      notificationTimers.clear();

      update((state) => ({
        ...state,
        notifications: [],
      }));
    },
    startDrag: (dragState: DragState) => {
      update((state) => ({
        ...state,
        isDragDropActive: true,
        dragState,
      }));
    },
    updateDragState: (dragState: Partial<DragState>) => {
      update((state) => ({
        ...state,
        dragState: state.dragState ? { ...state.dragState, ...dragState } : null,
      }));
    },
    endDrag: () => {
      update((state) => ({
        ...state,
        isDragDropActive: false,
        dragState: null,
      }));
    },
    showContextMenu: (x: number, y: number) => {
      update((state) => ({
        ...state,
        contextMenuOpen: true,
        contextMenuPosition: { x, y },
      }));
    },
    hideContextMenu: () => {
      update((state) => ({
        ...state,
        contextMenuOpen: false,
        contextMenuPosition: null,
      }));
    },
    selectItem: (itemId: string) => {
      update((state) => {
        const selected = new Set(state.selectedItems);
        selected.add(itemId);
        return { ...state, selectedItems: selected };
      });
    },
    deselectItem: (itemId: string) => {
      update((state) => {
        const selected = new Set(state.selectedItems);
        selected.delete(itemId);
        return { ...state, selectedItems: selected };
      });
    },
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
    clearSelection: () => {
      update((state) => ({
        ...state,
        selectedItems: new Set(),
      }));
    },
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
          viewportWidth: width,
        };
      });
    },
    restoreState: () => {
      const savedViewMode = localStorage.getItem('ui_view_mode');
      const savedSidebarCollapsed = localStorage.getItem('ui_sidebar_collapsed');
      const savedSubpanelCollapsed = localStorage.getItem('ui_subpanel_collapsed');

      update((state) => ({
        ...state,
        viewMode: (savedViewMode as ViewMode) ?? state.viewMode,
        sidebarCollapsed: savedSidebarCollapsed === null ? state.sidebarCollapsed : savedSidebarCollapsed === 'true',
        subpanelCollapsed: savedSubpanelCollapsed === null ? state.subpanelCollapsed : savedSubpanelCollapsed === 'true',
      }));
    },
  };
}

export const ui = createUIStore();

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
export const responsiveLayout = derived(ui, ($ui) => $ui.viewportWidth <= 1030);

export const hasActiveModals = derived(activeModals, ($modals) => $modals.length > 0);

export const notificationsCount = derived(
  notifications,
  ($notifs) => $notifs.length
);

export const selectionCount = derived(
  selectedItems,
  ($selected) => $selected.size
);

export const modals = {
  open: (id: string, title: string, data?: Record<string, unknown>) => {
    ui.openModal({ id, title, type: 'custom', data });
  },
  close: (id: string) => {
    ui.closeModal(id);
  },
};
