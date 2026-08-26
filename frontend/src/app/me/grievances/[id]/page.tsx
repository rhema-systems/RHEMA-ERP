'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, ArrowUp, Reply, UserPlus, Ban, CheckCircle2, Circle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { grievanceService } from '@/services/hr/grievance.service';
import { toast } from 'sonner';
import { GRIEVANCE_LADDER, SETTLED_GRIEVANCE_STATUSES } from '@/types/hr/grievance';

const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');
const levelLabel = (v: string) => GRIEVANCE_LADDER.find((l) => l.value === v)?.label ?? v;

/**
 * One grievance, shown as the ladder.
 *
 * The whole route is rendered, not just the rungs reached — a griever deciding whether to escalate
 * needs to see where it can still go, and FR-HR-181's value is the trail read as a sequence.
 *
 * Every action here is offered unconditionally where the status allows it and the server owns the
 * rest: escalation is refused to anyone but the griever, answering is refused to the griever and to
 * anyone not asked, and each refusal comes back as a 422 or 403 carrying its own reason. Deciding in
 * the client who the caller is would put a second, disagreeing copy of those rules on screen — and
 * this page is reachable by three different kinds of user.
 *
 * Area 25 slice 9: re-homed from /hr/grievances/[id] (D3) — the one-working-surface precedent.
 * Its three audiences (griever, named responder, HR) are ALL portal users; the desk register's
 * rows open this page. The assign dialog stays: it is HR's act, HR holds the employee-search
 * permission its picker needs, and anyone else is refused in words.
 */
export default function GrievanceDetailPage() {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();

  const [respondOpen, setRespondOpen] = useState(false);
  const [response, setResponse] = useState('');
  const [resolves, setResolves] = useState(false);
  const [assignOpen, setAssignOpen] = useState(false);
  const [assigneeId, setAssigneeId] = useState<string | null>(null);
  const [withdrawOpen, setWithdrawOpen] = useState(false);
  const [withdrawReason, setWithdrawReason] = useState('');

  const { data: g, isLoading, isError } = useQuery({
    queryKey: ['me', 'grievances', 'detail', id],
    queryFn: () => grievanceService.getById(id),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['me', 'grievances'] });

  const fail = (fallback: string) => (e: Error) => toast.error(e.message || fallback);

  const respondMutation = useMutation({
    mutationFn: () => grievanceService.respond(id, { response: response.trim(), resolvesGrievance: resolves }),
    onSuccess: () => {
      toast.success(
        resolves
          ? 'Recorded, and the grievance is resolved'
          : 'Response recorded — the employee will decide whether this settles the matter.',
      );
      setRespondOpen(false); setResponse(''); setResolves(false); refresh();
    },
    onError: fail('Could not record the response'),
  });

  const assignMutation = useMutation({
    // Narrowed rather than asserted: the picker's value is nullable until a selection is made, and
    // the button that calls this is disabled until then.
    mutationFn: (assignedToId: string) => grievanceService.assign(id, { assignedToId }),
    onSuccess: () => {
      toast.success('Assigned — they can now see and answer this grievance.');
      setAssignOpen(false); setAssigneeId(null); refresh();
    },
    onError: fail('Could not assign it'),
  });

  const escalateMutation = useMutation({
    mutationFn: () => grievanceService.escalate(id, {}),
    onSuccess: (updated) => {
      toast.success(`Escalated to ${levelLabel(updated.currentLevel)}`);
      refresh();
    },
    onError: fail('Could not escalate'),
  });

  const withdrawMutation = useMutation({
    mutationFn: () => grievanceService.withdraw(id, { reason: withdrawReason.trim() }),
    onSuccess: () => {
      toast.success('Grievance withdrawn');
      setWithdrawOpen(false); setWithdrawReason(''); refresh();
    },
    onError: fail('Could not withdraw it'),
  });

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
        title="Grievance not available"
        description="A grievance can be read by the person who raised it, by HR, and by anyone asked to answer it — and by nobody else."
      />
    );
  }

  const settled = SETTLED_GRIEVANCE_STATUSES.includes(g.status);
  const currentIndex = GRIEVANCE_LADDER.findIndex((l) => l.value === g.currentLevel);
  const stepFor = (level: string) => g.steps.find((s) => s.level === level);

  return (
    <div className="space-y-6">
      <PageHeader
        title={`${g.grievanceNumber} — ${g.subject}`}
        description={`Raised by ${g.employeeName} on ${new Date(g.filedDate).toLocaleDateString()}`}
        backHref="/me/grievances"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={g.statusName} />
            {g.awaitingResponse && <StatusBadge status="Awaiting Response" />}
          </div>
        }
      />

      {!settled && (
        <Card>
          <CardContent className="flex flex-wrap gap-2 p-4">
            <Button size="sm" onClick={() => setRespondOpen(true)}>
              <Reply className="mr-2 h-4 w-4" /> Record a response
            </Button>
            <Button size="sm" variant="outline" onClick={() => setAssignOpen(true)}>
              <UserPlus className="mr-2 h-4 w-4" /> Assign someone to answer
            </Button>
            <Button size="sm" variant="outline" onClick={() => escalateMutation.mutate()}>
              <ArrowUp className="mr-2 h-4 w-4" /> Escalate to the next level
            </Button>
            <Button size="sm" variant="ghost" onClick={() => setWithdrawOpen(true)}>
              <Ban className="mr-2 h-4 w-4" /> Withdraw
            </Button>
          </CardContent>
          <CardContent className="pt-0 text-xs text-muted-foreground">
            Escalating and withdrawing belong to the person who raised the grievance; recording a
            response belongs to HR or whoever has been asked to answer. Anything you are not entitled
            to do will say so.
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader><CardTitle>The grievance</CardTitle></CardHeader>
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
        </CardContent>
      </Card>

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
                    <span className="text-xs text-muted-foreground">
                      asked: {step.assignedToName}
                    </span>
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

      {/* Record a response */}
      <Dialog open={respondOpen} onOpenChange={setRespondOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Respond at {levelLabel(g.currentLevel)}</DialogTitle>
            <DialogDescription>
              Your answer is kept on the grievance and stays there if it is escalated further.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Your response *</Label>
              <Textarea rows={6} value={response} onChange={(e) => setResponse(e.target.value)} />
            </div>
            <div className="flex items-start justify-between gap-4">
              <div>
                <Label htmlFor="resolves">This settles the grievance</Label>
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

      {/* Assign a responder */}
      <Dialog open={assignOpen} onOpenChange={setAssignOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Who should answer at {levelLabel(g.currentLevel)}?</DialogTitle>
            <DialogDescription>
              Naming someone lets them see and answer this grievance. The system does not work out
              who holds a level — you choose the person.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Employee</Label>
            <EmployeePicker
              value={assigneeId}
              onChange={(v) => setAssigneeId(v)}
              placeholder="Search for the person who should answer…"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAssignOpen(false)}>Cancel</Button>
            <Button
              onClick={() => assigneeId && assignMutation.mutate(assigneeId)}
              disabled={!assigneeId || assignMutation.isPending}
            >
              {assignMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Assign
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Withdraw */}
      <Dialog open={withdrawOpen} onOpenChange={setWithdrawOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Withdraw this grievance</DialogTitle>
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
    </div>
  );
}
