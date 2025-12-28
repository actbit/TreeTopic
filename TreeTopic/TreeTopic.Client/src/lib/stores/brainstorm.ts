import { writable, derived } from 'svelte/store';

/**
 * Vote mark type
 */
export type VoteMark = 'circle' | 'square' | 'triangle' | 'cross';

/**
 * Idea card information
 */
export interface Idea {
  id: string;
  boardId: string;
  text: string;
  x: number;
  y: number;
  width?: number;
  height?: number;
  userId: string;
  userName: string;
  displayName: string;
  isAnonymous: boolean;
  linkedMessageId?: string;
  color?: string;
  votes: Record<VoteMark, number>;
  userVotes: Record<VoteMark, boolean>;
  createdAt: Date;
  updatedAt?: Date;
}

/**
 * Brainstorm board information
 */
export interface BrainstormBoard {
  id: string;
  roomId: string;
  topicId?: string;
  title: string;
  description?: string;
  backgroundImageUrl?: string;
  backgroundPdfUrl?: string;
  createdAt: Date;
  updatedAt: Date;
  createdBy: string;
  isArchived: boolean;
  allowAnonymous: boolean;
  ideas: Idea[];
}

/**
 * Brainstorm store state
 */
export interface BrainstormState {
  boards: BrainstormBoard[];
  currentBoard: BrainstormBoard | null;
  selectedBoardId: string | null;
  editingIdeaId: string | null;
  isLoading: boolean;
  error: string | null;
  lastUpdated: number | null;
}

/**
 * Create brainstorm store
 */
