'use client';

/**
 * The medical board a leave request rests on (residue plan G4, finding R-15b).
 *
 * ⚠ **This screen is the whole point of the link endpoint.** `PUT /api/Leaves/{id}/medical-board`
 * went in with G4 and I recorded the board arm of the evidence gate as closed — but nothing in the
 * product could call it, so a leave type with a board threshold could be submitted only by
 * attaching a paper report. The board half of the rule was unreachable. An endpoint is not a
 * feature until something a person can press reaches it.
 *
 * ⚠ **The bridge is one way.** Leave READS a board; it never creates, convenes or concludes one.
 * Those all live in the Medical module, and the "Ask for a board" control here is a link to that
 * module, not a shortcut around it.
 *
 * ⚠ **Linking is not satisfying.** A board that is only Requested or Convened can be linked — that
 * is the ordinary case, because a board is asked for before it sits. Only a *Concluded* board
 * satisfies the gate (see `LeaveService.EnsureMedicalEvidenceAsync`), so this panel says which of
 * the two it is instead of showing a link and leaving the reader to assume the rule is met.
 */

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Stethoscope, ExternalLink, Link2Off } from 'lucide-react';
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
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/components/ui/use-toast';
import { leaveService } from '@/services/hr/leave.service';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import { medicalBoardService } from '@/services/hr/medical-board.service';
import { MEDICAL_BOARD_OUTCOME_LABEL } from '@/types/hr/medical-board';
import type { MedicalBoard } from '@/types/hr/medical-board';
import type { LeaveRequest } from '@/types/hr/leave-request';

const day = (d?: string | null) => (d ? d.slice(0, 10) : '—');

