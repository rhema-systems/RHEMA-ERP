'use client';
import { ProcurementControlAccordion } from '@/components/procurement/ProcurementControlAccordion';

import {
  CheckCircle2,
  Clock,
  Loader2,
  XCircle,
} from 'lucide-react';
import { format } from 'date-fns';
import { Badge } from '@/components/ui/badge';
import { CardContent } from '@/components/ui/card';
import type {
  PurchaseRequisitionSourcingReadinessDto,
  PurchaseRequisitionSourcingReleaseDto,
} from '@/services/purchasingService';
import {
  getSourcingReleasePresentation,
  getSourcingRequirementPresentation,
} from '@/lib/procurement-requisition-sourcing';

interface Props {
  readiness?: PurchaseRequisitionSourcingReadinessDto;
  history: PurchaseRequisitionSourcingReleaseDto[];
  loading: boolean;
}

export function PurchaseRequisitionSourcingReleaseControl({
  readiness,
  history,
  loading,
}: Props) {
  const presentation = getSourcingReleasePresentation(readiness, loading);

  return (
    <ProcurementControlAccordion
        title="Sourcing release control"
        data-testid="sourcing-release-control"
        summary={presentation.title}
        status={<Badge variant="outline">{presentation.badge}</Badge>}
        notice={['blocked', 'stale'].includes(presentation.tone) && (readiness?.message || 'Sourcing readiness needs attention. Expand for details.')}
      >
        <p className="mb-4 text-sm text-muted-foreground">
              Confirms the requisition is approved, complete, and still covered
              by an available approved budget before RFQ or tender entry.
            </p>
      <CardContent className="space-y-4">
        <div>
          <p className="font-medium">{presentation.title}</p>
          <p className="mt-1 text-sm text-muted-foreground">
            {readiness?.message ||
              'The sourcing-release service could not be loaded.'}
          </p>
          {readiness?.evaluatedAtUtc && (
            <p className="mt-2 text-xs text-muted-foreground">
              Last evaluated{' '}
              {format(
                new Date(readiness.evaluatedAtUtc),
                'MMM dd, yyyy HH:mm:ss'
              )}
            </p>
          )}
        </div>

        {loading ? (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" /> Checking sourcing
            readiness…
          </div>
        ) : (
          <div className="grid gap-2 md:grid-cols-2">
            {(readiness?.requirements || []).map((requirement) => {
              const requirementPresentation =
                readiness == null
                  ? { state: 'actionRequired' as const, message: requirement.message }
                  : getSourcingRequirementPresentation(readiness, requirement);
              return (
                <div
                  key={requirement.key}
                  className={
                    requirementPresentation.state === 'actionRequired'
                      ? 'rounded-md border border-red-200 bg-red-50/70 p-3'
                      : requirementPresentation.state === 'waiting'
                        ? 'rounded-md border border-slate-200 bg-slate-50/80 p-3'
                        : 'rounded-md border border-emerald-200 bg-emerald-50/70 p-3'
                  }
                >
                  <div className="flex items-start gap-2">
                    {requirementPresentation.state === 'satisfied' ? (
                      <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" />
                    ) : requirementPresentation.state === 'actionRequired' ? (
                      <XCircle className="mt-0.5 h-4 w-4 shrink-0 text-red-600" />
                    ) : (
                      <Clock className="mt-0.5 h-4 w-4 shrink-0 text-slate-500" />
                    )}
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <p className="text-sm font-medium">
                          {requirement.label}
                        </p>
                        {requirementPresentation.state === 'waiting' && (
                          <Badge variant="outline">Waiting</Badge>
                        )}
                      </div>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {requirementPresentation.message}
                      </p>
                      {requirement.evidenceReference && (
                        <p className="mt-1 truncate font-mono text-[11px] text-muted-foreground">
                          {requirement.evidenceReference}
                        </p>
                      )}
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}

        {readiness?.currentRelease && (
          <div className="rounded-md border border-emerald-200 bg-emerald-50 p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div className="flex items-center gap-2">
                <CheckCircle2 className="h-4 w-4 text-emerald-700" />
                <span className="font-medium text-emerald-950">
                  {readiness.currentRelease.releaseReference}
                </span>
                <Badge variant="outline">
                  Attempt {readiness.currentRelease.attemptNumber}
                </Badge>
              </div>
              <span className="text-xs text-emerald-900">
                {format(
                  new Date(readiness.currentRelease.releasedAtUtc),
                  'MMM dd, yyyy HH:mm'
                )}
              </span>
            </div>
            <p className="mt-2 text-sm text-emerald-900">
              Released by {readiness.currentRelease.releasedByName}:{' '}
              {readiness.currentRelease.releaseReason}
            </p>
          </div>
        )}

        <div className="flex flex-wrap items-center justify-between gap-3">
          <p className="flex items-center gap-2 text-xs text-muted-foreground">
            <Clock className="h-4 w-4" />
            {history.length} immutable release attempt
            {history.length === 1 ? '' : 's'} retained
          </p>
          {presentation.canEnterSourcing && !readiness?.isReleased && (
            <p className="text-xs font-medium text-blue-800">
              The release audit record is created automatically when sourcing starts.
            </p>
          )}
        </div>

        {history.length > 0 && (
          <div className="space-y-2">
            <p className="text-sm font-medium">Immutable release history</p>
            {history.map((item) => (
              <div
                key={item.id}
                className="flex flex-wrap items-start justify-between gap-2 rounded-md border bg-background/80 p-3 text-sm"
              >
                <div>
                  <p className="font-medium">{item.releaseReference}</p>
                  <p className="text-xs text-muted-foreground">
                    {item.releasedByName} · {item.releaseReason}
                  </p>
                </div>
                <div className="text-right text-xs text-muted-foreground">
                  <p>
                    {format(new Date(item.releasedAtUtc), 'MMM dd, yyyy HH:mm')}
                  </p>
                </div>
              </div>
            ))}
          </div>
        )}
      </CardContent>

    </ProcurementControlAccordion>
  );
}
