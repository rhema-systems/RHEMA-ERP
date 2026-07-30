import type {
  FrameworkAgreement,
  FrameworkAgreementStatus,
  FrameworkExtensionStatus,
} from '@/types/procurement-framework-agreement';

export const frameworkStatusTone = (
  status: FrameworkAgreementStatus
): 'default' | 'secondary' | 'destructive' | 'outline' => {
  if (status === 'Published') return 'default';
  if (status === 'PendingApproval') return 'secondary';
  if (status === 'Rejected' || status === 'Expired' || status === 'Terminated')
    return 'destructive';
  return 'outline';
};

export const frameworkExtensionTone = (
  status: FrameworkExtensionStatus
): 'default' | 'secondary' | 'destructive' | 'outline' => {
  if (status === 'Approved') return 'default';
  if (status === 'Rejected') return 'destructive';
  return 'secondary';
};

export const frameworkActionState = (agreement?: FrameworkAgreement) => {
  const allowed = new Set(agreement?.allowedActions ?? []);
  return {
    canEdit: allowed.has('edit'),
    canAddDocument: allowed.has('add-document'),
    canRetireDocument: allowed.has('retire-document'),
    canSubmit: allowed.has('submit'),
    canApprove: allowed.has('approve'),
    canReject: allowed.has('reject'),
    canClone: allowed.has('clone'),
    canRequestExtension: allowed.has('request-extension'),
    canTerminate: allowed.has('terminate'),
  };
};

export const frameworkRemainingCeilingNote = (agreement: FrameworkAgreement) =>
  agreement.availableCeiling === agreement.ceilingAmount
    ? 'No call-off deductions are applied in TDC-0401.'
    : 'Available ceiling reflects downstream call-off deductions.';

export const frameworkUtcInstantToLocalInput = (
  value?: string,
  offsetMinutes?: number
) => {
  if (!value) return '';
  const instant = new Date(value);
  if (Number.isNaN(instant.getTime())) return '';
  const offset = offsetMinutes ?? instant.getTimezoneOffset();
  return new Date(instant.getTime() - offset * 60_000)
    .toISOString()
    .slice(0, 16);
};

export const frameworkLocalInputToUtcInstant = (
  value: string,
  offsetMinutes?: number
) => {
  if (offsetMinutes === undefined) return new Date(value).toISOString();
  const localAsUtc = new Date(`${value}:00.000Z`);
  return new Date(localAsUtc.getTime() + offsetMinutes * 60_000).toISOString();
};
