/**
 * HR document handling.
 *
 * Since the "controlled upload gate" change, every HR document upload goes through a
 * scanning gate and is registered in the central DMS. Attachment DTOs across HR now carry
 * three nullable identifiers instead of a bare path.
 */

/**
 * Identifiers a gated HR attachment carries. `filePath` is only populated for rows that
 * predate the migration — it is NOT a URL and must never be put in an `<img src>` or
 * `<a href>`. Always stream through the resource's authorized download endpoint.
 */
export interface HrDocumentRef {
  fileUploadRecordId?: string | null;
  documentRecordId?: string | null;
  documentVersionId?: string | null;
  /** Legacy path, pre-migration rows only. Not a URL. */
  filePath?: string | null;
  originalFileName?: string | null;
  contentType?: string | null;
  fileSize?: number | null;
}

/**
 * Upload rejections come back as HTTP 422 with `{ code, message }` — the code identifies
 * the gate rule that fired (file type, size, virus scan pending/failed, and so on).
 */
export interface HrDocumentUploadError {
  code: string;
  message: string;
  status: number;
}

/**
 * Friendlier wording for the codes the gate returns. Unknown codes fall back to the
 * server's own message, so a new backend rule still surfaces something useful.
 */
export const HR_UPLOAD_ERROR_MESSAGES: Record<string, string> = {
  FileTypeNotAllowed: 'That file type is not allowed. Upload a PDF, image, or document file.',
  FileTooLarge: 'That file is too large. The limit for HR documents is 10 MB.',
  EmptyFile: 'That file is empty.',
  VirusScanPending: 'The file is still being scanned. Try again in a moment.',
  VirusScanFailed: 'The file failed the security scan and was rejected.',
  CategoryNotConfigured: 'Uploads are not configured for this document type. Contact an administrator.',
  StorageUnavailable: 'Document storage is unavailable right now. Try again shortly.',
};

export function describeHrUploadError(error: HrDocumentUploadError): string {
  return HR_UPLOAD_ERROR_MESSAGES[error.code] ?? error.message;
}
