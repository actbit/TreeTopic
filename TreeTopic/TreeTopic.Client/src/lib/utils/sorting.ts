/**
 * Sorting and filtering utilities
 */

export type SortDirection = 'asc' | 'desc';

/**
 * Generic sort function
 */
export function sort<T>(
  items: T[],
  field: keyof T,
  direction: SortDirection = 'asc'
): T[] {
  return [...items].sort((a, b) => {
    const aVal = a[field];
    const bVal = b[field];

    if (aVal === bVal) return 0;
    if (aVal === null || aVal === undefined) return direction === 'asc' ? 1 : -1;
    if (bVal === null || bVal === undefined) return direction === 'asc' ? -1 : 1;

    let comparison = 0;

    if (typeof aVal === 'string' && typeof bVal === 'string') {
      comparison = aVal.localeCompare(bVal);
    } else if (typeof aVal === 'number' && typeof bVal === 'number') {
      comparison = aVal - bVal;
    } else if (aVal instanceof Date && bVal instanceof Date) {
      comparison = aVal.getTime() - bVal.getTime();
    } else {
      comparison = String(aVal).localeCompare(String(bVal));
    }

    return direction === 'asc' ? comparison : -comparison;
  });
}

/**
 * Sort by multiple fields
 */
export function sortByFields<T>(
  items: T[],
  fields: Array<{ field: keyof T; direction?: SortDirection }>
): T[] {
  return [...items].sort((a, b) => {
    for (const { field, direction = 'asc' } of fields) {
      const aVal = a[field];
      const bVal = b[field];

      if (aVal === bVal) continue;
      if (aVal === null || aVal === undefined) return direction === 'asc' ? 1 : -1;
      if (bVal === null || bVal === undefined) return direction === 'asc' ? -1 : 1;

      let comparison = 0;

      if (typeof aVal === 'string' && typeof bVal === 'string') {
        comparison = aVal.localeCompare(bVal);
      } else if (typeof aVal === 'number' && typeof bVal === 'number') {
        comparison = aVal - bVal;
      } else if (aVal instanceof Date && bVal instanceof Date) {
        comparison = aVal.getTime() - bVal.getTime();
      } else {
        comparison = String(aVal).localeCompare(String(bVal));
      }

      return direction === 'asc' ? comparison : -comparison;
    }
    return 0;
  });
}

/**
 * Filter array by predicate
 */
export function filter<T>(items: T[], predicate: (item: T) => boolean): T[] {
  return items.filter(predicate);
}

/**
 * Filter array by field value
 */
export function filterByField<T>(
  items: T[],
  field: keyof T,
  value: any
): T[] {
  return items.filter((item) => item[field] === value);
}

/**
 * Filter array by multiple conditions
 */
export function filterByMultiple<T>(
  items: T[],
  conditions: Array<{ field: keyof T; value: any }>
): T[] {
  return items.filter((item) =>
    conditions.every((condition) => item[condition.field] === condition.value)
  );
}

/**
 * Search in array by text
 */
export function search<T>(
  items: T[],
  searchTerm: string,
  fields: (keyof T)[]
): T[] {
  if (!searchTerm) return items;

  const lowerSearchTerm = searchTerm.toLowerCase();
  return items.filter((item) =>
    fields.some((field) =>
      String(item[field]).toLowerCase().includes(lowerSearchTerm)
    )
  );
}

/**
 * Group array by field
 */
export function groupBy<T>(
  items: T[],
  field: keyof T
): Map<any, T[]> {
  const groups = new Map<any, T[]>();

  items.forEach((item) => {
    const key = item[field];
    if (!groups.has(key)) {
      groups.set(key, []);
    }
    groups.get(key)!.push(item);
  });

  return groups;
}

/**
 * Unique values from array
 */
export function unique<T>(
  items: T[],
  field?: keyof T
): T[] {
  if (!field) {
    return [...new Set(items)];
  }

  const seen = new Set<any>();
  return items.filter((item) => {
    const value = item[field];
    if (seen.has(value)) return false;
    seen.add(value);
    return true;
  });
}

/**
 * Paginate array
 */
export function paginate<T>(
  items: T[],
  page: number,
  pageSize: number
): T[] {
  const start = (page - 1) * pageSize;
  return items.slice(start, start + pageSize);
}

/**
 * Get pagination info
 */
export function getPaginationInfo(
  totalItems: number,
  pageSize: number,
  currentPage: number
) {
  const totalPages = Math.ceil(totalItems / pageSize);
  const hasNextPage = currentPage < totalPages;
  const hasPreviousPage = currentPage > 1;

  return {
    totalItems,
    pageSize,
    currentPage,
    totalPages,
    hasNextPage,
    hasPreviousPage,
    startIndex: (currentPage - 1) * pageSize,
    endIndex: Math.min(currentPage * pageSize, totalItems),
  };
}

/**
 * Reverse array
 */
export function reverse<T>(items: T[]): T[] {
  return [...items].reverse();
}

/**
 * Shuffle array
 */
export function shuffle<T>(items: T[]): T[] {
  const shuffled = [...items];
  for (let i = shuffled.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
  }
  return shuffled;
}

/**
 * Check if array contains value
 */
export function contains<T>(items: T[], value: T): boolean {
  return items.includes(value);
}

/**
 * Find first item matching predicate
 */
export function findFirst<T>(
  items: T[],
  predicate: (item: T) => boolean
): T | undefined {
  return items.find(predicate);
}

/**
 * Find last item matching predicate
 */
export function findLast<T>(
  items: T[],
  predicate: (item: T) => boolean
): T | undefined {
  return [...items].reverse().find(predicate);
}

/**
 * Flatten nested array
 */
export function flatten<T>(items: any[]): T[] {
  return items.reduce((flat, item) => {
    return flat.concat(Array.isArray(item) ? flatten(item) : item);
  }, []);
}
