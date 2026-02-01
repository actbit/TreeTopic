/**
 * Authentication Cache Module
 *
 * Provides caching for authentication data to reduce redundant API calls.
 * Cache is automatically cleared on authentication errors (401/403).
 */

const AUTH_CACHE_TTL = 5 * 60 * 1000; // 5 minutes

interface AuthCacheEntry {
	tenant: string;
	user: any;
	timestamp: number;
}

let authCache: AuthCacheEntry | null = null;

/**
 * Get cached authentication data for a tenant
 * @param tenant - The tenant identifier
 * @returns The cached user data if valid, null otherwise
 */
export function getCachedAuth(tenant: string): any | null {
	if (authCache && authCache.tenant === tenant) {
		const age = Date.now() - authCache.timestamp;
		if (age < AUTH_CACHE_TTL) {
			return authCache.user;
		}
		// Cache expired, clear it
		authCache = null;
	}
	return null;
}

/**
 * Set cached authentication data for a tenant
 * @param tenant - The tenant identifier
 * @param user - The user data to cache
 */
export function setCachedAuth(tenant: string, user: any): void {
	authCache = { tenant, user, timestamp: Date.now() };
}

/**
 * Clear the authentication cache
 * Called on authentication errors (401/403) or logout
 */
export function clearAuthCache(): void {
	authCache = null;
}

/**
 * Check if cached authentication data is still valid
 * @param tenant - The tenant identifier
 * @returns True if cache is valid, false otherwise
 */
export function isAuthCacheValid(tenant: string): boolean {
	if (authCache && authCache.tenant === tenant) {
		const age = Date.now() - authCache.timestamp;
		return age < AUTH_CACHE_TTL;
	}
	return false;
}

/**
 * Get the age of the cached authentication data in milliseconds
 * @returns The age of the cache, or -1 if no cache exists
 */
export function getAuthCacheAge(): number {
	if (authCache) {
		return Date.now() - authCache.timestamp;
	}
	return -1;
}
