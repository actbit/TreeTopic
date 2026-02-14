export interface DragPayload {
  type: 'message' | 'topic' | 'idea' | 'file';
  id: string;
  data?: Record<string, unknown>;
}

let cachedDragPayload: DragPayload | null = null;

export interface DropZone {
  id: string;
  type: string;
  accept: string[];
  onDrop: (event: DragPayload, position?: { x: number; y: number }) => void;
}

export function isOverDropZone(dragEvent: DragPayload, dropZone: DropZone): boolean {
  return dropZone.accept.includes(dragEvent.type);
}

export function getDropEffect(dragEvent: DragPayload, dropZone: DropZone): DataTransfer['dropEffect'] {
  if (isOverDropZone(dragEvent, dropZone)) {
    return 'move';
  }
  return 'none';
}

export function getElementPosition(element: HTMLElement): { x: number; y: number; width: number; height: number } {
  const rect = element.getBoundingClientRect();
  return { x: rect.x, y: rect.y, width: rect.width, height: rect.height };
}

export function isPointInElement(x: number, y: number, element: HTMLElement): boolean {
  const { x: elX, y: elY, width, height } = getElementPosition(element);
  return x >= elX && x <= elX + width && y >= elY && y <= elY + height;
}

export function getDropPosition(x: number, y: number, element: HTMLElement): 'before' | 'after' | 'inside' {
  const { y: elY, height } = getElementPosition(element);
  const relativeY = y - elY;
  const threshold = height / 3;

  if (relativeY < threshold) return 'before';
  if (relativeY > threshold * 2) return 'after';
  return 'inside';
}

export function preventDragDefaults(e: DragEvent): void {
  e.preventDefault();
  e.stopPropagation();
}

export function createDragImage(element: HTMLElement, text?: string): HTMLElement {
  const dragImage = element.cloneNode(true) as HTMLElement;
  dragImage.style.position = 'absolute';
  dragImage.style.top = '-9999px';
  dragImage.style.opacity = '0.8';

  if (text) {
    dragImage.textContent = text;
  }

  document.body.appendChild(dragImage);
  return dragImage;
}

export function setDragData(event: DragEvent, dragPayload: DragPayload): void {
  const dataTransfer = event.dataTransfer;
  if (dataTransfer) {
    dataTransfer.effectAllowed = 'move';
    dataTransfer.setData('application/json', JSON.stringify(dragPayload));
    dataTransfer.setData('text/plain', `${dragPayload.type}:${dragPayload.id}`);
    cachedDragPayload = dragPayload;
  }
}

export function getDragData(event: DragEvent): DragPayload | null {
  const dataTransfer = event.dataTransfer;
  if (dataTransfer) {
    const data = dataTransfer.getData('application/json');
    if (data) {
      try {
        const parsed = JSON.parse(data) as DragPayload;
        cachedDragPayload = parsed;
        return parsed;
      } catch {
        return cachedDragPayload;
      }
    }
  }
  return cachedDragPayload;
}

export function clearDragData(): void {
  cachedDragPayload = null;
}

export function snapToGrid(x: number, y: number, gridSize: number = 10): { x: number; y: number } {
  return {
    x: Math.round(x / gridSize) * gridSize,
    y: Math.round(y / gridSize) * gridSize,
  };
}

export function constrainPosition(x: number, y: number, width: number, height: number, boundWidth: number, boundHeight: number): { x: number; y: number } {
  return {
    x: Math.max(0, Math.min(x, boundWidth - width)),
    y: Math.max(0, Math.min(y, boundHeight - height)),
  };
}

export function getDistance(x1: number, y1: number, x2: number, y2: number): number {
  return Math.sqrt(Math.pow(x2 - x1, 2) + Math.pow(y2 - y1, 2));
}

export function hasMoved(startX: number, startY: number, currentX: number, currentY: number, threshold: number = 5): boolean {
  return getDistance(startX, startY, currentX, currentY) > threshold;
}

export function getScrollDirection(x: number, y: number, containerX: number, containerY: number, containerWidth: number, containerHeight: number, scrollThreshold: number = 30): { scrollX: number; scrollY: number } {
  let scrollX = 0;
  let scrollY = 0;

  if (x < containerX + scrollThreshold) scrollX = -1;
  else if (x > containerX + containerWidth - scrollThreshold) scrollX = 1;

  if (y < containerY + scrollThreshold) scrollY = -1;
  else if (y > containerY + containerHeight - scrollThreshold) scrollY = 1;

  return { scrollX, scrollY };
}

export function autoScroll(container: HTMLElement, x: number, y: number, speed: number = 10, threshold: number = 30): void {
  const rect = container.getBoundingClientRect();
  const { scrollX, scrollY } = getScrollDirection(x, y, rect.left, rect.top, rect.width, rect.height, threshold);

  if (scrollX !== 0) {
    container.scrollLeft += scrollX * speed;
  }

  if (scrollY !== 0) {
    container.scrollTop += scrollY * speed;
  }
}
