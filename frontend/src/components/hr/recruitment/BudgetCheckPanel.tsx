'use client';

import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, Info, Loader2 } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { staffRequisitionService } from '@/services/hr/recruitment.service';

/**
 * How a requisition stands against its position's approved manpower budget.
 *
 * Worth showing before anyone presses Submit, because in **Block** mode the server will simply
 * refuse — and the reason it gives is this same sentence. Surfacing it up front turns a refusal
 * into something the requester could see coming.
 *
 * The server owns the wording: it knows the fiscal year, the budget line, how many are already
 * filled and what the enforcement mode is. This component decides only how loudly to say it.
 */
export function BudgetCheckPanel({ requisitionId }: { requisitionId: string }) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['hr', 'requisition-budget', requisitionId],
    queryFn: () => staffRequisitionService.checkBudget(requisitionId),
    enabled: !!requisitionId,
  });

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" /> Checking the manpower budget…
      </div>
    );
  }

  // A budget check that cannot be computed is not worth an error banner on a form — the
  // submit attempt will say so plainly enough.
  if (isError || !data) return null;

  const blocking = data.wouldBlock;
  const warning = data.isOverBudget && !blocking;

  const Icon = blocking ? AlertTriangle : warning ? AlertTriangle : data.hasBudgetLine ? CheckCircle2 : Info;

  return (
    <Alert variant={blocking ? 'destructive' : 'default'}>
      <Icon className="h-4 w-4" />
      <AlertTitle>
        {blocking
          ? 'Over budget — this cannot be submitted'
          : warning
            ? 'Over budget'
            : data.hasBudgetLine
              ? 'Within budget'
              : 'Not budget-constrained'}
      </AlertTitle>
      <AlertDescription className="space-y-1">
        <p>{data.message}</p>
        {data.hasBudgetLine && (
          <p className="text-xs text-muted-foreground">
            {data.currentFilled ?? 0} filled + {data.requestedPositions} requested ={' '}
            {data.projectedHeadcount} against {data.plannedCount ?? 0} budgeted for {data.fiscalYear}.
            Enforcement is set to {data.mode}.
          </p>
        )}
      </AlertDescription>
    </Alert>
  );
}
