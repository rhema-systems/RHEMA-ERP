'use client';

/**
 * Area 25 slice 6 — My Waitlist: spec destination #16, built fresh (census verdict E — the
 * endpoint existed, no screen anywhere).
 *
 * The lifecycle is three deliberate steps (see trainingWaitlistService): HR OFFERS a freed
 * slot, the employee RESPONDS here, and HR then PROMOTES the accepted entry into a
 * nomination — accepting an offer does not enrol on its own, and the copy says so. The
 * employee/{id} read is the self-arm route (own id 200 / other's 403); the caller's id
 * comes from the auth context, as on My Leave.
 */

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Hourglass, Check, X, Loader2, Undo2 } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useAuth } from '@/hooks/use-auth';
import { trainingWaitlistService } from '@/services/hr/training-waitlist.service';
import { TRAINING_WAITLIST_STATUS_OPTIONS } from '@/types/hr/training-delivery';
import type { TrainingWaitlistEntry } from '@/types/hr/training-delivery';

const statusLabel = (v: string) =>
  TRAINING_WAITLIST_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MyWaitlistPage() {
  const { user } = useAuth();
  const employeeId = user?.employeeId ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [respondTarget, setRespondTarget] = useState<{ entry: TrainingWaitlistEntry; accept: boolean } | null>(null);
  const [withdrawTarget, setWithdrawTarget] = useState<TrainingWaitlistEntry | null>(null);
  const [busy, setBusy] = useState(false);

  const queryKey = ['me', 'training', 'waitlist', employeeId];
  const { data: entries, isLoading } = useQuery({
    queryKey,
    queryFn: () => trainingWaitlistService.getByEmployee(employeeId),
    enabled: !!employeeId,
  });

  const rows = entries ?? [];
  const offered = rows.filter((e) => e.status === 'Offered');

  const act = async (label: string, fn: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await fn();
      await queryClient.invalidateQueries({ queryKey });
      toast({ title: label });
      return true;
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || `${label} failed.`, variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Waitlist"
        description="Where you stand in the queue for full training runs."
        backHref="/me/training"
      />

      {offered.length > 0 && (
        <Alert className="border-amber-500/50 [&>svg]:text-amber-600">
          <Hourglass className="h-4 w-4" />
          <AlertTitle>
            {offered.length === 1
              ? 'A seat has been offered to you'
              : `${offered.length} seats have been offered to you`}
          </AlertTitle>
          <AlertDescription>
            Answer before the offer expires — an unanswered offer passes to the next person in
            the queue.
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Queue positions</CardTitle>
          <CardDescription>
            Accepting an offer holds your seat; the training desk then confirms your enrolment
            and a nomination appears under My Training.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Programme</TableHead>
                  <TableHead>Schedule</TableHead>
                  <TableHead>Position</TableHead>
                  <TableHead>Joined</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[200px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(3)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(6)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[80px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <EmptyState
                        icon={Hourglass}
                        title="You are not on any waitlist"
                        description="When a training run you want is full, you can be queued for a freed seat."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((e) => (
                    <TableRow key={e.id}>
                      <TableCell className="font-medium">{e.programName}</TableCell>
                      <TableCell className="font-mono text-xs">{e.scheduleNumber}</TableCell>
                      <TableCell>
                        {e.status === 'Active' ? (
                          <Badge variant="outline">#{e.position}</Badge>
                        ) : (
                          <span className="text-muted-foreground">—</span>
                        )}
                      </TableCell>
                      <TableCell className="text-muted-foreground">{fmt(e.addedDate)}</TableCell>
                      <TableCell>
                        <div className="flex flex-col gap-1">
                          <StatusBadge status={statusLabel(e.status)} />
                          {e.status === 'Offered' && e.offerExpiryDate && (
                            <span className="text-xs text-amber-600">
                              Answer by {fmt(e.offerExpiryDate)}
                            </span>
                          )}
                          {e.status === 'Accepted' && !e.createdNominationNumber && (
                            <span className="text-xs text-muted-foreground">
                              Awaiting enrolment by the training desk
                            </span>
                          )}
                          {e.createdNominationNumber && (
                            <span className="text-xs text-muted-foreground">
                              Enrolled · {e.createdNominationNumber}
                            </span>
                          )}
                        </div>
                      </TableCell>
                      <TableCell>
                        {e.status === 'Offered' ? (
                          <div className="flex items-center justify-end gap-2">
                            <Button
                              size="sm"
                              disabled={busy}
                              onClick={() => setRespondTarget({ entry: e, accept: true })}
                            >
                              <Check className="mr-1 h-4 w-4" /> Accept
                            </Button>
                            <Button
                              size="sm"
                              variant="outline"
                              disabled={busy}
                              onClick={() => setRespondTarget({ entry: e, accept: false })}
                            >
                              <X className="mr-1 h-4 w-4" /> Decline
                            </Button>
                          </div>
                        ) : e.status === 'Active' ? (
                          <div className="flex items-center justify-end">
                            <Button
                              size="sm"
                              variant="ghost"
                              disabled={busy}
                              onClick={() => setWithdrawTarget(e)}
                            >
                              <Undo2 className="mr-1 h-4 w-4" /> Leave queue
                            </Button>
                          </div>
                        ) : null}
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={respondTarget !== null}
        onOpenChange={(o) => !o && setRespondTarget(null)}
        title={respondTarget?.accept ? 'Accept the offered seat?' : 'Decline the offer?'}
        description={
          respondTarget?.accept
            ? `You take the freed seat on ${respondTarget.entry.programName} (${respondTarget.entry.scheduleNumber}). The training desk completes your enrolment.`
            : `The seat on ${respondTarget?.entry.programName} passes to the next person in the queue.`
        }
        confirmText={respondTarget?.accept ? 'Accept' : 'Decline'}
        variant={respondTarget?.accept ? 'default' : 'destructive'}
        isLoading={busy}
        onConfirm={async () => {
          if (!respondTarget) return false;
          const ok = await act(respondTarget.accept ? 'Offer accepted' : 'Offer declined', () =>
            trainingWaitlistService.respond(respondTarget.entry.id, {
              offerAccepted: respondTarget.accept,
              responseDate: new Date().toISOString(),
            }),
          );
          if (ok) setRespondTarget(null);
          return ok;
        }}
      />

      <ConfirmationDialog
        open={withdrawTarget !== null}
        onOpenChange={(o) => !o && setWithdrawTarget(null)}
        title="Leave this waitlist?"
        description={
          withdrawTarget
            ? `You give up your place in the queue for ${withdrawTarget.programName}.`
            : ''
        }
        confirmText="Leave queue"
        variant="destructive"
        isLoading={busy}
        onConfirm={async () => {
          if (!withdrawTarget) return false;
          const ok = await act('Left the waitlist', () =>
            trainingWaitlistService.remove(withdrawTarget.id),
          );
          if (ok) setWithdrawTarget(null);
          return ok;
        }}
      />
    </div>
  );
}
