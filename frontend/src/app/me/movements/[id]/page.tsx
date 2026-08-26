'use client';

/**
 * Area 25 slice 7 — one movement, from the SUBJECT's side.
 *
 * The desk detail at /hr/movements/[id] carries the workflow approval actions and stays for
 * approvers and HR; this page is what the person being moved sees: where they are going,
 * what changes, where the record stands — and the accept/decline act when the movement is
 * awaiting their answer. The portal read 403s for anyone who is not the subject; the
 * respond arm additionally 409s outside the acceptance-pending stage, and the message is
 * shown as-is.
 */

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, CheckCircle2, XCircle, ArrowRight } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { mePortalService } from '@/services/hr/me-portal.service';
import { useToast } from '@/hooks/use-toast';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MyMovementDetailPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [responding, setResponding] = useState<{ accepted: boolean } | null>(null);
  const [comments, setComments] = useState('');

  const queryKey = ['me', 'movements', id];
  const { data: m, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => mePortalService.getMovement(id),
    enabled: !!id,
  });

  const respond = useMutation({
    mutationFn: () => {
      if (!responding) throw new Error('Nothing selected.');
      return mePortalService.respondToMovement(id, responding.accepted, comments || undefined);
    },
    onSuccess: () => {
      toast({ title: responding?.accepted ? 'Movement accepted' : 'Movement declined' });
      setResponding(null);
      setComments('');
      queryClient.invalidateQueries({ queryKey: ['me', 'movements'] });
    },
    onError: (error: any) =>
      toast({ title: 'Refused', description: error?.message, variant: 'destructive' }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !m) {
    return (
      <EmptyState
        title="Movement not found"
        description="It may have been removed, or it may not be yours to see."
      />
    );
  }

  const awaitingMe = m.status === 'EmployeeAcceptancePending';
  const responded = m.employeeResponseDate != null;

  return (
    <div className="space-y-6">
      <PageHeader
        title={`${m.movementTypeName} — ${m.movementNumber}`}
        description={`Effective ${fmtDate(m.effectiveDate)}${m.isTemporary && m.temporaryEndDate ? ` · temporary until ${fmtDate(m.temporaryEndDate)}` : ''}`}
        backHref="/me/movements"
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge status={m.status} />
            {awaitingMe && (
              <>
                <Button size="sm" onClick={() => setResponding({ accepted: true })}>
                  <CheckCircle2 className="mr-2 h-4 w-4" /> Accept
                </Button>
                <Button variant="outline" size="sm" onClick={() => setResponding({ accepted: false })}>
                  <XCircle className="mr-2 h-4 w-4" /> Decline
                </Button>
              </>
            )}
          </div>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">What changes</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 sm:grid-cols-[1fr_auto_1fr] sm:items-center">
            <div className="rounded-md border p-4">
              <p className="text-xs uppercase tracking-wide text-muted-foreground">From</p>
              <p className="mt-1 font-medium">{m.currentPositionTitle}</p>
              <p className="text-sm text-muted-foreground">{m.currentOrganizationUnitName}</p>
              {m.currentLocationName && (
                <p className="text-sm text-muted-foreground">{m.currentLocationName}</p>
              )}
              {m.currentSupervisorName && (
                <p className="mt-1 text-xs text-muted-foreground">
                  Reports to {m.currentSupervisorName}
                </p>
              )}
            </div>
            <ArrowRight className="hidden h-5 w-5 justify-self-center text-muted-foreground sm:block" />
            <div className="rounded-md border border-primary/40 p-4">
              <p className="text-xs uppercase tracking-wide text-muted-foreground">To</p>
              <p className="mt-1 font-medium">{m.newPositionTitle}</p>
              <p className="text-sm text-muted-foreground">{m.newOrganizationUnitName}</p>
              {m.newLocationName && (
                <p className="text-sm text-muted-foreground">{m.newLocationName}</p>
              )}
              {m.newSupervisorName && (
                <p className="mt-1 text-xs text-muted-foreground">
                  Reports to {m.newSupervisorName}
                </p>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">The record</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 py-2 sm:grid-cols-2">
          <Detail label="Category" value={m.categoryName} />
          <Detail label="Requested" value={fmtDate(m.requestDate)} />
          <Detail label="Requested by" value={m.requestedByName ?? '—'} />
          <Detail label="Authorized" value={m.authorizedByName ? `${m.authorizedByName} · ${fmtDate(m.authorizationDate)}` : 'Not yet'} />
          <div className="sm:col-span-2">
            <p className="text-sm text-muted-foreground">Reason</p>
            <p className="font-medium">{m.reason || '—'}</p>
          </div>
          {m.rejectionReason && (
            <div className="sm:col-span-2">
              <p className="text-sm text-muted-foreground">Rejection reason</p>
              <p className="font-medium text-destructive">{m.rejectionReason}</p>
            </div>
          )}
        </CardContent>
      </Card>

      {(m.requiresEmployeeAcceptance || responded) && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Your response</CardTitle>
          </CardHeader>
          <CardContent className="py-2">
            {responded ? (
              <div className="flex flex-wrap items-center gap-3">
                <Badge variant={m.employeeAccepted ? 'default' : 'destructive'}>
                  {m.employeeAccepted ? 'Accepted' : 'Declined'}
                </Badge>
                <span className="text-sm text-muted-foreground">
                  {fmtDate(m.employeeResponseDate)}
                </span>
                {m.employeeComments && <span className="text-sm">{m.employeeComments}</span>}
              </div>
            ) : awaitingMe ? (
              <p className="text-sm text-muted-foreground">
                This movement is waiting on your answer — nobody else, HR included, can give it
                for you.
              </p>
            ) : (
              <p className="text-sm text-muted-foreground">
                Your acceptance will be asked for once the approvals are complete.
              </p>
            )}
          </CardContent>
        </Card>
      )}

      <Dialog open={responding !== null} onOpenChange={(open) => !open && setResponding(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {responding?.accepted ? 'Accept this movement' : 'Decline this movement'}
            </DialogTitle>
            <DialogDescription>
              This is your own response and it is recorded against your name.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="comments">Comments</Label>
            <Textarea
              id="comments"
              rows={3}
              value={comments}
              onChange={(e) => setComments(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setResponding(null)}>
              Cancel
            </Button>
            <Button onClick={() => respond.mutate()} disabled={respond.isPending}>
              {respond.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {responding?.accepted ? 'Accept' : 'Decline'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-sm text-muted-foreground">{label}</p>
      <p className="font-medium">{value}</p>
    </div>
  );
}
