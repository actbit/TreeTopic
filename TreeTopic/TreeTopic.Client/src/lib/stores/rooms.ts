import { writable, derived } from 'svelte/store';
import { isCacheValid } from '$lib/utils/store';

const ROOMS_CACHE_TTL = 5 * 60 * 1000; // 5分

export interface RoomMember {
  id: string;
  userName: string;
  displayName: string;
  email: string;
  role: string;
  joinedAt: Date;
  isOwner: boolean;
}

export interface Room {
  id: string;
  name: string;
  description?: string;
  joinPolicy?: number;
  avatar?: string;
  createdAt: Date;
  updatedAt: Date;
  ownerId: string;
  memberCount: number;
  members?: RoomMember[];
  unreadCount: number;
  isArchived: boolean;
  settings?: {
    isPublic?: boolean;
    allowGuestAccess?: boolean;
    [key: string]: unknown;
  };
  canEdit?: boolean;
  canDelete?: boolean;
  canJoin?: boolean;
  isJoined?: boolean;
}

export interface CurrentRoomUser {
  id: string;
  displayName: string;
  iconUrl?: string;
  useMainIcon: boolean;
}

export interface RoomsState {
  rooms: Room[];
  currentRoom: Room | null;
  selectedRoomId: string | null;
  currentRoomUser: CurrentRoomUser | null;
  isLoading: boolean;
  error: string | null;
  lastUpdated: number | null;
  cacheExpiry: number;
}

function createRoomsStore() {
  const { subscribe, set, update } = writable<RoomsState>({
    rooms: [],
    currentRoom: null,
    selectedRoomId: null,
    currentRoomUser: null,
    isLoading: false,
    error: null,
    lastUpdated: null,
    cacheExpiry: 0,
  });

  return {
    subscribe,
    setRooms: (rooms: Room[]) => {
      update((state) => ({
        ...state,
        rooms,
        error: null,
        lastUpdated: Date.now(),
        cacheExpiry: Date.now() + ROOMS_CACHE_TTL,
      }));
    },
    setCurrentRoom: (room: Room | null) => {
      update((state) => ({
        ...state,
        currentRoom: room,
        selectedRoomId: room?.id ?? null,
        currentRoomUser: null,
        error: null,
      }));
    },
    setCurrentRoomUser: (roomUser: CurrentRoomUser | null) => {
      update((state) => ({
        ...state,
        currentRoomUser: roomUser,
      }));
    },
    addRoom: (room: Room) => {
      update((state) => ({
        ...state,
        rooms: [
          room,
          ...state.rooms.filter((r) => r.id !== room.id),
        ],
      }));
    },
    updateRoom: (roomId: string, updates: Partial<Room>) => {
      update((state) => ({
        ...state,
        rooms: state.rooms.map((r) =>
          r.id === roomId ? { ...r, ...updates } : r
        ),
        currentRoom:
          state.currentRoom?.id === roomId
            ? { ...state.currentRoom, ...updates }
            : state.currentRoom,
      }));
    },
    deleteRoom: (roomId: string) => {
      update((state) => ({
        ...state,
        rooms: state.rooms.filter((r) => r.id !== roomId),
        currentRoom:
          state.currentRoom?.id === roomId ? null : state.currentRoom,
      }));
    },
    updateRoomMembers: (roomId: string, members: RoomMember[]) => {
      update((state) => ({
        ...state,
        rooms: state.rooms.map((r) =>
          r.id === roomId ? { ...r, members, memberCount: members.length } : r
        ),
        currentRoom:
          state.currentRoom?.id === roomId
            ? { ...state.currentRoom, members, memberCount: members.length }
            : state.currentRoom,
      }));
    },
    updateUnreadCount: (roomId: string, count: number) => {
      update((state) => ({
        ...state,
        rooms: state.rooms.map((r) =>
          r.id === roomId ? { ...r, unreadCount: count } : r
        ),
      }));
    },
    incrementUnreadCount: (roomId: string) => {
      update((state) => ({
        ...state,
        rooms: state.rooms.map((r) =>
          r.id === roomId ? { ...r, unreadCount: r.unreadCount + 1 } : r
        ),
      }));
    },
    clearUnreadCount: (roomId: string) => {
      update((state) => ({
        ...state,
        rooms: state.rooms.map((r) =>
          r.id === roomId ? { ...r, unreadCount: 0 } : r
        ),
      }));
    },
    setLoading: (isLoading: boolean) => {
      update((state) => ({ ...state, isLoading }));
    },
    setError: (error: string | null) => {
      update((state) => ({ ...state, error }));
    },
    clear: () => {
      set({
        rooms: [],
        currentRoom: null,
        selectedRoomId: null,
        currentRoomUser: null,
        isLoading: false,
        error: null,
        lastUpdated: null,
        cacheExpiry: 0,
      });
    },
  };
}

export const rooms = createRoomsStore();

export const roomList = derived(rooms, ($rooms) => $rooms?.rooms ?? []);
export const currentRoom = derived(rooms, ($rooms) => $rooms?.currentRoom ?? null);
export const selectedRoomId = derived(rooms, ($rooms) => $rooms?.selectedRoomId ?? null);
export const currentRoomUser = derived(rooms, ($rooms) => $rooms?.currentRoomUser ?? null);
export const roomsLoading = derived(rooms, ($rooms) => $rooms?.isLoading ?? false);
export const roomsError = derived(rooms, ($rooms) => $rooms?.error ?? null);

export const getRoomById = (roomId: string) =>
  derived(roomList, ($rooms) => ($rooms || []).find((r) => r?.id === roomId));

export const unreadRooms = derived(roomList, ($rooms) =>
  ($rooms || []).filter((r) => r?.unreadCount > 0)
);

export const totalUnreadCount = derived(roomList, ($rooms) =>
  ($rooms || []).reduce((sum, room) => sum + (room?.unreadCount ?? 0), 0)
);

/**
 * Get active (non-archived) rooms
 */
export const activeRooms = derived(roomList, ($rooms) =>
  ($rooms || []).filter((r) => r && !r.isArchived)
);

/**
 * Get archived rooms
 */
export const archivedRooms = derived(roomList, ($rooms) =>
  ($rooms || []).filter((r) => r && r.isArchived)
);

/**
 * Helper functions to interact with rooms store
 */
export function setCurrentRoom(room: Room | null) {
  rooms.setCurrentRoom(room);
}

export function addRoom(room: Room) {
  rooms.addRoom(room);
}

export function updateRoom(roomId: string, updates: Partial<Room>) {
  rooms.updateRoom(roomId, updates);
}

export function deleteRoom(roomId: string) {
  rooms.deleteRoom(roomId);
}

export function setRooms(roomsList: Room[]) {
  rooms.setRooms(roomsList);
}