export function MedicalBoardLinkPanel({
  request,
  canEdit,
  onChanged,
}: {
  request: LeaveRequest;
  /** The desk tier. The API is self-or-desk; this only hides what would 403. */
  canEdit: boolean;
  onChanged: () => void | Promise<void>;
}) {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [picking, setPicking] = useState(false);

  const linkedId = request.medicalBoardId ?? null;

  /**
   * ⚠ Read from the leave TYPE, not the request: the request DTO carries a bare board id and
   * nothing about the rule. Without this the panel could not say at what point a board is demanded,
   * and "link a board" with no number attached is advice nobody can act on.
   */
  const { data: leaveType } = useQuery({
    queryKey: ['hr', 'leave-types', request.leaveTypeId],
    queryFn: () => leaveTypeService.getById(request.leaveTypeId),
    enabled: !!request.leaveTypeId,
  });

  /**
   * ⚠ Fetched by id rather than expanded onto the request. The board belongs to another module and
   * another permission set — resolving it here keeps leave's DTO free of a copy that would go stale
   * the moment the board concludes.
   */
  const {
    data: board,
    isLoading: boardLoading,
    isError: boardUnreadable,
  } = useQuery({
    queryKey: ['hr', 'medical-boards', linkedId],
    queryFn: () => medicalBoardService.getById(linkedId as string),
    enabled: !!linkedId,
    retry: false,
  });

  // The employee's boards, loaded only when the picker opens. ⚠ Scoped to this employee because
  // the server refuses a board about somebody else — offering one would be offering a 400.
  const { data: candidates, isLoading: candidatesLoading } = useQuery({
    queryKey: ['hr', 'medical-boards', 'for-employee', request.employeeId],
    queryFn: () => medicalBoardService.list({ employeeId: request.employeeId, pageSize: 50 }),
    enabled: picking && !!request.employeeId,
  });

  const link = useMutation({
    mutationFn: (boardId: string | null) => leaveService.linkMedicalBoard(request.id, boardId),
    onSuccess: async (_res, boardId) => {
      setPicking(false);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-requests'] });
      await onChanged();
      toast({
        title: boardId ? 'Board linked' : 'Board unlinked',
        description: boardId
          ? 'The request now names the board it rests on.'
          : 'This request no longer names a board.',
      });
    },
    onError: (e: any) =>
      toast({
        title: 'Error',
        description: e?.message || 'The board could not be linked.',
        variant: 'destructive',
      }),
  });

  const gateArmed = !!leaveType?.requiresMedicalCertificate;
  const threshold = leaveType?.medicalBoardThresholdDays ?? null;
  const boardRuleLive = gateArmed && threshold != null;

  // Nothing to say: no board named and no rule that would ever ask for one.
  if (!linkedId && !boardRuleLive) return null;

  const concluded = board?.status === 'Concluded';

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <CardTitle className="flex items-center gap-2 text-base">
          <Stethoscope className="h-4 w-4" /> Medical board
        </CardTitle>
        {canEdit && (
          <div className="flex gap-2">
            <Button variant="outline" size="sm" onClick={() => setPicking(true)}>
              {linkedId ? 'Change' : 'Link a board'}
            </Button>
            {linkedId && (
              <Button
                variant="ghost"
                size="sm"
                disabled={link.isPending}
                onClick={() => link.mutate(null)}
              >
                {link.isPending ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Link2Off className="mr-2 h-4 w-4" />
                )}
                Unlink
              </Button>
            )}
          </div>
        )}
      </CardHeader>

      <CardContent className="space-y-3 text-sm">
        {/* What the rule actually is, in this leave type's own numbers. */}
        {boardRuleLive && (
          <p className="text-muted-foreground">
            {leaveType?.name} needs a medical board once it passes{' '}
            <span className="font-medium">{threshold} day(s)</span> in a year — counted across
            every such absence in the year, not this request alone. A board satisfies the rule only
            once it has <span className="font-medium">concluded</span>; its written recommendation,
            attached under Attachments, does too.
          </p>
        )}

        {/*
          ⚠ Both flags or nothing. The threshold is read only after RequiresMedicalCertificate is
          checked, so a type with a threshold and no certificate requirement enforces nothing at
          all. Said out loud, because the configuration looks armed and is not.
        */}
        {!gateArmed && threshold != null && (
          <p className="rounded-md border border-amber-300/60 bg-amber-50 p-2 text-xs dark:border-amber-900/60 dark:bg-amber-950/40">
            ⚠ This leave type sets a {threshold}-day board threshold but does not require medical
            evidence, so nothing is enforced. Switch on “requires medical certificate” on the leave
            type if the threshold is meant to bite.
          </p>
        )}

        {!linkedId && (
          <p>
            No board is named on this request.{' '}
            <Link href="/hr/medical/boards" className="underline underline-offset-2">
              Ask for one in the medical module
            </Link>
            , then link it here.
          </p>
        )}

        {linkedId && boardLoading && (
          <p className="flex items-center gap-2 text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" /> Reading the board…
          </p>
        )}

        {/*
          ⚠ A board is named but cannot be read — most often because the reader holds leave rights
          and not medical ones. Saying so beats rendering an empty panel that reads exactly like
          "no board", which is the opposite of the truth.
        */}
        {linkedId && boardUnreadable && (
          <p className="text-muted-foreground">
            A board is linked to this request, but it could not be read from here — you may not have
            access to medical records. The evidence rule still reads it server-side.
          </p>
        )}

        {board && (
          <div className="space-y-2">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-medium">{board.boardNumber}</span>
              <StatusBadge status={board.status} />
              {board.outcome && (
                <span className="text-muted-foreground">
                  {MEDICAL_BOARD_OUTCOME_LABEL[board.outcome] ?? board.outcome}
                </span>
              )}
              <Link
                href={`/hr/medical/boards/${board.id}`}
                className="inline-flex items-center gap-1 text-xs underline underline-offset-2"
              >
                Open <ExternalLink className="h-3 w-3" />
              </Link>
            </div>

            <div className="grid gap-x-6 gap-y-1 text-xs text-muted-foreground sm:grid-cols-2">
              <span>Requested {day(board.requestedOn)}</span>
              <span>Convened {day(board.convenedOn)}</span>
              <span>Concluded {day(board.concludedOn)}</span>
              <span>{board.members.length} member(s)</span>
            </div>

            {board.recommendation && (
              <p className="whitespace-pre-wrap">{board.recommendation}</p>
            )}
            {board.restrictions && (
              <p className="whitespace-pre-wrap text-muted-foreground">
                Restrictions: {board.restrictions}
              </p>
            )}

            {/*
              ⚠ The reason this panel exists rather than a plain board number. "Linked" and
              "satisfied" are different states, and a submission refused at the gate after somebody
              has linked a board reads as a bug unless the screen has already said which one this is.
            */}
            {boardRuleLive && (
              <p
                className={
                  concluded
                    ? 'rounded-md border border-emerald-300/60 bg-emerald-50 p-2 text-xs dark:border-emerald-900/60 dark:bg-emerald-950/40'
                    : 'rounded-md border border-amber-300/60 bg-amber-50 p-2 text-xs dark:border-amber-900/60 dark:bg-amber-950/40'
                }
              >
                {concluded
                  ? 'This board has reported, so it satisfies the board rule for this leave type.'
                  : board.status === 'Cancelled'
                    ? '⚠ This board was cancelled, so it satisfies nothing. Link the board that replaced it.'
                    : '⚠ This board has not reported yet. Submission will still be refused until it concludes, or until its recommendation is attached.'}
              </p>
            )}
          </div>
        )}
      </CardContent>

      <BoardPicker
        open={picking}
        onOpenChange={setPicking}
        loading={candidatesLoading}
        boards={candidates?.items ?? []}
        currentId={linkedId}
        busy={link.isPending}
        onPick={(b) => link.mutate(b.id)}
        employeeName={request.employeeName}
      />
    </Card>
  );
}