function createBrainstormStore() {
  const { subscribe, set, update } = writable<BrainstormState>({
    boards: [],
    currentBoard: null,
    selectedBoardId: null,
    editingIdeaId: null,
    isLoading: false,
    error: null,
    lastUpdated: null,
  });

  return {
    subscribe,
    /**
     * Set all boards
     */
    setBoards: (boards: BrainstormBoard[]) => {
      update((state) => ({
        ...state,
        boards,
        error: null,
        lastUpdated: Date.now(),
      }));
    },
    /**
     * Set current board
     */
    setCurrentBoard: (board: BrainstormBoard | null) => {
      update((state) => ({
        ...state,
        currentBoard: board,
        selectedBoardId: board?.id ?? null,
        error: null,
      }));
      if (board) {
        localStorage.setItem('selected_board', board.id);
      }
    },
    /**
     * Add a new board
     */
    addBoard: (board: BrainstormBoard) => {
      update((state) => ({
        ...state,
        boards: [board, ...state.boards],
      }));
    },
    /**
     * Update board
     */
    updateBoard: (boardId: string, updates: Partial<BrainstormBoard>) => {
      update((state) => ({
        ...state,
        boards: state.boards.map((b) =>
          b.id === boardId ? { ...b, ...updates } : b
        ),
        currentBoard:
          state.currentBoard?.id === boardId
            ? { ...state.currentBoard, ...updates }
            : state.currentBoard,
      }));
    },
    /**
     * Delete board
     */
    deleteBoard: (boardId: string) => {
      update((state) => ({
        ...state,
        boards: state.boards.filter((b) => b.id !== boardId),
        currentBoard:
          state.currentBoard?.id === boardId ? null : state.currentBoard,
      }));
    },
    /**
     * Add idea to board
     */
    addIdea: (boardId: string, idea: Idea) => {
      update((state) => ({
        ...state,
        boards: state.boards.map((b) =>
          b.id === boardId ? { ...b, ideas: [...b.ideas, idea] } : b
        ),
        currentBoard:
          state.currentBoard?.id === boardId
            ? { ...state.currentBoard, ideas: [...state.currentBoard.ideas, idea] }
            : state.currentBoard,
      }));
    },
    /**
     * Update idea
     */
    updateIdea: (boardId: string, ideaId: string, updates: Partial<Idea>) => {
      update((state) => ({
        ...state,
        boards: state.boards.map((b) =>
          b.id === boardId
            ? {
                ...b,
                ideas: b.ideas.map((i) =>
                  i.id === ideaId ? { ...i, ...updates } : i
                ),
              }
            : b
        ),
        currentBoard:
          state.currentBoard?.id === boardId
            ? {
                ...state.currentBoard,
                ideas: state.currentBoard.ideas.map((i) =>
                  i.id === ideaId ? { ...i, ...updates } : i
                ),
              }
            : state.currentBoard,
      }));
    },
    /**
     * Move idea on board
     */
    moveIdea: (boardId: string, ideaId: string, x: number, y: number) => {
      update((state) => ({
        ...state,
        boards: state.boards.map((b) =>
          b.id === boardId
            ? {
                ...b,
                ideas: b.ideas.map((i) =>
                  i.id === ideaId ? { ...i, x, y } : i
                ),
              }
            : b
        ),
        currentBoard:
          state.currentBoard?.id === boardId
            ? {
                ...state.currentBoard,
                ideas: state.currentBoard.ideas.map((i) =>
                  i.id === ideaId ? { ...i, x, y } : i
                ),
              }
            : state.currentBoard,
      }));
    },
    /**
     * Delete idea
     */
    deleteIdea: (boardId: string, ideaId: string) => {
      update((state) => ({
        ...state,
        boards: state.boards.map((b) =>
          b.id === boardId
            ? {
                ...b,
                ideas: b.ideas.filter((i) => i.id !== ideaId),
              }
            : b
        ),
        currentBoard:
          state.currentBoard?.id === boardId
            ? {
                ...state.currentBoard,
                ideas: state.currentBoard.ideas.filter((i) => i.id !== ideaId),
              }
            : state.currentBoard,
      }));
    },
    /**
     * Add vote to idea
     */
    addVote: (boardId: string, ideaId: string, voteMark: VoteMark) => {
      update((state) => ({
        ...state,
        boards: state.boards.map((b) =>
          b.id === boardId
            ? {
                ...b,
                ideas: b.ideas.map((i) =>
                  i.id === ideaId
                    ? {
                        ...i,
                        votes: { ...i.votes, [voteMark]: i.votes[voteMark] + 1 },
                        userVotes: { ...i.userVotes, [voteMark]: true },
                      }
                    : i
                ),
              }
            : b
        ),
        currentBoard:
          state.currentBoard?.id === boardId
            ? {
                ...state.currentBoard,
                ideas: state.currentBoard.ideas.map((i) =>
                  i.id === ideaId
                    ? {
                        ...i,
                        votes: { ...i.votes, [voteMark]: i.votes[voteMark] + 1 },
                        userVotes: { ...i.userVotes, [voteMark]: true },
                      }
                    : i
                ),
              }
            : state.currentBoard,
      }));
    },
    /**
     * Remove vote from idea
     */
    removeVote: (boardId: string, ideaId: string, voteMark: VoteMark) => {
      update((state) => ({
        ...state,
        boards: state.boards.map((b) =>
          b.id === boardId
            ? {
                ...b,
                ideas: b.ideas.map((i) =>
                  i.id === ideaId
                    ? {
                        ...i,
                        votes: { ...i.votes, [voteMark]: Math.max(0, i.votes[voteMark] - 1) },
                        userVotes: { ...i.userVotes, [voteMark]: false },
                      }
                    : i
                ),
              }
            : b
        ),
        currentBoard:
          state.currentBoard?.id === boardId
            ? {
                ...state.currentBoard,
                ideas: state.currentBoard.ideas.map((i) =>
                  i.id === ideaId
                    ? {
                        ...i,
                        votes: { ...i.votes, [voteMark]: Math.max(0, i.votes[voteMark] - 1) },
                        userVotes: { ...i.userVotes, [voteMark]: false },
                      }
                    : i
                ),
              }
            : state.currentBoard,
      }));
    },
    /**
     * Start editing idea
     */
    startEditingIdea: (ideaId: string) => {
      update((state) => ({
        ...state,
        editingIdeaId: ideaId,
      }));
    },
    /**
     * Stop editing idea
     */
    stopEditingIdea: () => {
      update((state) => ({
        ...state,
        editingIdeaId: null,
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
     * Clear all boards
     */
    clear: () => {
      set({
        boards: [],
        currentBoard: null,
        selectedBoardId: null,
        editingIdeaId: null,
        isLoading: false,
        error: null,
        lastUpdated: null,
      });
      localStorage.removeItem('selected_board');
    },
  };
}

export const brainstorm = createBrainstormStore();

/**
 * Helper functions for managing ideas globally
 */
export function addIdea(idea: Idea) {
  if (brainstorm && typeof brainstorm === 'object' && 'subscribe' in brainstorm) {
    // Use store subscription to get current state and find board
    let currentBoardId: string | null = null;
    brainstorm.subscribe((state) => {
      currentBoardId = state.currentBoard?.id ?? null;
    })();

    if (currentBoardId) {
      brainstorm.addIdea(currentBoardId, idea);
    }
  }
}

export function deleteIdea(ideaId: string) {
  if (brainstorm && typeof brainstorm === 'object' && 'subscribe' in brainstorm) {
    let currentBoardId: string | null = null;
    brainstorm.subscribe((state) => {
      currentBoardId = state.currentBoard?.id ?? null;
    })();

    if (currentBoardId) {
      brainstorm.deleteIdea(currentBoardId, ideaId);
    }
  }
}

export function updateIdeaPosition(ideaId: string, x: number, y: number) {
  if (brainstorm && typeof brainstorm === 'object' && 'subscribe' in brainstorm) {
    let currentBoardId: string | null = null;
    brainstorm.subscribe((state) => {
      currentBoardId = state.currentBoard?.id ?? null;
    })();

    if (currentBoardId) {
      brainstorm.moveIdea(currentBoardId, ideaId, x, y);
    }
  }
}

/**
 * Get all ideas from all boards
 */
export const ideas = derived(brainstorm, ($brainstorm) => {
  return $brainstorm.boards.flatMap((b) => b.ideas);
});

/**
 * Get all ideas from current board
 */
export const brainstormBoard = derived(brainstorm, ($brainstorm) => $brainstorm.currentBoard);

/**
 * Derived stores
 */
export const boardList = derived(brainstorm, ($brainstorm) => $brainstorm.boards);
export const currentBoard = derived(brainstorm, ($brainstorm) => $brainstorm.currentBoard);
export const brainstormLoading = derived(brainstorm, ($brainstorm) => $brainstorm.isLoading);
export const brainstormError = derived(brainstorm, ($brainstorm) => $brainstorm.error);
export const editingIdeaId = derived(brainstorm, ($brainstorm) => $brainstorm.editingIdeaId);

/**
 * Get board by ID
 */
export const getBoardById = (boardId: string) =>
  derived(boardList, ($boards) => $boards.find((b) => b.id === boardId));

/**
 * Get ideas on current board
 */
export const currentBoardIdeas = derived(currentBoard, ($board) =>
  $board?.ideas ?? []
);

/**
 * Get boards by room
 */
export const getBoardsByRoom = (roomId: string) =>
  derived(boardList, ($boards) =>
    $boards.filter((b) => b.roomId === roomId)
  );

/**
 * Get active boards (not archived)
 */
export const activeBoards = derived(boardList, ($boards) =>
  $boards.filter((b) => !b.isArchived)
);

/**
 * Get archived boards
 */
export const archivedBoards = derived(boardList, ($boards) =>
  $boards.filter((b) => b.isArchived)
);

/**
 * Get total ideas count on a board
 */
export const getBoardIdeasCount = (boardId: string) =>
  derived(
    getBoardById(boardId),
    ($board) => $board?.ideas.length ?? 0
  );
