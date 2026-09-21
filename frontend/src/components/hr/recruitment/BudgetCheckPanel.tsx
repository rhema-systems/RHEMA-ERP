'use client';

import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, Info, Loader2, ShieldAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { staffRequisitionService } from '@/services/hr/recruitment.service';
import type { RequisitionBudgetCheck, RequisitionBudgetCheckPreview } from '@/types/hr/recruitment';

/**
 * How a requisition stands against its position's approved manpower budget AND its establishment.
 *
 * Worth showing before anyone presses Submit, because in **Block** mode the server will simply
 * refuse — and the reason it gives is this same sentence. Surfacing it up front turns a refusal
 * into something the requester could see coming.
 *
 * Round 2b, R5: two blocks, not one. The establishment check (FR-HR-136, default Block) used to
 * be thrown at submit and never shown; it is now the second half of the same read. The panel also
 * says when an exception justification will be required (D-4), and it renders at every status —
 * an approver a month later wants the same figures the requester saw, not a blank.
 *
 * Three ways to feed it: a `requisitionId` (the record's check), a `preview` (the form's
 * not-yet-saved figures — the server computes the same thing), or `data` already fetched by the
 * host page.
 */
export function BudgetCheckPanel({
  requisitionId,
  preview,
  data: given,
}: {
  requisitionId?: string;
  preview?: RequisitionBudgetCheckPreview | null;
  data?: RequisitionBudgetCheck;
}) {
  const byId = useQuery({
    queryKey: ['hr', 'requisition-budget', requisitionId],
    queryFn: () => staffRequisitionService.checkBudget(requisitionId!),
    enabled: !!requisitionId && !given,
  });
  const byPreview = useQuery({
    queryKey: ['hr', 'requisition-budget-preview', preview?.positionId, preview?.numberOfPositions, preview?.desiredStartDate, preview?.manpowerBudgetLineId, preview?.excludeRequisitionId],
    queryFn: () => staffRequisitionService.previewBudgetCheck(preview!),
    enabled: !!preview?.positionId && !requisitionId && !given,
    staleTime: 15 * 1000,
  });

  const data = given ?? byId.data ?? byPreview.data;
  const isLoading = !given && (byId.isLoading || byPreview.isLoading);

  if (!requisitionId && !preview?.positionId && !given) return null;

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" /> Checking the manpower budget and the establishment…
      </div>
    );
  }

  // A budget check that cannot be computed is not worth an error banner on a form — the
  // submit attempt will say so plainly enough.
  if (!data) return null;

  const blocking = data.wouldBlock;
  const warning = data.isOverBudget && !blocking;
  const Icon = blocking ? AlertTriangle : warning ? AlertTriangle : data.isLinked ? CheckCircle2 : Info;
  const est = data.establishment;

  return (
    <div className="space-y-2">
      <Alert variant={blocking ? 'destructive' : 'default'}>
        <Icon className="h-4 w-4" />
        <AlertTitle>
          {blocking
            ? data.isLinked ? 'Over budget — this cannot be submitted' : 'Not raised against an approved budget — this cannot be submitted'
            : warning
              ? 'Over budget'
              : data.isLinked
                ? `Budgeted · ${data.linkedBudgetNumber}`
                : data.hasBudgetLine
                  ? 'A budget covers this post, but the requisition is not raised against it'
                  : 'Not from an approved budget'}
        </AlertTitle>
        <AlertDescription className="space-y-1">
          <p>{data.message}</p>
          {data.hasBudgetLine && (
            <p className="text-xs text-muted-foreground">
              {data.budgetedNewPosts ?? 0} new post{(data.budgetedNewPosts ?? 0) === 1 ? '' : 's'} budgeted for {data.fiscalYear}
              {data.linkedBudgetNumber ? ` on ${data.linkedBudgetNumber}` : ''} · {data.drawdown} already requested by other requisitions ·{' '}
              {data.remaining ?? 0} left before this one · this one asks for {data.requestedPositions}. Enforcement is set to {data.mode}.
            </p>
          )}
          {data.linkedBudgetStatus && !data.isLinked && data.linkedBudgetNumber && data.linkedBudgetStatus !== 'Approved' && data.linkedBudgetStatus !== 'Active' && (
            <p className="text-xs text-amber-700">Budget {data.linkedBudgetNumber} is {data.linkedBudgetStatus}, so it no longer authorises anything.</p>
          )}
          {data.exceptionRequired && (
            <p className="text-xs">
              <span className="font-medium">An exception justification is required to submit:</span> {data.exceptionReason}
            </p>
          )}
        </AlertDescription>
      </Alert>

      {est && (
        <Alert variant={est.wouldBlock ? 'destructive' : 'default'}>
          {est.wouldBlock ? <ShieldAlert className="h-4 w-4" /> : est.wouldExceed ? <AlertTriangle className="h-4 w-4" /> : <Info className="h-4 w-4" />}
          <AlertTitle>
            {est.wouldBlock
              ? 'Outside the approved establishment — this cannot be submitted'
              : est.wouldExceed
                ? 'Outside the approved establishment'
                : est.isEstablished
                  ? `Established for ${est.expectedHeadcount} · ${est.filled} in post · gap ${est.gap}`
                  : 'Post not established'}
          </AlertTitle>
          <AlertDescription className="space-y-1">
            <p>{est.message}</p>
            {est.sourceBudgetNumber && (
              <p className="text-xs text-muted-foreground">Establishment set by budget {est.sourceBudgetNumber}. Enforcement is set to {est.mode}.</p>
            )}
          </AlertDescription>
        </Alert>
      )}
    </div>
  );
}
