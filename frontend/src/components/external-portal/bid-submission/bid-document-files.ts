import type { TenderDocumentRequirement } from '@/services/tenderService';

export const bidDocumentAccept = '.pdf,.doc,.docx,.jpg,.jpeg,.png';

export function validateBidDocumentFile(file: File, requirement?: TenderDocumentRequirement): string | null {
  const maxBytes = Math.min((requirement?.maxFileSizeMB || 20) * 1024 * 1024, 20_000_000);
  if (file.size > maxBytes) return `File size must not exceed ${Math.round(maxBytes / 1_000_000)} MB.`;
  const allowed = (requirement?.allowedFileTypes || 'pdf,doc,docx,jpg,jpeg,png')
    .split(',').map(value => value.trim().toLowerCase().replace(/^\./, ''));
  const extension = file.name.split('.').pop()?.toLowerCase() ?? '';
  if (!allowed.includes(extension)) return `Allowed file types: ${allowed.join(', ')}.`;
  return null;
}
