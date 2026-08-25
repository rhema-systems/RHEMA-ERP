'use client';

/**
 * Area 25 slice 4 — my leave planner (spec destination #3).
 *
 * Annual planning is collaborative: I draft and submit a plan; my manager (the workflow
 * assignee) can approve, reject, or suggest other dates; a suggestion comes back to ME to
 * accept or counter (respond-suggestion is the plan owner's act). The desk's org-wide
 * planner at /hr/leave/plans is a different, HR-gated read and stays where it is (D3).
 */

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
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
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { ArrowLeft, Ban, CalendarPlus, Loader2, Pencil, Send } from 'lucide-react';
import { useAuth } from '@/hooks/use-auth';
import { leavePlanService } from '@/services/hr/leave.service';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import { LEAVE_PLAN_STATUS_BADGE } from '@/components/me/leave/leave-status';
import type { LeavePlan } from '@/types/hr/leave-request';

const fmtDate = (d: string) =>
  new Date(d).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });

interface PlanFormState {
  id: string | null; // null = creating
  leaveTypeId: string;
  startDate: string;
  endDate: string;
  notes: string;
}

const emptyPlanForm: PlanFormState = {
  id: null,
  leaveTypeId: '',
  startDate: '',
  endDate: '',
  notes: '',
};

export default function MyLeavePlannerPage() {
  const { user } = useAuth();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const employeeId = user?.employeeId ?? '';
  const currentYear = new Date().getFullYear();
  const [year, setYear] = useState(currentYear);
  const [form, setForm] = useState<PlanFormState | null>(null);
  const [respondingTo, setRespondingTo] = useState<LeavePlan | null>(null);
  const [counterStart, setCounterStart] = useState('');
  const [counterEnd, setCounterEnd] = useState('');
  const [responseNotes, setResponseNotes] = useState('');

  const { data: plans, isLoading } = useQuery({
    queryKey: ['me', 'leave-plans', employeeId, year],
    queryFn: () => leavePlanService.getByEmployee(employeeId, year),
    enabled: !!employeeId,
  });

  const { data: leaveTypes } = useQuery({
    queryKey: ['hr', 'leave-types', 'active'],
    queryFn: () => leaveTypeService.getAll(true),
  });

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['me', 'leave-plans', employeeId] });

  const saveMutation = useMutation({
    mutationFn: async (f: PlanFormState) => {
      const payload = {
        employeeId,
        leaveTypeId: f.leaveTypeId,
        startDate: f.startDate,
        endDate: f.endDate,
        notes: f.notes || null,
        plannedBy: employeeId,
        year: new Date(f.startDate).getFullYear(),
      };
      return f.id ? leavePlanService.update(f.id, payload) : leavePlanService.create(payload);
    },
    onSuccess: async (_, f) => {
      toast({
        title: f.id ? 'Plan updated' : 'Plan saved',
        description: 'Submit it to send it to your manager.',
      });
      setForm(null);
      await refresh();
    },
    onError: (e: any) =>
      toast({
        title: 'Could not save the plan',
        description: e?.message || 'Something went wrong.',
        variant: 'destructive',
      }),
  });

  const submitMutation = useMutation({
    mutationFn: (id: string) => leavePlanService.submit(id),
    onSuccess: async () => {
      toast({ title: 'Submitted', description: 'Your plan went to your manager.' });
      await refresh();
    },
    onError: (e: any) =>
      toast({
        title: 'Could not submit',
        description: e?.message || 'The plan could not be submitted.',
        variant: 'destructive',
      }),
  });

  const cancelMutation = useMutation({
    mutationFn: (id: string) => leavePlanService.cancel(id),
    onSuccess: async () => {
      toast({ title: 'Cancelled', description: 'The plan was cancelled.' });
      await refresh();
    },
    onError: (e: any) =>
      toast({
        title: 'Could not cancel',
        description: e?.message || 'The plan could not be cancelled.',
        variant: 'destructive',
      }),
  });

  const respondMutation = useMutation({
    mutationFn: (accept: boolean) => {
      if (!respondingTo) return Promise.reject(new Error('No suggestion selected.'));
      return leavePlanService.respondToSuggestion(respondingTo.id, {
        accept,
        startDate: accept ? null : counterStart,
        endDate: accept ? null : counterEnd,
        notes: responseNotes || null,
      });
    },
    onSuccess: async (_, accept) => {
      toast({
        title: accept ? 'Suggestion accepted' : 'Counter-proposal sent',
        description: 'The plan went back for review.',
      });
      setRespondingTo(null);
      await refresh();
    },
    onError: (e: any) =>
      toast({
        title: 'Could not respond',
        description: e?.message || 'Your response could not be recorded.',
        variant: 'destructive',
      }),
  });

  const years = [currentYear + 1, currentYear, currentYear - 1];

  return (
    <div className="space-y-6">
      <div>
        <Button variant="ghost" size="sm" asChild className="-ml-2 mb-2">
          <Link href="/me/leave">
            <ArrowLeft className="mr-1 h-4 w-4" /> My Leave
          </Link>
        </Button>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-2xl font-bold tracking-tight">Leave planner</h1>
            <p className="text-sm text-muted-foreground">
              Sketch your leave for the year, submit it, and settle the dates with your
              manager before filing the real requests.
            </p>
          </div>
          <div className="flex items-center gap-2">
            <Select value={String(year)} onValueChange={(v) => setYear(Number(v))}>
              <SelectTrigger className="w-28">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {years.map((y) => (
                  <SelectItem key={y} value={String(y)}>
                    {y}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button onClick={() => setForm(emptyPlanForm)}>
              <CalendarPlus className="mr-2 h-4 w-4" /> Plan leave
            </Button>
          </div>
        </div>
      </div>

      {isLoading ? (
        <Skeleton className="h-40" />
      ) : plans?.length ? (
        <div className="space-y-2">
          {plans.map((p) => (
            <Card key={p.id}>
              <CardContent className="flex flex-wrap items-center justify-between gap-3 p-4 text-sm">
                <div className="min-w-0">
                  <div className="font-medium">{p.leaveTypeName}</div>
                  <div className="mt-0.5 text-xs text-muted-foreground">
                    {fmtDate(p.startDate)} – {fmtDate(p.endDate)}
                    {p.notes ? ` · ${p.notes}` : ''}
                  </div>
                  {p.status === 'ChangesSuggested' && p.suggestedStartDate && p.suggestedEndDate && (
                    <div className="mt-1 rounded-md border border-blue-200 bg-blue-50 px-2 py-1 text-xs text-blue-900 dark:border-blue-500/40 dark:bg-blue-950/40 dark:text-blue-200">
                      Your manager suggests {fmtDate(p.suggestedStartDate)} –{' '}
                      {fmtDate(p.suggestedEndDate)}
                      {p.managerSuggestionNotes ? ` — “${p.managerSuggestionNotes}”` : ''}
                    </div>
                  )}
                  {p.status === 'Rejected' && p.rejectionReason && (
                    <div className="mt-1 text-xs text-red-600">“{p.rejectionReason}”</div>
                  )}
                </div>
                <div className="flex shrink-0 items-center gap-2">
                  <Badge className={LEAVE_PLAN_STATUS_BADGE[p.status] ?? ''} variant="outline">
                    {p.status}
                  </Badge>
                  {p.status === 'Draft' && (
                    <>
                      <Button
                        variant="ghost"
                        size="sm"
                        title="Edit"
                        onClick={() =>
                          setForm({
                            id: p.id,
                            leaveTypeId: p.leaveTypeId,
                            startDate: p.startDate,
                            endDate: p.endDate,
                            notes: p.notes ?? '',
                          })
                        }
                      >
                        <Pencil className="h-4 w-4" />
                      </Button>
                      <Button
                        size="sm"
                        disabled={submitMutation.isPending}
                        onClick={() => submitMutation.mutate(p.id)}
                      >
                        <Send className="mr-1 h-4 w-4" /> Submit
                      </Button>
                    </>
                  )}
                  {p.status === 'ChangesSuggested' && (
                    <Button
                      size="sm"
                      onClick={() => {
                        setRespondingTo(p);
                        setCounterStart(p.startDate);
                        setCounterEnd(p.endDate);
                        setResponseNotes('');
                      }}
                    >
                      Respond
                    </Button>
                  )}
                  {['Draft', 'Submitted', 'ChangesSuggested'].includes(p.status) && (
                    <Button
                      variant="ghost"
                      size="sm"
                      title="Cancel plan"
                      disabled={cancelMutation.isPending}
                      onClick={() => cancelMutation.mutate(p.id)}
                    >
                      <Ban className="h-4 w-4" />
                    </Button>
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">
          No plans for {year} yet. Planning ahead helps your team cover for you — and
          approved plans make the real requests smoother.
        </p>
      )}

      {/* ── create / edit dialog ─────────────────────────────────────────── */}
      <Dialog open={!!form} onOpenChange={(open) => !open && setForm(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{form?.id ? 'Edit plan' : 'Plan leave'}</DialogTitle>
            <DialogDescription>
              A plan is an intention, not a request — days are only charged when you file
              the leave request itself.
            </DialogDescription>
          </DialogHeader>
          {form && (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label>Leave type</Label>
                <Select
                  value={form.leaveTypeId}
                  onValueChange={(v) => setForm({ ...form, leaveTypeId: v })}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Choose a leave type" />
                  </SelectTrigger>
                  <SelectContent>
                    {(leaveTypes ?? []).map((t) => (
                      <SelectItem key={t.id} value={t.id}>
                        {t.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>From</Label>
                  <Input
                    type="date"
                    value={form.startDate}
                    onChange={(e) => setForm({ ...form, startDate: e.target.value })}
                  />
                </div>
                <div className="space-y-2">
                  <Label>To</Label>
                  <Input
                    type="date"
                    value={form.endDate}
                    onChange={(e) => setForm({ ...form, endDate: e.target.value })}
                  />
                </div>
              </div>
              <div className="space-y-2">
                <Label>Notes</Label>
                <Textarea
                  rows={2}
                  value={form.notes}
                  onChange={(e) => setForm({ ...form, notes: e.target.value })}
                  placeholder="Anything your manager should know"
                />
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setForm(null)}>
              Close
            </Button>
            <Button
              disabled={
                !form ||
                !form.leaveTypeId ||
                !form.startDate ||
                !form.endDate ||
                form.endDate < form.startDate ||
                saveMutation.isPending
              }
              onClick={() => form && saveMutation.mutate(form)}
            >
              {saveMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {form?.id ? 'Save changes' : 'Save plan'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── respond-to-suggestion dialog ─────────────────────────────────── */}
      <Dialog open={!!respondingTo} onOpenChange={(open) => !open && setRespondingTo(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Your manager suggested different dates</DialogTitle>
            <DialogDescription>
              {respondingTo?.suggestedStartDate && respondingTo?.suggestedEndDate && (
                <>
                  Suggested: {fmtDate(respondingTo.suggestedStartDate)} –{' '}
                  {fmtDate(respondingTo.suggestedEndDate)}.
                </>
              )}{' '}
              Accept them, or counter with your own — either way the plan goes back for
              review.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Counter from</Label>
                <Input
                  type="date"
                  value={counterStart}
                  onChange={(e) => setCounterStart(e.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label>Counter to</Label>
                <Input
                  type="date"
                  value={counterEnd}
                  onChange={(e) => setCounterEnd(e.target.value)}
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Notes</Label>
              <Textarea
                rows={2}
                value={responseNotes}
                onChange={(e) => setResponseNotes(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter className="gap-2">
            <Button
              variant="outline"
              disabled={
                !counterStart || !counterEnd || counterEnd < counterStart || respondMutation.isPending
              }
              onClick={() => respondMutation.mutate(false)}
            >
              Counter with my dates
            </Button>
            <Button
              disabled={respondMutation.isPending}
              onClick={() => respondMutation.mutate(true)}
            >
              {respondMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Accept suggestion
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
