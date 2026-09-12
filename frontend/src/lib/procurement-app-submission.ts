import type {
  ProcurementAppSubmission,
  ProcurementAppSubmissionSearch,
  ProcurementAppSubmissionStatus,
  RecordProcurementAppExport,
} from '@/types/procurement-app-submission';

export const procurementAppSubmissionActions = (
  item: Pick<ProcurementAppSubmission, 'status'>
) => ({
  canSubmit: item.status === 'Exported',
  canAcknowledge: item.status === 'Submitted',
  canReject: item.status === 'Submitted',
  canResubmit: item.status === 'Rejected',
  isTerminal: item.status === 'Acknowledged',
});

export const compactProcurementAppSubmissionSearch = (
  search: ProcurementAppSubmissionSearch
) =>
  Object.fromEntries(
    Object.entries(search).filter(
      ([, value]) => value !== undefined && value !== null && value !== ''
    )
  ) as ProcurementAppSubmissionSearch;

export const validateProcurementAppExport = (
  value: RecordProcurementAppExport
) => {
  if (!value.procurementPlanId) return 'Select a published plan version.';
  if (!['CSV', 'JSON', 'XML'].includes(value.exportFormat.toUpperCase()))
    return 'Select CSV, JSON or XML.';
  return undefined;
};

export const toProcurementAppEventInputValue = (
  minimumAtUtc?: string,
  now = new Date()
) => {
  const minimum = minimumAtUtc ? new Date(minimumAtUtc).getTime() : 0;
  const minimumMilliseconds = Number.isFinite(minimum) ? minimum : 0;
  const safeInstant = new Date(
    Math.ceil((Math.max(now.getTime(), minimumMilliseconds) + 1) / 1000) *
      1000
  );
  const localValue = new Date(
    safeInstant.getTime() - safeInstant.getTimezoneOffset() * 60_000
  );
  return localValue.toISOString().slice(0, 19);
};

export async function readProcurementAppExportFile(file: Blob & { name: string }) {
  const bytes = typeof file.arrayBuffer === 'function'
    ? await file.arrayBuffer()
    : await new Promise<ArrayBuffer>((resolve, reject) => {
        const reader = new FileReader();
        reader.onerror = () => reject(reader.error ?? new Error('Unable to read export package.'));
        reader.onload = () => {
          if (reader.result instanceof ArrayBuffer) resolve(reader.result);
          else reject(new Error('Unable to read export package.'));
        };
        reader.readAsArrayBuffer(file);
      });
  const digest = await globalThis.crypto.subtle.digest(
    'SHA-256',
    new Uint8Array(bytes)
  );
  const exportChecksumSha256 = Array.from(new Uint8Array(digest), (value) =>
    value.toString(16).padStart(2, '0')
  ).join('');
  const extension = file.name.split('.').pop()?.trim().toUpperCase();

  return {
    exportFileName: file.name,
    exportFormat: extension || 'FILE',
    exportChecksumSha256,
  };
}

export const procurementAppSubmissionStatusTone = (
  status: ProcurementAppSubmissionStatus
) => {
  if (status === 'Acknowledged') return 'border-emerald-300 text-emerald-700';
  if (status === 'Rejected') return 'border-red-300 text-red-700';
  if (status === 'Submitted') return 'border-blue-300 text-blue-700';
  return 'border-amber-300 text-amber-700';
};
