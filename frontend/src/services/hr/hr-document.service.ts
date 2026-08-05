import { apiService } from '../api.service';
import type { HrDocumentUploadError } from '@/types/hr/document';

/**
 * Shared client for HR documents.
 *
 * Every HR upload goes through the backend's controlled upload gate (scanning + central
 * DMS registration) rather than a plain file POST, and every download must be streamed
 * from an authorized endpoint with the bearer token attached. Stored paths are not URLs.
 *
 * Each HR area keeps its own upload/download routes — this service supplies the transport
 * and the error contract so those areas do not each re-implement it.
 */
class HrDocumentService {
  /**
   * Uploads a file to a gated HR endpoint (always multipart).
   *
   * @param endpoint  the area's upload route, e.g. `/Leaves/{id}/attachments`
   * @param file      the file to upload
   * @param fields    extra form fields the endpoint expects (e.g. `description`, `examId`)
   * @param fileField the form field name; a few endpoints deviate from `file`
   */
  async upload<T>(
    endpoint: string,
    file: File,
    fields: Record<string, string | number | boolean | undefined | null> = {},
    fileField = 'file',
  ): Promise<T> {
    const formData = new FormData();
    formData.append(fileField, file);

    for (const [key, value] of Object.entries(fields)) {
      if (value !== undefined && value !== null) {
        formData.append(key, String(value));
      }
    }

    try {
      return await apiService.post<T>(endpoint, formData);
    } catch (error) {
      throw toUploadError(error);
    }
  }

  /**
   * Fetches a document through its authorized download endpoint and returns an object URL.
   *
   * The caller owns the returned URL and must call `URL.revokeObjectURL` when done —
   * `withObjectUrl` below does that automatically for one-shot uses.
   */
  async fetchObjectUrl(endpoint: string): Promise<string> {
    const blob = await apiService.downloadBlob(endpoint);
    return URL.createObjectURL(blob);
  }

  /** Triggers a browser download, cleaning up the object URL afterwards. */
  async download(endpoint: string, fileName: string): Promise<void> {
    const url = await this.fetchObjectUrl(endpoint);
    try {
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
    } finally {
      URL.revokeObjectURL(url);
    }
  }

  /** Opens a document in a new tab (inline preview for PDFs and images). */
  async openInNewTab(endpoint: string): Promise<void> {
    const url = await this.fetchObjectUrl(endpoint);
    const opened = window.open(url, '_blank', 'noopener');
    // Revoke once the tab has had a chance to load; revoking immediately breaks it.
    setTimeout(() => URL.revokeObjectURL(url), opened ? 60_000 : 0);
  }
}

/**
 * Normalizes a failed upload into the gate's `{ code, message }` contract. The gate
 * answers 422 for a rejected file; 400/404 and network failures are mapped to a stable
 * shape too so callers only handle one error type.
 */
function toUploadError(error: any): HrDocumentUploadError {
  const status: number = error?.status ?? error?.response?.status ?? 0;
  const body = error?.data ?? error?.response?.data ?? error?.body;

  const code =
    body?.code ??
    (status === 413 ? 'FileTooLarge' : undefined) ??
    (status === 404 ? 'CategoryNotConfigured' : undefined) ??
    'UploadFailed';

  const message =
    body?.message ??
    error?.message ??
    'The file could not be uploaded. Please try again.';

  const uploadError: HrDocumentUploadError = { code, message, status };
  return uploadError;
}

export const hrDocumentService = new HrDocumentService();
