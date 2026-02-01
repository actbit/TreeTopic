/**
 * API Cache Manager
 *
 * Provides in-memory caching for API responses with TTL support.
 * Cache is automatically cleared on authentication errors.
 */

/**
 * Cache configuration with different TTL durations
 */
export const CACHE_CONFIG = {
	short: 30 * 1000, // 30 seconds - volatile data
	medium: 5 * 60 * 1000, // 5 minutes - user data
	long: 15 * 60 * 1000 // 15 minutes - static data
} as const;

/**
 * Cache entry with metadata
 */
interface CacheEntry<T> {
	data: T;
	timestamp: number;
	etag?: string;
}

/**
 * Cache storage
 */
const cache = new Map<string, CacheEntry<any>>();

/**
 * Cache configuration for specific URL patterns
 */
const CACHE_TTL_BY_PATTERN: Array<{ pattern: RegExp; ttl: number }> = [
	// User-related endpoints - short cache
	{ pattern: /\/auth\/(me|check)/, ttl: CACHE_CONFIG.short },
	// Topics - medium cache
	{ pattern: /\/api\/Topic/, ttl: CACHE_CONFIG.medium },
	// Rooms - medium cache
	{ pattern: /\/api\/Room/, ttl: CACHE_CONFIG.medium },
	// Messages - short cache (frequently updated)
	{ pattern: /\/api\/Message/, ttl: CACHE_CONFIG.short },
	// Files - long cache
	{ pattern: /\/api\/File/, ttl: CACHE_CONFIG.long }
];

/**
 * Generate a cache key from request parameters
 * @param method - HTTP method
 * @param path - Request path
 * @param params - Query parameters
 * @returns A unique cache key
 */
export function generateCacheKey(method: string, path: string, params?: Record<string, any>): string {
	const parts = [method.toUpperCase(), path];
	if (params) {
		const sortedParams = Object.keys(params)
			.sort()
			.map((key) => `${key}=${JSON.stringify(params[key])}`)
			.join('&');
		if (sortedParams) {
			parts.push(sortedParams);
		}
	}
	return parts.join(':');
}

/**
 * Get TTL for a specific URL pattern
 * @param path - Request path
 * @returns TTL in milliseconds
 */
function getTtlForPath(path: string): number {
	for (const { pattern, ttl } of CACHE_TTL_BY_PATTERN) {
		if (pattern.test(path)) {
			return ttl;
		}
	}
	return CACHE_CONFIG.medium; // Default TTL
}

/**
 * Get cached data
 * @param key - Cache key
 * @returns Cached data if valid, null otherwise
 */
export function get<T>(key: string): T | null {
	const entry = cache.get(key);
	if (!entry) return null;

	const ttl = getTtlForPath(key);
	const age = Date.now() - entry.timestamp;

	if (age >= ttl) {
		cache.delete(key);
		return null;
	}

	return entry.data as T;
}

/**
 * Set cached data
 * @param key - Cache key
 * @param data - Data to cache
 * @param etag - Optional ETag for validation
 */
export function set<T>(key: string, data: T, etag?: string): void {
	cache.set(key, {
		data,
		timestamp: Date.now(),
		etag
	});
}

/**
 * Invalidate cache entries matching a pattern
 * @param pattern - Regular expression pattern to match cache keys
 */
export function invalidate(pattern: RegExp): void {
	const keysToDelete: string[] = [];
	for (const key of cache.keys()) {
		if (pattern.test(key)) {
			keysToDelete.push(key);
		}
	}
	keysToDelete.forEach((key) => cache.delete(key));
}

/**
 * Invalidate cache for a specific resource
 * @param method - HTTP method of the mutating request
 * @param path - Path of the mutated resource
 */
export function invalidateByResource(method: string, path: string): void {
	// Invalidate related cache entries based on the mutated resource
	if (path.includes('/api/Topic')) {
		invalidate(/^GET:.*\/api\/Topic/);
	}
	if (path.includes('/api/Room')) {
		invalidate(/^GET:.*\/api\/Room/);
	}
	if (path.includes('/api/Message')) {
		invalidate(/^GET:.*\/api\/Message/);
	}
	if (path.includes('/api/File')) {
		invalidate(/^GET:.*\/api\/File/);
	}
}

/**
 * Clear all cache entries
 * Called on authentication errors
 */
export function clear(): void {
	cache.clear();
}

/**
 * Get cache statistics
 * @returns Cache size and entry information
 */
export function getCacheStats(): { size: number; entries: Array<{ key: string; age: number }> } {
	const entries: Array<{ key: string; age: number }> = [];
	for (const [key, entry] of cache.entries()) {
		entries.push({ key, age: Date.now() - entry.timestamp });
	}
	return { size: cache.size, entries };
}

/**
 * Check if a cache entry exists and is valid
 * @param key - Cache key
 * @returns True if cache entry exists and is valid
 */
export function has(key: string): boolean {
	const entry = cache.get(key);
	if (!entry) return false;

	const ttl = getTtlForPath(key);
	const age = Date.now() - entry.timestamp;
	return age < ttl;
}

/**
 * Get the age of a cache entry
 * @param key - Cache key
 * @returns Age in milliseconds, or -1 if not found
 */
export function getAge(key: string): number {
	const entry = cache.get(key);
	if (!entry) return -1;
	return Date.now() - entry.timestamp;
}
