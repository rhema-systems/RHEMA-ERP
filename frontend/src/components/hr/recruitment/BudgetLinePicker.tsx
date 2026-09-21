'use client';

import { useQuery } from '@tanstack/react-query';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';

const NONE = '__none__';

/**
 * The approved manpower budget lines a requisition for this position may draw down from
 * (round 2b, R5). Replaces the self-declared "already budgeted" checkbox and the free-text
 * budget code: budgeted is a fact about a link, and the code is the budget's number.
 *
 * Each option says what the line has left: `MPB-2026-0003 · 2026 · 5 new posts · 2 left`. A
 * line with nothing left is still offered (the server decides whether over-budget is a warning
 * or a refusal), marked as such.
 */
export function BudgetLinePicker({
  positionId,
  value,
  onChange,
  disabled,
}: {
  positionId: string;
  value: string;
  onChange: (lineId: string) => void;
  disabled?: boolean;
}) {
  const { data: lines, isLoading } = useQuery({
    queryKey: ['hr', 'budget-lines-for-position', positionId],
    queryFn: () => jobArchitectureService.getLinesForPosition(positionId),
    enabled: !!positionId,
    staleTime: 60 * 1000,
  });

  const none = !positionId ? 'Choose the position first' : isLoading ? 'Looking for approved budgets…' : (lines?.length ?? 0) === 0 ? 'No approved budget covers this post' : 'Not from an approved budget';

  return (
    <div className="space-y-1.5">
      <Label htmlFor="budgetLine">Approved manpower budget</Label>
      <Select
        value={value || NONE}
        onValueChange={(v) => onChange(v === NONE ? '' : v)}
        disabled={disabled || !positionId}
      >
        <SelectTrigger id="budgetLine">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={NONE}>{none}</SelectItem>
          {(lines ?? []).map((l) => (
            <SelectItem key={l.lineId} value={l.lineId}>
              {l.budgetNumber} · {l.fiscalYear}
              {l.organizationUnitName ? ` · ${l.organizationUnitName}` : ''}
              <span className="ml-2 text-xs text-muted-foreground">
                {l.plannedNewPositions} new post{l.plannedNewPositions === 1 ? '' : 's'} · {l.remaining > 0 ? `${l.remaining} left` : 'nothing left'}
              </span>
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <p className="text-xs text-muted-foreground">
        Raising it against a budget line makes it a budgeted requisition and draws the posts down
        from that line. Without one, say why below.
      </p>
    </div>
  );
}
