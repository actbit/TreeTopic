import type { PageLoad } from './$types';

export const load: PageLoad = async ({ params, fetch }) => {
  const boardId = params.boardId;
  const tenant = params.tenant;

  if (!boardId || boardId === 'undefined' || boardId === 'null') {
    return {
      boardId: '',
      tenant,
      board: null,
      loadError: 'Board ID is required',
    };
  }

  if (!tenant) {
    return {
      boardId,
      tenant: '',
      board: null,
      loadError: 'Tenant is required',
    };
  }

  try {
    const response = await fetch(`/${tenant}/api/Brainstorm/${boardId}`, {
      credentials: 'include',
    });
    if (!response.ok) {
      return {
        boardId,
        tenant,
        board: null,
        loadError: response.statusText || 'Failed to load brainstorm board',
      };
    }

    const board = await response.json();

    return {
      boardId,
      tenant,
      board,
      loadError: null,
    };
  } catch (error) {
    console.error('Failed to load brainstorm board:', error);
    return {
      boardId,
      tenant,
      board: null,
      loadError: 'Failed to load brainstorm board',
    };
  }
};
