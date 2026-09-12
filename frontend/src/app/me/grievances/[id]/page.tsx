'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Loader2, ArrowUp, Reply, Ban, CheckCircle2, Circle, Users, CalendarDays,
  Paperclip, Download, Scale, Handshake, EyeOff, Search,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { employeeRelationsService } from '@/services/hr/employee-relations.service';
import { toast } from 'sonner';
import {
  GRIEVANCE_LADDER, SETTLED_GRIEVANCE_STATUSES, ER_CASE_TYPE_OPTIONS,
} from '@/types/hr/employee-relations';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');
const fmtSize = (n: number) => (n < 1024 ? `${n} B` : n < 1048576 ? `${(n / 1024).toFixed(1)} KB` : `${(n / 1048576).toFixed(1)} MB`);
const levelLabel = (v: string) => GRIEVANCE_LADDER.find((l) => l.value === v)?.label ?? v;
const typeLabel = (v: string) => ER_CASE_TYPE_OPTIONS.find((t) => t.value === v)?.label ?? v;

/**
 * One employee-relations case, shown to the people it belongs to.
 *
 * The whole route is rendered, not just the rungs reached — somebody deciding whether to escalate
 * needs to see where it can still go, and FR-HR-181's value is the trail read as a sequence.
 *
 * ⚠ **Three audiences, and the client never guesses which one you are.** The griever, the person
 * named to answer, and HR all land here. Every action is offered where the status allows it and the
 * server owns the rest: escalating and withdrawing are refused to anyone but the griever, answering
 * is refused to the griever and to anyone not asked, and accepting the agreement is refused to
 * everyone but the employee. Each refusal comes back carrying its own reason. Deciding in the client
 * who the caller is would put a second, disagreeing copy of those rules on screen.
 *
 * ⚠ **Slice 11 removed the assign dialog.** Naming a responder is HR's act and the desk's case file
 * at `/hr/employee-relations/[id]` now carries it. It sat here only because, before slice 10, this
 * was the module's one working surface.
 *
 * ⚠ **What is absent here is absent from the payload, not hidden by this screen.** Cross-links
 * arrive empty for anyone but HR, and a conference's notes arrive null unless you are HR or its
 * chair. The screen renders the difference between "nothing was written" and "you may not read it"
 * rather than showing both as blank.
 */
