export const CACHE_CONFIG = {
	short: 30 * 1000,
	medium: 5 * 60 * 1000,
	long: 15 * 60 * 1000
} as const;

interface CacheEntry<T> {
	data: T;
	timestamp: number;
	etag?: string;
}

const cache = new Map<string, CacheEntry<any>>();

const CACHE_TTL_BY_PATTERN: Array<{ pattern: RegExp; ttl: number }> = [
	{ pattern: /\/auth\/(me|check)/i, ttl: CACHE_CONFIG.short },
	{ pattern: /\/api\/Topic/i, ttl: CACHE_CONFIG.medium },
	{ pattern: /\/api\/Room/i, ttl: CACHE_CONFIG.medium },
	{ pattern: /\/api\/Message/i, ttl: CACHE_CONFIG.short },
	{ pattern: /\/api\/File/i, ttl: CACHE_CONFIG.long }
];

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

function getTtlForPath(path: string): number {
	for (const { pattern, ttl } of CACHE_TTL_BY_PATTERN) {
		if (pattern.test(path)) {
			return ttl;
		}
	}
	return CACHE_CONFIG.medium;
}

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

export function set<T>(key: string, data: T, etag?: string): void {
	cache.set(key, {
		data,
		timestamp: Date.now(),
		etag
	});
}

export function invalidate(pattern: RegExp): void {
	const keysToDelete: string[] = [];
	for (const key of cache.keys()) {
		if (pattern.test(key)) {
			keysToDelete.push(key);
		}
	}
	keysToDelete.forEach((key) => cache.delete(key));
}

export function invalidateByResource(method: string, path: string): void {
	const normalizedPath = path.toLowerCase();

	if (normalizedPath.includes('/api/topic')) {
		invalidate(/^GET:.*\/api\/Topic/i);
	}
	if (normalizedPath.includes('/api/room')) {
		invalidate(/^GET:.*\/api\/Room/i);
	}
	if (normalizedPath.includes('/api/message')) {
		invalidate(/^GET:.*\/api\/Message/i);
	}
	if (normalizedPath.includes('/api/file')) {
		invalidate(/^GET:.*\/api\/File/i);
	}

	if (normalizedPath.includes('/api/tenantroles') && normalizedPath.includes('/permissions')) {
		invalidate(/^GET:.*\/api\/tenantroles/i);
		invalidate(/^GET:.*\/api\/permissions/i);
		invalidate(/^GET:.*\/auth\//i);
		invalidate(/^GET:.*\/api\/Room/i);
		invalidate(/^GET:.*\/api\/Topic/i);
	}

	if (normalizedPath.includes('/api/rooms') && normalizedPath.includes('/roomroles') && normalizedPath.includes('/permissions')) {
		invalidate(/^GET:.*\/api\/rooms\/[^/]+\/roomroles/i);
		invalidate(/^GET:.*\/api\/permissions/i);
		invalidate(/^GET:.*\/api\/Room/i);
		invalidate(/^GET:.*\/api\/Topic/i);
	}

	if (normalizedPath.includes('/users/') && normalizedPath.includes('/roles')) {
		invalidate(/^GET:.*\/auth\//i);
		invalidate(/^GET:.*\/api\/Users/i);
		invalidate(/^GET:.*\/api\/Roles/i);
		invalidate(/^GET:.*\/api\/Room/i);
		invalidate(/^GET:.*\/api\/Topic/i);
	}

	if (normalizedPath.includes('/api/room') && (normalizedPath.includes('/users') || normalizedPath.includes('/roles'))) {
		invalidate(/^GET:.*\/api\/Room/i);
		invalidate(/^GET:.*\/api\/Topic/i);
	}

	if (normalizedPath.includes('/api/topics') && normalizedPath.includes('/permissions')) {
		invalidate(/^GET:.*\/api\/topics\/[^/]+\/permissions/i);
		invalidate(/^GET:.*\/api\/Topic/i);
	}
}

export function clear(): void {
	cache.clear();
}

export function getCacheStats(): { size: number; entries: Array<{ key: string; age: number }> } {
	const entries: Array<{ key: string; age: number }> = [];
	for (const [key, entry] of cache.entries()) {
		entries.push({ key, age: Date.now() - entry.timestamp });
	}
	return { size: cache.size, entries };
}

export function has(key: string): boolean {
	const entry = cache.get(key);
	if (!entry) return false;

	const ttl = getTtlForPath(key);
	const age = Date.now() - entry.timestamp;
	return age < ttl;
}

export function getAge(key: string): number {
	const entry = cache.get(key);
	if (!entry) return -1;
	return Date.now() - entry.timestamp;
}
