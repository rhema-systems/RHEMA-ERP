'use client';

import { Badge, type BadgeProps } from '@/components/ui/badge';

type BadgeVariant = BadgeProps['variant'];

// Maps common HR status strings to Badge variants. Extend as more areas are built.
// Keys are the status text with spaces, underscores and hyphens stripped, lower-cased.
const STATUS_VARIANTS: Record<string, BadgeVariant> = {
  active: 'default',
  inactive: 'secondary',
  pending: 'secondary',
  submitted: 'secondary',
  approved: 'default',
  rejected: 'destructive',
  cancelled: 'destructive',
  terminated: 'destructive',
  onhold: 'destructive',
  draft: 'outline',

  // Attendance & Time. "Absent" and "Late" are the exceptions worth spotting in a list,
  // so they carry the destructive/secondary weight rather than reading as neutral.
  present: 'default',
  absent: 'destructive',
  late: 'secondary',
  halfday: 'secondary',
  onleave: 'secondary',
  publicholiday: 'outline',
  weekend: 'outline',
  offday: 'outline',
  remotework: 'secondary',
  onduty: 'default',
  applied: 'default',
  completed: 'default',
  open: 'default',
  pendingclose: 'secondary',
  closed: 'secondary',
  exportedtopayroll: 'default',
  processing: 'secondary',
  inprogress: 'secondary',
  failed: 'destructive',
  partialsuccess: 'secondary',
  acknowledged: 'secondary',
  resolved: 'default',
  dismissed: 'outline',
  critical: 'destructive',
  warning: 'secondary',
  info: 'outline',

  // Consultant timesheets and invoicing.
  senttoclient: 'secondary',
  clientconfirmed: 'default',
  clientrejected: 'destructive',
  billed: 'default',
  void: 'outline',
  voided: 'outline',
  sent: 'secondary',
  viewed: 'secondary',
  confirmed: 'default',
  partiallypaid: 'secondary',
  paid: 'default',
  overdue: 'destructive',
  expired: 'destructive',
  resent: 'secondary',
  suspended: 'secondary',
};

interface StatusBadgeProps {
  /** A status string (case-insensitive) mapped to a Badge variant. */
  status?: string;
  /** Convenience for boolean active/inactive flags; ignored when `status` is set. */
  active?: boolean;
  className?: string;
}

/**
 * Renders an HR status as a colored Badge. Pass either a `status` string or an
 * `active` boolean. Unknown statuses fall back to the `outline` variant.
 */
export function StatusBadge({ status, active, className }: StatusBadgeProps) {
  const label = status ?? (active ? 'Active' : 'Inactive');
  const key = label.toLowerCase().replace(/[\s_-]/g, '');
  const variant = STATUS_VARIANTS[key] ?? 'outline';
  return (
    <Badge variant={variant} className={className}>
      {label}
    </Badge>
  );
}
