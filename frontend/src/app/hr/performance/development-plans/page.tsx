'use client';

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
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { CycleSelect } from '@/components/hr/performance/CycleSelect';
import { useToast } from '@/hooks/use-toast';
import { formatDate, humanizeEnum, today } from '@/lib/hr/attendance-format';
import { developmentPlanService } from '@/services/hr/development.service';

/**
 * The organisation-wide development-plan register — HR's paged view, and the desk place to
 * draft a plan for ANY employee.
 *
 * Area 25 slice 5: the "my plans" / "my team" scopes moved to the self-service portal
 * (`/me/performance/development-plans`), which is also where the plan detail lives — the
 * subject updates their progress there, so the row links below cross into the portal
 * chrome deliberately rather than duplicating a 700-line working surface per world.
 */
export default function DevelopmentPlanRegisterPage() {
  const [createOpen, setCreateOpen] = useState(false);

  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [form, setForm] = useState({
    employeeId: null as string | null,
    cycleId: '',
    title: '',
    startDate: today(),
    endDate: '',
    overallNotes: '',
  });

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'development-plans', 'register'],
    queryFn: async () => (await developmentPlanService.getPaged(1, 100)).items,
    retry: false,
  });

  const create = useMutation({
    mutationFn: () =>
      developmentPlanService.create({
        employeeId: form.employeeId ?? '',
        appraisalCycleId: form.cycleId || null,
        title: form.title.trim(),
        startDate: form.startDate,
        endDate: form.endDate || null,
        // Draft on purpose: a plan the employee has not agreed to is not yet theirs to work on.
        planStatus: 'Draft',
        overallNotes: form.overallNotes.trim() || null,
      }),
    onSuccess: () => {
      toast({
        title: 'Draft plan created',
        description: 'Add objectives, then activate it to share it with the employee.',
      });
      setCreateOpen(false);
      setForm({
        employeeId: null,
        cycleId: '',
        title: '',
        startDate: today(),
        endDate: '',
        overallNotes: '',
      });
      queryClient.invalidateQueries({ queryKey: ['hr', 'development-plans'] });
    },
    onError: (e: Error) =>
      toast({ title: 'Could not create the plan', description: e.message, variant: 'destructive' }),
  });

  const rows = data ?? [];
  const canCreate = Boolean(form.employeeId && form.title.trim() && form.startDate);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Development plans"
        description="Every development plan in the organisation. Your own and your team's live in My Self-Service."
        backHref="/hr/performance"
        actions={
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="mr-2 h-4 w-4" />
            New plan
          </Button>
        }
      />

      <Card>
        <CardContent className="p-0">
          {isError ? (
            <EmptyState
              icon={TriangleAlert}
              title="Could not load development plans"
              description={
                (error as Error)?.message ?? 'The organisation-wide list is restricted to HR.'
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
              description="Create a plan to agree what someone will work on this cycle."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Plan</TableHead>
                  <TableHead>Employee</TableHead>
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
                    <TableCell className="text-sm">{plan.employeeName}</TableCell>
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
              Saved as a draft. Add the objectives first, then activate it — activating is what
              tells the employee the plan is theirs to work on.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Employee</Label>
              <EmployeePicker
                value={form.employeeId}
                onChange={(id) => setForm((p) => ({ ...p, employeeId: id }))}
              />
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
              {create.isPending ? 'Creating…' : 'Create draft'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
