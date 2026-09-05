'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  CalendarPlus,
  CheckCircle2,
  FileText,
  Loader2,
  Send,
  Undo2,
  XCircle,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { probationService } from '@/services/hr/probation.service';
import type { ProbationStatus } from '@/types/hr/probation';
import { toast } from 'sonner';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const STATUS_LABEL: Record<ProbationStatus, string> = {
  Active: 'Running',
  PendingConfirmation: 'With the confirming authority',
  ConfirmationApproved: 'Approved — awaiting HR',
  Completed: 'Confirmed',
  Terminated: 'Terminated',
};

/**
 * One probation, and the actions available on it.
 *
 * ⚠ **Every action here can be refused by the API for a reason the screen cannot see.** Confirm is
 * Admin-only and additionally refused when a workflow definition is published; submit is refused
 * when no confirming authority covers the employee; the letter is refused until HR has recorded the
 * confirmation. So the buttons are offered on the record's STATE and the refusal message is shown
 * verbatim — guessing at the caller's permissions here would either hide a button someone needs or
 * promise one that will fail.
 */
export default function ProbationDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const qc = useQueryClient();
  const [letterHtml, setLetterHtml] = useState<string | null>(null);

  const { data: probation, isLoading } = useQuery({
    queryKey: ['probation', id],
    queryFn: () => probationService.getWithReviews(id),
  });

  const { data: reviews } = useQuery({
    queryKey: ['probation-reviews', id],
    queryFn: () => probationService.getReviews(id),
  });

  const { data: extensions } = useQuery({
    queryKey: ['probation-extensions', id],
    queryFn: () => probationService.getExtensions(id),
  });

  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ['probation', id] });
    void qc.invalidateQueries({ queryKey: ['probation-reviews', id] });
    void qc.invalidateQueries({ queryKey: ['probation-extensions', id] });
  };

  // The screen never sets a status itself — it calls, then refetches and lets the record say what
  // happened. Optimistically flipping a status here would paper over a refusal.
  const act = (fn: () => Promise<unknown>, success: string) =>
    fn()
      .then(() => {
        toast.success(success);
        refresh();
      })
      .catch((e: unknown) =>
        toast.error(e instanceof Error ? e.message : 'The action was refused'),
      );

  const letter = useMutation({
    mutationFn: () => probationService.getConfirmationLetter(id),
    onSuccess: (l) => setLetterHtml(l.htmlBody),
    onError: (e: unknown) =>
      toast.error(e instanceof Error ? e.message : 'The letter could not be produced'),
  });

  if (isLoading || !probation) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const status = probation.statusName;
  const open = status === 'Active' || status === 'PendingConfirmation' || status === 'ConfirmationApproved';
  const overdueDays = Math.round(
    (Date.now() - new Date(probation.currentEndDate).getTime()) / 86_400_000,
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={probation.employeeName}
        description={`${probation.employeeNumber} · probation ${fmtDate(probation.startDate)} → ${fmtDate(probation.currentEndDate)}`}
        backHref="/hr/probation"
        actions={
          <div className="flex flex-wrap gap-2">
            {status === 'Active' && (
              <Button
                variant="outline"
                onClick={() =>
                  act(() => probationService.submitForConfirmation(id), 'Sent for confirmation')
                }
              >
                <Send className="mr-2 h-4 w-4" />
                Send for confirmation
              </Button>
            )}
            {status === 'PendingConfirmation' && (
              <Button
                variant="outline"
                onClick={() => act(() => probationService.recallConfirmation(id), 'Recalled')}
              >
                <Undo2 className="mr-2 h-4 w-4" />
                Recall
              </Button>
            )}
            {(status === 'Active' || status === 'ConfirmationApproved') && (
              <Button onClick={() => act(() => probationService.confirm(id), 'Probation confirmed')}>
                <CheckCircle2 className="mr-2 h-4 w-4" />
                Confirm
              </Button>
            )}
            {open && (
              <Button
                variant="destructive"
                onClick={() =>
                  act(
                    () => probationService.terminate(id, { probationId: id }),
                    'Probation terminated',
                  )
                }
              >
                <XCircle className="mr-2 h-4 w-4" />
                Terminate
              </Button>
            )}
            {status === 'Completed' && (
              <Button variant="outline" onClick={() => letter.mutate()}>
                {letter.isPending ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <FileText className="mr-2 h-4 w-4" />
                )}
                Confirmation letter
              </Button>
            )}
          </div>
        }
      />

      {open && overdueDays > 0 && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            This probation ended {overdueDays} day{overdueDays === 1 ? '' : 's'} ago and no outcome
            has been recorded. The employee is working under terms that have expired.
          </AlertDescription>
        </Alert>
      )}

      <div className="grid gap-4 sm:grid-cols-4">
        <Card>
          <CardContent className="p-4">
            <p className="text-xs text-muted-foreground">Status</p>
            <Badge variant="secondary" className="mt-1">
              {STATUS_LABEL[status]}
            </Badge>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-4">
            <p className="text-xs text-muted-foreground">Length</p>
            <p className="mt-1 font-medium">{probation.durationMonths} months</p>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-4">
            <p className="text-xs text-muted-foreground">Originally ended</p>
            {/* Shown alongside the current end date on purpose: the gap between them is the whole
                extension history in one glance. */}
            <p className="mt-1 font-medium">{fmtDate(probation.originalEndDate)}</p>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-4">
            <p className="text-xs text-muted-foreground">Extensions</p>
            <p className="mt-1 font-medium">{probation.extensionCount}</p>
          </CardContent>
        </Card>
      </div>

      <Tabs defaultValue="reviews">
        <TabsList>
          <TabsTrigger value="reviews">Reviews ({probation.reviewCount})</TabsTrigger>
          <TabsTrigger value="extensions">Extensions ({probation.extensionCount})</TabsTrigger>
          <TabsTrigger value="outcome">Outcome</TabsTrigger>
        </TabsList>

        <TabsContent value="reviews">
          <Card>
            <CardContent className="p-0">
              {!reviews || reviews.length === 0 ? (
                <EmptyState
                  icon={CalendarPlus}
                  title="No reviews scheduled"
                  description="A confirmation decision is supposed to rest on reviews. Schedule at least one before the probation ends."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>#</TableHead>
                      <TableHead>Scheduled</TableHead>
                      <TableHead>Held</TableHead>
                      <TableHead>Reviewer</TableHead>
                      <TableHead>Recommendation</TableHead>
                      <TableHead>Acknowledged</TableHead>
                      <TableHead>HR sign-off</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {reviews.map((r) => (
                      <TableRow key={r.id}>
                        <TableCell>{r.reviewNumber}</TableCell>
                        <TableCell>{fmtDate(r.scheduledDate)}</TableCell>
                        <TableCell>{fmtDate(r.actualDate)}</TableCell>
                        <TableCell>
                          {r.reviewedByName}
                          {r.secondReviewerName ? (
                            <div className="text-xs text-muted-foreground">
                              with {r.secondReviewerName}
                            </div>
                          ) : null}
                        </TableCell>
                        <TableCell>
                          {/* ⚠ An unconducted review has no recommendation, and must not be shown
                              as though it recommended nothing in particular. */}
                          {r.recommendationName ?? (
                            <span className="text-muted-foreground">not yet conducted</span>
                          )}
                        </TableCell>
                        <TableCell>{r.employeeAcknowledged ? 'Yes' : '—'}</TableCell>
                        <TableCell>{r.hrApproved ? r.hrApprovedByName ?? 'Yes' : '—'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="extensions">
          <Card>
            <CardContent className="p-0">
              {!extensions || extensions.length === 0 ? (
                <EmptyState
                  icon={CalendarPlus}
                  title="No extensions"
                  description="This probation has run to its original length."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Applied</TableHead>
                      <TableHead>From</TableHead>
                      <TableHead>To</TableHead>
                      <TableHead className="text-right">Months</TableHead>
                      <TableHead>Reason</TableHead>
                      <TableHead>By</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {extensions.map((x) => (
                      <TableRow key={x.id}>
                        <TableCell>{fmtDate(x.extendedDate)}</TableCell>
                        <TableCell>{fmtDate(x.previousEndDate)}</TableCell>
                        <TableCell>{fmtDate(x.newEndDate)}</TableCell>
                        <TableCell className="text-right">{x.extensionMonths}</TableCell>
                        <TableCell className="max-w-md">{x.reason}</TableCell>
                        <TableCell>{x.extendedByName ?? '—'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="outcome">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Outcome</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3 text-sm">
              <p>
                <span className="text-muted-foreground">Status: </span>
                {STATUS_LABEL[status]}
              </p>
              {probation.outcomeNotes ? (
                <p className="whitespace-pre-wrap">{probation.outcomeNotes}</p>
              ) : (
                <p className="text-muted-foreground">No outcome recorded yet.</p>
              )}
              {status === 'ConfirmationApproved' && (
                <Alert>
                  <CheckCircle2 className="h-4 w-4" />
                  <AlertDescription>
                    The confirming authority has approved this. Record the confirmation to update the
                    employee&apos;s record and issue the letter.
                  </AlertDescription>
                </Alert>
              )}
              <p className="pt-2">
                <Link href={`/hr/employees/${probation.employeeId}`} className="text-primary hover:underline">
                  Open the employee record
                </Link>
              </p>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {letterHtml && (
        <Card>
          <CardHeader className="flex-row items-center justify-between">
            <CardTitle className="text-base">Confirmation letter</CardTitle>
            <div className="flex gap-2">
              <Button size="sm" variant="outline" onClick={() => window.print()}>
                Print
              </Button>
              <Button size="sm" variant="ghost" onClick={() => setLetterHtml(null)}>
                Close
              </Button>
            </div>
          </CardHeader>
          <CardContent>
            {/* The letter is a self-contained HTML document from the server template, so it renders
                in its own frame rather than inheriting the app's styles. */}
            <iframe
              title="Confirmation letter"
              srcDoc={letterHtml}
              className="h-[38rem] w-full rounded border bg-white"
            />
          </CardContent>
        </Card>
      )}
    </div>
  );
}
