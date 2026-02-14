export function formatPermissionName(
  name: string,
  excludePrefixes: string[] = ['tenant', 'room', 'topic']
): string {
  return name
    .split('.')
    .filter((part) => !excludePrefixes.includes(part))
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ')
    .trim();
}
