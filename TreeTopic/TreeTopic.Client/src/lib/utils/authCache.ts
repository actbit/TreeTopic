/**
 * Authentication Cache Module
 *
 * Provides caching for authentication data to reduce redundant API calls.
 * Cache is automatically cleared on authentication errors (401/403).
 */

const AUTH_CACHE_TTL = 5 * 60 * 1000; // 5 minutes

interface AuthCacheEntry {
	user: Record<string, unknown>;
	timestamp: number;
}

// Map to store multiple tenant caches
const authCacheMap = new Map<string, AuthCacheEntry>();

/**
 * Get cached authentication data for a tenant
 * @param tenant - The tenant identifier
 * @returns The cached user data if valid, null otherwise
 */
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

/**
 * Set cached authentication data for a tenant
 * @param tenant - The tenant identifier
 * @param user - The user data to cache
 */
export function setCachedAuth(tenant: string, user: Record<string, unknown>): void {
	authCacheMap.set(tenant, { user, timestamp: Date.now() });
}

/**
 * Clear the authentication cache for a specific tenant or all tenants
 * Called on authentication errors (401/403) or logout
 * @param tenant - Optional tenant identifier. If not provided, clears all caches
 */
export function clearAuthCache(tenant?: string): void {
	if (tenant) {
		authCacheMap.delete(tenant);
	} else {
		authCacheMap.clear();
	}
}

/**
 * Check if cached authentication data is still valid
 * @param tenant - The tenant identifier
 * @returns True if cache is valid, false otherwise
 */
export function isAuthCacheValid(tenant: string): boolean {
	const cache = authCacheMap.get(tenant);
	if (cache) {
		const age = Date.now() - cache.timestamp;
		return age < AUTH_CACHE_TTL;
	}
	return false;
}

/**
 * Get the age of the cached authentication data in milliseconds
 * @param tenant - The tenant identifier
 * @returns The age of the cache, or -1 if no cache exists
 */
export function getAuthCacheAge(tenant: string): number {
	const cache = authCacheMap.get(tenant);
	if (cache) {
		return Date.now() - cache.timestamp;
	}
	return -1;
}
