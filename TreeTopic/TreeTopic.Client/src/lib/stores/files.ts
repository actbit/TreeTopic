import { writable, derived } from 'svelte/store';
import { api } from '$lib/api/client';

export interface FileVersion {
  id: string;
  versionNumber: number;
  uploadedAt: Date;
  uploadedBy: string;
  size: number;
  url: string;
  changeNote?: string;
}

export interface Material {
  id: string;
  roomId: string;
  messageId?: string;
  fileName: string;
  originalFileName: string;
  mimeType: string;
  size: number;
  url: string;
  fileType: 'image' | 'pdf' | 'document' | 'other';
  uploadedAt: Date;
  uploadedBy: string;
  uploadedByName: string;
  versions: FileVersion[];
}

export interface FileUploadProgress {
  fileId: string;
  fileName: string;
  progress: number;
  status: 'pending' | 'uploading' | 'completed' | 'failed';
  error?: string;
  size: number;
  uploadedBytes: number;
}

export interface FilesState {
  files: Material[];
  uploads: Map<string, FileUploadProgress>;
  isLoading: boolean;
  error: string | null;
  lastUpdated: number | null;
}

function createFilesStore() {
  const { subscribe, set, update } = writable<FilesState>({
    files: [],
    uploads: new Map(),
    isLoading: false,
    error: null,
    lastUpdated: null,
  });

  return {
    subscribe,
    setFiles: (files: Material[]) => {
      update((state) => ({
        ...state,
        files,
        error: null,
        lastUpdated: Date.now(),
      }));
    },
    addFile: (file: Material) => {
      update((state) => {
        // Check if file already exists to prevent duplicates
        const existingFile = state.files.find((f) => f.id === file.id);
        if (existingFile) {
          console.warn(`File with ID ${file.id} already exists, skipping duplicate`);
          return state;
        }

        return {
          ...state,
          files: [file, ...state.files],
        };
      });
    },
    /**
     * Update file
     */
    updateFile: (fileId: string, updates: Partial<Material>) => {
      update((state) => ({
        ...state,
        files: state.files.map((f) =>
          f.id === fileId ? { ...f, ...updates } : f
        ),
      }));
    },
    /**
     * Delete file
     */
    deleteFile: (fileId: string) => {
      update((state) => ({
        ...state,
        files: state.files.filter((f) => f.id !== fileId),
      }));
    },
    addFileVersion: (fileId: string, version: FileVersion) => {
      update((state) => ({
        ...state,
        files: state.files.map((f) =>
          f.id === fileId
            ? { ...f, versions: [...f.versions, version], url: version.url }
            : f
        ),
      }));
    },
    startUpload: (fileId: string, progress: FileUploadProgress) => {
      update((state) => {
        const uploads = new Map(state.uploads);
        uploads.set(fileId, progress);
        return { ...state, uploads };
      });
    },
    updateUploadProgress: (
      fileId: string,
      progress: number,
      uploadedBytes: number
    ) => {
      update((state) => {
        const uploads = new Map(state.uploads);
        const current = uploads.get(fileId);
        if (current) {
          uploads.set(fileId, { ...current, progress, uploadedBytes });
        }
        return { ...state, uploads };
      });
    },
    completeUpload: (fileId: string) => {
      update((state) => {
        const uploads = new Map(state.uploads);
        const current = uploads.get(fileId);
        if (current) {
          uploads.set(fileId, { ...current, status: 'completed', progress: 100 });
        }
        return { ...state, uploads };
      });
    },
    failUpload: (fileId: string, error: string) => {
      update((state) => {
        const uploads = new Map(state.uploads);
        const current = uploads.get(fileId);
        if (current) {
          uploads.set(fileId, { ...current, status: 'failed', error });
        }
        return { ...state, uploads };
      });
    },
    removeUpload: (fileId: string) => {
      update((state) => {
        const uploads = new Map(state.uploads);
        uploads.delete(fileId);
        return { ...state, uploads };
      });
    },
    setLoading: (isLoading: boolean) => {
      update((state) => ({ ...state, isLoading }));
    },
    setError: (error: string | null) => {
      update((state) => ({ ...state, error }));
    },
    clear: () => {
      set({
        files: [],
        uploads: new Map(),
        isLoading: false,
        error: null,
        lastUpdated: null,
      });
    },
  };
}

export const files = createFilesStore();

/**
 * Derived stores
 */
export const fileList = derived(files, ($files) => $files.files);
export const filesLoading = derived(files, ($files) => $files.isLoading);
export const filesError = derived(files, ($files) => $files.error);
export const uploads = derived(files, ($files) => $files.uploads);

/**
 * Get files by type
 */
export const imageFiles = derived(fileList, ($files) =>
  $files.filter((f) => f.fileType === 'image')
);

export const pdfFiles = derived(fileList, ($files) =>
  $files.filter((f) => f.fileType === 'pdf')
);

export const documentFiles = derived(fileList, ($files) =>
  $files.filter((f) => f.fileType === 'document')
);

/**
 * Get files by room
 */
export const getFilesByRoom = (roomId: string) =>
  derived(fileList, ($files) =>
    $files.filter((f) => f.roomId === roomId)
  );

/**
 * Get files by message
 */
export const getFilesByMessage = (messageId: string) =>
  derived(fileList, ($files) =>
    $files.filter((f) => f.messageId === messageId)
  );

/**
 * Get file by ID
 */
export const getFileById = (fileId: string) =>
  derived(fileList, ($files) => $files.find((f) => f.id === fileId));

/**
 * Get upload progress by file ID
 */
export const getUploadProgress = (fileId: string) =>
  derived(uploads, ($uploads) => $uploads.get(fileId));

/**
 * Get all active uploads
 */
export const activeUploads = derived(uploads, ($uploads) =>
  Array.from($uploads.values()).filter((u) => u.status === 'uploading')
);

/**
 * Get upload completion percentage
 */
export const uploadProgress = (fileId: string) =>
  derived(
    getUploadProgress(fileId),
    ($progress) => $progress?.progress ?? 0
  );

/**
 * Helper functions to interact with files store
 */
export function addFile(file: Material) {
  files.addFile(file);
}

/**
 * Update file metadata via API
 * @param fileId File ID to update
 * @param updates Partial file data to update
 * @param tenant Tenant identifier
 */
export async function updateFile(fileId: string, updates: Partial<Material>, tenant: string) {
  try {
    // Call backend API to update file
    // Note: Backend UpdateFileRequest only supports FileName and FileType
    await api.put(`/${tenant}/api/file/${fileId}`, {
      fileName: updates.fileName,
      fileType: updates.mimeType
    });

    // Update local store after successful API call
    files.updateFile(fileId, updates);
  } catch (error) {
    console.error('Failed to update file:', error);
    throw error;
  }
}

/**
 * Delete file via API
 * @param fileId File ID to delete
 * @param tenant Tenant identifier
 */
export async function deleteFile(fileId: string, tenant: string) {
  try {
    // Call backend API to delete file
    await api.delete(`/${tenant}/api/File/${fileId}`);

    // Update local store after successful API call
    files.deleteFile(fileId);
  } catch (error) {
    console.error('Failed to delete file:', error);
    throw error;
  }
}

export function setFiles(filesList: Material[]) {
  files.setFiles(filesList);
}
