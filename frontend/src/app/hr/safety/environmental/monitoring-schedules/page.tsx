'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { CalendarClock, Loader2, MoreHorizontal, Plus } from 'lucide-react';
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
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
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
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyEnvironmentalComplianceService } from '@/services/hr/safety-environmental-compliance.service';
import { locationService } from '@/services/hr/location.service';
import { SHE_ENV_MONITORING_TYPE_OPTIONS } from '@/types/hr/safety-environment';
import type { SheEnvironmentalMonitoringType } from '@/types/hr/safety-environment';
import type { SheEnvironmentalMonitoringSchedule } from '@/types/hr/safety-environment-compliance';

/**
 * Environmental monitoring schedules (FR-ENV-023/024): recurring dust, noise,
 * air, water, waste-storage and annual-review cycles. The MonitoringDue
 * reminders ride the reminder engine off NextDueDate; recording a completed
 * cycle advances the due date by the schedule's interval and can link the
 * monitoring record that evidences the activity.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);
const isoDay = (d: Date) => d.toISOString().slice(0, 10);

const scheduleSchema = z.object({
  monitoringType: z.string().min(1),
  locationId: z.string().optional().or(z.literal('')),
  monitoringPoint: z.string().max(200).optional().or(z.literal('')),
  description: z.string().max(2000).optional().or(z.literal('')),
  frequencyDays: z.coerce.number().int().min(1, 'At least one day').max(3660),
  nextDueDate: z.string().min(1, 'A next due date is required'),
  responsibleOfficerId: z.string().optional().or(z.literal('')),
  isActive: z.boolean(),
});
type ScheduleForm = z.input<typeof scheduleSchema>;

const completeSchema = z.object({
  performedDate: z.string().min(1, 'The performed date is required'),
  monitoringRecordId: z.string().optional().or(z.literal('')),
});
type CompleteForm = z.input<typeof completeSchema>;

export default function MonitoringSchedulesPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<SheEnvironmentalMonitoringSchedule | null>(null);
  const [completing, setCompleting] = useState<SheEnvironmentalMonitoringSchedule | null>(null);
  const [pendingDelete, setPendingDelete] = useState<SheEnvironmentalMonitoringSchedule | null>(null);
  const [busy, setBusy] = useState(false);

  const { data: due = [] } = useQuery({
    queryKey: ['hr', 'safety-env-compliance', 'schedules', 'due'],
    queryFn: () => safetyEnvironmentalComplianceService.getSchedules(true, undefined, 30),
  });
  const { data: all = [] } = useQuery({
    queryKey: ['hr', 'safety-env-compliance', 'schedules', 'all'],
    queryFn: () => safetyEnvironmentalComplianceService.getSchedules(),
  });
  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const scheduleForm = useForm<ScheduleForm>({ resolver: zodResolver(scheduleSchema) });
  const completeForm = useForm<CompleteForm>({ resolver: zodResolver(completeSchema) });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'safety-env-compliance'] });
  const fail = (fallback: string) => (error: any) =>
    toast({ title: 'Error', description: error?.message || fallback, variant: 'destructive' });

  const locationOptions = locations.map((l) => ({ value: l.id, label: l.name }));

  const openCreate = () => {
    setEditing(null);
    scheduleForm.reset({
      monitoringType: 'AmbientDust',
      locationId: '',
      monitoringPoint: '',
      description: '',
      frequencyDays: 30,
      nextDueDate: isoDay(new Date()),
      responsibleOfficerId: '',
      isActive: true,
    });
    setDialogOpen(true);
  };

  const openEdit = (row: SheEnvironmentalMonitoringSchedule) => {
    setEditing(row);
    scheduleForm.reset({
      monitoringType: row.monitoringType,
      locationId: row.locationId ?? '',
      monitoringPoint: row.monitoringPoint ?? '',
      description: row.description ?? '',
      frequencyDays: row.frequencyDays,
      nextDueDate: row.nextDueDate.slice(0, 10),
      responsibleOfficerId: row.responsibleOfficerId ?? '',
      isActive: row.isActive,
    });
    setDialogOpen(true);
  };

  const submitSchedule = scheduleForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = scheduleSchema.parse(values);
      const payload = {
        monitoringType: v.monitoringType as SheEnvironmentalMonitoringType,
        locationId: blank(v.locationId),
        monitoringPoint: blank(v.monitoringPoint),
        description: blank(v.description),
        frequencyDays: v.frequencyDays,
        nextDueDate: new Date(v.nextDueDate).toISOString(),
        responsibleOfficerId: blank(v.responsibleOfficerId),
        isActive: v.isActive,
      };
      if (editing) {
        const saved = await safetyEnvironmentalComplianceService.updateSchedule(editing.id, {
          id: editing.id,
          ...payload,
        });
        await invalidate();
        toast({ title: 'Schedule updated', description: saved.scheduleNumber });
      } else {
        const saved = await safetyEnvironmentalComplianceService.createSchedule(payload);
        await invalidate();
        toast({ title: 'Schedule created', description: saved.scheduleNumber });
      }
      setDialogOpen(false);
    } catch (error: any) {
      fail('Saving the schedule failed.')(error);
    } finally {
      setBusy(false);
    }
  });

  const submitComplete = completeForm.handleSubmit(async (values) => {
    if (!completing) return;
    setBusy(true);
    try {
      const v = completeSchema.parse(values);
      const saved = await safetyEnvironmentalComplianceService.completeScheduleCycle(completing.id, {
        performedDate: new Date(v.performedDate).toISOString(),
        monitoringRecordId: blank(v.monitoringRecordId),
      });
      await invalidate();
      toast({
        title: 'Cycle recorded',
        description: `${saved.scheduleNumber} — next due ${fmtDate(saved.nextDueDate)}`,
      });
      setCompleting(null);
    } catch (error: any) {
      fail('Recording the cycle failed.')(error);
    } finally {
      setBusy(false);
    }
  });

  const ScheduleTable = ({
    items,
    emptyText,
  }: {
    items: SheEnvironmentalMonitoringSchedule[];
    emptyText: string;
  }) =>
    items.length === 0 ? (
      <EmptyState title="Nothing here" description={emptyText} icon={CalendarClock} />
    ) : (
      <Card>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Schedule #</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Point</TableHead>
                <TableHead>Location</TableHead>
                <TableHead>Frequency</TableHead>
                <TableHead>Next due</TableHead>
                <TableHead>Last performed</TableHead>
                <TableHead>Officer</TableHead>
                <TableHead>Active</TableHead>
                <TableHead className="w-[60px]" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.map((s) => (
                <TableRow key={s.id}>
                  <TableCell className="font-mono">{s.scheduleNumber}</TableCell>
                  <TableCell className="font-medium">{s.monitoringTypeName}</TableCell>
                  <TableCell>{s.monitoringPoint ?? '—'}</TableCell>
                  <TableCell>{s.locationName ?? '—'}</TableCell>
                  <TableCell>every {s.frequencyDays} d</TableCell>
                  <TableCell
                    className={
                      s.daysToDue < 0
                        ? 'text-destructive font-medium'
                        : s.daysToDue <= 7
                          ? 'font-medium text-amber-600'
                          : undefined
                    }
                  >
                    {fmtDate(s.nextDueDate)}
                    <span className="ml-1 text-sm">
                      {s.daysToDue < 0 ? `(${-s.daysToDue} d overdue)` : `(in ${s.daysToDue} d)`}
                    </span>
                  </TableCell>
                  <TableCell>{fmtDate(s.lastPerformedDate)}</TableCell>
                  <TableCell>{s.responsibleOfficerName ?? '—'}</TableCell>
                  <TableCell>
                    {s.isActive ? (
                      <Badge>Active</Badge>
                    ) : (
                      <Badge variant="outline" className="text-muted-foreground">
                        Inactive
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
                        <DropdownMenuItem
                          onClick={() => {
                            completeForm.reset({
                              performedDate: isoDay(new Date()),
                              monitoringRecordId: '',
                            });
                            setCompleting(s);
                          }}
                        >
                          Record completed cycle…
                        </DropdownMenuItem>
                        <DropdownMenuItem onClick={() => openEdit(s)}>Edit…</DropdownMenuItem>
                        <DropdownMenuItem
                          className="text-red-600"
                          onClick={() => setPendingDelete(s)}
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
    );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Environmental Monitoring Schedules"
        description="Recurring monitoring obligations (FR-ENV-023) — dust, noise, air and water quality, waste-storage inspections and the annual performance review. Reminders ride the reminder engine off the next due date."
        backHref="/hr/safety"
        actions={
          <Button onClick={openCreate}>
            <Plus className="mr-2 h-4 w-4" /> New schedule
          </Button>
        }
      />

      <Tabs defaultValue="due">
        <TabsList>
          <TabsTrigger value="due">Due (30 days) ({due.length})</TabsTrigger>
          <TabsTrigger value="all">All schedules ({all.length})</TabsTrigger>
        </TabsList>
        <TabsContent value="due" className="mt-4">
          <ScheduleTable items={due} emptyText="No cycles fall due in the next 30 days." />
        </TabsContent>
        <TabsContent value="all" className="mt-4">
          <ScheduleTable items={all} emptyText="No monitoring schedules yet." />
        </TabsContent>
      </Tabs>

      {/* ── Create / edit dialog ── */}
      <Dialog open={dialogOpen} onOpenChange={(o) => !busy && setDialogOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[640px]">
          <DialogHeader>
            <DialogTitle>{editing ? `Edit ${editing.scheduleNumber}` : 'New schedule'}</DialogTitle>
            <DialogDescription>
              Completion advances the next due date by the frequency; the reminder engine ladders
              toward it.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitSchedule} className="space-y-4">
            <FieldRow>
              <SelectField
                form={scheduleForm}
                name="monitoringType"
                label="Monitoring type"
                required
                options={SHE_ENV_MONITORING_TYPE_OPTIONS}
              />
              <SelectField
                form={scheduleForm}
                name="locationId"
                label="Location"
                allowEmpty
                emptyLabel="Not set"
                options={locationOptions}
              />
            </FieldRow>
            <TextField form={scheduleForm} name="monitoringPoint" label="Monitoring point" />
            <TextareaField form={scheduleForm} name="description" label="Description" rows={2} />
            <FieldRow>
              <NumberField form={scheduleForm} name="frequencyDays" label="Frequency (days)" required />
              <DateField form={scheduleForm} name="nextDueDate" label="Next due" required />
            </FieldRow>
            <EmployeePickerField
              form={scheduleForm}
              name="responsibleOfficerId"
              label="Responsible officer"
            />
            <SwitchField
              form={scheduleForm}
              name="isActive"
              label="Active"
              description="Inactive schedules neither remind nor accept completed cycles."
            />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setDialogOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {editing ? 'Save' : 'Create schedule'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Complete-cycle dialog ── */}
      <Dialog open={completing !== null} onOpenChange={(o) => !busy && !o && setCompleting(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Record cycle — {completing?.scheduleNumber}</DialogTitle>
            <DialogDescription>
              Advances the next due date by {completing?.frequencyDays} days from the performed
              date.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitComplete} className="space-y-4">
            <DateField form={completeForm} name="performedDate" label="Performed on" required />
            <TextField
              form={completeForm}
              name="monitoringRecordId"
              label="Monitoring record id"
              placeholder="Paste the EM- record id that evidences this cycle (optional)"
            />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setCompleting(null)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Record cycle
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={pendingDelete !== null}
        onOpenChange={(o) => !o && setPendingDelete(null)}
        title={`Delete ${pendingDelete?.scheduleNumber}?`}
        description="Only a schedule without linked monitoring records can be deleted — one with history deactivates instead."
        confirmText="Delete"
        variant="destructive"
        onConfirm={async () => {
          if (!pendingDelete) return;
          try {
            await safetyEnvironmentalComplianceService.removeSchedule(pendingDelete.id);
            await invalidate();
            toast({ title: 'Schedule deleted', description: pendingDelete.scheduleNumber });
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
