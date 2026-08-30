export type PurchaseOrderStatusKey =
  | 'Draft'
  | 'Submitted'
  | 'Pending Approval'
  | 'Approved'
  | 'Rejected'
  | 'Sent'
  | 'Acknowledged'
  | 'Partially Received'
  | 'Received'
  | 'Cancelled'
  | 'Closed'
  | 'Unknown';

export interface PurchaseOrderStatusPresentation {
  key: PurchaseOrderStatusKey;
  label: string;
  badgeClass: string;
}

const STATUS_PRESENTATIONS: Record<PurchaseOrderStatusKey, PurchaseOrderStatusPresentation> = {
  Draft: {
    key: 'Draft',
    label: 'Draft',
    badgeClass: 'border-slate-300 bg-slate-100 text-slate-900 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100',
  },
  Submitted: {
    key: 'Submitted',
    label: 'Submitted',
    badgeClass: 'border-amber-300 bg-amber-100 text-amber-950 dark:border-amber-700 dark:bg-amber-950 dark:text-amber-100',
  },
  'Pending Approval': {
    key: 'Pending Approval',
    label: 'Pending Approval',
    badgeClass: 'border-amber-300 bg-amber-100 text-amber-950 dark:border-amber-700 dark:bg-amber-950 dark:text-amber-100',
  },
  Approved: {
    key: 'Approved',
    label: 'Approved',
    badgeClass: 'border-emerald-300 bg-emerald-100 text-emerald-950 dark:border-emerald-700 dark:bg-emerald-950 dark:text-emerald-100',
  },
  Rejected: {
    key: 'Rejected',
    label: 'Rejected',
    badgeClass: 'border-red-300 bg-red-100 text-red-950 dark:border-red-700 dark:bg-red-950 dark:text-red-100',
  },
  Sent: {
    key: 'Sent',
    label: 'Sent',
    badgeClass: 'border-blue-300 bg-blue-100 text-blue-950 dark:border-blue-700 dark:bg-blue-950 dark:text-blue-100',
  },
  Acknowledged: {
    key: 'Acknowledged',
    label: 'Acknowledged',
    badgeClass: 'border-indigo-300 bg-indigo-100 text-indigo-950 dark:border-indigo-700 dark:bg-indigo-950 dark:text-indigo-100',
  },
  'Partially Received': {
    key: 'Partially Received',
    label: 'Partially Received',
    badgeClass: 'border-purple-300 bg-purple-100 text-purple-950 dark:border-purple-700 dark:bg-purple-950 dark:text-purple-100',
  },
  Received: {
    key: 'Received',
    label: 'Received',
    badgeClass: 'border-teal-300 bg-teal-100 text-teal-950 dark:border-teal-700 dark:bg-teal-950 dark:text-teal-100',
  },
  Cancelled: {
    key: 'Cancelled',
    label: 'Cancelled',
    badgeClass: 'border-red-300 bg-red-100 text-red-950 dark:border-red-700 dark:bg-red-950 dark:text-red-100',
  },
  Closed: {
    key: 'Closed',
    label: 'Closed',
    badgeClass: 'border-slate-400 bg-slate-200 text-slate-950 dark:border-slate-500 dark:bg-slate-700 dark:text-slate-50',
  },
  Unknown: {
    key: 'Unknown',
    label: 'Unknown',
    badgeClass: 'border-slate-300 bg-slate-100 text-slate-950 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100',
  },
};

const STATUS_KEY_BY_TOKEN: Record<string, PurchaseOrderStatusKey> = {
  draft: 'Draft',
  submitted: 'Submitted',
  pendingapproval: 'Pending Approval',
  approved: 'Approved',
  rejected: 'Rejected',
  sent: 'Sent',
  acknowledged: 'Acknowledged',
  partiallyreceived: 'Partially Received',
  received: 'Received',
  cancelled: 'Cancelled',
  canceled: 'Cancelled',
  closed: 'Closed',
};

export const PURCHASE_ORDER_STATUS_OPTIONS = [
  STATUS_PRESENTATIONS.Draft,
  STATUS_PRESENTATIONS['Pending Approval'],
  STATUS_PRESENTATIONS.Approved,
  STATUS_PRESENTATIONS.Sent,
  STATUS_PRESENTATIONS.Acknowledged,
  STATUS_PRESENTATIONS['Partially Received'],
  STATUS_PRESENTATIONS.Received,
  STATUS_PRESENTATIONS.Cancelled,
] as const;

const statusToken = (status: string | null | undefined) =>
  (status ?? '').trim().toLowerCase().replace(/[\s_-]+/g, '');

const readableFallback = (status: string) =>
  status
    .trim()
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/\s+/g, ' ');

export function getPurchaseOrderStatusPresentation(
  status: string | null | undefined
): PurchaseOrderStatusPresentation {
  const rawStatus = status?.trim() ?? '';
  const key = STATUS_KEY_BY_TOKEN[statusToken(rawStatus)] ?? 'Unknown';
  const presentation = STATUS_PRESENTATIONS[key];

  if (key !== 'Unknown' || rawStatus.length === 0) {
    return presentation;
  }

  return {
    ...presentation,
    label: readableFallback(rawStatus),
  };
}

export function isPurchaseOrderStatus(
  status: string | null | undefined,
  ...expected: PurchaseOrderStatusKey[]
) {
  return expected.includes(getPurchaseOrderStatusPresentation(status).key);
}
