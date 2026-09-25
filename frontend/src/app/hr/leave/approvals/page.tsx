'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Info, Loader2, ThumbsDown, ThumbsUp } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { Checkbox } from '@/components/ui/checkbox';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useToast } from '@/components/ui/use-toast';
import { leaveService } from '@/services/hr/leave.service';
import { MatchesApprovedPlanBadge } from '@/components/hr/leave/MatchesApprovedPlanBadge';

/**
 * Leave requests awaiting a given manager's decision.
 *
 * A single decision is still taken on the request's own page, where the shared workflow action
 * component enforces the engine's rules (step order, delegation, checklists, e-signature). What
 * this screen adds is the BULK path for a queue that is 200 rows in December (closure plan R-12).
 *
 * ⚠ Bulk here is not a shortcut past any of that. The server loops the real per-request service
 * call, so every item is authorized on its own and every workflow transition runs — cross-module
 * defect #15 is precisely what happens when a bulk path reaches for the engine directly instead
 * (bulk catalogue §4.1). A refusal on one item does not abandon the rest, and the result says which
 * ones did not go through and why.
 */
export default function LeaveApprovalsPage() {
  const router = useRouter();
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [decision, setDecision] = useState<null | 'approve' | 'reject'>(null);
  const [comments, setComments] = useState('');
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'leave-requests', 'my-approvals', page],
    queryFn: () => leaveService.getMyApprovals(page, 20),
  });

  const rows = data?.items ?? [];
  const selectedRows = rows.filter((r) => selected.has(r.id));
  const allOnPageSelected = rows.length > 0 && rows.every((r) => selected.has(r.id));

  const toggle = (id: string) =>
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const toggleAll = () =>
    setSelected((prev) => {
      const next = new Set(prev);
      if (allOnPageSelected) rows.forEach((r) => next.delete(r.id));
      else rows.forEach((r) => next.add(r.id));
      return next;
    });

  const bulk = useMutation({
    mutationFn: async () => {
      const ids = [...selected];
      return decision === 'approve'
        ? leaveService.bulkApprove({ leaveRequestIds: ids, comments: comments.trim() || null })
        : leaveService.bulkReject({
            leaveRequestIds: ids,
            rejectionReason: comments.trim(),
          });
    },
    onSuccess: async (result) => {
      const failed = result.results.filter((r) => !r.success);

      // ⚠ A 200 does NOT mean every item went through — the server decides each one separately.
      // The failures are named rather than swallowed, because the handful that refused is exactly
      // what the person needs to see.
      if (failed.length === 0) {
        toast({
          title: decision === 'approve' ? 'Approved' : 'Rejected',
          description: `${result.succeededCount} request${result.succeededCount === 1 ? '' : 's'} processed.`,
        });
      } else {
        toast({
          title: `${result.succeededCount} of ${result.requestedCount} processed`,
          description: `${failed.length} could not be: ${failed[0].reason ?? 'refused'}${failed.length > 1 ? ` (and ${failed.length - 1} more)` : ''}`,
          variant: 'destructive',
        });
      }

      setDecision(null);
      setComments('');
      // Keep only the ones that failed selected, so a retry does not re-send what already went.
      setSelected(new Set(failed.map((f) => f.id)));
      await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-requests'] });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-balances'] });
    },
    onError: (e: any) =>
      toast({
        title: 'Nothing was processed',
        description: e?.message || 'The bulk decision failed.',
        variant: 'destructive',
      }),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Leave Approvals"
        description="Requests waiting on a manager's decision."
        actions={
          <Button variant="outline" onClick={() => router.push('/workflow/inbox')}>
            Open approval inbox
          </Button>
        }
      />

      <div className="flex items-start gap-2 rounded-md border bg-muted/40 p-3 text-sm text-muted-foreground">
        <Info className="mt-0.5 h-4 w-4 shrink-0" />
        <p>
          Everything here is waiting on <span className="font-medium">you</span> — the workflow
          engine decides what that means, so a request appears once it reaches a step you own and
          leaves the moment you decide it. Leave is approved in two stages: the line manager first,
          then HR confirms the dates. Open a request to see where it sits, or select several to
          decide them together.
        </p>
      </div>

      <Card>
        <CardHeader className="flex-row items-center justify-between">
          <CardTitle>Pending requests</CardTitle>
          {selectedRows.length > 0 && (
            <div className="flex items-center gap-2">
              <span className="text-sm text-muted-foreground">
                {selectedRows.length} selected
              </span>
              <Button size="sm" onClick={() => setDecision('approve')}>
                <ThumbsUp className="mr-2 h-4 w-4" /> Approve
              </Button>
              <Button size="sm" variant="destructive" onClick={() => setDecision('reject')}>
                <ThumbsDown className="mr-2 h-4 w-4" /> Reject
              </Button>
            </div>
          )}
        </CardHeader>
        <CardContent>
          {(
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-10">
                      <Checkbox
                        checked={allOnPageSelected}
                        onCheckedChange={toggleAll}
                        aria-label="Select every request on this page"
                      />
                    </TableHead>
                    <TableHead>Request</TableHead>
                    <TableHead>Employee</TableHead>
                    <TableHead>Leave type</TableHead>
                    <TableHead>From</TableHead>
                    <TableHead>To</TableHead>
                    <TableHead className="text-right">Days</TableHead>
                    <TableHead>Status</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {isLoading ? (
                    [...Array(4)].map((_, i) => (
                      <TableRow key={i}>
                        {[...Array(8)].map((__, j) => (
                          <TableCell key={j}>
                            <Skeleton className="h-4 w-[90px]" />
                          </TableCell>
                        ))}
                      </TableRow>
                    ))
                  ) : rows.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={8}>
                        <EmptyState
                          icon={CheckCircle2}
                          title="Nothing waiting on you"
                          description="No leave request is currently at a step you own."
                        />
                      </TableCell>
                    </TableRow>
                  ) : (
                    rows.map((r) => (
                      <TableRow
                        key={r.id}
                        className="cursor-pointer hover:bg-muted/50"
                        onClick={() => router.push(`/hr/leave/requests/${r.id}`)}
                      >
                        <TableCell onClick={(e) => e.stopPropagation()}>
                          <Checkbox
                            checked={selected.has(r.id)}
                            onCheckedChange={() => toggle(r.id)}
                            aria-label={`Select ${r.requestNumber}`}
                          />
                        </TableCell>
                        <TableCell className="font-medium">
                          <div className="flex flex-wrap items-center gap-1.5">
                            {r.requestNumber}
                            <MatchesApprovedPlanBadge show={r.matchesApprovedPlan} />
                          </div>
                        </TableCell>
                        <TableCell>{r.employeeName}</TableCell>
                        <TableCell>{r.leaveTypeName}</TableCell>
                        <TableCell>{r.startDate?.slice(0, 10)}</TableCell>
                        <TableCell>{r.endDate?.slice(0, 10)}</TableCell>
                        <TableCell className="text-right">{r.totalDays}</TableCell>
                        <TableCell>
                          <StatusBadge status={r.status} />
                        </TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>
          )}

          {data && data.totalPages > 1 && (
            <div className="flex items-center justify-between pt-4">
              <p className="text-sm text-muted-foreground">
                Page {data.page} of {data.totalPages} · {data.totalCount} pending
              </p>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!data.hasPrevious}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!data.hasNext}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={decision !== null} onOpenChange={(open) => !open && setDecision(null)}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>
              {decision === 'approve' ? 'Approve' : 'Reject'} {selectedRows.length} request
              {selectedRows.length === 1 ? '' : 's'}?
            </DialogTitle>
            <DialogDescription>
              Each one is decided separately and authorized on its own, so some may be refused while
              the rest go through. You will be told which.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="max-h-40 overflow-y-auto rounded-md border text-sm">
              {selectedRows.map((r) => (
                <div key={r.id} className="flex justify-between gap-3 border-b px-3 py-1.5 last:border-0">
                  <span className="font-medium">{r.requestNumber}</span>
                  <span className="truncate text-muted-foreground">{r.employeeName}</span>
                  <span className="whitespace-nowrap text-muted-foreground">
                    {r.startDate?.slice(0, 10)} – {r.endDate?.slice(0, 10)}
                  </span>
                </div>
              ))}
            </div>

            <div className="space-y-2">
              <Label htmlFor="bulk-comments">
                {decision === 'approve' ? 'Comment (optional)' : 'Reason'}
                {decision === 'reject' && <span className="text-red-500"> *</span>}
              </Label>
              <Textarea
                id="bulk-comments"
                rows={3}
                value={comments}
                onChange={(e) => setComments(e.target.value)}
                placeholder={
                  decision === 'approve'
                    ? 'Applied to every request in this batch.'
                    : 'Applied to every request in this batch — say why.'
                }
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setDecision(null)} disabled={bulk.isPending}>
              Cancel
            </Button>
            <Button
              variant={decision === 'reject' ? 'destructive' : 'default'}
              disabled={bulk.isPending || (decision === 'reject' && !comments.trim())}
              onClick={() => bulk.mutate()}
            >
              {bulk.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {decision === 'approve' ? 'Approve all' : 'Reject all'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
