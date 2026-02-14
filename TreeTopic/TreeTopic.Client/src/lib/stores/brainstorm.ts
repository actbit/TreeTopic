import { writable, derived, get } from 'svelte/store';

export interface BrainIdeaVote {
  id: string;
  brainIdeaId: string;
  applicationUserId?: string;
  userName?: string;
  roomUserId?: string;
  voteType: string;
  value: number;
}

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

export interface BrainstormBoard {
  id: string;
  topicId: string;
  name: string;
  isSign: boolean;
  ideaCount: number;
  ideas: BrainIdea[];
}

export interface BrainstormState {
  boards: BrainstormBoard[];
  currentBoard: BrainstormBoard | null;
  selectedBoardId: string | null;
  editingIdeaId: string | null;
  isLoading: boolean;
  error: string | null;
  lastUpdated: number | null;
}

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
    setBoards: (boards: BrainstormBoard[]) => {
      update((state) => ({
        ...state,
        boards,
        error: null,
        lastUpdated: Date.now(),
      }));
    },
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
    addBoard: (board: BrainstormBoard) => {
      update((state) => ({
        ...state,
        boards: [board, ...state.boards],
      }));
    },
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
    deleteBoard: (boardId: string) => {
      update((state) => ({
        ...state,
        boards: state.boards.filter((b) => b.id !== boardId),
        currentBoard:
          state.currentBoard?.id === boardId ? null : state.currentBoard,
      }));
    },
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
    startEditingIdea: (ideaId: string) => {
      update((state) => ({
        ...state,
        editingIdeaId: ideaId,
      }));
    },
    stopEditingIdea: () => {
      update((state) => ({
        ...state,
        editingIdeaId: null,
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

export function addIdea(idea: BrainIdea) {
  if (brainstorm && typeof brainstorm === 'object' && 'subscribe' in brainstorm) {
    try {
      const state = get(brainstorm);
      const currentBoardId = state.currentBoard?.id ?? null;

      if (currentBoardId) {
        brainstorm.addIdea(currentBoardId, idea);
      } else {
        console.warn('Cannot add idea: No current board selected');
      }
    } catch (error) {
      console.error('Error adding idea:', error);
    }
  } else {
    console.warn('Brainstorm store not initialized');
  }
}

export function deleteIdea(ideaId: string) {
  if (brainstorm && typeof brainstorm === 'object' && 'subscribe' in brainstorm) {
    try {
      const state = get(brainstorm);
      const currentBoardId = state.currentBoard?.id ?? null;

      if (currentBoardId) {
        brainstorm.deleteIdea(currentBoardId, ideaId);
      } else {
        console.warn('Cannot delete idea: No current board selected');
      }
    } catch (error) {
      console.error('Error deleting idea:', error);
    }
  } else {
    console.warn('Brainstorm store not initialized');
  }
}

export function updateIdeaPosition(ideaId: string, positionLeft: number, positionTop: number) {
  if (brainstorm && typeof brainstorm === 'object' && 'subscribe' in brainstorm) {
    try {
      const state = get(brainstorm);
      const currentBoardId = state.currentBoard?.id ?? null;

      if (currentBoardId) {
        brainstorm.moveIdea(currentBoardId, ideaId, positionLeft, positionTop);
      } else {
        console.warn('Cannot move idea: No current board selected');
      }
    } catch (error) {
      console.error('Error moving idea:', error);
    }
  } else {
    console.warn('Brainstorm store not initialized');
  }
}

export const ideas = derived(brainstorm, ($brainstorm) => {
  return $brainstorm.boards.flatMap((b) => b.ideas);
});

export const brainstormBoard = derived(brainstorm, ($brainstorm) => $brainstorm.currentBoard);

export const boardList = derived(brainstorm, ($brainstorm) => $brainstorm.boards);
export const currentBoard = derived(brainstorm, ($brainstorm) => $brainstorm.currentBoard);
export const brainstormLoading = derived(brainstorm, ($brainstorm) => $brainstorm.isLoading);
export const brainstormError = derived(brainstorm, ($brainstorm) => $brainstorm.error);
export const editingIdeaId = derived(brainstorm, ($brainstorm) => $brainstorm.editingIdeaId);

export const getBoardById = (boardId: string) =>
  derived(boardList, ($boards) => $boards.find((b) => b.id === boardId));

export const currentBoardIdeas = derived(currentBoard, ($board) =>
  $board?.ideas ?? []
);

export const getBoardIdeasCount = (boardId: string) =>
  derived(
    getBoardById(boardId),
    ($board) => $board?.ideas.length ?? 0
  );
