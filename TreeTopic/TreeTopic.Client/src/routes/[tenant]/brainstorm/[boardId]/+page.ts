import type { PageLoad } from './$types';
import { api } from '$lib/api/client';

export const load: PageLoad = async ({ params }) => {
  try {
    const boardId = params.boardId;

    if (!boardId) {
      throw new Error('Board ID is required');
    }

    // Fetch board data
    const board = await api.get(`/api/brainstorm/${boardId}`);

    return {
      boardId,
      board,
    };
  } catch (error) {
    console.error('Failed to load brainstorm board:', error);
    throw error;
  }
};
