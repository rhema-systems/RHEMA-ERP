'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Plus, Search, ListChecks } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Badge } from '@/components/ui/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import {
  DateField,
  SelectField,
  TextareaField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { onboardingPlanService, onboardingPlanTemplateService } from '@/services/hr/onboarding.service';
import type { OnboardingStatus } from '@/types/hr/onboarding';

const planSchema = z.object({
  employeeId: z.string().min(1, 'Choose the new hire'),
  templatePlanId: z.string().optional().or(z.literal('')),
  startDate: z.string().min(1, 'A start date is required'),
  targetCompletionDate: z.string().optional().or(z.literal('')),
  assignedBuddyId: z.string().optional().or(z.literal('')),
  onboardingCoordinatorId: z.string().optional().or(z.literal('')),
  notes: z.string().max(2000).optional().or(z.literal('')),
});
type PlanForm = z.infer<typeof planSchema>;

const emptyPlan: PlanForm = {
  employeeId: '',
  templatePlanId: '',
  startDate: new Date().toISOString().slice(0, 10),
  targetCompletionDate: '',
  assignedBuddyId: '',
  onboardingCoordinatorId: '',
  notes: '',
};

const SCOPES: { value: OnboardingStatus; label: string }[] = [
  { value: 'InProgress', label: 'In progress' },
  { value: 'NotStarted', label: 'Not started' },
  { value: 'Overdue', label: 'Overdue' },
  { value: 'Completed', label: 'Completed' },
];

const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);

/**
 * New hires' onboarding plans.
 *
 * There is no "all plans" read — the API is per-status — so the tabs each fetch their own list
 * rather than filtering one. In-progress leads because that is the working queue.
 */
