import { CalendarCheck } from 'lucide-react';
import { Badge } from '@/components/ui/badge';

/**
 * "Matches the approved plan" — round 5, decision B4.
 *
 * The stakeholders' rule: annual leave that was scheduled is then applied for "as of right". The
 * request still goes through both approvals — a right to leave is not leave without approval — but
 * an approver should see at a glance that these exact dates were agreed in advance. The server sets
 * the flag only when the plan is Approved and the request asks for exactly its dates.
 */
export function MatchesApprovedPlanBadge({ show }: { show?: boolean }) {
  if (!show) return null;
  return (
    <Badge
      variant="outline"
      className="gap-1 border-emerald-300 text-emerald-700 dark:border-emerald-500/50 dark:text-emerald-400"
      title="Raised from an approved leave plan, for exactly the plan's dates — they were agreed in advance."
    >
      <CalendarCheck className="h-3 w-3" />
      Matches the approved plan
    </Badge>
  );
}
