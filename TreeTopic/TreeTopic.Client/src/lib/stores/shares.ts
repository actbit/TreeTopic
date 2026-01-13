import { writable, derived } from 'svelte/store';
import { api } from '$lib/api/client';

export type ShareKind = 'document' | 'image' | 'brainstorm';

export interface ShareItem {
  id: string;
  roomId: string;
  topicId?: string | null;
  kind: ShareKind;
  boardId?: string | null;
  title: string;
  fileName: string;
  mimeType: string;
  size: number;
  url: string;
  createdAt: Date;
  createdBy?: {
    id: string;
    name?: string | null;
    displayName?: string | null;
  } | null;
  createdByName: string;
  sourceMessage?: {
    id: string;
    header: string;
  } | null;
  sourceFile?: {
    id: string;
    fileName: string;
  } | null;
  sourceShareItem?: {
    id: string;
    title: string;
  } | null;
}

export interface SharesState {
  items: ShareItem[];
  isLoading: boolean;
  error: string | null;
  lastUpdated: number | null;
}

function normalizeShare(raw: any): ShareItem {
  const createdAt = raw?.createdAt ?? raw?.CreatedAt ?? null;
  const kind = (raw?.kind ?? raw?.Kind ?? 'document') as ShareKind;

  return {
    id: raw?.id ?? raw?.Id ?? '',
    roomId: raw?.roomId ?? raw?.RoomId ?? '',
    topicId: raw?.topicId ?? raw?.TopicId ?? null,
    kind,
    boardId: raw?.boardId ?? raw?.BoardId ?? null,
    title: raw?.title ?? raw?.Title ?? '',
    fileName: raw?.fileName ?? raw?.FileName ?? '',
    mimeType: raw?.mimeType ?? raw?.MimeType ?? '',
    size: raw?.size ?? raw?.Size ?? 0,
    url: raw?.url ?? raw?.Url ?? '',
    createdAt: createdAt ? new Date(createdAt) : new Date(),
    createdBy: raw?.createdByUser ?? raw?.CreatedByUser ?? null,
    createdByName:
      raw?.createdByName ??
      raw?.CreatedByName ??
      raw?.createdByUser?.displayName ??
      raw?.CreatedByUser?.DisplayName ??
      '',
    sourceMessage: raw?.sourceMessage ?? raw?.SourceMessage ?? null,
    sourceFile: raw?.sourceFile ?? raw?.SourceFile ?? null,
    sourceShareItem: raw?.sourceShareItem ?? raw?.SourceShareItem ?? null,
  };
}

function createSharesStore() {
  const { subscribe, set, update } = writable<SharesState>({
    items: [],
    isLoading: false,
    error: null,
    lastUpdated: null,
  });

  return {
    subscribe,
    setLoading: (isLoading: boolean) => update((s) => ({ ...s, isLoading })),
    setError: (error: string | null) => update((s) => ({ ...s, error })),
    setShares: (items: ShareItem[]) =>
      update((s) => ({
        ...s,
        items,
        error: null,
        lastUpdated: Date.now(),
      })),
    addShare: (item: ShareItem) => update((s) => ({ ...s, items: [item, ...s.items] })),
    removeShare: (id: string) => update((s) => ({ ...s, items: s.items.filter((x) => x.id !== id) })),
    clear: () => set({ items: [], isLoading: false, error: null, lastUpdated: null }),
  };
}

export const shares = createSharesStore();

export const shareItems = derived(shares, ($s) => $s.items);
export const sharesLoading = derived(shares, ($s) => $s.isLoading);
export const sharesError = derived(shares, ($s) => $s.error);

export async function loadShares(params: { tenant: string; roomId: string; topicId?: string | null }) {
  shares.setLoading(true);
  shares.setError(null);

  try {
    const list = await api.get<any[]>(`/${params.tenant}/api/Share/room/${params.roomId}`, {
      params: {
        topicId: params.topicId || undefined,
      },
    });

    const normalized = Array.isArray(list) ? list.map(normalizeShare).filter((x) => x.id) : [];
    shares.setShares(normalized);
  } catch (err: unknown) {
    shares.setError(err instanceof Error ? err.message : 'Failed to load shares');
  } finally {
    shares.setLoading(false);
  }
}

export function denormalizeShareForAdd(raw: any): ShareItem {
  return normalizeShare(raw);
}
