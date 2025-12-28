// Export all utilities from a single entry point

// Date utilities
export {
  formatDate,
  formatRelativeTime,
  formatTime,
  isToday,
  isYesterday,
  startOfDay,
  endOfDay,
  addDays,
  daysBetween,
} from './date';

// Validation utilities
export {
  isValidEmail,
  validatePasswordStrength,
  isValidUsername,
  isValidUrl,
  isValidFileSize,
  isValidFileType,
  formatFileSize,
  isValidHexColor,
  isValidJson,
  isInRange,
  isRequired,
  minLength,
  maxLength,
  isValidUUID,
  isValidGUID,
  sanitizeInput,
  escapeHtml,
} from './validation';

// Sorting utilities
export {
  sort,
  sortByFields,
  filter,
  filterByField,
  filterByMultiple,
  search,
  groupBy,
  unique,
  paginate,
  getPaginationInfo,
  reverse,
  shuffle,
  contains,
  findFirst,
  findLast,
  flatten,
} from './sorting';

// Drag and drop utilities
export {
  isOverDropZone,
  getDropEffect,
  getElementPosition,
  isPointInElement,
  getDropPosition,
  preventDragDefaults,
  createDragImage,
  setDragData,
  getDragData,
  snapToGrid,
  constrainPosition,
  getDistance,
  hasMoved,
  getScrollDirection,
  autoScroll,
} from './dragdrop';

export type { DragEvent, DropZone } from './dragdrop';
