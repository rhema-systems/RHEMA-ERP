const baseBadgeClassName = 'border font-semibold';

const toneClassNames = {
  neutral:
    'border-slate-200 bg-slate-50 text-slate-700 dark:border-slate-800 dark:bg-slate-950/40 dark:text-slate-300',
  info: 'border-sky-200 bg-sky-50 text-sky-700 dark:border-sky-900/60 dark:bg-sky-950/40 dark:text-sky-300',
  progress:
    'border-indigo-200 bg-indigo-50 text-indigo-700 dark:border-indigo-900/60 dark:bg-indigo-950/40 dark:text-indigo-300',
  warning:
    'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900/60 dark:bg-amber-950/40 dark:text-amber-300',
  success:
    'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/60 dark:bg-emerald-950/40 dark:text-emerald-300',
  danger:
    'border-red-200 bg-red-50 text-red-700 dark:border-red-900/60 dark:bg-red-950/40 dark:text-red-300',
  special:
    'border-violet-200 bg-violet-50 text-violet-700 dark:border-violet-900/60 dark:bg-violet-950/40 dark:text-violet-300',
};

const hasAny = (value: string, tokens: string[]) =>
  tokens.some((token) => value.includes(token));

export function getStatusBadgeClassName(status?: string | null) {
  const normalized = (status || '').trim().toLowerCase();

  if (!normalized) {
    return `${baseBadgeClassName} ${toneClassNames.neutral}`;
  }

  if (
    hasAny(normalized, [
      'reject',
      'cancel',
      'fail',
      'overdue',
      'blocked',
      'missing',
      'returned',
      'unavailable',
      'not linked',
      'not generated',
    ])
  ) {
    return `${baseBadgeClassName} ${toneClassNames.danger}`;
  }

  if (
    hasAny(normalized, [
      'complete',
      'closed',
      'done',
      'paid',
      'approved',
      'current',
      'active',
      'available',
      'signed',
      'validated',
      'published',
      'linked',
      'ready',
      'settled',
    ])
  ) {
    return `${baseBadgeClassName} ${toneClassNames.success}`;
  }

  if (
    hasAny(normalized, [
      'legal hold',
      'reserved',
      'leased',
      'occupied',
      'lease',
      'rent',
      'sale',
      'purchase',
    ])
  ) {
    return `${baseBadgeClassName} ${toneClassNames.special}`;
  }

  if (
    hasAny(normalized, [
      'pending',
      'draft',
      'submitted',
      'requested',
      'invoice',
      'not started',
      'review',
      'open',
      'progress',
      'processing',
      'manual',
    ])
  ) {
    return `${baseBadgeClassName} ${toneClassNames.warning}`;
  }

  if (
    hasAny(normalized, ['archived', 'superseded', 'read only', 'not required'])
  ) {
    return `${baseBadgeClassName} ${toneClassNames.neutral}`;
  }

  return `${baseBadgeClassName} ${toneClassNames.info}`;
}
