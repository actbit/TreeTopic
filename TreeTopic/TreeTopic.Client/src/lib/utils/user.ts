interface HasDisplayName {
  displayName?: string;
  userName?: string;
  email?: string;
  userId?: string;
}

export function getDisplayName(user: HasDisplayName | null | undefined): string {
  if (!user) return 'Unknown';
  return user.displayName || user.userName || user.email || user.userId || 'Unknown';
}
