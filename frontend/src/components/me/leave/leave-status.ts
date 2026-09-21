import type {
  LeaveEncashmentStatus,
  LeavePlanStatus,
  LeaveStatus,
} from '@/types/hr/leave-request';

/** Badge classes per status — shared by the portal's leave screens. */
export const LEAVE_STATUS_BADGE: Record<LeaveStatus, string> = {
  Draft: 'bg-muted text-muted-foreground',
  Pending: 'bg-amber-100 text-amber-900 dark:bg-amber-950/60 dark:text-amber-200',
  Approved: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950/60 dark:text-emerald-200',
  Rejected: 'bg-red-100 text-red-900 dark:bg-red-950/60 dark:text-red-200',
  Cancelled: 'bg-muted text-muted-foreground line-through',
  InProgress: 'bg-blue-100 text-blue-900 dark:bg-blue-950/60 dark:text-blue-200',
  Completed: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950/60 dark:text-emerald-200',
  // Same blue as the plan's own ChangesSuggested — it means the same thing on both records:
  // the approver sent it back with dates of their own and it is the employee's move.
  ChangesSuggested: 'bg-blue-100 text-blue-900 dark:bg-blue-950/60 dark:text-blue-200',
};

export const LEAVE_PLAN_STATUS_BADGE: Record<LeavePlanStatus, string> = {
  Draft: 'bg-muted text-muted-foreground',
  Submitted: 'bg-amber-100 text-amber-900 dark:bg-amber-950/60 dark:text-amber-200',
  Approved: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950/60 dark:text-emerald-200',
  Rejected: 'bg-red-100 text-red-900 dark:bg-red-950/60 dark:text-red-200',
  Cancelled: 'bg-muted text-muted-foreground line-through',
  ChangesSuggested: 'bg-blue-100 text-blue-900 dark:bg-blue-950/60 dark:text-blue-200',
};

export const ENCASHMENT_STATUS_BADGE: Record<LeaveEncashmentStatus, string> = {
  Draft: 'bg-muted text-muted-foreground',
  Submitted: 'bg-amber-100 text-amber-900 dark:bg-amber-950/60 dark:text-amber-200',
  PendingApproval: 'bg-amber-100 text-amber-900 dark:bg-amber-950/60 dark:text-amber-200',
  Approved: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950/60 dark:text-emerald-200',
  Rejected: 'bg-red-100 text-red-900 dark:bg-red-950/60 dark:text-red-200',
  Processed: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950/60 dark:text-emerald-200',
  Cancelled: 'bg-muted text-muted-foreground line-through',
};
