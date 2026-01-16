import { writable, derived } from 'svelte/store';

/**
 * Room member information
 */
export interface RoomMember {
  id: string;
  userName: string;
  displayName: string;
  email: string;
  role: string;
  joinedAt: Date;
  isOwner: boolean;
}

/**
 * Room information
 */
export interface Room {
  id: string;
  name: string;
  description?: string;
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
    [key: string]: any;
  };
  canEdit?: boolean;
  canDelete?: boolean;
}

/**
 * Current user's RoomUser information
 */
export interface CurrentRoomUser {
  id: string;
  displayName: string;
  iconUrl?: string;
  useMainIcon: boolean;
}

/**
 * Rooms store state
 */
export interface RoomsState {
  rooms: Room[];
  currentRoom: Room | null;
  selectedRoomId: string | null;
  currentRoomUser: CurrentRoomUser | null;
  isLoading: boolean;
  error: string | null;
  lastUpdated: number | null;
}

/**
 * Create rooms store
 */
function createRoomsStore() {
  const { subscribe, set, update } = writable<RoomsState>({
    rooms: [],
    currentRoom: null,
    selectedRoomId: null,
    currentRoomUser: null,
    isLoading: false,
    error: null,
    lastUpdated: null,
  });

  return {
    subscribe,
    /**
     * Set all rooms
     */
    setRooms: (rooms: Room[]) => {
      update((state) => ({
        ...state,
        rooms,
        error: null,
        lastUpdated: Date.now(),
      }));
    },
    /**
     * Set current room
     */
    setCurrentRoom: (room: Room | null) => {
      update((state) => ({
        ...state,
        currentRoom: room,
        selectedRoomId: room?.id ?? null,
        currentRoomUser: null,
        error: null,
      }));
    },
    /**
     * Set current room user
     */
    setCurrentRoomUser: (roomUser: CurrentRoomUser | null) => {
      update((state) => ({
        ...state,
        currentRoomUser: roomUser,
      }));
    },
    /**
     * Add a new room
     */
    addRoom: (room: Room) => {
      update((state) => ({
        ...state,
        rooms: [room, ...state.rooms],
      }));
    },
    /**
     * Update room
     */
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
    /**
     * Delete room
     */
    deleteRoom: (roomId: string) => {
      update((state) => ({
        ...state,
        rooms: state.rooms.filter((r) => r.id !== roomId),
        currentRoom:
          state.currentRoom?.id === roomId ? null : state.currentRoom,
      }));
    },
    /**
     * Update room members
     */
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
    /**
     * Update room unread count
     */
    updateUnreadCount: (roomId: string, count: number) => {
      update((state) => ({
        ...state,
        rooms: state.rooms.map((r) =>
          r.id === roomId ? { ...r, unreadCount: count } : r
        ),
      }));
    },
    /**
     * Increment unread count for room
     */
    incrementUnreadCount: (roomId: string) => {
      update((state) => ({
        ...state,
        rooms: state.rooms.map((r) =>
          r.id === roomId ? { ...r, unreadCount: r.unreadCount + 1 } : r
        ),
      }));
    },
    /**
     * Clear unread count
     */
    clearUnreadCount: (roomId: string) => {
      update((state) => ({
        ...state,
        rooms: state.rooms.map((r) =>
          r.id === roomId ? { ...r, unreadCount: 0 } : r
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
     * Clear all rooms
     */
    clear: () => {
      set({
        rooms: [],
        currentRoom: null,
        selectedRoomId: null,
        currentRoomUser: null,
        isLoading: false,
        error: null,
        lastUpdated: null,
      });
    },
  };
}

export const rooms = createRoomsStore();

/**
 * Derived stores
 */
export const roomList = derived(rooms, ($rooms) => $rooms.rooms);
export const currentRoom = derived(rooms, ($rooms) => $rooms.currentRoom);
export const selectedRoomId = derived(rooms, ($rooms) => $rooms.selectedRoomId);
export const currentRoomUser = derived(rooms, ($rooms) => $rooms.currentRoomUser);
export const roomsLoading = derived(rooms, ($rooms) => $rooms.isLoading);
export const roomsError = derived(rooms, ($rooms) => $rooms.error);

/**
 * Get room by ID
 */
export const getRoomById = (roomId: string) =>
  derived(roomList, ($rooms) => $rooms.find((r) => r.id === roomId));

/**
 * Get all unread rooms
 */
export const unreadRooms = derived(roomList, ($rooms) =>
  $rooms.filter((r) => r.unreadCount > 0)
);

/**
 * Get total unread count
 */
export const totalUnreadCount = derived(roomList, ($rooms) =>
  $rooms.reduce((sum, room) => sum + room.unreadCount, 0)
);

/**
 * Get active (non-archived) rooms
 */
export const activeRooms = derived(roomList, ($rooms) =>
  $rooms.filter((r) => !r.isArchived)
);

/**
 * Get archived rooms
 */
export const archivedRooms = derived(roomList, ($rooms) =>
  $rooms.filter((r) => r.isArchived)
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
