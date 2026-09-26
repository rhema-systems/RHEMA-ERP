'use client';

/**
 * The medical board a medical retirement rests on (round 5, lane K-II-a).
 *
 * ⚠ **The control for a column nothing could set.** `EmployeeSeparation.MedicalBoardId` went in with
 * the residue plan's G4 and no screen, endpoint or service ever wrote it — a medical retirement
 * could point at nobody's finding.
 *
 * ⚠ **Only a decided case recommending retirement can be linked**, unlike leave (which lets a request
 * name a board it is waiting on): here the link IS the evidence, accepted at submission in place of
 * the medical report, so the server checks it when it is made. A board can find somebody unfit for
 * their post and fit for redeployment — that finding does not carry a retirement.
 *
 * ⚠ **The bridge is one way.** Separation READS the board; everything about it — cases, members,
 * sittings, the finding — lives in the Medical module, under the Medical permissions. What is shown
 * here is the recommendation and who decided it, not the clinical findings.
 */

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery } from '@tanstack/react-query';
import { ExternalLink, Link2Off, Loader2, Stethoscope } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/components/ui/use-toast';
import { medicalBoardService } from '@/services/hr/medical-board.service';
import { separationService } from '@/services/hr/separation.service';
import {
  MEDICAL_BOARD_CASE_STATUS_LABEL,
  MEDICAL_BOARD_OUTCOME_LABEL,
  boardStatusLabel,
  caseFor,
  type MedicalBoard,
} from '@/types/hr/medical-board';
import type { SeparationDetail } from '@/types/hr/separation';

const day = (d?: string | null) => (d ? d.slice(0, 10) : '—');

/** Why a board cannot carry this retirement, or null when it can — the server's tests, in its order. */
function whyNot(board: MedicalBoard, employeeId: string): string | null {
  const c = caseFor(board, employeeId);
  if (!c) return 'No case about this employee.';
  if (c.status === 'Withdrawn') return 'Their case was withdrawn without a finding.';
  if (c.status !== 'Concluded') return 'Their case has not been decided yet.';
  if (!c.recommendsMedicalRetirement) return 'The finding does not recommend medical retirement.';
  return null;
}

