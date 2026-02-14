import type { Writable } from 'svelte/store';

export const STORE_CACHE_TTL = {
	SHORT: 30 * 1000, // 30 seconds - volatile data
	MEDIUM: 5 * 60 * 1000, // 5 minutes - default
	LONG: 15 * 60 * 1000 // 15 minutes - static data
} as const;

export interface CachedState {
	lastUpdated: number | null;
	cacheExpiry: number;
}

export function isCacheValid(state: CachedState): boolean {
	if (state.lastUpdated === null) return false;
	return Date.now() < state.cacheExpiry;
}

export function getCacheAge(state: CachedState): number {
	if (state.lastUpdated === null) return -1;
	return Date.now() - state.lastUpdated;
}

export function getCacheRemaining(state: CachedState): number {
	if (state.lastUpdated === null) return 0;
	const remaining = state.cacheExpiry - Date.now();
	return Math.max(0, remaining);
}

export function updateWithCache<T extends CachedState>(
	store: Writable<T>,
	data: Partial<T>,
	ttl: number = STORE_CACHE_TTL.MEDIUM
): void {
	store.update((state) => ({
		...state,
		...data,
		lastUpdated: Date.now(),
		cacheExpiry: Date.now() + ttl
	}));
}

export function clearCache<T extends CachedState>(store: Writable<T>): void {
	store.update((state) => ({
		...state,
		lastUpdated: null,
		cacheExpiry: 0
	}));
}

export function createCachedUpdater<T extends CachedState>(
	store: Writable<T>,
	ttl: number = STORE_CACHE_TTL.MEDIUM
) {
	return (data: Partial<T>) => {
		updateWithCache(store, data, ttl);
	};
}

/**
 * Batch update multiple stores atomically
 * @param updates - Array of store update functions
 */
export function batchUpdate(...updates: Array<() => void>): void {
	// Svelte stores are updated synchronously, so we can just call them in sequence
	updates.forEach((update) => update());
}

/**
 * Create a derived store that includes cache information
 * @param store - The source store
 * @returns An object with cache status information
 */
export function getCacheInfo(state: CachedState): {
	isValid: boolean;
	age: number;
	remaining: number;
	isExpired: boolean;
} {
	const isValid = isCacheValid(state);
	const age = getCacheAge(state);
	const remaining = getCacheRemaining(state);
	return {
		isValid,
		age,
		remaining,
		isExpired: !isValid && state.lastUpdated !== null
	};
}

/**
 * Persist store state to localStorage
 * @param key - Storage key
 * @param state - State to persist
 */
export function persistToStorage<T>(key: string, state: T): void {
	try {
		localStorage.setItem(key, JSON.stringify(state));
	} catch (error) {
		console.error(`Failed to persist store to localStorage (${key}):`, error);
	}
}

/**
 * Restore store state from localStorage
 * @param key - Storage key
 * @returns Restored state or null
 */
export function restoreFromStorage<T>(key: string): T | null {
	try {
		const stored = localStorage.getItem(key);
		if (stored) {
			return JSON.parse(stored) as T;
		}
	} catch (error) {
		console.error(`Failed to restore store from localStorage (${key}):`, error);
	}
	return null;
}

export function clearStorage(key: string): void {
	try {
		localStorage.removeItem(key);
	} catch (error) {
		console.error(`Failed to clear store from localStorage (${key}):`, error);
	}
}
