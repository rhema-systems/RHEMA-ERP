'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import {
  Layers,
  Link2,
  Link2Off,
  Loader2,
  MoreHorizontal,
  Pencil,
  Plus,
  Search,
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
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TextField,
  NumberField,
  DateField,
  TextareaField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { companyGoalService, unitGoalService } from '@/services/hr/goals.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { useDebounce } from '@/hooks/use-debounce';
import { formatDate } from '@/lib/hr/attendance-format';
import { GOAL_PRIORITY_OPTIONS } from '@/types/hr/goals';
import type { GoalPriority, UnitGoal, UnitGoalListItem } from '@/types/hr/goals';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';
import { OrganizationUnitPickerField } from '@/components/hr/common/OrganizationUnitPickerField';

/**
 * Unit goals — a company goal taken up by one org unit, or an objective a unit sets for
 * itself. The middle of the cascade: employee goals align to these.
 *
 * "Unlinked" is the number to watch. A unit goal with no parent company goal is legitimate —
 * not everything a unit does rolls up to a corporate objective — but a cycle where most of
 * them are unlinked usually means the cascade was never done, so it gets its own filter and
 * its own tile.
 *
 * A caller holding only the Manager role sees just the goals they created; the API narrows
 * the same request server-side, so this screen shows HR the whole cycle and a manager their
 * own slice without asking which one it is talking to.
 */
const PAGE_SIZE = 12;
const ANY = '__any__';

const unitGoalSchema = z.object({
  parentCompanyGoalId: z.string().optional(),
  organizationUnitId: z.string().min(1, 'Select an org unit'),
  createdByManagerId: z.string().min(1, 'Select the owning manager'),
  title: z.string().min(1, 'Required').max(300),
  description: z.string().max(2000).optional(),
  successCriteria: z.string().max(1000).optional(),
  priority: z.string().min(1, 'Required'),
  targetValue: z.coerce.number().min(0).optional(),
  unit: z.string().max(50).optional(),
  dueDate: z.string().optional(),
});

type UnitGoalForm = z.input<typeof unitGoalSchema>;

const emptyForm: UnitGoalForm = {
  parentCompanyGoalId: '',
  organizationUnitId: '',
  createdByManagerId: '',
  title: '',
  description: '',
  successCriteria: '',
  priority: 'High',
  targetValue: undefined,
  unit: '',
  dueDate: '',
};

const PRIORITY_VARIANT: Record<GoalPriority, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Critical: 'destructive',
  High: 'default',
  Medium: 'secondary',
  Low: 'outline',
};

