'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { ClipboardCheck, Loader2, MoreHorizontal, Pencil, Plus, Users } from 'lucide-react';
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
import { StatusBadge } from '@/components/hr/common/StatusBadge';
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
import { safetyTrainingService } from '@/services/hr/safety-training.service';
import { locationService } from '@/services/hr/location.service';
import {
  SHE_TRAINING_CATEGORY_OPTIONS,
  SHE_TRAINING_DELIVERY_OPTIONS,
  SHE_TRAINING_STATUS_OPTIONS,
} from '@/types/hr/safety-training';
import type {
  SheTrainingAttendance,
  SheTrainingCategory,
  SheTrainingDeliveryMethod,
  SheTrainingStatus,
} from '@/types/hr/safety-training';

/**
 * Training program workspace: the delivery record (debrief-on-edit: actual date, status,
 * headcount), the one-shot effectiveness evaluation, and the attendance register — employee
 * rows carry the employee's real name and refuse repeats; visitor/contractor rows sign by
 * name and company.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);

const editSchema = z.object({
  title: z.string().min(1, 'A title is required').max(200),
  description: z.string().max(1000).optional().or(z.literal('')),
  category: z.string().min(1),
  deliveryMethod: z.string().min(1),
  durationMinutes: z.coerce.number().int().positive(),
  scheduledDate: z.string().optional().or(z.literal('')),
  actualDate: z.string().optional().or(z.literal('')),
  locationId: z.string().optional().or(z.literal('')),
  trainerId: z.string().optional().or(z.literal('')),
  externalTrainerName: z.string().max(200).optional().or(z.literal('')),
  externalTrainerOrganization: z.string().max(200).optional().or(z.literal('')),
  status: z.string().min(1),
  maxParticipants: z.coerce.number().int().positive().optional().or(z.literal('')),
  actualAttendees: z.coerce.number().int().nonnegative().optional().or(z.literal('')),
  materialPath: z.string().max(500).optional().or(z.literal('')),
});
type EditForm = z.input<typeof editSchema>;

const evaluateSchema = z.object({
  evaluatedById: z.string().min(1, 'An evaluator is required'),
  evaluationDate: z.string().min(1, 'A date is required'),
  evaluationSummary: z.string().max(1000).optional().or(z.literal('')),
});
type EvaluateForm = z.input<typeof evaluateSchema>;

const attendanceSchema = z.object({
  isEmployee: z.boolean(),
  employeeId: z.string().optional().or(z.literal('')),
  attendanceName: z.string().max(200).optional().or(z.literal('')),
  companyName: z.string().max(100).optional().or(z.literal('')),
  attended: z.boolean(),
  signedDate: z.string().optional().or(z.literal('')),
  assessmentPassed: z.string().optional().or(z.literal('')),
  assessmentScore: z.coerce.number().int().min(0).max(100).optional().or(z.literal('')),
  certificateExpiryDate: z.string().optional().or(z.literal('')),
  certificateDocumentPath: z.string().max(200).optional().or(z.literal('')),
});
type AttendanceForm = z.input<typeof attendanceSchema>;

const ASSESSMENT_OPTIONS = [
  { value: 'Passed', label: 'Passed' },
  { value: 'Failed', label: 'Failed' },
];

export default function SafetyTrainingProgramDetailPage() {
  const params = useParams<{ id: string }>();
  const programId = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [editOpen, setEditOpen] = useState(false);
  const [evaluateOpen, setEvaluateOpen] = useState(false);
  const [attendanceOpen, setAttendanceOpen] = useState(false);
  const [editingAttendance, setEditingAttendance] = useState<SheTrainingAttendance | null>(null);
  const [pendingRemove, setPendingRemove] = useState<SheTrainingAttendance | null>(null);
  const [busy, setBusy] = useState(false);

  const { data: program, isLoading } = useQuery({
    queryKey: ['hr', 'safety-training', 'program', programId],
    queryFn: () => safetyTrainingService.getProgram(programId),
    enabled: !!programId,
  });
  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const editForm = useForm<EditForm>({ resolver: zodResolver(editSchema) });
  const evaluateForm = useForm<EvaluateForm>({ resolver: zodResolver(evaluateSchema) });
  const attendanceForm = useForm<AttendanceForm>({ resolver: zodResolver(attendanceSchema) });
  const watchedIsEmployee = attendanceForm.watch('isEmployee');

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'safety-training'] });

  const fail = (error: any, fallback: string) =>
    toast({ title: 'Error', description: error?.message || fallback, variant: 'destructive' });

  const openEdit = () => {
    if (!program) return;
    editForm.reset({
      title: program.title,
      description: program.description ?? '',
      category: program.category,
      deliveryMethod: program.deliveryMethod,
      durationMinutes: program.durationMinutes,
      scheduledDate: program.scheduledDate ? program.scheduledDate.slice(0, 10) : '',
      actualDate: program.actualDate ? program.actualDate.slice(0, 10) : '',
      locationId: program.locationId ?? '',
      trainerId: program.trainerId ?? '',
      externalTrainerName: program.externalTrainerName ?? '',
      externalTrainerOrganization: program.externalTrainerOrganization ?? '',
      status: program.status,
      maxParticipants: program.maxParticipants ?? '',
      actualAttendees: program.actualAttendees ?? '',
      materialPath: program.materialPath ?? '',
    });
    setEditOpen(true);
  };

  const submitEdit = editForm.handleSubmit(async (values) => {
    if (!program) return;
    setBusy(true);
    try {
      const v = editSchema.parse(values);
      await safetyTrainingService.updateProgram(program.id, {
        id: program.id,
        title: v.title,
        description: blank(v.description),
        category: v.category as SheTrainingCategory,
        planId: program.planId,
        deliveryMethod: v.deliveryMethod as SheTrainingDeliveryMethod,
        durationMinutes: v.durationMinutes,
        scheduledDate: v.scheduledDate ? new Date(v.scheduledDate).toISOString() : null,
        actualDate: v.actualDate ? new Date(v.actualDate).toISOString() : null,
        locationId: blank(v.locationId),
        trainerId: blank(v.trainerId),
        externalTrainerName: blank(v.externalTrainerName),
        externalTrainerOrganization: blank(v.externalTrainerOrganization),
        status: v.status as SheTrainingStatus,
        maxParticipants: typeof v.maxParticipants === 'number' ? v.maxParticipants : null,
        actualAttendees: typeof v.actualAttendees === 'number' ? v.actualAttendees : null,
        materialPath: blank(v.materialPath),
      });
      await refresh();
      toast({ title: 'Program updated' });
      setEditOpen(false);
    } catch (error: any) {
      fail(error, 'Saving the program failed.');
    } finally {
      setBusy(false);
    }
  });

  const submitEvaluate = evaluateForm.handleSubmit(async (values) => {
    if (!program) return;
    setBusy(true);
    try {
      const v = evaluateSchema.parse(values);
      await safetyTrainingService.evaluateProgram(program.id, {
        programId: program.id,
        evaluatedById: v.evaluatedById,
        evaluationDate: new Date(v.evaluationDate).toISOString(),
        evaluationSummary: blank(v.evaluationSummary),
      });
      await refresh();
      toast({ title: 'Evaluation recorded' });
      setEvaluateOpen(false);
    } catch (error: any) {
      fail(error, 'Recording the evaluation failed.');
    } finally {
      setBusy(false);
    }
  });

  const openAddAttendance = () => {
    setEditingAttendance(null);
    attendanceForm.reset({
      isEmployee: true,
      employeeId: '',
      attendanceName: '',
      companyName: '',
      attended: true,
      signedDate: new Date().toISOString().slice(0, 10),
      assessmentPassed: '',
      assessmentScore: '',
      certificateExpiryDate: '',
      certificateDocumentPath: '',
    });
    setAttendanceOpen(true);
  };

  const openEditAttendance = (a: SheTrainingAttendance) => {
    setEditingAttendance(a);
    attendanceForm.reset({
      isEmployee: a.isEmployee,
      employeeId: a.employeeId ?? '',
      attendanceName: a.attendanceName,
      companyName: a.companyName ?? '',
      attended: a.attended,
      signedDate: a.signedDate ? a.signedDate.slice(0, 10) : '',
      assessmentPassed: a.assessmentPassed == null ? '' : a.assessmentPassed ? 'Passed' : 'Failed',
      assessmentScore: a.assessmentScore ?? '',
      certificateExpiryDate: a.certificateExpiryDate ? a.certificateExpiryDate.slice(0, 10) : '',
      certificateDocumentPath: a.certificateDocumentPath ?? '',
    });
    setAttendanceOpen(true);
  };

  const submitAttendance = attendanceForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = attendanceSchema.parse(values);
      const common = {
        companyName: blank(v.companyName),
        attended: v.attended,
        signedDate: v.signedDate ? new Date(v.signedDate).toISOString() : null,
        assessmentPassed: v.assessmentPassed === '' ? null : v.assessmentPassed === 'Passed',
        assessmentScore: typeof v.assessmentScore === 'number' ? v.assessmentScore : null,
        certificateExpiryDate: v.certificateExpiryDate
          ? new Date(v.certificateExpiryDate).toISOString()
          : null,
        certificateDocumentPath: blank(v.certificateDocumentPath),
      };
      if (editingAttendance) {
        await safetyTrainingService.updateAttendance(editingAttendance.id, {
          id: editingAttendance.id,
          attendanceName: v.attendanceName || editingAttendance.attendanceName,
          ...common,
        });
      } else if (v.isEmployee) {
        if (!v.employeeId) throw new Error('Pick the employee for an employee sign-in.');
        await safetyTrainingService.addAttendance(programId, {
          programId,
          isEmployee: true,
          employeeId: v.employeeId,
          // The server derives the name from the employee record; this is just a fallback value.
          attendanceName: v.attendanceName || 'Employee',
          ...common,
        });
      } else {
        if (!v.attendanceName) throw new Error('A visitor sign-in needs a name.');
        await safetyTrainingService.addAttendance(programId, {
          programId,
          isEmployee: false,
          employeeId: null,
          attendanceName: v.attendanceName,
          ...common,
        });
      }
      await refresh();
      toast({ title: editingAttendance ? 'Attendance updated' : 'Attendance recorded' });
      setAttendanceOpen(false);
    } catch (error: any) {
      fail(error, 'Saving the attendance failed.');
    } finally {
      setBusy(false);
    }
  });

  if (isLoading || !program) {
    return (
      <div className="text-muted-foreground flex items-center gap-2 p-6 text-sm">
        <Loader2 className="h-4 w-4 animate-spin" /> Loading program…
      </div>
    );
  }

  const trainer =
    program.trainerName ??
    (program.externalTrainerName
      ? `${program.externalTrainerName}${program.externalTrainerOrganization ? ` (${program.externalTrainerOrganization})` : ''}`
      : null);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${program.programCode} — ${program.title}`}
        description={`${program.categoryName} · ${program.deliveryMethodName} · ${program.durationMinutes} min`}
        backHref={
          program.planId ? `/hr/safety/training/plans/${program.planId}` : '/hr/safety/training'
        }
        actions={
          <div className="flex gap-2">
            <Button variant="outline" onClick={openEdit}>
              <Pencil className="mr-2 h-4 w-4" /> Edit / debrief
            </Button>
            {!program.wasEvaluated && (
              <Button
                variant="outline"
                onClick={() => {
                  evaluateForm.reset({
                    evaluatedById: '',
                    evaluationDate: new Date().toISOString().slice(0, 10),
                    evaluationSummary: '',
                  });
                  setEvaluateOpen(true);
                }}
              >
                <ClipboardCheck className="mr-2 h-4 w-4" /> Evaluate
              </Button>
            )}
            <Button onClick={openAddAttendance}>
              <Plus className="mr-2 h-4 w-4" /> Record attendance
            </Button>
          </div>
        }
      />

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Delivery</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4 text-sm">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <div>
              <div className="text-muted-foreground">Status</div>
              <StatusBadge status={program.statusName} />
            </div>
            <div>
              <div className="text-muted-foreground">Plan</div>
              <div className="font-medium">
                {program.planId ? (
                  <Link
                    href={`/hr/safety/training/plans/${program.planId}`}
                    className="text-primary hover:underline"
                  >
                    {program.planNumber}
                  </Link>
                ) : (
                  'Stand-alone'
                )}
              </div>
            </div>
            <div>
              <div className="text-muted-foreground">Scheduled / delivered</div>
              <div className="font-medium">
                {fmtDate(program.scheduledDate)} / {fmtDate(program.actualDate)}
              </div>
            </div>
            <div>
              <div className="text-muted-foreground">Location</div>
              <div className="font-medium">{program.locationName ?? '—'}</div>
            </div>
            <div>
              <div className="text-muted-foreground">Trainer</div>
              <div className="font-medium">{trainer ?? '—'}</div>
            </div>
            <div>
              <div className="text-muted-foreground">Headcount (reported)</div>
              <div className="font-medium">
                {program.actualAttendees ?? '—'}
                {program.maxParticipants ? ` / max ${program.maxParticipants}` : ''}
              </div>
            </div>
            <div>
              <div className="text-muted-foreground">Recorded sign-ins</div>
              <div className="font-medium">{program.attendances.length}</div>
            </div>
            <div>
              <div className="text-muted-foreground">Evaluation</div>
              <div className="font-medium">
                {program.wasEvaluated
                  ? `${program.evaluatedByName ?? '—'} · ${fmtDate(program.evaluationDate)}`
                  : 'Not evaluated'}
              </div>
            </div>
          </div>
          {program.description && (
            <div>
              <div className="text-muted-foreground">Description</div>
              <p className="whitespace-pre-wrap">{program.description}</p>
            </div>
          )}
          {program.wasEvaluated && program.evaluationSummary && (
            <div>
              <div className="text-muted-foreground">Evaluation summary</div>
              <p className="whitespace-pre-wrap">{program.evaluationSummary}</p>
            </div>
          )}
        </CardContent>
      </Card>

      <div className="space-y-3">
        <h2 className="text-sm font-medium">Attendance ({program.attendances.length})</h2>
        {program.attendances.length === 0 ? (
          <EmptyState
            title="No attendance recorded"
            description="Employees sign in against their record; contractors and visitors sign by name and company."
            icon={Users}
          />
        ) : (
          <Card>
            <CardContent className="p-0">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Attendee</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead>Attended</TableHead>
                    <TableHead>Assessment</TableHead>
                    <TableHead>Certificate expiry</TableHead>
                    <TableHead className="w-[60px]" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {program.attendances.map((a) => {
                    const lapsed =
                      !!a.certificateExpiryDate &&
                      new Date(a.certificateExpiryDate).getTime() < Date.now();
                    return (
                      <TableRow key={a.id}>
                        <TableCell className="font-medium">{a.attendanceName}</TableCell>
                        <TableCell>
                          {a.isEmployee ? (
                            <Badge variant="secondary">Employee</Badge>
                          ) : (
                            <Badge variant="outline">{a.companyName ?? 'Visitor'}</Badge>
                          )}
                        </TableCell>
                        <TableCell>
                          {a.attended ? (
                            <Badge variant="default">Present</Badge>
                          ) : (
                            <Badge variant="secondary">Absent</Badge>
                          )}
                        </TableCell>
                        <TableCell>
                          {a.assessmentPassed == null
                            ? '—'
                            : `${a.assessmentPassed ? 'Passed' : 'Failed'}${a.assessmentScore != null ? ` (${a.assessmentScore})` : ''}`}
                        </TableCell>
                        <TableCell>
                          {a.certificateExpiryDate ? (
                            <span className={lapsed ? 'text-destructive font-medium' : ''}>
                              {fmtDate(a.certificateExpiryDate)}
                              {lapsed && ' (lapsed)'}
                            </span>
                          ) : (
                            '—'
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
                              <DropdownMenuItem onClick={() => openEditAttendance(a)}>
                                Edit…
                              </DropdownMenuItem>
                              <DropdownMenuItem
                                className="text-red-600"
                                onClick={() => setPendingRemove(a)}
                              >
                                Remove
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        )}
      </div>

      {/* ── Edit / debrief dialog ── */}
      <Dialog open={editOpen} onOpenChange={(o) => !busy && setEditOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[640px]">
          <DialogHeader>
            <DialogTitle>Edit / debrief program</DialogTitle>
            <DialogDescription>
              The program code is fixed. After delivery, record the actual date, status and
              headcount here; sign-ins live on the attendance register below.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitEdit} className="space-y-4">
            <TextField form={editForm} name="title" label="Title" required />
            <TextareaField form={editForm} name="description" label="Description" rows={2} />
            <FieldRow>
              <SelectField
                form={editForm}
                name="category"
                label="Category"
                required
                options={SHE_TRAINING_CATEGORY_OPTIONS}
              />
              <SelectField
                form={editForm}
                name="deliveryMethod"
                label="Delivery method"
                required
                options={SHE_TRAINING_DELIVERY_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <DateField form={editForm} name="scheduledDate" label="Scheduled date" />
              <DateField form={editForm} name="actualDate" label="Actual (delivered) date" />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={editForm}
                name="status"
                label="Status"
                required
                options={SHE_TRAINING_STATUS_OPTIONS}
              />
              <NumberField form={editForm} name="durationMinutes" label="Duration (minutes)" required />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={editForm}
                name="locationId"
                label="Location"
                allowEmpty
                emptyLabel="Not set"
                options={locations.map((l) => ({ value: l.id, label: l.name }))}
              />
              <NumberField form={editForm} name="actualAttendees" label="Headcount (reported)" />
            </FieldRow>
            <NumberField form={editForm} name="maxParticipants" label="Max participants" />
            <EmployeePickerField form={editForm} name="trainerId" label="Internal trainer" />
            <FieldRow>
              <TextField form={editForm} name="externalTrainerName" label="External trainer" />
              <TextField
                form={editForm}
                name="externalTrainerOrganization"
                label="External trainer organization"
              />
            </FieldRow>
            <TextField form={editForm} name="materialPath" label="Material path" />
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

      {/* ── Evaluate dialog (one-shot) ── */}
      <Dialog open={evaluateOpen} onOpenChange={(o) => !busy && setEvaluateOpen(o)}>
        <DialogContent className="sm:max-w-[480px]">
          <DialogHeader>
            <DialogTitle>Evaluate program</DialogTitle>
            <DialogDescription>
              One-shot: once recorded, the evaluation cannot be re-run.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitEvaluate} className="space-y-4">
            <EmployeePickerField form={evaluateForm} name="evaluatedById" label="Evaluated by" required />
            <DateField form={evaluateForm} name="evaluationDate" label="Evaluation date" required />
            <TextareaField
              form={evaluateForm}
              name="evaluationSummary"
              label="Summary"
              rows={3}
            />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setEvaluateOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Record evaluation
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Attendance dialog ── */}
      <Dialog open={attendanceOpen} onOpenChange={(o) => !busy && setAttendanceOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>
              {editingAttendance ? 'Edit attendance' : 'Record attendance'}
            </DialogTitle>
            <DialogDescription>
              {editingAttendance
                ? editingAttendance.isEmployee
                  ? `${editingAttendance.attendanceName} — the employee and their name are fixed.`
                  : 'Visitor / contractor sign-in.'
                : 'Employees sign in against their record (one row each); visitors and contractor workers sign by name.'}
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitAttendance} className="space-y-4">
            {!editingAttendance && (
              <>
                <SwitchField
                  form={attendanceForm}
                  name="isEmployee"
                  label="Employee sign-in"
                  description="Off records a visitor / contractor worker by name and company."
                />
                {watchedIsEmployee ? (
                  <EmployeePickerField
                    form={attendanceForm}
                    name="employeeId"
                    label="Employee"
                    required
                  />
                ) : (
                  <FieldRow>
                    <TextField
                      form={attendanceForm}
                      name="attendanceName"
                      label="Name"
                      required
                    />
                    <TextField form={attendanceForm} name="companyName" label="Company" />
                  </FieldRow>
                )}
              </>
            )}
            {editingAttendance && !editingAttendance.isEmployee && (
              <FieldRow>
                <TextField form={attendanceForm} name="attendanceName" label="Name" required />
                <TextField form={attendanceForm} name="companyName" label="Company" />
              </FieldRow>
            )}
            <FieldRow>
              <SwitchField form={attendanceForm} name="attended" label="Attended" />
              <DateField form={attendanceForm} name="signedDate" label="Signed date" />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={attendanceForm}
                name="assessmentPassed"
                label="Assessment"
                allowEmpty
                emptyLabel="Not assessed"
                options={ASSESSMENT_OPTIONS}
              />
              <NumberField form={attendanceForm} name="assessmentScore" label="Score (0–100)" />
            </FieldRow>
            <FieldRow>
              <DateField
                form={attendanceForm}
                name="certificateExpiryDate"
                label="Certificate expiry"
              />
              <TextField
                form={attendanceForm}
                name="certificateDocumentPath"
                label="Certificate document path"
              />
            </FieldRow>
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setAttendanceOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {editingAttendance ? 'Save' : 'Record'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={pendingRemove !== null}
        onOpenChange={(open) => !open && setPendingRemove(null)}
        title={`Remove ${pendingRemove?.attendanceName}?`}
        description="This removes the sign-in row from the register."
        confirmText="Remove"
        variant="destructive"
        onConfirm={async () => {
          if (!pendingRemove) return;
          try {
            await safetyTrainingService.removeAttendance(pendingRemove.id);
            await refresh();
            toast({ title: 'Removed', description: pendingRemove.attendanceName });
          } catch (error: any) {
            fail(error, 'Removing failed.');
          } finally {
            setPendingRemove(null);
          }
        }}
      />
    </div>
  );
}