export default function EmployeeRelationsCasePage() {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();

  const [respondOpen, setRespondOpen] = useState(false);
  const [response, setResponse] = useState('');
  const [resolves, setResolves] = useState(false);
  const [withdrawOpen, setWithdrawOpen] = useState(false);
  const [withdrawReason, setWithdrawReason] = useState('');
  const [acceptOpen, setAcceptOpen] = useState(false);
  const [acceptComment, setAcceptComment] = useState('');

  const { data: g, isLoading, isError } = useQuery({
    queryKey: ['me', 'grievances', 'detail', id],
    queryFn: () => employeeRelationsService.getById(id),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['me', 'grievances'] });
  const fail = (fallback: string) => (e: Error) => toast.error(e.message || fallback);

  const respondMutation = useMutation({
    mutationFn: () => employeeRelationsService.respond(id, {
      response: response.trim(), resolvesGrievance: resolves,
    }),
    onSuccess: () => {
      toast.success(resolves
        ? 'Recorded, and the case is resolved'
        : 'Response recorded — the employee decides whether this settles the matter.');
      setRespondOpen(false); setResponse(''); setResolves(false); refresh();
    },
    onError: fail('Could not record the response'),
  });

  const escalateMutation = useMutation({
    mutationFn: () => employeeRelationsService.escalate(id, {}),
    onSuccess: (updated) => { toast.success(`Escalated to ${levelLabel(updated.currentLevel)}`); refresh(); },
    onError: fail('Could not escalate'),
  });

  const withdrawMutation = useMutation({
    mutationFn: () => employeeRelationsService.withdraw(id, { reason: withdrawReason.trim() }),
    onSuccess: () => { toast.success('Withdrawn'); setWithdrawOpen(false); setWithdrawReason(''); refresh(); },
    onError: fail('Could not withdraw it'),
  });

  const acceptMutation = useMutation({
    mutationFn: () => employeeRelationsService.acceptAgreement(id, {
      comment: acceptComment.trim() || null,
    }),
    onSuccess: () => {
      toast.success('Agreement accepted — it is now on the record as settled between you.');
      setAcceptOpen(false); setAcceptComment(''); refresh();
    },
    onError: fail('Could not accept it'),
  });

  const download = async (documentId: string, fileName: string) => {
    try {
      const blob = await employeeRelationsService.downloadDocument(documentId);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName;
      a.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      toast.error((e as Error).message || 'Could not download it');
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-16">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !g) {
    return (
      <EmptyState
        title="Case not available"
        description="A case can be read by the person it belongs to, by HR, and by anyone asked to answer it — and by nobody else."
      />
    );
  }

  const settled = SETTLED_GRIEVANCE_STATUSES.includes(g.status);
  const currentIndex = GRIEVANCE_LADDER.findIndex((l) => l.value === g.currentLevel);
  const stepFor = (level: string) => g.steps.find((s) => s.level === level);
  const activeParties = g.parties.filter((p) => p.isActive);
  const representatives = activeParties.filter(
    (p) => p.role === 'Representative' || p.role === 'UnionRepresentative');
  const resolution = g.resolution;
  // The agreement is signed on paper, filed by HR, and then accepted by the employee — in that
  // order. Offering acceptance before the document exists would ask them to agree to nothing.
  const canAccept = !!resolution?.agreementSignedDate && !resolution.agreementAccepted;

  return (
    <div className="space-y-6">
      <PageHeader
        title={`${g.grievanceNumber} — ${g.subject}`}
        description={`${typeLabel(g.caseType)} · ${g.employeeName} · ${fmtDate(g.filedDate)}`}
        backHref="/me/grievances"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={g.statusName} />
            {g.awaitingResponse && <StatusBadge status="Awaiting Response" />}
          </div>
        }
      />

      {(!settled || canAccept) && (
        <Card>
          <CardContent className="flex flex-wrap gap-2 p-4">
            {!settled && (
              <>
                <Button size="sm" onClick={() => setRespondOpen(true)}>
                  <Reply className="mr-2 h-4 w-4" /> Record a response
                </Button>
                <Button size="sm" variant="outline" onClick={() => escalateMutation.mutate()}>
                  <ArrowUp className="mr-2 h-4 w-4" /> Escalate to the next level
                </Button>
                <Button size="sm" variant="ghost" onClick={() => setWithdrawOpen(true)}>
                  <Ban className="mr-2 h-4 w-4" /> Withdraw
                </Button>
              </>
            )}
            {/* Offered on a RESOLVED case, and deliberately: the signed agreement is written from
                the decision, so it necessarily arrives after it. */}
            {canAccept && (
              <Button size="sm" onClick={() => setAcceptOpen(true)}>
                <Handshake className="mr-2 h-4 w-4" /> Accept the signed agreement
              </Button>
            )}
          </CardContent>
          <CardContent className="pt-0 text-xs text-muted-foreground">
            Escalating, withdrawing and accepting the agreement belong to the person the case is
            about; recording a response belongs to HR or whoever has been asked to answer. Anything
            you are not entitled to do will say so.
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader><CardTitle>The statement</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          <p className="whitespace-pre-wrap text-sm">{g.statement}</p>
          {g.resolutionSummary && (
            <div className="rounded-md border border-green-600/40 p-3">
              <div className="text-xs uppercase tracking-wide text-muted-foreground">
                Resolved {fmtDateTime(g.resolvedDate)}
              </div>
              <p className="mt-1 whitespace-pre-wrap text-sm">{g.resolutionSummary}</p>
            </div>
          )}
          {g.withdrawalReason && (
            <div className="rounded-md border p-3">
              <div className="text-xs uppercase tracking-wide text-muted-foreground">
                Withdrawn {fmtDateTime(g.withdrawnDate)}
              </div>
              <p className="mt-1 whitespace-pre-wrap text-sm">{g.withdrawalReason}</p>
            </div>
          )}
          {g.closedDate && (
            <div className="rounded-md border p-3">
              <div className="text-xs uppercase tracking-wide text-muted-foreground">
                Closed without being resolved · {fmtDateTime(g.closedDate)}
              </div>
              <p className="mt-1 whitespace-pre-wrap text-sm">{g.closureReason}</p>
              <p className="mt-1 text-xs text-muted-foreground">
                The escalation route was exhausted at Board level.
              </p>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Who else is on the case — and, for the employee, who is speaking for them. */}
      {activeParties.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Users className="h-4 w-4" /> Who is on this case
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {representatives.length > 0 && (
              <div className="rounded-md border border-primary/40 p-3 text-sm">
                <div className="text-xs uppercase tracking-wide text-muted-foreground">
                  Speaking for {representatives[0].representsEmployeeName ?? 'a party'}
                </div>
                {representatives.map((r) => (
                  <div key={r.id} className="mt-1">
                    <span className="font-medium">{r.displayName}</span>
                    {r.unionName ? <span className="text-muted-foreground"> · {r.unionName}</span> : null}
                    {r.externalOrganisation ? (
                      <span className="text-muted-foreground"> · {r.externalOrganisation}</span>
                    ) : null}
                  </div>
                ))}
              </div>
            )}
            <ul className="space-y-1 text-sm">
              {activeParties.map((p) => (
                <li key={p.id} className="flex flex-wrap items-center gap-2">
                  <span className="font-medium">{p.displayName}</span>
                  <Badge variant="outline">{p.roleName}</Badge>
                  {p.externalOrganisation && (
                    <span className="text-xs text-muted-foreground">{p.externalOrganisation}</span>
                  )}
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader><CardTitle>Escalation route</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          {GRIEVANCE_LADDER.map((level, index) => {
            const step = stepFor(level.value);
            const isCurrent = index === currentIndex;
            const reached = step != null;

            return (
              <div
                key={level.value}
                className={`rounded-md border p-3 ${isCurrent ? 'border-primary' : reached ? '' : 'opacity-50'}`}
              >
                <div className="flex flex-wrap items-center gap-2">
                  {reached
                    ? <CheckCircle2 className="h-4 w-4 text-muted-foreground" />
                    : <Circle className="h-4 w-4 text-muted-foreground" />}
                  <span className="text-sm font-medium">{level.label}</span>
                  {isCurrent && <StatusBadge status="Current" />}
                  {step && <StatusBadge status={step.outcomeName} />}
                  {step?.assignedToName && (
                    <span className="text-xs text-muted-foreground">asked: {step.assignedToName}</span>
                  )}
                </div>

                {step?.response ? (
                  <div className="mt-2 space-y-1">
                    <p className="whitespace-pre-wrap text-sm">{step.response}</p>
                    <p className="text-xs text-muted-foreground">
                      {step.respondedByName ?? 'Unknown'} · {fmtDateTime(step.respondedDate)}
                    </p>
                  </div>
                ) : reached ? (
                  <p className="mt-2 text-xs text-muted-foreground">
                    Reached {fmtDateTime(step?.reachedDate)} — no response recorded yet.
                  </p>
                ) : (
                  <p className="mt-2 text-xs text-muted-foreground">Not reached.</p>
                )}
              </div>
            );
          })}
          <p className="text-xs text-muted-foreground">
            The Board is the final level. Each level&apos;s response is kept — escalating never
            replaces the answer below it.
          </p>
        </CardContent>
      </Card>

      {g.hrInterpretation && (
        <Card>
          <CardHeader><CardTitle>HR&apos;s reading of the case</CardTitle></CardHeader>
          <CardContent>
            <p className="whitespace-pre-wrap text-sm">{g.hrInterpretation}</p>
            <p className="mt-2 text-xs text-muted-foreground">
              {g.hrInterpretationByName} · {fmtDateTime(g.hrInterpretationDate)} — what the policy,
              the Conditions of Service or the collective agreement say about this. It is not the
              decision; every level above HR reads it on the way up.
            </p>
          </CardContent>
        </Card>
      )}

      {g.investigation && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Search className="h-4 w-4" /> Investigation
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <div className="flex flex-wrap gap-4 text-xs text-muted-foreground">
              <span>Investigator: {g.investigation.investigatorDisplayName}</span>
              <span>Started {fmtDate(g.investigation.startedDate)}</span>
              {g.investigation.isComplete
                ? <span>Completed {fmtDate(g.investigation.completedDate)}</span>
                : <span>Still open</span>}
            </div>
            {g.investigation.isComplete ? (
              <>
                <div>
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Findings</div>
                  <p className="mt-1 whitespace-pre-wrap">{g.investigation.findings}</p>
                </div>
                {g.investigation.recommendation && (
                  <div>
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Recommendation</div>
                    <p className="mt-1 whitespace-pre-wrap">{g.investigation.recommendation}</p>
                  </div>
                )}
              </>
            ) : (
              // An investigation is not "complete" until it has findings, so there is genuinely
              // nothing to show — say that rather than rendering an empty Findings heading.
              <p className="text-muted-foreground">
                The investigation is still under way. Its findings appear here once it is completed.
              </p>
            )}
          </CardContent>
        </Card>
      )}

      {g.conferences.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <CalendarDays className="h-4 w-4" /> Meetings
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {g.conferences.map((c) => (
              <div key={c.id} className="rounded-md border p-3 text-sm">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-medium">{c.conferenceTypeName}</span>
                  <StatusBadge status={c.statusName} />
                  <span className="text-xs text-muted-foreground">{fmtDateTime(c.scheduledFor)}</span>
                  {c.venue && <span className="text-xs text-muted-foreground">· {c.venue}</span>}
                </div>
                <p className="mt-1 text-xs text-muted-foreground">
                  Chaired by {c.chairDisplayName}
                  {c.unionName ? ` · with ${c.unionName}` : ''}
                </p>
                {c.purpose && <p className="mt-2 whitespace-pre-wrap">{c.purpose}</p>}

                {c.attendees.length > 0 && (
                  <p className="mt-2 text-xs text-muted-foreground">
                    Asked: {c.attendees.map((a) => a.displayName).join(', ')}
                  </p>
                )}

                {c.status === 'Held' && (
                  <div className="mt-2 space-y-1">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Outcome</div>
                    <p className="whitespace-pre-wrap">{c.outcome}</p>
                    {/*
                      ⚠ Withheld and absent are different facts, and this is the screen where the
                      distinction matters most: a mediation whose notes are restricted would
                      otherwise read as one nobody bothered to minute.
                    */}
                    {c.notesRedacted && (
                      <p className="flex items-center gap-1 text-xs text-muted-foreground">
                        <EyeOff className="h-3 w-3" />
                        The notes of this meeting are kept between HR and the chair.
                      </p>
                    )}
                    {!c.notesRedacted && c.notes && (
                      <>
                        <div className="text-xs uppercase tracking-wide text-muted-foreground">Notes</div>
                        <p className="whitespace-pre-wrap">{c.notes}</p>
                      </>
                    )}
                  </div>
                )}

                {c.status === 'Cancelled' && (
                  <p className="mt-2 text-xs text-muted-foreground">
                    Cancelled {fmtDate(c.cancelledDate)} — {c.cancellationReason}
                  </p>
                )}
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      {resolution && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2"><Scale className="h-4 w-4" /> The decision</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <div className="flex flex-wrap gap-4 text-xs text-muted-foreground">
              <span>Outcome: {resolution.outcomeName}</span>
              <span>Decided at {levelLabel(resolution.decidedAtLevel)}</span>
              <span>{fmtDate(resolution.decidedDate)}</span>
            </div>
            <div>
              <div className="text-xs uppercase tracking-wide text-muted-foreground">Decision</div>
              <p className="mt-1 whitespace-pre-wrap">{resolution.decision}</p>
            </div>
            {resolution.remedyOrUndertakings && (
              <div>
                <div className="text-xs uppercase tracking-wide text-muted-foreground">
                  What was agreed or undertaken
                </div>
                <p className="mt-1 whitespace-pre-wrap">{resolution.remedyOrUndertakings}</p>
              </div>
            )}

            <div className="rounded-md border p-3">
              <div className="text-xs uppercase tracking-wide text-muted-foreground">
                The signed agreement
              </div>
              {resolution.agreementSignedDate ? (
                <p className="mt-1">
                  Signed and filed {fmtDate(resolution.agreementSignedDate)}.{' '}
                  {resolution.agreementAccepted
                    ? `You accepted it on ${fmtDate(resolution.agreementAcceptedDate)}.`
                    : 'It is waiting for you to accept it.'}
                </p>
              ) : (
                <p className="mt-1 text-muted-foreground">
                  Not yet filed. The agreement is written from the decision, so it comes afterwards.
                </p>
              )}
              {resolution.agreementAcceptanceComment && (
                <p className="mt-1 text-xs text-muted-foreground">
                  Your note: &ldquo;{resolution.agreementAcceptanceComment}&rdquo;
                </p>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {g.documents.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Paperclip className="h-4 w-4" /> Documents
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {g.documents.map((d) => (
              <div key={d.id} className="flex items-center justify-between rounded-md border p-3 text-sm">
                <div>
                  <div className="font-medium">{d.fileName}</div>
                  <div className="text-xs text-muted-foreground">
                    {d.scopeName} · {fmtSize(d.fileSize)} · {fmtDate(d.uploadDate)}
                    {d.description ? ` · ${d.description}` : ''}
                  </div>
                </div>
                {/* Fetched with the bearer token — the file sits outside the web root and
                    `filePath` can never be used as an href. */}
                <Button variant="ghost" size="sm" onClick={() => download(d.id, d.fileName)}>
                  <Download className="h-4 w-4" />
                </Button>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      <Dialog open={respondOpen} onOpenChange={setRespondOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Respond at {levelLabel(g.currentLevel)}</DialogTitle>
            <DialogDescription>
              Your answer is kept on the case and stays there if it is escalated further.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Your response *</Label>
              <Textarea rows={6} value={response} onChange={(e) => setResponse(e.target.value)} />
            </div>
            <div className="flex items-start justify-between gap-4">
              <div>
                <Label htmlFor="resolves">This settles the case</Label>
                <p className="text-xs text-muted-foreground">
                  Leave this off if you are stating a position rather than closing the matter — the
                  employee then decides whether to take it further.
                </p>
              </div>
              <Switch id="resolves" checked={resolves} onCheckedChange={setResolves} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRespondOpen(false)}>Cancel</Button>
            <Button
              onClick={() => respondMutation.mutate()}
              disabled={response.trim().length < 10 || respondMutation.isPending}
            >
              {respondMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record response
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={withdrawOpen} onOpenChange={setWithdrawOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Withdraw this case</DialogTitle>
            <DialogDescription>
              It stays on record as withdrawn, with your reason. It cannot be reopened.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Reason *</Label>
            <Textarea rows={4} value={withdrawReason} onChange={(e) => setWithdrawReason(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setWithdrawOpen(false)}>Cancel</Button>
            <Button
              variant="destructive"
              onClick={() => withdrawMutation.mutate()}
              disabled={withdrawReason.trim().length < 5 || withdrawMutation.isPending}
            >
              {withdrawMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Withdraw
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={acceptOpen} onOpenChange={setAcceptOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Accept the signed agreement</DialogTitle>
            <DialogDescription>
              This records that you agree the case is settled on the terms above. Read the signed
              agreement on the Documents list first if you have not already.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Anything you want on the record (optional)</Label>
            <Textarea rows={3} value={acceptComment} onChange={(e) => setAcceptComment(e.target.value)} />
            {/*
              Acceptance is not an approval queued for somebody else — it is this employee's own act,
              which is why it is not on the workflow engine. Declining is simply not accepting; the
              ladder they already have is the remedy.
            */}
            <p className="text-xs text-muted-foreground">
              Only you can do this — not HR, and not a representative. If you do not agree, do not
              accept: the case is then simply not settled, and the escalation route is still open.
            </p>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAcceptOpen(false)}>Not yet</Button>
            <Button onClick={() => acceptMutation.mutate()} disabled={acceptMutation.isPending}>
              {acceptMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Accept
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
