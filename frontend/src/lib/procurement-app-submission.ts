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
  if (!value.exportFileName.trim()) return 'Export file name is required.';
  if (!value.exportFormat.trim()) return 'Export format is required.';
  if (!value.exportTemplateVersion.trim())
    return 'Export template version is required.';
  if (!/^[0-9a-f]{64}$/i.test(value.exportChecksumSha256.trim()))
    return 'SHA-256 checksum must contain exactly 64 hexadecimal characters.';
  return undefined;
};

export const procurementAppSubmissionStatusTone = (
  status: ProcurementAppSubmissionStatus
) => {
  if (status === 'Acknowledged') return 'border-emerald-300 text-emerald-700';
  if (status === 'Rejected') return 'border-red-300 text-red-700';
  if (status === 'Submitted') return 'border-blue-300 text-blue-700';
  return 'border-amber-300 text-amber-700';
};