function BoardPicker({
  open,
  onOpenChange,
  loading,
  boards,
  currentId,
  busy,
  onPick,
  employeeName,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  loading: boolean;
  boards: MedicalBoard[];
  currentId: string | null;
  busy: boolean;
  onPick: (board: MedicalBoard) => void;
  employeeName: string;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Which board ruled on this absence?</DialogTitle>
          <DialogDescription>
            Boards held on {employeeName}. A board that has not reported yet can be linked — the
            request can say which board it is waiting on — but only a concluded one satisfies the
            evidence rule.
          </DialogDescription>
        </DialogHeader>

        <div className="max-h-80 space-y-2 overflow-y-auto">
          {loading && (
            <p className="flex items-center gap-2 py-6 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" /> Loading…
            </p>
          )}

          {!loading && boards.length === 0 && (
            <p className="py-6 text-sm text-muted-foreground">
              No board has been asked for on this employee.{' '}
              <Link href="/hr/medical/boards" className="underline underline-offset-2">
                Request one in the medical module
              </Link>
              .
            </p>
          )}

          {boards.map((b) => {
            // ⚠ The server refuses a cancelled board. Disabled rather than hidden, so somebody
            // looking for the board they remember can see what became of it.
            const cancelled = b.status === 'Cancelled';
            return (
              <button
                key={b.id}
                type="button"
                disabled={busy || cancelled || b.id === currentId}
                onClick={() => onPick(b)}
                className="flex w-full items-start justify-between gap-3 rounded-md border p-3 text-left text-sm hover:bg-muted disabled:cursor-not-allowed disabled:opacity-60"
              >
                <span>
                  <span className="font-medium">{b.boardNumber}</span>
                  <span className="mt-0.5 block text-xs text-muted-foreground">
                    Requested {day(b.requestedOn)}
                    {b.concludedOn ? ` · concluded ${day(b.concludedOn)}` : ''}
                    {b.outcome ? ` · ${MEDICAL_BOARD_OUTCOME_LABEL[b.outcome] ?? b.outcome}` : ''}
                  </span>
                  <span className="mt-0.5 block text-xs text-muted-foreground">{b.reason}</span>
                </span>
                <span className="flex shrink-0 items-center gap-2">
                  {b.id === currentId && <span className="text-xs">linked</span>}
                  <StatusBadge status={b.status} />
                </span>
              </button>
            );
          })}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
            Close
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
