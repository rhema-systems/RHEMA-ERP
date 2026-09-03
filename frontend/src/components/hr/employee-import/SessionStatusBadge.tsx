import { Badge } from '@/components/ui/badge';
import {
  ROW_OUTCOME_LABEL,
  SESSION_STATUS_LABEL,
  type EmployeeImportRowOutcome,
  type EmployeeImportSessionStatus,
} from '@/types/hr/employee-import';

const SESSION_VARIANT: Record<EmployeeImportSessionStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Validated: 'secondary',
  CommitRequested: 'secondary',
  Committing: 'secondary',
  Committed: 'default',
  CommittedWithErrors: 'destructive',
  Cancelled: 'outline',
  Failed: 'destructive',
};

export function SessionStatusBadge({ status }: { status: EmployeeImportSessionStatus }) {
  return <Badge variant={SESSION_VARIANT[status] ?? 'outline'}>{SESSION_STATUS_LABEL[status] ?? status}</Badge>;
}

const OUTCOME_CLASS: Record<EmployeeImportRowOutcome, string> = {
  Ready: 'bg-green-100 text-green-800 border-green-200',
  Warning: 'bg-amber-100 text-amber-800 border-amber-200',
  Error: 'bg-red-100 text-red-800 border-red-200',
  Committed: 'bg-green-100 text-green-800 border-green-200',
  CommittedWithIssues: 'bg-amber-100 text-amber-800 border-amber-200',
  Failed: 'bg-red-100 text-red-800 border-red-200',
  Skipped: 'bg-muted text-muted-foreground',
};

export function RowOutcomeBadge({ outcome, skip }: { outcome: EmployeeImportRowOutcome; skip?: boolean }) {
  const key: EmployeeImportRowOutcome = skip && (outcome === 'Ready' || outcome === 'Warning' || outcome === 'Error') ? 'Skipped' : outcome;
  return (
    <Badge variant="outline" className={OUTCOME_CLASS[key] ?? ''}>
      {ROW_OUTCOME_LABEL[key] ?? key}
    </Badge>
  );
}
