'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, MoreHorizontal, Plus, Sprout } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
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
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyEnvironmentalComplianceService } from '@/services/hr/safety-environmental-compliance.service';
import { locationService } from '@/services/hr/location.service';
import {
  SHE_SUSTAINABILITY_CATEGORY_OPTIONS,
  SHE_SUSTAINABILITY_STATUS_OPTIONS,
} from '@/types/hr/safety-environment-compliance';
import type {
  SheSustainabilityInitiative,
  SheSustainabilityCategory,
  SheSustainabilityStatus,
} from '@/types/hr/safety-environment-compliance';

/**
 * Sustainability initiatives (FR-ENV-028/029): energy, water, paper, tree
 * planting, recycling, carbon and cost-saving initiatives with a per-category
 * KPI rollup — displayed here, on the SHE dashboard, and in the monthly
 * environmental report.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);
const isoDay = (d: Date) => d.toISOString().slice(0, 10);
const isoOrNull = (v?: string) => (v && v.length > 0 ? new Date(v).toISOString() : null);
const fmtGhs = (v?: number | null) =>
  v == null ? '—' : `GHS ${v.toLocaleString(undefined, { maximumFractionDigits: 0 })}`;

const optionalNumber = z.coerce.number().min(0).optional().or(z.literal(''));
const numOrNull = (v: number | '' | undefined) => (v === '' || v == null ? null : v);

const initiativeSchema = z.object({
  title: z.string().min(1, 'A title is required').max(300),
  category: z.string().min(1),
  description: z.string().max(2000).optional().or(z.literal('')),
  locationId: z.string().optional().or(z.literal('')),
  ownerId: z.string().optional().or(z.literal('')),
  startDate: z.string().min(1, 'A start date is required'),
  endDate: z.string().optional().or(z.literal('')),
  status: z.string().min(1),
  targetValue: optionalNumber,
  actualValue: optionalNumber,
  measurementUnit: z.string().max(50).optional().or(z.literal('')),
  estimatedCostSavings: optionalNumber,
  notes: z.string().max(1000).optional().or(z.literal('')),
});
type InitiativeForm = z.input<typeof initiativeSchema>;

function InitiativeStatusBadge({ status, name }: { status: SheSustainabilityStatus; name: string }) {
  switch (status) {
    case 'Completed':
      return <Badge>{name}</Badge>;
    case 'InProgress':
      return <Badge variant="secondary">In progress</Badge>;
    case 'Cancelled':
      return (
        <Badge variant="outline" className="text-muted-foreground">
          {name}
        </Badge>
      );
    default:
      return <Badge variant="outline">{name}</Badge>;
  }
}

export default function SustainabilityPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const year = new Date().getFullYear();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<SheSustainabilityInitiative | null>(null);
  const [pendingDelete, setPendingDelete] = useState<SheSustainabilityInitiative | null>(null);
  const [categoryFilter, setCategoryFilter] = useState<string>('');
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [busy, setBusy] = useState(false);

  const { data: initiatives = [] } = useQuery({
    queryKey: ['hr', 'safety-env-compliance', 'sustainability', categoryFilter, statusFilter],
    queryFn: () =>
      safetyEnvironmentalComplianceService.getInitiatives(
        (categoryFilter || undefined) as SheSustainabilityCategory | undefined,
        (statusFilter || undefined) as SheSustainabilityStatus | undefined,
      ),
  });
  const { data: kpis } = useQuery({
    queryKey: ['hr', 'safety-env-compliance', 'sustainability-kpis', year],
    queryFn: () => safetyEnvironmentalComplianceService.getSustainabilityKpis(year),
  });
  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const initiativeForm = useForm<InitiativeForm>({ resolver: zodResolver(initiativeSchema) });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'safety-env-compliance'] });
  const fail = (fallback: string) => (error: any) =>
    toast({ title: 'Error', description: error?.message || fallback, variant: 'destructive' });

  const locationOptions = locations.map((l) => ({ value: l.id, label: l.name }));

  const openCreate = () => {
    setEditing(null);
    initiativeForm.reset({
      title: '',
      category: 'EnergySavings',
      description: '',
      locationId: '',
      ownerId: '',
      startDate: isoDay(new Date()),
      endDate: '',
      status: 'Planned',
      targetValue: '',
      actualValue: '',
      measurementUnit: '',
      estimatedCostSavings: '',
      notes: '',
    });
    setDialogOpen(true);
  };

  const openEdit = (row: SheSustainabilityInitiative) => {
    setEditing(row);
    initiativeForm.reset({
      title: row.title,
      category: row.category,
      description: row.description ?? '',
      locationId: row.locationId ?? '',
      ownerId: row.ownerId ?? '',
      startDate: row.startDate.slice(0, 10),
      endDate: row.endDate?.slice(0, 10) ?? '',
      status: row.status,
      targetValue: row.targetValue ?? '',
      actualValue: row.actualValue ?? '',
      measurementUnit: row.measurementUnit ?? '',
      estimatedCostSavings: row.estimatedCostSavings ?? '',
      notes: row.notes ?? '',
    });
    setDialogOpen(true);
  };

  const submitInitiative = initiativeForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = initiativeSchema.parse(values);
      const payload = {
        title: v.title,
        category: v.category as SheSustainabilityCategory,
        description: blank(v.description),
        locationId: blank(v.locationId),
        ownerId: blank(v.ownerId),
        startDate: new Date(v.startDate).toISOString(),
        endDate: isoOrNull(v.endDate),
        status: v.status as SheSustainabilityStatus,
        targetValue: numOrNull(v.targetValue),
        actualValue: numOrNull(v.actualValue),
        measurementUnit: blank(v.measurementUnit),
        estimatedCostSavings: numOrNull(v.estimatedCostSavings),
        notes: blank(v.notes),
      };
      if (editing) {
        const saved = await safetyEnvironmentalComplianceService.updateInitiative(editing.id, {
          id: editing.id,
          ...payload,
        });
        await invalidate();
        toast({ title: 'Initiative updated', description: saved.initiativeNumber });
      } else {
        const saved = await safetyEnvironmentalComplianceService.createInitiative(payload);
        await invalidate();
        toast({ title: 'Initiative created', description: saved.initiativeNumber });
      }
      setDialogOpen(false);
    } catch (error: any) {
      fail('Saving the initiative failed.')(error);
    } finally {
      setBusy(false);
    }
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Sustainability Initiatives"
        description="Energy, water, paper, tree-planting, recycling, carbon and cost-saving initiatives (FR-ENV-028). The KPI rollup feeds the dashboard and the monthly environmental report (FR-ENV-029)."
        backHref="/hr/safety"
        actions={
          <Button onClick={openCreate}>
            <Plus className="mr-2 h-4 w-4" /> New initiative
          </Button>
        }
      />

      {/* ── KPI strip (FR-ENV-029) ── */}
      {kpis && (
        <div className="grid gap-4 md:grid-cols-3">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground">
                Active initiatives ({year})
              </CardTitle>
            </CardHeader>
            <CardContent className="text-2xl font-bold">{kpis.activeInitiatives}</CardContent>
          </Card>
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground">Completed</CardTitle>
            </CardHeader>
            <CardContent className="text-2xl font-bold">{kpis.completedInitiatives}</CardContent>
          </Card>
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground">
                Estimated cost savings
              </CardTitle>
            </CardHeader>
            <CardContent className="text-2xl font-bold">{fmtGhs(kpis.totalCostSavings)}</CardContent>
          </Card>
        </div>
      )}
      {kpis && kpis.categories.length > 0 && (
        <Card>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Category</TableHead>
                  <TableHead>Initiatives</TableHead>
                  <TableHead>Completed</TableHead>
                  <TableHead>Target total</TableHead>
                  <TableHead>Actual total</TableHead>
                  <TableHead>Cost savings</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {kpis.categories.map((c) => (
                  <TableRow key={c.category}>
                    <TableCell className="font-medium">{c.categoryName}</TableCell>
                    <TableCell>{c.initiatives}</TableCell>
                    <TableCell>{c.completed}</TableCell>
                    <TableCell>{c.targetTotal.toLocaleString()}</TableCell>
                    <TableCell>{c.actualTotal.toLocaleString()}</TableCell>
                    <TableCell>{fmtGhs(c.costSavings)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {/* ── Register ── */}
      <div className="flex flex-wrap gap-4">
        <select
          className="border-input bg-background h-9 rounded-md border px-3 text-sm"
          value={categoryFilter}
          onChange={(e) => setCategoryFilter(e.target.value)}
        >
          <option value="">All categories</option>
          {SHE_SUSTAINABILITY_CATEGORY_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
        <select
          className="border-input bg-background h-9 rounded-md border px-3 text-sm"
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
        >
          <option value="">All statuses</option>
          {SHE_SUSTAINABILITY_STATUS_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      </div>

      {initiatives.length === 0 ? (
        <EmptyState
          title="No initiatives"
          description="Nothing matches the current filters."
          icon={Sprout}
        />
      ) : (
        <Card>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Title</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Owner</TableHead>
                  <TableHead>Start</TableHead>
                  <TableHead>End</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Target / actual</TableHead>
                  <TableHead>Est. savings</TableHead>
                  <TableHead className="w-[60px]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {initiatives.map((i) => (
                  <TableRow key={i.id}>
                    <TableCell className="font-mono">{i.initiativeNumber}</TableCell>
                    <TableCell className="max-w-[260px] truncate font-medium" title={i.title}>
                      {i.title}
                    </TableCell>
                    <TableCell>{i.categoryName}</TableCell>
                    <TableCell>{i.ownerName ?? '—'}</TableCell>
                    <TableCell>{fmtDate(i.startDate)}</TableCell>
                    <TableCell>{fmtDate(i.endDate)}</TableCell>
                    <TableCell>
                      <InitiativeStatusBadge status={i.status} name={i.statusName} />
                    </TableCell>
                    <TableCell>
                      {i.targetValue != null || i.actualValue != null
                        ? `${i.targetValue?.toLocaleString() ?? '—'} / ${i.actualValue?.toLocaleString() ?? '—'}${i.measurementUnit ? ` ${i.measurementUnit}` : ''}`
                        : '—'}
                    </TableCell>
                    <TableCell>{fmtGhs(i.estimatedCostSavings)}</TableCell>
                    <TableCell>
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon" className="h-8 w-8">
                            <MoreHorizontal className="h-4 w-4" />
                            <span className="sr-only">Actions</span>
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem onClick={() => openEdit(i)}>Edit…</DropdownMenuItem>
                          <DropdownMenuItem
                            className="text-red-600"
                            onClick={() => setPendingDelete(i)}
                          >
                            Delete
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {/* ── Create / edit dialog ── */}
      <Dialog open={dialogOpen} onOpenChange={(o) => !busy && setDialogOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[640px]">
          <DialogHeader>
            <DialogTitle>
              {editing ? `Edit ${editing.initiativeNumber}` : 'New initiative'}
            </DialogTitle>
            <DialogDescription>
              Target and actual values share the measurement unit; savings are estimated in GHS.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitInitiative} className="space-y-4">
            <TextField form={initiativeForm} name="title" label="Title" required />
            <FieldRow>
              <SelectField
                form={initiativeForm}
                name="category"
                label="Category"
                required
                options={SHE_SUSTAINABILITY_CATEGORY_OPTIONS}
              />
              <SelectField
                form={initiativeForm}
                name="status"
                label="Status"
                required
                options={SHE_SUSTAINABILITY_STATUS_OPTIONS}
              />
            </FieldRow>
            <TextareaField form={initiativeForm} name="description" label="Description" rows={2} />
            <FieldRow>
              <SelectField
                form={initiativeForm}
                name="locationId"
                label="Location"
                allowEmpty
                emptyLabel="Not set"
                options={locationOptions}
              />
              <div />
            </FieldRow>
            <EmployeePickerField form={initiativeForm} name="ownerId" label="Owner" />
            <FieldRow>
              <DateField form={initiativeForm} name="startDate" label="Start" required />
              <DateField form={initiativeForm} name="endDate" label="End" />
            </FieldRow>
            <FieldRow>
              <NumberField form={initiativeForm} name="targetValue" label="Target value" />
              <NumberField form={initiativeForm} name="actualValue" label="Actual value" />
            </FieldRow>
            <FieldRow>
              <TextField
                form={initiativeForm}
                name="measurementUnit"
                label="Unit"
                placeholder="kWh, m³, kg, trees…"
              />
              <NumberField
                form={initiativeForm}
                name="estimatedCostSavings"
                label="Estimated cost savings (GHS)"
              />
            </FieldRow>
            <TextareaField form={initiativeForm} name="notes" label="Notes" rows={2} />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setDialogOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {editing ? 'Save' : 'Create initiative'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={pendingDelete !== null}
        onOpenChange={(o) => !o && setPendingDelete(null)}
        title={`Delete ${pendingDelete?.initiativeNumber}?`}
        description="Removes the initiative from the register and its KPI rollups."
        confirmText="Delete"
        variant="destructive"
        onConfirm={async () => {
          if (!pendingDelete) return;
          try {
            await safetyEnvironmentalComplianceService.removeInitiative(pendingDelete.id);
            await invalidate();
            toast({ title: 'Initiative deleted', description: pendingDelete.initiativeNumber });
          } catch (error: any) {
            fail('Deleting failed.')(error);
          } finally {
            setPendingDelete(null);
          }
        }}
      />
    </div>
  );
}
