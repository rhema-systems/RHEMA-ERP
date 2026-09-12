'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, FileSignature, Loader2, Scale } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { disciplineService, disciplineAppealService } from '@/services/hr/discipline.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');

/**
 * The SUBJECT's view of one disciplinary case (area 25 slice 9, built from nothing).
 *
 * The old /hr/discipline/mine rows dropped the subject into the desk case-management screen —
 * an 800-line HR working surface whose actions all 403 for them. This page is the natural-justice
 * read: `cases/{id}` answers the subject as well as HR (the dual arm area 9 built), and it
 * carries the allegation, the decision, the penalty, and the notices served — WITH their text,
 * added this slice, because acknowledging a notice you cannot read defeats the notice.
 *
 * Two acts belong to the subject alone and live here: acknowledging a notice (server-stamped,
 * refused for anyone else including HR) and filing an appeal within FR-HR-180's window (the
 * process clock says whether it is open — computed server-side, never re-derived from dates).
 */
export default function MyDisciplineCasePage() {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();
  const [appealReason, setAppealReason] = useState('');

  const { data: caseDetail, isLoading, isError } = useQuery({
    queryKey: ['me', 'discipline', 'cases', id],
    queryFn: () => disciplineService.getById(id),
    retry: false,
  });
  const { data: clock } = useQuery({
    queryKey: ['me', 'discipline', 'cases', id, 'clock'],
    queryFn: () => disciplineService.getProcessClock(id),
    enabled: !!caseDetail,
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['me', 'discipline'] });
  };

  const acknowledge = useMutation({
    mutationFn: (notificationId: string) => disciplineService.acknowledgeNotification(notificationId),
    onSuccess: () => {
      toast.success('Notice acknowledged — the date is on record.');
      invalidate();
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : 'Refused'),
  });

  const fileAppeal = useMutation({
    mutationFn: () =>
      disciplineAppealService.file(id, { caseId: id, reason: appealReason.trim() }),
    onSuccess: () => {
      toast.success('Appeal filed — it goes to an appeal officer from here.');
      setAppealReason('');
      invalidate();
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : 'Refused'),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  if (isError || !caseDetail) {
    return (
      <div className="space-y-6">
        <PageHeader title="Case" backHref="/me/discipline" />
        <Alert>
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>This case does not exist, or it is not yours to read.</AlertDescription>
        </Alert>
      </div>
    );
  }

  const notices = caseDetail.notifications ?? [];
  const appeal = caseDetail.appeal;
  const canAppeal =
    !appeal && !!clock?.decisionMade && !!clock?.appealFilingWindowOpen && !clock?.appealFiled;

  return (
    <div className="space-y-6">
      <PageHeader
        title={caseDetail.caseNumber}
        description={`${caseDetail.offenseName} · ${caseDetail.severityName}`}
        backHref="/me/discipline"
        actions={<StatusBadge status={caseDetail.statusName} />}
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">The allegation</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3 text-sm">
          <p>{caseDetail.incidentDescription}</p>
          <div className="text-muted-foreground grid gap-x-8 gap-y-1 sm:grid-cols-2">
            <span>Incident: {fmtDate(caseDetail.incidentDate)}</span>
            <span>Reported: {fmtDate(caseDetail.reportedDate)}</span>
          </div>
        </CardContent>
      </Card>

      {notices.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <FileSignature className="h-4 w-4" />
              Notices served on you
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {notices.map((n) => (
              <div key={n.id} className="rounded-md border p-3">
                <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
                  <span className="font-medium">{n.notificationTypeName}</span>
                  <span className="text-muted-foreground text-sm">
                    Sent {fmtDate(n.sentDate)}
                  </span>
                </div>
                <p className="text-sm">{n.content}</p>
                <div className="mt-3">
                  {n.isAcknowledged ? (
                    <Badge variant="secondary">
                      Acknowledged {fmtDate(n.acknowledgedDate)}
                    </Badge>
                  ) : (
                    <Button
                      size="sm"
                      onClick={() => acknowledge.mutate(n.id)}
                      disabled={acknowledge.isPending}
                    >
                      Acknowledge this notice
                    </Button>
                  )}
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      {caseDetail.actionTypeName && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">The decision</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <div className="flex flex-wrap items-center gap-2">
              <Badge>{caseDetail.actionTypeName}</Badge>
              <span className="text-muted-foreground">
                decided {fmtDate(caseDetail.decisionDate)}
              </span>
            </div>
            {caseDetail.actionDetails && <p>{caseDetail.actionDetails}</p>}
            {caseDetail.decisionRationale && (
              <div>
                <p className="text-muted-foreground">Rationale</p>
                <p>{caseDetail.decisionRationale}</p>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {(caseDetail.warning || caseDetail.suspension || caseDetail.fine || caseDetail.termination) && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">What it means for you</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            {caseDetail.warning && (
              <p>
                <span className="font-medium">{caseDetail.warning.warningTypeName}</span>
                {caseDetail.warning.warningExpiryDate &&
                  ` — on your record until ${fmtDate(caseDetail.warning.warningExpiryDate)}`}
                {caseDetail.warning.isExpired && ' (expired)'}
              </p>
            )}
            {caseDetail.suspension && (
              <p>
                <span className="font-medium">
                  Suspension {caseDetail.suspension.suspensionWithPay ? 'with' : 'without'} pay
                </span>
                {' — '}
                {fmtDate(caseDetail.suspension.suspensionStartDate)} to{' '}
                {fmtDate(caseDetail.suspension.suspensionEndDate)}
                {caseDetail.suspension.suspensionDays != null &&
                  ` (${caseDetail.suspension.suspensionDays} days)`}
              </p>
            )}
            {caseDetail.fine && (
              <p>
                <span className="font-medium">
                  Fine of {caseDetail.fine.fineAmount?.toLocaleString() ?? '—'}
                </span>
                {caseDetail.fine.fineDueDate && ` — due ${fmtDate(caseDetail.fine.fineDueDate)}`}
                {caseDetail.fine.outstandingBalance != null &&
                  ` · outstanding ${caseDetail.fine.outstandingBalance.toLocaleString()}`}
              </p>
            )}
            {caseDetail.termination && (
              <p>
                <span className="font-medium">{caseDetail.termination.typeName}</span>
              </p>
            )}
          </CardContent>
        </Card>
      )}

      {(appeal || canAppeal) && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Scale className="h-4 w-4" />
              Appeal
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            {appeal ? (
              <>
                <div className="flex flex-wrap items-center gap-2">
                  <StatusBadge status={appeal.appealStatusName} />
                  <span className="text-muted-foreground">filed {fmtDate(appeal.filedDate)}</span>
                </div>
                <p>{appeal.reason}</p>
                {appeal.hearingDate && (
                  <p className="text-muted-foreground">
                    Hearing {fmtDateTime(appeal.hearingDate)}
                    {appeal.hearingVenue ? ` · ${appeal.hearingVenue}` : ''}
                  </p>
                )}
                {appeal.appealOutcomeName && (
                  <div>
                    <p className="text-muted-foreground">Outcome</p>
                    <p>
                      {appeal.appealOutcomeName}
                      {appeal.appealOutcomeNotes ? ` — ${appeal.appealOutcomeNotes}` : ''}
                    </p>
                  </div>
                )}
              </>
            ) : (
              <>
                <p className="text-muted-foreground">
                  You may appeal this decision
                  {clock?.appealFilingClosesAt
                    ? ` until ${fmtDateTime(clock.appealFilingClosesAt)}`
                    : ''}
                  . Filing is your act alone — nobody can file on your behalf.
                </p>
                <div className="space-y-2">
                  <Label htmlFor="appeal-reason">Why the decision should be reconsidered</Label>
                  <Textarea
                    id="appeal-reason"
                    rows={4}
                    value={appealReason}
                    onChange={(e) => setAppealReason(e.target.value)}
                  />
                </div>
                <div className="flex justify-end">
                  <Button
                    disabled={appealReason.trim().length === 0 || fileAppeal.isPending}
                    onClick={() => fileAppeal.mutate()}
                  >
                    {fileAppeal.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                    File appeal
                  </Button>
                </div>
              </>
            )}
          </CardContent>
        </Card>
      )}

      {clock && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Where the process stands</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <p>
              Written query:{' '}
              {clock.queryIssued
                ? `issued ${fmtDate(clock.queryIssuedAt)}${
                    clock.queryAcknowledged
                      ? `, acknowledged ${fmtDate(clock.queryAcknowledgedAt)}`
                      : clock.queryResponseClosesAt
                        ? ` — your response window closes ${fmtDateTime(clock.queryResponseClosesAt)}`
                        : ''
                  }`
                : 'not yet issued'}
            </p>
            <p>Decision: {clock.decisionMade ? 'made' : 'not yet made'}</p>
            {clock.decisionMade && (
              <p>
                Appeal window:{' '}
                {clock.appealFiled
                  ? `appeal filed ${fmtDate(clock.appealFiledAt)}`
                  : clock.appealFilingWindowOpen
                    ? `open until ${fmtDateTime(clock.appealFilingClosesAt)}`
                    : 'closed'}
              </p>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
