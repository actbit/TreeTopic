import { writable, derived } from 'svelte/store';

export interface BrainIdeaVote {
  id: string;
  brainIdeaId: string;
  applicationUserId?: string;
  userName?: string;
  roomUserId?: string;
  voteType: string;
  value: number;
}

/**
 * Brainstorm idea information
 */
export interface BrainIdea {
  id: string;
  brainBoardId: string;
  topicId: string;
  applicationUserId?: string;
  userName?: string;
  idea: string;
  positionTop: number;
  positionLeft: number;
  votes?: BrainIdeaVote[];
}

/**
 * Brainstorm board information
 */
export interface BrainstormBoard {
  id: string;
  topicId: string;
  name: string;
  isSign: boolean;
  ideaCount: number;
  ideas: BrainIdea[];
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
    addIdea: (boardId: string, idea: BrainIdea) => {
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
    updateIdea: (boardId: string, ideaId: string, updates: Partial<BrainIdea>) => {
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
    moveIdea: (boardId: string, ideaId: string, positionLeft: number, positionTop: number) => {
      update((state) => ({
        ...state,
        boards: state.boards.map((b) =>
          b.id === boardId
            ? {
                ...b,
                ideas: b.ideas.map((i) =>
                  i.id === ideaId ? { ...i, positionLeft, positionTop } : i
                ),
              }
            : b
        ),
        currentBoard:
          state.currentBoard?.id === boardId
            ? {
                ...state.currentBoard,
                ideas: state.currentBoard.ideas.map((i) =>
                  i.id === ideaId ? { ...i, positionLeft, positionTop } : i
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
export function addIdea(idea: BrainIdea) {
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

export function updateIdeaPosition(ideaId: string, positionLeft: number, positionTop: number) {
  if (brainstorm && typeof brainstorm === 'object' && 'subscribe' in brainstorm) {
    let currentBoardId: string | null = null;
    brainstorm.subscribe((state) => {
      currentBoardId = state.currentBoard?.id ?? null;
    })();

    if (currentBoardId) {
      brainstorm.moveIdea(currentBoardId, ideaId, positionLeft, positionTop);
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
 * Get total ideas count on a board
 */
export const getBoardIdeasCount = (boardId: string) =>
  derived(
    getBoardById(boardId),
    ($board) => $board?.ideas.length ?? 0
  );