export default function OnboardingPlansPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [scope, setScope] = useState<OnboardingStatus>('InProgress');
  const [search, setSearch] = useState('');
  const [createOpen, setCreateOpen] = useState(false);
  const [creating, setCreating] = useState(false);

  const { data: plans = [], isLoading } = useQuery({
    queryKey: ['hr', 'onboarding-plans', scope],
    queryFn: () => onboardingPlanService.getByStatus(scope),
  });

  const { data: templates = [] } = useQuery({
    queryKey: ['hr', 'onboarding-templates'],
    queryFn: () => onboardingPlanTemplateService.getAll(),
  });

  const form = useForm<PlanForm>({
    resolver: zodResolver(planSchema) as any,
    defaultValues: emptyPlan,
  });

  const handleCreate = form.handleSubmit(async (values) => {
    setCreating(true);
    try {
      const created = await onboardingPlanService.create({
        employeeId: values.employeeId,
        templatePlanId: blank(values.templatePlanId),
        startDate: values.startDate,
        targetCompletionDate: blank(values.targetCompletionDate),
        assignedBuddyId: blank(values.assignedBuddyId),
        onboardingCoordinatorId: blank(values.onboardingCoordinatorId),
        notes: blank(values.notes),
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'onboarding-plans'] });
      toast({
        title: 'Plan created',
        description: values.templatePlanId
          ? 'The template’s tasks have been copied onto it, each due from the start date.'
          : 'It has no tasks yet — add them on the plan.',
      });
      setCreateOpen(false);
      form.reset(emptyPlan);
      router.push(`/hr/orientation/onboarding/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create the plan.',
        variant: 'destructive',
      });
    } finally {
      setCreating(false);
    }
  });

  const term = search.trim().toLowerCase();
  const filtered = term
    ? plans.filter(
        (p) =>
          p.employeeName.toLowerCase().includes(term) ||
          p.employeeNumber.toLowerCase().includes(term),
      )
    : plans;

  const totalTasks = plans.reduce((sum, p) => sum + p.totalTasks, 0);
  const completedTasks = plans.reduce((sum, p) => sum + p.completedTasks, 0);
  const overdueTasks = plans.reduce((sum, p) => sum + p.overdueTasks, 0);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Onboarding Plans"
        description="A new hire’s checklist — tasks, owners, due dates and the assets they need on day one."
        backHref="/hr/orientation"
        actions={
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="mr-2 h-4 w-4" />
            New plan
          </Button>
        }
      />

      <MetricTiles
        tiles={[
          { label: 'Plans', value: plans.length, icon: ListChecks },
          { label: 'Tasks', value: totalTasks },
          {
            label: 'Completed',
            value: completedTasks,
            hint:
              totalTasks > 0
                ? `${Math.round((completedTasks / totalTasks) * 100)}% of tasks on these plans`
                : undefined,
            tone: 'success',
          },
          {
            label: 'Overdue tasks',
            value: overdueTasks,
            tone: overdueTasks > 0 ? 'danger' : 'default',
          },
        ]}
      />

      <Card>
        <CardContent className="flex flex-wrap items-center justify-between gap-3 p-4">
          <Tabs value={scope} onValueChange={(v) => setScope(v as OnboardingStatus)}>
            <TabsList>
              {SCOPES.map((s) => (
                <TabsTrigger key={s.value} value={s.value}>
                  {s.label}
                </TabsTrigger>
              ))}
            </TabsList>
          </Tabs>

          <div className="relative min-w-[240px] flex-1">
            <Search className="text-muted-foreground absolute left-2.5 top-2.5 h-4 w-4" />
            <Input
              placeholder="Search by name or employee number…"
              className="pl-8"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <p className="text-muted-foreground p-6 text-sm">Loading plans…</p>
          ) : filtered.length === 0 ? (
            <EmptyState
              icon={ListChecks}
              title="No plans here"
              description={
                plans.length === 0
                  ? `No onboarding plans are ${SCOPES.find((s) => s.value === scope)?.label.toLowerCase()}.`
                  : 'Try a different search.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>New hire</TableHead>
                  <TableHead>Starts</TableHead>
                  <TableHead>Target</TableHead>
                  <TableHead className="w-[180px]">Tasks</TableHead>
                  <TableHead>Overdue</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filtered.map((p) => {
                  const pct =
                    p.totalTasks > 0 ? Math.round((p.completedTasks / p.totalTasks) * 100) : 0;
                  return (
                    <TableRow key={p.id}>
                      <TableCell>
                        <Link
                          href={`/hr/orientation/onboarding/${p.id}`}
                          className="font-medium hover:underline"
                        >
                          {p.employeeName}
                        </Link>
                        <div className="text-muted-foreground text-xs">{p.employeeNumber}</div>
                      </TableCell>
                      <TableCell>{fmt(p.startDate)}</TableCell>
                      <TableCell>{fmt(p.targetCompletionDate)}</TableCell>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <Progress value={pct} className="h-2 w-20" />
                          <span className="text-muted-foreground text-xs tabular-nums">
                            {p.completedTasks}/{p.totalTasks}
                          </span>
                        </div>
                      </TableCell>
                      <TableCell>
                        {p.overdueTasks > 0 ? (
                          <Badge variant="destructive">{p.overdueTasks}</Badge>
                        ) : (
                          <span className="text-muted-foreground">—</span>
                        )}
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={p.statusName ?? p.status} />
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>New onboarding plan</DialogTitle>
            <DialogDescription>
              Choosing a template copies its tasks onto the plan, each due at the start date plus its
              own offset. The copy is taken now, so later edits to the template leave this plan alone.
            </DialogDescription>
          </DialogHeader>

          <div className="max-h-[60vh] space-y-4 overflow-y-auto py-2">
            <EmployeePickerField form={form} name="employeeId" label="New hire" required />
            <SelectField
              form={form}
              name="templatePlanId"
              label="Template"
              options={templates.map((t) => ({
                value: t.id,
                label: `${t.name}${t.isDefault ? ' (default)' : ''} — ${t.taskTemplateCount} tasks`,
              }))}
              allowEmpty
              emptyLabel="No template — start empty"
            />
            <FieldRow>
              <DateField form={form} name="startDate" label="Start date" required />
              <DateField form={form} name="targetCompletionDate" label="Target completion" />
            </FieldRow>
            <EmployeePickerField form={form} name="assignedBuddyId" label="Buddy" />
            <EmployeePickerField
              form={form}
              name="onboardingCoordinatorId"
              label="Coordinator"
            />
            <TextareaField form={form} name="notes" label="Notes" rows={2} />
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)} disabled={creating}>
              Cancel
            </Button>
            <Button onClick={handleCreate} disabled={creating}>
              {creating && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Create plan
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
