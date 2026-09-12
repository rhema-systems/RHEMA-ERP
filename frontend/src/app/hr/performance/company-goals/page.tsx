'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import {
  Building2,
  Eye,
  EyeOff,
  Layers,
  Loader2,
  MoreHorizontal,
  Pencil,
  Plus,
  Search,
  Target,
  Trash2,
  Users,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { CycleSelect } from '@/components/hr/performance/CycleSelect';
import { GoalFormDialog } from '@/components/hr/performance/GoalFormDialog';
import {
  TextField,
  NumberField,
  DateField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { companyGoalService, strategicGoalService } from '@/services/hr/goals.service';
import { useDebounce } from '@/hooks/use-debounce';
import { formatDate } from '@/lib/hr/attendance-format';
import { GOAL_PRIORITY_OPTIONS } from '@/types/hr/goals';
import type { CompanyGoal, CompanyGoalListItem, GoalPriority } from '@/types/hr/goals';

/**
 * Company goals — one appraisal cycle's organisation-wide objectives, and the top of the
 * cascade managers align their unit goals to.
 *
 * Visibility is the lever that matters here. A visible goal appears to everyone choosing an
 * alignment for their own goal; an invisible one is HR's to cascade deliberately. It does not
 * hide anything already aligned, so turning it off mid-cycle stops new alignments rather than
 * unpicking existing ones.
 *
 * The counts on each row are cascade depth: how many unit goals took this up, and how many
 * employee goals point at it directly.
 */
const PAGE_SIZE = 12;

const ANY = '__any__';

const companyGoalSchema = z.object({
  strategicGoalId: z.string().optional(),
  title: z.string().min(1, 'Required').max(300),
  description: z.string().max(2000).optional(),
  successCriteria: z.string().max(1000).optional(),
  priority: z.string().min(1, 'Required'),
  targetValue: z.coerce.number().min(0).optional(),
  unit: z.string().max(50).optional(),
  dueDate: z.string().optional(),
  isVisible: z.boolean(),
});

type CompanyGoalForm = z.input<typeof companyGoalSchema>;

const emptyForm: CompanyGoalForm = {
  strategicGoalId: '',
  title: '',
  description: '',
  successCriteria: '',
  priority: 'High',
  targetValue: undefined,
  unit: '',
  dueDate: '',
  isVisible: true,
};

const PRIORITY_VARIANT: Record<GoalPriority, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Critical: 'destructive',
  High: 'default',
  Medium: 'secondary',
  Low: 'outline',
};