export default function UnitGoalsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [cycleId, setCycleId] = useState('');
  const [search, setSearch] = useState('');
  const [priority, setPriority] = useState<string>(ANY);
  const [orgUnitId, setOrgUnitId] = useState<string>(ANY);
  const [linked, setLinked] = useState<string>(ANY);
  const [page, setPage] = useState(1);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<UnitGoal | null>(null);
  const [pendingDelete, setPendingDelete] = useState<UnitGoalListItem | null>(null);

  const debouncedSearch = useDebounce(search, 350);

  const { data: units } = useQuery({
    queryKey: ['hr', 'organization-units'],
    queryFn: () => organizationUnitService.getAll(),
  });

  const { data: metrics } = useQuery({
    queryKey: ['hr', 'unit-goals', cycleId, 'metrics'],
    queryFn: () => unitGoalService.getDashboardMetrics(cycleId),
    enabled: !!cycleId,
  });

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'unit-goals', cycleId, debouncedSearch, priority, orgUnitId, linked, page],
    queryFn: () =>
      unitGoalService.getDashboardPaged({
        cycleId: cycleId,
        search: debouncedSearch || undefined,
        priority: priority === ANY ? null : (priority as GoalPriority),
        orgUnitId: orgUnitId === ANY ? null : orgUnitId,
        isLinked: linked === ANY ? null : linked === 'linked',
        pageNumber: page,
        pageSize: PAGE_SIZE,
      }),
    enabled: !!cycleId,
  });

  // Only visible company goals can be aligned to — hidden ones are HR's to cascade by hand.
  const { data: companyGoals } = useQuery({
    queryKey: ['hr', 'company-goals', cycleId, 'visible'],
    queryFn: () => companyGoalService.getVisible(cycleId),
    enabled: !!cycleId,
  });

  const companyGoalOptions = useMemo(
    () => (companyGoals ?? []).map((g) => ({ value: g.id, label: g.title })),
    [companyGoals],
  );

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['hr', 'unit-goals'] });

  const failed = (verb: string) => (e: any) =>
    toast({
      title: 'Error',
      description: e?.message || `Failed to ${verb} the unit goal.`,
      variant: 'destructive',
    });

  const saveMutation = useMutation({
    mutationFn: async (values: UnitGoalForm) => {
      const v = unitGoalSchema.parse(values);
      // The org level is not asked for: it is a property of the unit, so taking it from the
      // chosen unit keeps the two from disagreeing.
      const unit = (units ?? []).find((u) => u.id === v.organizationUnitId);
      if (!unit) throw new Error('That organisation unit could not be found.');

      const payload = {
        appraisalCycleId: cycleId,
        parentCompanyGoalId: v.parentCompanyGoalId || null,
        parentUnitGoalId: null,
        organizationLevelId: unit.organizationLevelId,
        organizationUnitId: v.organizationUnitId,
        createdByManagerId: v.createdByManagerId,
        title: v.title,
        description: v.description || null,
        successCriteria: v.successCriteria || null,
        priority: v.priority as GoalPriority,
        targetValue: v.targetValue ?? null,
        unit: v.unit || null,
        dueDate: v.dueDate || null,
      };
      return editing
        ? unitGoalService.update(editing.id, { id: editing.id, ...payload })
        : unitGoalService.create(payload);
    },
    onSuccess: async () => {
      await invalidate();
      toast({ title: 'Saved', description: `Unit goal ${editing ? 'updated' : 'created'}.` });
      setDialogOpen(false);
      setEditing(null);
    },
    onError: failed('save'),
  });

  const deleteMutation = useMutation({
    mutationFn: (row: UnitGoalListItem) => unitGoalService.remove(row.id),
    onSuccess: async () => {
      await invalidate();
      toast({ title: 'Deleted', description: 'Unit goal removed.' });
      setPendingDelete(null);
    },
    onError: failed('delete'),
  });

  const openEdit = async (row: UnitGoalListItem) => {
    try {
      setEditing(await unitGoalService.getById(row.id));
      setDialogOpen(true);
    } catch (e: any) {
      failed('load')(e);
    }
  };

  const formValues: UnitGoalForm = editing
    ? {
        parentCompanyGoalId: editing.parentCompanyGoalId ?? '',
        organizationUnitId: editing.organizationUnitId,
        createdByManagerId: editing.createdByManagerId,
        title: editing.title,
        description: editing.description ?? '',
        successCriteria: editing.successCriteria ?? '',
        priority: editing.priority,
        targetValue: editing.targetValue ?? undefined,
        unit: editing.unit ?? '',
        dueDate: editing.dueDate ? editing.dueDate.slice(0, 10) : '',
      }
    : emptyForm;

  const rows = data?.items ?? [];
  const unlinked = metrics?.unlinkedCount ?? 0;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Unit Goals"
        description="What each org unit is accountable for this cycle, and how it rolls up."
        backHref="/hr/performance"
        actions={
          <Button
            onClick={() => {
              setEditing(null);
              setDialogOpen(true);
            }}
            disabled={!cycleId}
          >
            <Plus className="mr-2 h-4 w-4" />
            New unit goal
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
              icon={Layers}
              title="No appraisal cycle selected"
              description="Unit goals belong to a cycle."
            />
          </CardContent>
        </Card>
      ) : (
        <>
          <MetricTiles
            tiles={[
              { label: 'Unit goals', value: metrics?.totalUnitGoals ?? '—', icon: Layers },
              {
                label: 'Linked to a company goal',
                value: metrics?.linkedToCompanyGoal ?? '—',
                hint: 'Rolls up to corporate strategy',
                icon: Link2,
              },
              {
                label: 'Unlinked',
                value: unlinked,
                hint: 'Standalone — fine in small numbers',
                icon: Link2Off,
                // Most of the cycle unlinked usually means the cascade was never done.
                tone:
                  metrics && metrics.totalUnitGoals > 0 && unlinked > metrics.totalUnitGoals / 2
                    ? 'warning'
                    : 'default',
              },
              {
                label: 'Employee goals cascaded',
                value: metrics?.totalEmployeeGoalsCascaded ?? '—',
                hint: 'Aligned to one of these',
                icon: Users,
              },
            ]}
          />

          <Card>
            <CardContent className="flex flex-wrap items-end gap-4 p-4">
              <div className="min-w-[220px] flex-1 space-y-2">
                <Label htmlFor="search">Search</Label>
                <div className="relative">
                  <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                  <Input
                    id="search"
                    className="pl-8"
                    placeholder="Goal title…"
                    value={search}
                    onChange={(e) => {
                      setSearch(e.target.value);
                      setPage(1);
                    }}
                  />
                </div>
              </div>
              <div className="w-[200px] space-y-2">
                <OrganizationUnitPicker
                  value={orgUnitId === ANY ? '' : orgUnitId}
                  onChange={(id) => {
                    setOrgUnitId(id || ANY);
                    setPage(1);
                  }}
                  allowNone="All units"
                  levelLabel="Level"
                  unitLabel="Org unit"
                  idPrefix="unit-goals-scope"
                  className="flex flex-wrap items-end gap-4"
                />
              </div>
              <div className="w-[160px] space-y-2">
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
                <Label htmlFor="linked">Alignment</Label>
                <Select
                  value={linked}
                  onValueChange={(v) => {
                    setLinked(v);
                    setPage(1);
                  }}
                >
                  <SelectTrigger id="linked">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ANY}>Any</SelectItem>
                    <SelectItem value="linked">Linked to a company goal</SelectItem>
                    <SelectItem value="unlinked">Unlinked</SelectItem>
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
                  title="Could not load unit goals"
                  description={(error as any)?.message || 'Please try again.'}
                />
              ) : rows.length === 0 ? (
                <EmptyState
                  icon={Layers}
                  title="No unit goals"
                  description="Nothing matches. Create one, or widen the filters."
                />
              ) : (
                <div className="overflow-x-auto">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Goal</TableHead>
                        <TableHead>Org unit</TableHead>
                        <TableHead>Owner</TableHead>
                        <TableHead>Aligned to</TableHead>
                        <TableHead>Priority</TableHead>
                        <TableHead>Due</TableHead>
                        <TableHead className="text-right">Employee goals</TableHead>
                        <TableHead className="w-[60px]" />
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {rows.map((row) => (
                        <TableRow key={row.id}>
                          <TableCell>
                            <Link
                              href={`/hr/performance/unit-goals/${row.id}`}
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
                            <span>{row.organizationUnitName}</span>
                            <p className="text-xs text-muted-foreground">
                              {row.organizationLevelName}
                            </p>
                          </TableCell>
                          <TableCell>{row.managerName}</TableCell>
                          <TableCell>
                            {row.parentCompanyGoalId ? (
                              <Link
                                href={`/hr/performance/company-goals/${row.parentCompanyGoalId}`}
                                className="text-sm hover:underline"
                              >
                                {row.parentCompanyGoalTitle}
                              </Link>
                            ) : (
                              <Badge variant="secondary" className="gap-1">
                                <Link2Off className="h-3 w-3" /> Unlinked
                              </Badge>
                            )}
                          </TableCell>
                          <TableCell>
                            <Badge variant={PRIORITY_VARIANT[row.priority]}>{row.priority}</Badge>
                          </TableCell>
                          <TableCell>{formatDate(row.dueDate)}</TableCell>
                          <TableCell className="text-right tabular-nums">
                            {row.employeeGoalsCount}
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

      <GoalFormDialog<UnitGoalForm>
        open={dialogOpen}
        onOpenChange={(open) => {
          setDialogOpen(open);
          if (!open) setEditing(null);
        }}
        editingId={editing?.id ?? null}
        title="unit goal"
        hint="Owned by one org unit. Link it to a company goal to make it roll up."
        schema={unitGoalSchema as any}
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
              placeholder="e.g. Cut invoice turnaround to three days"
            />
            <SelectField
              form={form}
              name="parentCompanyGoalId"
              label="Aligned to company goal"
              options={companyGoalOptions}
              allowEmpty
              emptyLabel="Standalone — not linked"
              placeholder={
                companyGoalOptions.length ? 'Select a company goal…' : 'No visible company goals'
              }
            />
            <FieldRow>
              <OrganizationUnitPickerField form={form} name="organizationUnitId" label="Organisation unit" required />
              <SelectField
                form={form}
                name="priority"
                label="Priority"
                required
                options={GOAL_PRIORITY_OPTIONS}
              />
            </FieldRow>
            <EmployeePickerField
              form={form}
              name="createdByManagerId"
              label="Owning manager"
              required
              initialLabel={editing?.managerName}
              placeholder="Search for the accountable manager…"
            />
            <TextareaField form={form} name="description" label="Description" rows={3} />
            <TextareaField
              form={form}
              name="successCriteria"
              label="Success criteria"
              rows={2}
            />
            <FieldRow>
              <NumberField form={form} name="targetValue" label="Target value" step="0.01" />
              <TextField form={form} name="unit" label="Unit" placeholder="days, %, GHS…" />
            </FieldRow>
            <DateField form={form} name="dueDate" label="Due date" />
          </>
        )}
      />

      <ConfirmationDialog
        open={pendingDelete !== null}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title="Delete unit goal?"
        description={
          pendingDelete
            ? `“${pendingDelete.title}” has ${pendingDelete.employeeGoalsCount} employee goal(s) cascaded from it. They stay, but lose the link.`
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