export function SeparationMedicalBoardPanel({
  separation,
  onChanged,
}: {
  separation: SeparationDetail;
  onChanged: () => void | Promise<void>;
}) {
  const { toast } = useToast();
  const [picking, setPicking] = useState(false);

  const linkedId = separation.medicalBoardId ?? null;
  const canEdit = separation.status === 'Draft';

  const { data: board, isLoading: boardLoading, isError: boardUnreadable } = useQuery({
    queryKey: ['hr', 'medical-boards', linkedId],
    queryFn: () => medicalBoardService.getById(linkedId as string),
    enabled: !!linkedId,
    retry: false,
  });

  const { data: candidates, isLoading: candidatesLoading } = useQuery({
    queryKey: ['hr', 'medical-boards', 'for-employee', separation.employeeId],
    queryFn: () => medicalBoardService.list({ employeeId: separation.employeeId, pageSize: 50 }),
    enabled: picking && !!separation.employeeId,
  });

  const link = useMutation({
    mutationFn: (boardId: string | null) => separationService.linkMedicalBoard(separation.id, boardId),
    onSuccess: async (_res, boardId) => {
      setPicking(false);
      await onChanged();
      toast({
        title: boardId ? 'Board linked' : 'Board unlinked',
        description: boardId
          ? 'This medical retirement now names the finding it rests on.'
          : 'This separation no longer names a board.',
      });
    },
    onError: (e: any) =>
      toast({ title: 'Error', description: e?.message || 'The board could not be linked.', variant: 'destructive' }),
  });

  const ownCase = board ? caseFor(board, separation.employeeId) : undefined;

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
              <Button variant="ghost" size="sm" disabled={link.isPending} onClick={() => link.mutate(null)}>
                {link.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Link2Off className="mr-2 h-4 w-4" />}
                Unlink
              </Button>
            )}
          </div>
        )}
      </CardHeader>

      <CardContent className="space-y-3 text-sm">
        {!linkedId && (
          <p className="text-muted-foreground">
            No board is named. A medical retirement is submitted on a medical board&apos;s finding
            recommending it, linked here, or on the medical report attached under Documents.{' '}
            <Link href="/hr/medical/boards" className="underline underline-offset-2">
              Medical boards
            </Link>
          </p>
        )}

        {linkedId && boardLoading && (
          <p className="flex items-center gap-2 text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" /> Reading the board…
          </p>
        )}

        {/* ⚠ Named but unreadable — most often no medical permission. Not the same as "no board". */}
        {linkedId && boardUnreadable && (
          <p className="text-muted-foreground">
            A board is linked, but it could not be read from here — you may not have access to medical
            records. Submission still reads it server-side.
          </p>
        )}

        {board && (
          <div className="space-y-2">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-medium">{board.boardNumber}</span>
              <StatusBadge status={boardStatusLabel(board)} />
              {ownCase?.outcome && (
                <span className="text-muted-foreground">{MEDICAL_BOARD_OUTCOME_LABEL[ownCase.outcome]}</span>
              )}
              <Link
                href={`/hr/medical/boards/${board.id}`}
                className="inline-flex items-center gap-1 text-xs underline underline-offset-2"
              >
                Open <ExternalLink className="h-3 w-3" />
              </Link>
            </div>
            {ownCase && (
              <div className="grid gap-x-6 gap-y-1 text-xs text-muted-foreground sm:grid-cols-2">
                <span>Case decided {day(ownCase.concludedOn)}</span>
                <span>
                  Medical retirement {ownCase.recommendsMedicalRetirement ? 'recommended' : 'not recommended'}
                </span>
                {ownCase.decidedBy.length > 0 && (
                  <span className="sm:col-span-2">Decided by {ownCase.decidedBy.join(', ')}</span>
                )}
              </div>
            )}
            {ownCase?.recommendation && <p className="whitespace-pre-wrap">{ownCase.recommendation}</p>}
          </div>
        )}
      </CardContent>

      <Dialog open={picking} onOpenChange={setPicking}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Which board recommended this retirement?</DialogTitle>
            <DialogDescription>
              Boards with a case about {separation.employeeName}. Only a decided case that recommends
              medical retirement can carry it.
            </DialogDescription>
          </DialogHeader>

          <div className="max-h-80 space-y-2 overflow-y-auto">
            {candidatesLoading && (
              <p className="flex items-center gap-2 py-6 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" /> Loading…
              </p>
            )}
            {!candidatesLoading && (candidates?.items ?? []).length === 0 && (
              <p className="py-6 text-sm text-muted-foreground">
                No board has heard a case about this employee.{' '}
                <Link href="/hr/medical/boards" className="underline underline-offset-2">
                  Request one in the medical module
                </Link>
                .
              </p>
            )}
            {(candidates?.items ?? []).map((b) => {
              const c = caseFor(b, separation.employeeId);
              const reason = whyNot(b, separation.employeeId);
              return (
                <button
                  key={b.id}
                  type="button"
                  disabled={link.isPending || !!reason || b.id === linkedId}
                  onClick={() => link.mutate(b.id)}
                  className="flex w-full items-start justify-between gap-3 rounded-md border p-3 text-left text-sm hover:bg-muted disabled:cursor-not-allowed disabled:opacity-60"
                >
                  <span>
                    <span className="font-medium">{b.boardNumber}</span>
                    {c && (
                      <span className="mt-0.5 block text-xs text-muted-foreground">
                        {c.outcome ? MEDICAL_BOARD_OUTCOME_LABEL[c.outcome] : MEDICAL_BOARD_CASE_STATUS_LABEL[c.status]}
                        {c.concludedOn ? ` · decided ${day(c.concludedOn)}` : ''}
                      </span>
                    )}
                    {/* ⚠ Said before the pick, not after the server refuses it. */}
                    {reason && (
                      <span className="mt-0.5 block text-xs text-amber-700 dark:text-amber-300">{reason}</span>
                    )}
                  </span>
                  <span className="flex shrink-0 items-center gap-2">
                    {b.id === linkedId && <span className="text-xs">linked</span>}
                    <StatusBadge status={boardStatusLabel(b)} />
                  </span>
                </button>
              );
            })}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setPicking(false)} disabled={link.isPending}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
