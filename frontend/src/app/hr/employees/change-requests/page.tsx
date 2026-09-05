'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowRight, Check, Download, Loader2, ShieldQuestion, X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { hrDocumentService } from '@/services/hr/hr-document.service';
import {
  profileChangeQueueService,
  type ProfileChangeRequest,
  type ProfileChangeRequestStatus,
} from '@/services/hr/my-profile.service';
import { cn } from '@/lib/utils';

/**
 * Personal-data change requests — the HR desk queue (area 25 slice 12a, decision D6).
 *
 * Sits beside the employee register because that is what an approval edits: approving here
 * WRITES the values onto the employee record in the same transaction, re-running the same
 * uniqueness rules the register's own edit screen enforces.
 *
 * The two things this screen must never let an officer do are decide blind and refuse
 * silently — so every row shows before → after with the employee's stated reason and their
 * evidence, and the refusal dialog will not submit without a comment the employee reads back.
 */

const fmtDateTime = (v?: string | null) =>
  v ? new Date(v).toLocaleString(undefined, { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '—';

const STATUS_STYLE: Record<ProfileChangeRequestStatus, string> = {
  Pending: 'bg-amber-100 text-amber-900 dark:bg-amber-950/50 dark:text-amber-200',
  Approved: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950/50 dark:text-emerald-200',
  Rejected: 'bg-red-100 text-red-900 dark:bg-red-950/50 dark:text-red-200',
  Cancelled: 'bg-muted text-muted-foreground',
};

export default function ProfileChangeRequestQueuePage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [tab, setTab] = useState<'Pending' | 'all'>('Pending');
  const [rejecting, setRejecting] = useState<ProfileChangeRequest | null>(null);
  const [comments, setComments] = useState('');

  const { data: requests = [], isLoading, isError } = useQuery({
    queryKey: ['hr', 'profile-change-requests', tab],
    queryFn: () => profileChangeQueueService.getQueue(tab === 'all' ? undefined : 'Pending'),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'profile-change-requests'] });

  const approve = useMutation({
    mutationFn: (r: ProfileChangeRequest) => profileChangeQueueService.approve(r.id),
    onSuccess: (updated) => {
      refresh();
      toast({
        title: `${updated.requestNumber} approved`,
        description: 'The employee record has been updated.',
      });
    },
    onError: (e: any) =>
      toast({
        title: 'Not approved',
        description: e?.message || 'The request could not be approved.',
        variant: 'destructive',
      }),
  });

  const reject = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) =>
      profileChangeQueueService.reject(id, reason),
    onSuccess: (updated) => {
      refresh();
      setRejecting(null);
      setComments('');
      toast({
        title: `${updated.requestNumber} refused`,
        description: 'The employee can read your reason on their profile.',
      });
    },
    onError: (e: any) =>
      toast({
        title: 'Not refused',
        description: e?.message || 'The request could not be refused.',
        variant: 'destructive',
      }),
  });

  const downloadEvidence = async (r: ProfileChangeRequest) => {
    try {
      await hrDocumentService.download(
        profileChangeQueueService.evidenceEndpoint(r.id),
        r.evidenceFileName ?? `${r.requestNumber}-evidence`,
      );
    } catch {
      toast({
        title: 'Could not open the evidence',
        description: 'The attached file could not be downloaded.',
        variant: 'destructive',
      });
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Personal Data Change Requests"
        description="Corrections employees have asked for to their name, address, statutory numbers or bank details. Approving writes the change onto the employee record."
        backHref="/hr/employees"
      />

      <Tabs value={tab} onValueChange={(v) => setTab(v as 'Pending' | 'all')}>
        <TabsList>
          <TabsTrigger value="Pending">Waiting on HR</TabsTrigger>
          <TabsTrigger value="all">All</TabsTrigger>
        </TabsList>
      </Tabs>

      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-44" />
          <Skeleton className="h-44" />
        </div>
      ) : isError ? (
        <p className="text-sm text-muted-foreground">
          The queue could not be loaded right now. Try again in a moment.
        </p>
      ) : requests.length === 0 ? (
        <EmptyState
          icon={ShieldQuestion}
          title={tab === 'Pending' ? 'Nothing waiting' : 'No change requests'}
          description={
            tab === 'Pending'
              ? 'No employee is waiting on a personal-data correction.'
              : 'Employees have not asked for any corrections yet.'
          }
        />
      ) : (
        <div className="space-y-3">
          {requests.map((r) => (
            <Card key={r.id}>
              <CardContent className="space-y-4 p-4">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-medium">{r.employeeName}</span>
                  <span className="font-mono text-xs text-muted-foreground">{r.employeeNumber}</span>
                  <Badge className={cn('border-0', STATUS_STYLE[r.status])}>{r.statusName}</Badge>
                  {r.bankAccountMasked && (
                    <Badge variant="outline" className="font-mono">{r.bankAccountMasked}</Badge>
                  )}
                  <span className="ml-auto font-mono text-xs text-muted-foreground">{r.requestNumber}</span>
                </div>

                <div className="text-sm">
                  <span className="text-muted-foreground">Reason given: </span>
                  {r.reason}
                </div>

                <div className="space-y-1.5">
                  {r.items.map((item) => (
                    <div key={item.id} className="flex flex-wrap items-center gap-2 rounded-md bg-muted/40 px-3 py-2 text-sm">
                      <span className="font-medium">{item.fieldLabel}</span>
                      <span className="text-muted-foreground line-through">{item.oldValue || 'not set'}</span>
                      <ArrowRight className="h-3.5 w-3.5 text-muted-foreground" />
                      <span className="font-medium">{item.newValue}</span>
                    </div>
                  ))}
                </div>

                {r.reviewComments && (
                  <div className="rounded-md border-l-2 border-primary/50 bg-muted/30 px-3 py-2 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">
                      {r.reviewedByName ?? 'HR'} · {fmtDateTime(r.reviewedAt)}
                    </div>
                    <p className="mt-0.5">{r.reviewComments}</p>
                  </div>
                )}

                <div className="flex flex-wrap items-center gap-2">
                  {r.hasEvidence && (
                    <Button variant="outline" size="sm" onClick={() => downloadEvidence(r)}>
                      <Download className="mr-1 h-3.5 w-3.5" />
                      {r.evidenceFileName ?? 'Evidence'}
                    </Button>
                  )}
                  {r.status === 'Pending' && (
                    <>
                      <Button size="sm" onClick={() => approve.mutate(r)} disabled={approve.isPending}>
                        {approve.isPending ? (
                          <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" />
                        ) : (
                          <Check className="mr-1 h-3.5 w-3.5" />
                        )}
                        Approve &amp; apply
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => {
                          setRejecting(r);
                          setComments('');
                        }}
                      >
                        <X className="mr-1 h-3.5 w-3.5" /> Refuse
                      </Button>
                    </>
                  )}
                  <span className="ml-auto text-xs text-muted-foreground">
                    filed {fmtDateTime(r.submittedAt)}
                    {r.appliedAt ? ` · applied ${fmtDateTime(r.appliedAt)}` : ''}
                  </span>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Dialog open={!!rejecting} onOpenChange={(o) => !o && setRejecting(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Refuse {rejecting?.requestNumber}</DialogTitle>
            <DialogDescription>
              {rejecting?.employeeName} will read this on their profile, so say what is missing or
              wrong — a refusal without a reason leaves them with nothing to correct.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-1.5">
            <Label htmlFor="reject-comments">Reason</Label>
            <Textarea
              id="reject-comments"
              rows={3}
              value={comments}
              onChange={(e) => setComments(e.target.value)}
              placeholder="e.g. The bank letter does not show the account name — please attach one that does."
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejecting(null)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={() => rejecting && reject.mutate({ id: rejecting.id, reason: comments })}
              disabled={reject.isPending || comments.trim() === ''}
            >
              {reject.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Refuse request
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
