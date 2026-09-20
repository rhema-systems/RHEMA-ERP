import React from 'react';
import { ProcurementControlAccordion } from '@/components/procurement/ProcurementControlAccordion';
import { format } from 'date-fns';

import { Badge } from '@/components/ui/badge';
import { CardContent } from '@/components/ui/card';
import { formatProcurementMoney } from '@/lib/procurement-currency';
import type { PurchaseOrderBudgetCommitmentDto } from '@/services/purchasingService';

export interface PurchaseOrderBudgetCommitmentProps {
  commitment: PurchaseOrderBudgetCommitmentDto;
}

export function PurchaseOrderBudgetCommitment({
  commitment,
}: PurchaseOrderBudgetCommitmentProps) {
  const isContractAllocation = commitment.status === 'Allocated';
  const history = [...commitment.history].sort(
    (left, right) => left.sequence - right.sequence
  );

  return (
    <ProcurementControlAccordion
        title="Finance commitment"
        summary={`${commitment.reference} · ${formatProcurementMoney(commitment.amount, commitment.currency)}`}
        status={<Badge variant="outline">{isContractAllocation ? 'PO allocated to contract' : commitment.status === 'Committed' ? 'PO formally committed' : commitment.status}</Badge>}
      >
        <p className="mb-4 text-sm text-muted-foreground">
              {isContractAllocation
                ? 'Final PO approval allocated this order within the existing contract commitment.'
                : 'Final PO approval reserved and committed the approved budget atomically.'}
            </p>
      <CardContent className="space-y-5">
        <div className="grid gap-3 rounded-lg border border-emerald-200 bg-white/70 p-4 text-sm md:grid-cols-2 xl:grid-cols-6">
          <div>
            <span className="block text-muted-foreground">Reference</span>
            <span className="font-mono font-medium">{commitment.reference}</span>
          </div>
          <div>
            <span className="block text-muted-foreground">PO commitment status</span>
            <span className="font-medium">{commitment.status}</span>
          </div>
          <div>
            <span className="block text-muted-foreground">Reservation envelope</span>
            <span className="font-medium">
              {commitment.reservationStatus} ·{' '}
              {formatProcurementMoney(
                commitment.reservedAmount,
                commitment.currency
              )}
            </span>
          </div>
          <div>
            <span className="block text-muted-foreground">
              {isContractAllocation ? 'PO allocated exposure' : 'PO formally committed'}
            </span>
            <span className="font-medium">
              {formatProcurementMoney(commitment.amount, commitment.currency)}
            </span>
          </div>
          <div>
            <span className="block text-muted-foreground">Cumulative formal amount</span>
            <span className="font-medium">
              {formatProcurementMoney(
                commitment.formallyCommittedAmount,
                commitment.currency
              )}
            </span>
          </div>
          <div>
            <span className="block text-muted-foreground">Reservation sequence</span>
            <span className="font-medium">#{commitment.reservationSequence}</span>
          </div>
        </div>

        <div>
          <h3 className="text-sm font-semibold">Commitment evidence history</h3>
          {history.length === 0 ? (
            <p className="mt-2 text-sm text-muted-foreground">
              No commitment evidence events have been recorded.
            </p>
          ) : (
            <ol aria-label="Commitment evidence history" className="mt-3 space-y-3">
              {history.map((event) => (
                <li
                  key={`${event.sequence}-${event.status}`}
                  className="grid gap-2 rounded-lg border bg-background/80 p-3 text-sm md:grid-cols-[auto_1fr_auto] md:items-center"
                >
                  <Badge variant="outline">#{event.sequence}</Badge>
                  <div>
                    <div className="flex flex-wrap items-center gap-2 font-medium">
                      {event.action}
                      <Badge variant="secondary">{event.status}</Badge>
                    </div>
                    <p className="text-xs text-muted-foreground">
                      {event.event} · {commitment.reference} · {event.actorName} ·{' '}
                      {format(
                        new Date(event.occurredAtUtc),
                        'MMM dd, yyyy HH:mm'
                      )}
                    </p>
                  </div>
                  <span className="font-medium">
                    {formatProcurementMoney(event.amount, commitment.currency)}
                  </span>
                  <span className="sr-only">
                    Correlation {event.correlationId}
                  </span>
                </li>
              ))}
            </ol>
          )}
        </div>
      </CardContent>
    </ProcurementControlAccordion>
  );
}
