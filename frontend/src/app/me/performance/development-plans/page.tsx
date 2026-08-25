'use client';

/**
 * Area 25 slice 5 — my development plans (spec destination #9).
 *
 * Two scopes, both token-derived (`/mine` and `/my-team`): my own plans, and — for a
 * manager — the plans of my direct reports. The organisation-wide register stayed on the
 * desk (`/hr/performance/development-plans`, HR-gated), per D3: desk registers stay, the
 * employee-as-subject surfaces move.
 *
 * Creating here covers the two jobs this screen owns: a plan for MYSELF (born Active — you
 * do not need to agree with yourself) and a DRAFT for one of my reports, which becomes
 * theirs when it is activated. Anyone else's plan is the desk's business.
 */

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import Link from 'next/link';
import { GraduationCap, Plus, TriangleAlert } from 'lucide-react';
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
import { Progress } from '@/components/ui/progress';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { CycleSelect } from '@/components/hr/performance/CycleSelect';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { formatDate, humanizeEnum, today } from '@/lib/hr/attendance-format';
import { developmentPlanService } from '@/services/hr/development.service';
import { employeeService } from '@/services/hr/employee.service';

const SELF = 'self';

export default function MyDevelopmentPlansPage() {
  const [scope, setScope] = useState<'mine' | 'team'>('mine');
  const [createOpen, setCreateOpen] = useState(false);
  const { user } = useAuth();

  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [form, setForm] = useState({
    subject: SELF,
    cycleId: '',
    title: '',
    startDate: today(),
    endDate: '',
    overallNotes: '',
  });

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['me', 'development-plans', scope],
    queryFn: () =>
      scope === 'mine' ? developmentPlanService.getMine() : developmentPlanService.getMyTeam(),
    retry: false,
  });

  const { data: reports } = useQuery({
    queryKey: ['me', 'direct-reports'],
    queryFn: () => employeeService.getMyDirectReports(),
    enabled: createOpen,
    retry: false,
  });

  const create = useMutation({
    mutationFn: () => {
      const forSelf = form.subject === SELF;
      return developmentPlanService.create({
        employeeId: forSelf ? (user?.employeeId ?? '') : form.subject,
        appraisalCycleId: form.cycleId || null,
        title: form.title.trim(),
        startDate: form.startDate,
        endDate: form.endDate || null,
        // My own plan is live at once; a report's plan is a DRAFT until it is activated —
        // activating is what tells them the plan is theirs to work on.
        planStatus: forSelf ? 'Active' : 'Draft',
        overallNotes: form.overallNotes.trim() || null,
      });
    },
    onSuccess: (_r) => {
      toast({
        title: form.subject === SELF ? 'Plan created' : 'Draft plan created',
        description:
          form.subject === SELF
            ? 'Add the objectives you are working towards.'
            : 'Add objectives, then activate it to share it with them.',
      });
      setCreateOpen(false);
      setForm({ subject: SELF, cycleId: '', title: '', startDate: today(), endDate: '', overallNotes: '' });
      queryClient.invalidateQueries({ queryKey: ['me', 'development-plans'] });
    },
    onError: (e: Error) =>
      toast({ title: 'Could not create the plan', description: e.message, variant: 'destructive' }),
  });

  const rows = data ?? [];
  const canCreate = Boolean(form.title.trim() && form.startDate);

  return (
    <div className="space-y-6">
      <PageHeader
        title="My development plans"
        description="What you are working on becoming good at — and, for managers, your team's plans."
        backHref="/me"
        actions={
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="mr-2 h-4 w-4" />
            New plan
          </Button>
        }
      />

      <Tabs value={scope} onValueChange={(v) => setScope(v as typeof scope)}>
        <TabsList>
          <TabsTrigger value="mine">My plans</TabsTrigger>
          <TabsTrigger value="team">My team</TabsTrigger>
        </TabsList>
      </Tabs>

      <Card>
        <CardContent className="p-0">
          {isError ? (
            <EmptyState
              icon={TriangleAlert}
              title="Could not load development plans"
              description={
                (error as Error)?.message ??
                'Your account may not be linked to an employee record.'
              }
            />
          ) : isLoading ? (
            <div className="space-y-2 p-4">
              {[0, 1, 2].map((i) => (
                <Skeleton key={i} className="h-12 w-full" />
              ))}
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={GraduationCap}
              title="No development plans"
              description={
                scope === 'mine'
                  ? 'Create one for yourself, or a plan your manager activates for you appears here.'
                  : 'Plans you draft for your reports appear here.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Plan</TableHead>
                  {scope === 'team' && <TableHead>Employee</TableHead>}
                  <TableHead>Period</TableHead>
                  <TableHead>Objectives</TableHead>
                  <TableHead>Progress</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((plan) => (
                  <TableRow key={plan.id}>
                    <TableCell>
                      <div className="font-medium">{plan.title || 'Untitled plan'}</div>
                      {plan.cycleCode && (
                        <div className="text-xs text-muted-foreground">{plan.cycleCode}</div>
                      )}
                    </TableCell>
                    {scope === 'team' && (
                      <TableCell className="text-sm">{plan.employeeName}</TableCell>
                    )}
                    <TableCell className="text-sm text-muted-foreground">
                      {formatDate(plan.startDate)}
                      {plan.endDate ? ` – ${formatDate(plan.endDate)}` : ''}
                    </TableCell>
                    <TableCell className="text-sm tabular-nums">
                      {plan.completedObjectiveCount}/{plan.objectiveCount}
                    </TableCell>
                    <TableCell className="w-40">
                      <div className="flex items-center gap-2">
                        <Progress value={Number(plan.averageProgressPercent) || 0} className="h-2" />
                        <span className="w-10 text-right text-xs tabular-nums text-muted-foreground">
                          {Math.round(Number(plan.averageProgressPercent) || 0)}%
                        </span>
                      </div>
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={humanizeEnum(plan.planStatus)} />
                    </TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="sm" asChild>
                        <Link href={`/me/performance/development-plans/${plan.id}`}>Open</Link>
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New development plan</DialogTitle>
            <DialogDescription>
              A plan for yourself starts straight away. A plan for one of your reports is
              saved as a draft — activate it once the objectives are agreed.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Who is it for?</Label>
              <Select
                value={form.subject}
                onValueChange={(v) => setForm((p) => ({ ...p, subject: v }))}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={SELF}>Myself</SelectItem>
                  {(reports ?? []).map((r) => (
                    <SelectItem key={r.id} value={r.id}>
                      {r.firstName} {r.lastName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="dp-title">Title</Label>
              <Input
                id="dp-title"
                maxLength={200}
                value={form.title}
                onChange={(e) => setForm((p) => ({ ...p, title: e.target.value }))}
                placeholder="e.g. Step up to team lead"
              />
            </div>
            <div className="space-y-2">
              <Label>Appraisal cycle (optional)</Label>
              <CycleSelect
                value={form.cycleId}
                onChange={(id) => setForm((p) => ({ ...p, cycleId: id }))}
                standalone={false}
                allowAll
              />
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="dp-start">Starts</Label>
                <Input
                  id="dp-start"
                  type="date"
                  value={form.startDate}
                  onChange={(e) => setForm((p) => ({ ...p, startDate: e.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="dp-end">Ends (optional)</Label>
                <Input
                  id="dp-end"
                  type="date"
                  value={form.endDate}
                  onChange={(e) => setForm((p) => ({ ...p, endDate: e.target.value }))}
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="dp-notes">Notes</Label>
              <Textarea
                id="dp-notes"
                rows={3}
                maxLength={2000}
                value={form.overallNotes}
                onChange={(e) => setForm((p) => ({ ...p, overallNotes: e.target.value }))}
                placeholder="Context: what prompted the plan, and what good looks like at the end of it."
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => create.mutate()} disabled={!canCreate || create.isPending}>
              {create.isPending ? 'Creating…' : form.subject === SELF ? 'Create plan' : 'Create draft'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
