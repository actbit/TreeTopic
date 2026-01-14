export const MESSAGE_ANCHOR_PREFIX = 'message-';

export function getMessageAnchorId(messageId: string): string {
  return `${MESSAGE_ANCHOR_PREFIX}${messageId}`;
}

export function getMessageAnchorIdFromHash(hash: string): string | null {
  if (!hash) return null;
  if (!hash.startsWith('#')) return null;

  const id = hash.slice(1);
  if (!id.startsWith(MESSAGE_ANCHOR_PREFIX)) return null;

  return id;
}

export function scrollToMessageAnchor(anchorId: string, behavior: ScrollBehavior = 'auto'): boolean {
  if (typeof document === 'undefined') return false;
  const el = document.getElementById(anchorId);
  if (!el) return false;

  el.scrollIntoView({ block: 'center', behavior });
  return true;
}

