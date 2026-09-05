'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { GraduationCap, Loader2, Pencil, Plus } from 'lucide-react';
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
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyTrainingService } from '@/services/hr/safety-training.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { locationService } from '@/services/hr/location.service';
import {
  SHE_TRAINING_PLAN_STATUS_OPTIONS,
  SHE_TRAINING_CATEGORY_OPTIONS,
  SHE_TRAINING_DELIVERY_OPTIONS,
} from '@/types/hr/safety-training';
import type {
  SheTrainingPlanStatus,
  SheTrainingCategory,
  SheTrainingDeliveryMethod,
} from '@/types/hr/safety-training';

/**
 * Training plan detail: the plan record (edit doubles as the approval step — Approved
 * requires naming the approver) and its scheduled programs.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);

const editSchema = z.object({
  title: z.string().min(1, 'A title is required').max(200),
  year: z.coerce.number().int().min(2000).max(2100),
  quarter: z.coerce.number().int().min(1).max(4).optional().or(z.literal('')),
  organizationUnitId: z.string().optional().or(z.literal('')),
  status: z.string().min(1),
  approvedById: z.string().optional().or(z.literal('')),
  approvedDate: z.string().optional().or(z.literal('')),
  notes: z.string().max(500).optional().or(z.literal('')),
});
type EditForm = z.input<typeof editSchema>;

const programSchema = z.object({
  programCode: z.string().min(1, 'A code is required').max(30),
  title: z.string().min(1, 'A title is required').max(200),
  description: z.string().max(1000).optional().or(z.literal('')),
  category: z.string().min(1),
  deliveryMethod: z.string().min(1),
  durationMinutes: z.coerce.number().int().positive('A duration is required'),
  scheduledDate: z.string().optional().or(z.literal('')),
  locationId: z.string().optional().or(z.literal('')),
  trainerId: z.string().optional().or(z.literal('')),
  externalTrainerName: z.string().max(200).optional().or(z.literal('')),
  externalTrainerOrganization: z.string().max(200).optional().or(z.literal('')),
  maxParticipants: z.coerce.number().int().positive().optional().or(z.literal('')),
  materialPath: z.string().max(500).optional().or(z.literal('')),
});
type ProgramForm = z.input<typeof programSchema>;

export default function SafetyTrainingPlanDetailPage() {
  const params = useParams<{ id: string }>();
  const planId = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [editOpen, setEditOpen] = useState(false);
  const [programOpen, setProgramOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  const { data: plan, isLoading } = useQuery({
    queryKey: ['hr', 'safety-training', 'plan', planId],
    queryFn: () => safetyTrainingService.getPlan(planId),
    enabled: !!planId,
  });
  const { data: orgUnits = [] } = useQuery({
    queryKey: ['hr', 'organization-units'],
    queryFn: () => organizationUnitService.getAll(),
  });
  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const editForm = useForm<EditForm>({ resolver: zodResolver(editSchema) });
  const programForm = useForm<ProgramForm>({ resolver: zodResolver(programSchema) });
  const watchedStatus = editForm.watch('status');

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'safety-training'] });

  const openEdit = () => {
    if (!plan) return;
    editForm.reset({
      title: plan.title,
      year: plan.year,
      quarter: plan.quarter ?? '',
      organizationUnitId: plan.organizationUnitId ?? '',
      status: plan.status,
      approvedById: plan.approvedById ?? '',
      approvedDate: plan.approvedDate ? plan.approvedDate.slice(0, 10) : '',
      notes: plan.notes ?? '',
    });
    setEditOpen(true);
  };

  const submitEdit = editForm.handleSubmit(async (values) => {
    if (!plan) return;
    setBusy(true);
    try {
      const v = editSchema.parse(values);
      await safetyTrainingService.updatePlan(plan.id, {
        id: plan.id,
        title: v.title,
        year: v.year,
        quarter: typeof v.quarter === 'number' ? v.quarter : null,
        organizationUnitId: blank(v.organizationUnitId),
        status: v.status as SheTrainingPlanStatus,
        approvedById: blank(v.approvedById),
        approvedDate: v.approvedDate ? new Date(v.approvedDate).toISOString() : null,
        notes: blank(v.notes),
      });
      await refresh();
      toast({ title: 'Plan updated' });
      setEditOpen(false);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Saving the plan failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  const openAddProgram = () => {
    programForm.reset({
      programCode: '',
      title: '',
      description: '',
      category: 'ToolboxTalk',
      deliveryMethod: 'ToolboxTalk',
      durationMinutes: 30,
      scheduledDate: '',
      locationId: '',
      trainerId: '',
      externalTrainerName: '',
      externalTrainerOrganization: '',
      maxParticipants: '',
      materialPath: '',
    });
    setProgramOpen(true);
  };

  const submitProgram = programForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = programSchema.parse(values);
      await safetyTrainingService.createProgram({
        programCode: v.programCode,
        title: v.title,
        description: blank(v.description),
        category: v.category as SheTrainingCategory,
        planId,
        deliveryMethod: v.deliveryMethod as SheTrainingDeliveryMethod,
        durationMinutes: v.durationMinutes,
        scheduledDate: v.scheduledDate ? new Date(v.scheduledDate).toISOString() : null,
        locationId: blank(v.locationId),
        trainerId: blank(v.trainerId),
        externalTrainerName: blank(v.externalTrainerName),
        externalTrainerOrganization: blank(v.externalTrainerOrganization),
        maxParticipants: typeof v.maxParticipants === 'number' ? v.maxParticipants : null,
        materialPath: blank(v.materialPath),
      });
      await refresh();
      toast({ title: 'Program scheduled', description: 'Open it to record attendance.' });
      setProgramOpen(false);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Scheduling the program failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  if (isLoading || !plan) {
    return (
      <div className="text-muted-foreground flex items-center gap-2 p-6 text-sm">
        <Loader2 className="h-4 w-4 animate-spin" /> Loading plan…
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${plan.planNumber} — ${plan.title}`}
        description={`${plan.year}${plan.quarter ? ` Q${plan.quarter}` : ''} · ${plan.organizationUnitName ?? 'Company-wide'}`}
        backHref="/hr/safety/training"
        actions={
          <div className="flex gap-2">
            <Button variant="outline" onClick={openEdit}>
              <Pencil className="mr-2 h-4 w-4" /> Edit / approve
            </Button>
            <Button onClick={openAddProgram}>
              <Plus className="mr-2 h-4 w-4" /> Schedule program
            </Button>
          </div>
        }
      />

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Plan</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4 text-sm">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <div>
              <div className="text-muted-foreground">Status</div>
              <StatusBadge status={plan.statusName} />
            </div>
            <div>
              <div className="text-muted-foreground">Prepared by</div>
              <div className="font-medium">
                {plan.preparedByName} · {fmtDate(plan.preparedDate)}
              </div>
            </div>
            <div>
              <div className="text-muted-foreground">Approved by</div>
              <div className="font-medium">
                {plan.approvedByName
                  ? `${plan.approvedByName} · ${fmtDate(plan.approvedDate)}`
                  : 'Not approved'}
              </div>
            </div>
            <div>
              <div className="text-muted-foreground">Programs</div>
              <div className="font-medium">{plan.programs.length}</div>
            </div>
          </div>
          {plan.notes && (
            <div>
              <div className="text-muted-foreground">Notes</div>
              <p className="whitespace-pre-wrap">{plan.notes}</p>
            </div>
          )}
        </CardContent>
      </Card>

      <div className="space-y-3">
        <h2 className="text-sm font-medium">Programs ({plan.programs.length})</h2>
        {plan.programs.length === 0 ? (
          <EmptyState
            title="No programs scheduled"
            description="Schedule toolbox talks, drills and courses under this plan."
            icon={GraduationCap}
          />
        ) : (
          <Card>
            <CardContent className="p-0">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Code</TableHead>
                    <TableHead>Program</TableHead>
                    <TableHead>Category</TableHead>
                    <TableHead>Delivery</TableHead>
                    <TableHead>Scheduled</TableHead>
                    <TableHead>Delivered</TableHead>
                    <TableHead>Attendees</TableHead>
                    <TableHead>Status</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {plan.programs.map((p) => (
                    <TableRow key={p.id}>
                      <TableCell>
                        <Link
                          href={`/hr/safety/training/programs/${p.id}`}
                          className="font-mono text-primary hover:underline"
                        >
                          {p.programCode}
                        </Link>
                      </TableCell>
                      <TableCell className="max-w-[260px] truncate font-medium" title={p.title}>
                        {p.title}
                      </TableCell>
                      <TableCell>{p.categoryName}</TableCell>
                      <TableCell>{p.deliveryMethodName}</TableCell>
                      <TableCell>{fmtDate(p.scheduledDate)}</TableCell>
                      <TableCell>{fmtDate(p.actualDate)}</TableCell>
                      <TableCell>{p.actualAttendees ?? '—'}</TableCell>
                      <TableCell>
                        <StatusBadge status={p.statusName} />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        )}
      </div>

      {/* ── Edit / approve dialog ── */}
      <Dialog open={editOpen} onOpenChange={(o) => !busy && setEditOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>Edit / approve plan</DialogTitle>
            <DialogDescription>
              The plan number (<span className="font-mono">{plan.planNumber}</span>) and preparer
              are fixed. Setting the status to Approved requires naming the approver.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitEdit} className="space-y-4">
            <TextField form={editForm} name="title" label="Title" required />
            <FieldRow>
              <NumberField form={editForm} name="year" label="Year" required />
              <NumberField form={editForm} name="quarter" label="Quarter (1–4, optional)" />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={editForm}
                name="organizationUnitId"
                label="Organization unit"
                allowEmpty
                emptyLabel="Company-wide"
                options={orgUnits.map((u) => ({ value: u.id, label: u.name }))}
              />
              <SelectField
                form={editForm}
                name="status"
                label="Status"
                required
                options={SHE_TRAINING_PLAN_STATUS_OPTIONS}
              />
            </FieldRow>
            {watchedStatus === 'Approved' && !editForm.watch('approvedById') && (
              <p className="text-destructive text-sm">
                An approved plan must name who approved it — pick the approver below.
              </p>
            )}
            <EmployeePickerField form={editForm} name="approvedById" label="Approved by" />
            <DateField form={editForm} name="approvedDate" label="Approved date" />
            <TextareaField form={editForm} name="notes" label="Notes" rows={2} />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setEditOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Save
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Schedule program dialog ── */}
      <Dialog open={programOpen} onOpenChange={(o) => !busy && setProgramOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[640px]">
          <DialogHeader>
            <DialogTitle>Schedule program</DialogTitle>
            <DialogDescription>
              The program code is unique per tenant and fixed after creation. Delivery,
              attendance and evaluation are recorded on the program page.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitProgram} className="space-y-4">
            <FieldRow>
              <TextField
                form={programForm}
                name="programCode"
                label="Program code (unique)"
                required
                placeholder="e.g. STP-TBT-014"
              />
              <TextField form={programForm} name="title" label="Title" required />
            </FieldRow>
            <TextareaField form={programForm} name="description" label="Description" rows={2} />
            <FieldRow>
              <SelectField
                form={programForm}
                name="category"
                label="Category"
                required
                options={SHE_TRAINING_CATEGORY_OPTIONS}
              />
              <SelectField
                form={programForm}
                name="deliveryMethod"
                label="Delivery method"
                required
                options={SHE_TRAINING_DELIVERY_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <NumberField form={programForm} name="durationMinutes" label="Duration (minutes)" required />
              <DateField form={programForm} name="scheduledDate" label="Scheduled date" />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={programForm}
                name="locationId"
                label="Location"
                allowEmpty
                emptyLabel="Not set"
                options={locations.map((l) => ({ value: l.id, label: l.name }))}
              />
              <NumberField form={programForm} name="maxParticipants" label="Max participants" />
            </FieldRow>
            <EmployeePickerField form={programForm} name="trainerId" label="Internal trainer" />
            <FieldRow>
              <TextField form={programForm} name="externalTrainerName" label="External trainer" />
              <TextField
                form={programForm}
                name="externalTrainerOrganization"
                label="External trainer organization"
              />
            </FieldRow>
            <TextField form={programForm} name="materialPath" label="Material path" />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setProgramOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Schedule
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
