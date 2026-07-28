'use client';

import { Badge, type BadgeProps } from '@/components/ui/badge';

type BadgeVariant = BadgeProps['variant'];

// Maps common HR status strings to Badge variants. Extend as more areas are built.
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
