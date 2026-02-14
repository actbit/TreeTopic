export type SortDirection = 'asc' | 'desc';

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

export function filter<T>(items: T[], predicate: (item: T) => boolean): T[] {
  return items.filter(predicate);
}

export function filterByField<T>(
  items: T[],
  field: keyof T,
  value: unknown
): T[] {
  return items.filter((item) => item[field] === value);
}

export function filterByMultiple<T>(
  items: T[],
  conditions: Array<{ field: keyof T; value: unknown }>
): T[] {
  return items.filter((item) =>
    conditions.every((condition) => item[condition.field] === condition.value)
  );
}

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

export function groupBy<T>(
  items: T[],
  field: keyof T
): Map<unknown, T[]> {
  const groups = new Map<unknown, T[]>();

  items.forEach((item) => {
    const key = item[field];
    if (!groups.has(key)) {
      groups.set(key, []);
    }
    groups.get(key)!.push(item);
  });

  return groups;
}

export function unique<T>(
  items: T[],
  field?: keyof T
): T[] {
  if (!field) {
    return [...new Set(items)];
  }

  const seen = new Set<unknown>();
  return items.filter((item) => {
    const value = item[field];
    if (seen.has(value)) return false;
    seen.add(value);
    return true;
  });
}

export function paginate<T>(
  items: T[],
  page: number,
  pageSize: number
): T[] {
  const start = (page - 1) * pageSize;
  return items.slice(start, start + pageSize);
}

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

export function reverse<T>(items: T[]): T[] {
  return [...items].reverse();
}

export function shuffle<T>(items: T[]): T[] {
  const shuffled = [...items];
  for (let i = shuffled.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
  }
  return shuffled;
}

export function contains<T>(items: T[], value: T): boolean {
  return items.includes(value);
}

export function findFirst<T>(
  items: T[],
  predicate: (item: T) => boolean
): T | undefined {
  return items.find(predicate);
}

export function findLast<T>(
  items: T[],
  predicate: (item: T) => boolean
): T | undefined {
  return [...items].reverse().find(predicate);
}

export function flatten<T>(items: unknown[]): T[] {
  return items.reduce((flat: T[], item) => {
    return flat.concat(Array.isArray(item) ? flatten<T>(item) : [item as T]);
  }, []);
}