export default function CompanyGoalsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [cycleId, setCycleId] = useState('');
  const [search, setSearch] = useState('');
  const [priority, setPriority] = useState<string>(ANY);
  const [visibility, setVisibility] = useState<string>(ANY);
  const [page, setPage] = useState(1);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<CompanyGoal | null>(null);
  const [pendingDelete, setPendingDelete] = useState<CompanyGoalListItem | null>(null);

  const debouncedSearch = useDebounce(search, 350);

  const listKey = [
    'hr',
    'company-goals',
    cycleId,
    debouncedSearch,
    priority,
    visibility,
    page,
  ] as const;

  const { data: metrics } = useQuery({
    queryKey: ['hr', 'company-goals', cycleId, 'metrics'],
    queryFn: () => companyGoalService.getDashboardMetrics(cycleId),
    enabled: !!cycleId,
  });

  const { data, isLoading, isError, error } = useQuery({
    queryKey: listKey,
    queryFn: () =>
      companyGoalService.getDashboardPaged({
        cycleId: cycleId,
        search: debouncedSearch || undefined,
        priority: priority === ANY ? null : (priority as GoalPriority),
        isVisible: visibility === ANY ? null : visibility === 'visible',
        pageNumber: page,
        pageSize: PAGE_SIZE,
      }),
    enabled: !!cycleId,
  });

  const { data: strategicGoals } = useQuery({
    queryKey: ['hr', 'strategic-goals', 'active'],
    queryFn: () => strategicGoalService.getAll(true),
  });

  const strategicOptions = useMemo(
    () =>
      (strategicGoals ?? []).map((g) => ({
        value: g.id,
        label: `${g.title} (${g.startYear}–${g.endYear})`,
      })),
    [strategicGoals],
  );

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'company-goals'] });
    // Unit goal screens show which company goal each is linked to.
    await queryClient.invalidateQueries({ queryKey: ['hr', 'unit-goals'] });
  };

  const failed = (verb: string) => (e: any) =>
    toast({
      title: 'Error',
      description: e?.message || `Failed to ${verb} the company goal.`,
      variant: 'destructive',
    });

  const saveMutation = useMutation({
    mutationFn: async (values: CompanyGoalForm) => {
      const v = companyGoalSchema.parse(values);
      const payload = {
        appraisalCycleId: cycleId,
        strategicGoalId: v.strategicGoalId || null,
        title: v.title,
        description: v.description || null,
        successCriteria: v.successCriteria || null,
        priority: v.priority as GoalPriority,
        targetValue: v.targetValue ?? null,
        unit: v.unit || null,
        dueDate: v.dueDate || null,
        isVisible: v.isVisible,
      };
      return editing
        ? companyGoalService.update(editing.id, { id: editing.id, ...payload })
        : companyGoalService.create(payload);
    },
    onSuccess: async () => {
      await invalidate();
      toast({ title: 'Saved', description: `Company goal ${editing ? 'updated' : 'created'}.` });
      setDialogOpen(false);
      setEditing(null);
    },
    onError: failed('save'),
  });

  const visibilityMutation = useMutation({
    mutationFn: (row: CompanyGoalListItem) =>
      companyGoalService.setVisibility(row.id, !row.isVisible),
    onSuccess: invalidate,
    onError: failed('update'),
  });

  const deleteMutation = useMutation({
    mutationFn: (row: CompanyGoalListItem) => companyGoalService.remove(row.id),
    onSuccess: async () => {
      await invalidate();
      toast({ title: 'Deleted', description: 'Company goal removed.' });
      setPendingDelete(null);
    },
    onError: failed('delete'),
  });

  const openCreate = () => {
    setEditing(null);
    setDialogOpen(true);
  };

  // The list is a projection, so an edit fetches the full goal before filling the form —
  // the row carries only a 200-char description preview.
  const openEdit = async (row: CompanyGoalListItem) => {
    try {
      const full = await companyGoalService.getById(row.id);
      setEditing(full);
      setDialogOpen(true);
    } catch (e: any) {
      failed('load')(e);
    }
  };

  const formValues: CompanyGoalForm = editing
    ? {
        strategicGoalId: editing.strategicGoalId ?? '',
        title: editing.title,
        description: editing.description ?? '',
        successCriteria: editing.successCriteria ?? '',
        priority: editing.priority,
        targetValue: editing.targetValue ?? undefined,
        unit: editing.unit ?? '',
        dueDate: editing.dueDate ? editing.dueDate.slice(0, 10) : '',
        isVisible: editing.isVisible,
      }
    : emptyForm;

  const rows = data?.items ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Company Goals"
        description="A cycle's organisation-wide objectives — what unit and employee goals align to."
        backHref="/hr/performance"
        actions={
          <Button onClick={openCreate} disabled={!cycleId}>
            <Plus className="mr-2 h-4 w-4" />
            New company goal
          </Button>
        }
      />

      <CycleSelect
        value={cycleId}
        onChange={(id) => {
          setCycleId(id);
          setPage(1);
        }}
      />

      {!cycleId ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={Target}
              title="No appraisal cycle selected"
              description="Company goals belong to a cycle. Open one under Performance setup if none exist yet."
            />
          </CardContent>
        </Card>
      ) : (
        <>
          <MetricTiles
            tiles={[
              {
                label: 'Company goals',
                value: metrics?.totalGoals ?? '—',
                hint: 'In this cycle',
                icon: Target,
              },
              {
                label: 'Unit goals cascaded',
                value: metrics?.totalUnitGoalsCascaded ?? '—',
                hint: 'Units that took one up',
                icon: Layers,
              },
              {
                label: 'Employee goals aligned',
                value: metrics?.totalEmployeeGoalsAligned ?? '—',
                hint: 'Pointing straight at a company goal',
                icon: Users,
              },
              {
                label: 'Visible to employees',
                value: metrics ? `${metrics.visibleGoalPercent}%` : '—',
                hint: metrics
                  ? `${metrics.visibleGoalsCount} of ${metrics.totalGoals} can be aligned to`
                  : undefined,
                icon: Eye,
                // Nothing to align to is a setup problem worth noticing, not a neutral zero.
                tone: metrics && metrics.totalGoals > 0 && metrics.visibleGoalsCount === 0
                  ? 'warning'
                  : 'default',
              },
            ]}
          />

          <Card>
            <CardContent className="flex flex-wrap items-end gap-4 p-4">
              <div className="min-w-[240px] flex-1 space-y-2">
                <Label htmlFor="search">Search</Label>
                <div className="relative">
                  <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                  <Input
                    id="search"
                    className="pl-8"
                    placeholder="Title or description…"
                    value={search}
                    onChange={(e) => {
                      setSearch(e.target.value);
                      setPage(1);
                    }}
                  />
                </div>
              </div>
              <div className="w-[180px] space-y-2">
                <Label htmlFor="priority">Priority</Label>
                <Select
                  value={priority}
                  onValueChange={(v) => {
                    setPriority(v);
                    setPage(1);
                  }}
                >
                  <SelectTrigger id="priority">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ANY}>Any priority</SelectItem>
                    {GOAL_PRIORITY_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>
                        {o.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="w-[180px] space-y-2">
                <Label htmlFor="visibility">Visibility</Label>
                <Select
                  value={visibility}
                  onValueChange={(v) => {
                    setVisibility(v);
                    setPage(1);
                  }}
                >
                  <SelectTrigger id="visibility">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ANY}>Any visibility</SelectItem>
                    <SelectItem value="visible">Visible</SelectItem>
                    <SelectItem value="hidden">Hidden</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-0">
              {isLoading ? (
                <div className="flex items-center justify-center py-12">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : isError ? (
                <EmptyState
                  title="Could not load company goals"
                  description={(error as any)?.message || 'Please try again.'}
                />
              ) : rows.length === 0 ? (
                <EmptyState
                  icon={Building2}
                  title="No company goals"
                  description={
                    search || priority !== ANY || visibility !== ANY
                      ? 'No goal matches these filters.'
                      : 'Set the objectives this cycle is working towards.'
                  }
                  action={
                    <Button size="sm" variant="outline" onClick={openCreate}>
                      <Plus className="mr-2 h-4 w-4" />
                      New company goal
                    </Button>
                  }
                />
              ) : (
                <div className="overflow-x-auto">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Goal</TableHead>
                        <TableHead>Priority</TableHead>
                        <TableHead className="text-right">Target</TableHead>
                        <TableHead>Due</TableHead>
                        <TableHead className="text-right">Unit goals</TableHead>
                        <TableHead className="text-right">Employee goals</TableHead>
                        <TableHead>Visibility</TableHead>
                        <TableHead className="w-[60px]" />
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {rows.map((row) => (
                        <TableRow key={row.id}>
                          <TableCell>
                            <Link
                              href={`/hr/performance/company-goals/${row.id}`}
                              className="font-medium hover:underline"
                            >
                              {row.title}
                            </Link>
                            {row.descriptionPreview && (
                              <p className="line-clamp-1 text-sm text-muted-foreground">
                                {row.descriptionPreview}
                              </p>
                            )}
                          </TableCell>
                          <TableCell>
                            <Badge variant={PRIORITY_VARIANT[row.priority]}>{row.priority}</Badge>
                          </TableCell>
                          <TableCell className="text-right tabular-nums">
                            {row.targetValue == null
                              ? '—'
                              : `${row.targetValue}${row.unit ? ` ${row.unit}` : ''}`}
                          </TableCell>
                          <TableCell>{formatDate(row.dueDate)}</TableCell>
                          <TableCell className="text-right tabular-nums">
                            {row.unitGoalCount}
                          </TableCell>
                          <TableCell className="text-right tabular-nums">
                            {row.employeeGoalCount}
                          </TableCell>
                          <TableCell>
                            {row.isVisible ? (
                              <Badge variant="outline" className="gap-1">
                                <Eye className="h-3 w-3" /> Visible
                              </Badge>
                            ) : (
                              <Badge variant="secondary" className="gap-1">
                                <EyeOff className="h-3 w-3" /> Hidden
                              </Badge>
                            )}
                          </TableCell>
                          <TableCell>
                            <DropdownMenu>
                              <DropdownMenuTrigger asChild>
                                <Button variant="ghost" size="icon" className="h-8 w-8">
                                  <MoreHorizontal className="h-4 w-4" />
                                  <span className="sr-only">Actions</span>
                                </Button>
                              </DropdownMenuTrigger>
                              <DropdownMenuContent align="end">
                                <DropdownMenuItem onClick={() => openEdit(row)}>
                                  <Pencil className="mr-2 h-4 w-4" />
                                  Edit
                                </DropdownMenuItem>
                                <DropdownMenuItem
                                  onClick={() => visibilityMutation.mutate(row)}
                                >
                                  {row.isVisible ? (
                                    <>
                                      <EyeOff className="mr-2 h-4 w-4" />
                                      Hide from employees
                                    </>
                                  ) : (
                                    <>
                                      <Eye className="mr-2 h-4 w-4" />
                                      Make visible
                                    </>
                                  )}
                                </DropdownMenuItem>
                                <DropdownMenuSeparator />
                                <DropdownMenuItem
                                  className="text-red-600"
                                  onClick={() => setPendingDelete(row)}
                                >
                                  <Trash2 className="mr-2 h-4 w-4" />
                                  Delete
                                </DropdownMenuItem>
                              </DropdownMenuContent>
                            </DropdownMenu>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>

          {data && data.totalPages > 1 && (
            <div className="flex items-center justify-between">
              <p className="text-sm text-muted-foreground">
                Page {data.page} of {data.totalPages} · {data.totalCount} goals
              </p>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!data.hasPrevious}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!data.hasNext}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </>
      )}

      <GoalFormDialog<CompanyGoalForm>
        open={dialogOpen}
        onOpenChange={(open) => {
          setDialogOpen(open);
          if (!open) setEditing(null);
        }}
        editingId={editing?.id ?? null}
        title="company goal"
        hint="Applies to the whole organisation for this cycle."
        schema={companyGoalSchema as any}
        values={formValues}
        submitting={saveMutation.isPending}
        onSubmit={async (values) => saveMutation.mutateAsync(values)}
        renderFields={(form) => (
          <>
            <TextField
              form={form}
              name="title"
              label="Title"
              required
              placeholder="e.g. Grow recurring revenue by 15%"
            />
            <SelectField
              form={form}
              name="strategicGoalId"
              label="Strategic goal"
              options={strategicOptions}
              allowEmpty
              emptyLabel="Not linked to a strategy"
            />
            <TextareaField form={form} name="description" label="Description" rows={3} />
            <TextareaField
              form={form}
              name="successCriteria"
              label="Success criteria"
              rows={2}
              placeholder="How the organisation will judge this at year end."
            />
            <FieldRow>
              <SelectField
                form={form}
                name="priority"
                label="Priority"
                required
                options={GOAL_PRIORITY_OPTIONS}
              />
              <DateField form={form} name="dueDate" label="Due date" />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="targetValue" label="Target value" step="0.01" />
              <TextField form={form} name="unit" label="Unit" placeholder="%, GHS, customers…" />
            </FieldRow>
            <SwitchField
              form={form}
              name="isVisible"
              label="Visible to employees"
              description="Visible goals can be chosen as an alignment. Hiding one stops new alignments; it does not unpick existing ones."
            />
          </>
        )}
      />

      <ConfirmationDialog
        open={pendingDelete !== null}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title="Delete company goal?"
        description={
          pendingDelete
            ? `“${pendingDelete.title}” has ${pendingDelete.unitGoalCount} unit goal(s) and ${pendingDelete.employeeGoalCount} employee goal(s) aligned to it. They stay, but lose the link.`
            : ''
        }
        confirmText="Delete"
        variant="destructive"
        isLoading={deleteMutation.isPending}
        onConfirm={async () => {
          if (pendingDelete) await deleteMutation.mutateAsync(pendingDelete);
        }}
      />
    </div>
  );
}
