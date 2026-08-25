'use client';

import { useState } from 'react';
import {
  AlertCircle,
  CheckCircle2,
  Clock,
  Loader2,
  ShieldCheck,
  XCircle,
} from 'lucide-react';
import { format } from 'date-fns';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
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
  releasing: boolean;
  onRelease: (reason: string) => Promise<void>;
}

const toneClass = {
  neutral: 'border-slate-200 bg-slate-50',
  ready: 'border-blue-200 bg-blue-50',
  released: 'border-emerald-200 bg-emerald-50',
  blocked: 'border-amber-200 bg-amber-50',
  stale: 'border-red-200 bg-red-50',
};

export function PurchaseRequisitionSourcingReleaseControl({
  readiness,
  history,
  loading,
  releasing,
  onRelease,
}: Props) {
  const [dialogOpen, setDialogOpen] = useState(false);
  const [reason, setReason] = useState(
    'All pre-sourcing controls reviewed and confirmed.'
  );
  const presentation = getSourcingReleasePresentation(readiness, loading);

  const release = async () => {
    await onRelease(reason.trim());
    setDialogOpen(false);
  };

  return (
    <Card
      className={toneClass[presentation.tone]}
      data-testid="sourcing-release-control"
    >
      <CardHeader>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <CardTitle className="flex items-center gap-2">
              <ShieldCheck className="h-5 w-5" />
              Sourcing release control
            </CardTitle>
            <CardDescription className="mt-1">
              Confirms the requisition is approved, complete, and still covered
              by an available approved budget before RFQ or tender entry.
            </CardDescription>
          </div>
          <Badge
            variant={
              presentation.tone === 'blocked' || presentation.tone === 'stale'
                ? 'destructive'
                : 'outline'
            }
          >
            {presentation.badge}
          </Badge>
        </div>
      </CardHeader>
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
          {readiness?.controlFingerprint && (
            <p className="mt-2 break-all font-mono text-[11px] text-muted-foreground">
              Control fingerprint: {readiness.controlFingerprint}
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
                getSourcingRequirementPresentation(readiness!, requirement);
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
            <p className="mt-2 break-all font-mono text-[11px] text-emerald-900">
              Integrity: {readiness.currentRelease.integrityHash}
            </p>
          </div>
        )}

        <div className="flex flex-wrap items-center justify-between gap-3">
          <p className="flex items-center gap-2 text-xs text-muted-foreground">
            {readiness?.hasStaleRelease ? (
              <AlertCircle className="h-4 w-4" />
            ) : (
              <Clock className="h-4 w-4" />
            )}
            {history.length} immutable release attempt
            {history.length === 1 ? '' : 's'} retained
          </p>
          <Button
            onClick={() => setDialogOpen(true)}
            disabled={!presentation.canRelease || releasing}
          >
            {releasing ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <ShieldCheck className="mr-2 h-4 w-4" />
            )}
            Record sourcing release
          </Button>
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
                  <p className="font-mono">
                    {item.integrityHash.slice(0, 16)}…
                  </p>
                </div>
              </div>
            ))}
          </div>
        )}
      </CardContent>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record immutable sourcing release</DialogTitle>
            <DialogDescription>
              This appends a release for the current control fingerprint. It
              cannot be edited or deleted.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="sourcing-release-reason">Release reason</Label>
            <Textarea
              id="sourcing-release-reason"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              maxLength={500}
              rows={4}
            />
            <p className="text-xs text-muted-foreground">
              Minimum 5 characters · {reason.length}/500
            </p>
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setDialogOpen(false)}
              disabled={releasing}
            >
              Cancel
            </Button>
            <Button
              onClick={release}
              disabled={reason.trim().length < 5 || releasing}
            >
              {releasing && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Confirm release
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
