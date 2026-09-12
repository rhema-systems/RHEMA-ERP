'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, ClipboardList, Loader2, Mail, Trash2, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { formatDateTime, humanizeEnum } from '@/lib/hr/attendance-format';
import { jobInterviewService } from '@/services/hr/interviews.service';
import { INTERVIEW_OUTCOMES, type JobInterviewDetail } from '@/types/hr/interviews';

const NO_OUTCOME = '__none__';

/**
 * The candidates booked into this session.
 *
 * Two different "did they confirm" ideas live here and are deliberately kept apart:
 * **Confirmed** is the candidate's reply to their invitation, given by emailed link before the day;
 * **Attended** is what actually happened. A candidate can confirm and then not turn up, which is
 * exactly the case the no-show reason exists for.
 *
 * ⚠ Recording an outcome for a no-show is refused by the server — there is no verdict to give on an
 * interview that did not happen — so the picker is disabled rather than left to fail.
 */
export function InterviewCandidatesPanel({
  interview,
  canManage,
  canScore,
  myPanelistId,
}: {
  interview: JobInterviewDetail;
  canManage: boolean;
  canScore: boolean;
  myPanelistId?: string | null;
}) {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [slotEdits, setSlotEdits] = useState<Record<string, { start: string; end: string }>>({});

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['hr', 'interview', interview.id] });

  const sendInvites = useMutation({
    mutationFn: () => jobInterviewService.sendInvites(interview.id, []),
    onSuccess: (result) => {
      toast({
        title: `Invitations sent to ${result.sent} of ${result.totalRequested}`,
        description:
          result.skipped > 0
            ? `${result.skipped} could not be sent — check the candidates have an email address.`
            : 'Each candidate gets a calendar invite and a link to confirm.',
      });
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Could not send invitations', description: error?.message, variant: 'destructive' }),
  });

  const attendance = useMutation({
    mutationFn: ({ id, attended }: { id: string; attended: boolean }) =>
      jobInterviewService.recordIntervieweeAttendance(id, attended, attended ? null : 'Did not attend'),
    onSuccess: () => {
      toast({ title: 'Attendance recorded' });
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Could not record attendance', description: error?.message, variant: 'destructive' }),
  });

  const outcome = useMutation({
    mutationFn: ({ id, value }: { id: string; value: string }) =>
      jobInterviewService.recordIntervieweeOutcome(id, value),
    onSuccess: () => {
      toast({ title: 'Outcome recorded' });
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Could not record the outcome', description: error?.message, variant: 'destructive' }),
  });

  const saveSlot = useMutation({
    mutationFn: ({ id, start, end }: { id: string; start: string; end: string }) =>
      jobInterviewService.updateIntervieweeSlot(id, start ? `${start}:00` : null, end ? `${end}:00` : null),
    onSuccess: () => {
      toast({ title: 'Slot updated' });
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Could not update the slot', description: error?.message, variant: 'destructive' }),
  });

  const removeCandidate = useMutation({
    mutationFn: (id: string) => jobInterviewService.removeInterviewee(id),
    onSuccess: () => {
      toast({ title: 'Candidate removed from the interview' });
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Could not remove the candidate', description: error?.message, variant: 'destructive' }),
  });

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-4 space-y-0">
        <div>
          <CardTitle className="text-base">Candidates</CardTitle>
          <CardDescription>
            Invitations are never sent automatically — send them when the panel and slots are settled.
          </CardDescription>
        </div>
        {canManage && interview.interviewees.length > 0 && (
          <Button
            variant="outline"
            size="sm"
            disabled={sendInvites.isPending}
            onClick={() => sendInvites.mutate()}
          >
            {sendInvites.isPending ? (
              <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
            ) : (
              <Mail className="mr-1.5 h-4 w-4" />
            )}
            Send invitations
          </Button>
        )}
      </CardHeader>
      <CardContent className="p-0">
        {interview.interviewees.length === 0 ? (
          <div className="py-10">
            <EmptyState
              icon={ClipboardList}
              title="No candidates booked"
              description="Add applicants from the vacancy to interview them in this session."
            />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Candidate</TableHead>
                <TableHead className="w-[220px]">Slot</TableHead>
                <TableHead className="w-[150px]">Confirmed</TableHead>
                <TableHead className="w-[140px]">Attended</TableHead>
                <TableHead className="w-[210px]">Outcome</TableHead>
                <TableHead className="w-[150px]" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {interview.interviewees.map((row) => {
                const edit = slotEdits[row.id] ?? {
                  start: (row.slotStartTime ?? '').slice(0, 5),
                  end: (row.slotEndTime ?? '').slice(0, 5),
                };
                const isNoShow = row.candidateAttended === false;

                return (
                  <TableRow key={row.id}>
                    <TableCell>
                      <div className="font-medium">{row.candidateName}</div>
                      <div className="text-xs text-muted-foreground">
                        {row.applicationNumber}
                        {row.candidateEmail ? ` · ${row.candidateEmail}` : ''}
                      </div>
                    </TableCell>

                    <TableCell>
                      {canManage ? (
                        <div className="flex items-center gap-1">
                          <Input
                            type="time"
                            className="h-8 w-[92px]"
                            value={edit.start}
                            onChange={(e) =>
                              setSlotEdits((prev) => ({ ...prev, [row.id]: { ...edit, start: e.target.value } }))
                            }
                          />
                          <span className="text-muted-foreground">–</span>
                          <Input
                            type="time"
                            className="h-8 w-[92px]"
                            value={edit.end}
                            onChange={(e) =>
                              setSlotEdits((prev) => ({ ...prev, [row.id]: { ...edit, end: e.target.value } }))
                            }
                          />
                          {(edit.start !== (row.slotStartTime ?? '').slice(0, 5) ||
                            edit.end !== (row.slotEndTime ?? '').slice(0, 5)) && (
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() => saveSlot.mutate({ id: row.id, start: edit.start, end: edit.end })}
                            >
                              Save
                            </Button>
                          )}
                        </div>
                      ) : (
                        <span className="text-sm">
                          {row.slotStartTime ? `${row.slotStartTime.slice(0, 5)} – ${(row.slotEndTime ?? '').slice(0, 5)}` : 'Session time'}
                        </span>
                      )}
                    </TableCell>

                    <TableCell>
                      {row.confirmedAttendance ? (
                        <span className="flex items-center gap-1.5 text-sm">
                          <CheckCircle2 className="h-4 w-4 text-green-600" />
                          {formatDateTime(row.confirmationDate)}
                        </span>
                      ) : row.invitationSentDate ? (
                        <span className="text-sm text-muted-foreground">Awaiting reply</span>
                      ) : (
                        <span className="text-sm text-muted-foreground">Not invited</span>
                      )}
                    </TableCell>

                    <TableCell>
                      {row.candidateAttended === null || row.candidateAttended === undefined ? (
                        canScore || canManage ? (
                          <div className="flex gap-1">
                            <Button
                              variant="ghost"
                              size="icon"
                              aria-label="Mark attended"
                              onClick={() => attendance.mutate({ id: row.id, attended: true })}
                            >
                              <CheckCircle2 className="h-4 w-4 text-green-600" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon"
                              aria-label="Mark no-show"
                              onClick={() => attendance.mutate({ id: row.id, attended: false })}
                            >
                              <XCircle className="h-4 w-4 text-destructive" />
                            </Button>
                          </div>
                        ) : (
                          <span className="text-sm text-muted-foreground">—</span>
                        )
                      ) : (
                        <StatusBadge status={row.candidateAttended ? 'Attended' : 'NoShow'} />
                      )}
                    </TableCell>

                    <TableCell>
                      {isNoShow ? (
                        <span className="text-sm text-muted-foreground">
                          No verdict — did not attend
                        </span>
                      ) : canScore || canManage ? (
                        <Select
                          value={row.outcome ?? NO_OUTCOME}
                          onValueChange={(v) => v !== NO_OUTCOME && outcome.mutate({ id: row.id, value: v })}
                        >
                          <SelectTrigger className="h-8">
                            <SelectValue placeholder="Not recorded" />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value={NO_OUTCOME} disabled>
                              Not recorded
                            </SelectItem>
                            {INTERVIEW_OUTCOMES.map((o) => (
                              <SelectItem key={o} value={o}>
                                {humanizeEnum(o)}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      ) : row.outcome ? (
                        <StatusBadge status={row.outcome} />
                      ) : (
                        <span className="text-sm text-muted-foreground">Not recorded</span>
                      )}
                    </TableCell>

                    <TableCell>
                      <div className="flex items-center justify-end gap-1">
                        {(canScore || canManage) && (
                          <Button variant="outline" size="sm" asChild>
                            <Link
                              href={`/hr/recruitment/interviews/${interview.id}/score/${row.id}${
                                myPanelistId ? `?panelistId=${myPanelistId}` : ''
                              }`}
                            >
                              Scorecard
                            </Link>
                          </Button>
                        )}
                        {canManage && (
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label={`Remove ${row.candidateName}`}
                            onClick={() => removeCandidate.mutate(row.id)}
                          >
                            <Trash2 className="h-4 w-4 text-destructive" />
                          </Button>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}
