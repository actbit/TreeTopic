const AUTH_CACHE_TTL = 5 * 60 * 1000; // 5分

interface AuthCacheEntry {
	user: Record<string, unknown>;
	timestamp: number;
}

const authCacheMap = new Map<string, AuthCacheEntry>();

export function getCachedAuth(tenant: string): Record<string, unknown> | null {
	const cache = authCacheMap.get(tenant);
	if (cache) {
		const age = Date.now() - cache.timestamp;
		if (age < AUTH_CACHE_TTL) {
			return cache.user;
		}
		// Cache expired, clear it
		authCacheMap.delete(tenant);
	}
	return null;
}

export function setCachedAuth(tenant: string, user: Record<string, unknown>): void {
	authCacheMap.set(tenant, { user, timestamp: Date.now() });
}

export function clearAuthCache(tenant?: string): void {
	if (tenant) {
		authCacheMap.delete(tenant);
	} else {
		authCacheMap.clear();
	}
}

export function isAuthCacheValid(tenant: string): boolean {
	const cache = authCacheMap.get(tenant);
	if (cache) {
		const age = Date.now() - cache.timestamp;
		return age < AUTH_CACHE_TTL;
	}
	return false;
}

export function getAuthCacheAge(tenant: string): number {
	const cache = authCacheMap.get(tenant);
	if (cache) {
		return Date.now() - cache.timestamp;
	}
	return -1;
}
