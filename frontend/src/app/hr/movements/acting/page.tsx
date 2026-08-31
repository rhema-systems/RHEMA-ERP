'use client';

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { Loader2, Pencil, Plus, CalendarClock, CheckCircle2, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { actingAppointmentService } from '@/services/hr/movement-subtype.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { useToast } from '@/hooks/use-toast';
import {
  ACTING_REASONS,
  ALLOWANCE_CALCULATIONS,
  type StaffActingReason,
  type StaffActingAppointment,
  type StaffActingStatus,
  type HRAllowanceCalculationMethod,
} from '@/types/hr/movement-subtypes';

/** Radix needs a sentinel: an empty string is not a valid SelectItem value. */
const NONE = '__none__';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const money = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2 });

type View = 'active' | 'expiring' | 'all' | 'converted';

interface FormValues {
  employeeId: string;
  actingPositionId: string;
  actingForEmployeeId: string;
  startDate: string;
  endDate: string;
  reason: StaffActingReason;
  receivesActingAllowance: boolean;
  actingAllowance: number;
  notes: string;
}

/**
 * Acting appointments — someone covering a post temporarily.
 *
 * Standalone by design: an acting appointment may come from a movement, but most do not. Converting
 * one to permanent needs a promotion movement for the same employee — the conversion IS that
 * promotion, so there is no way to make someone permanent from this screen alone.
 */
export default function ActingAppointmentsPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [view, setView] = useState<View>('active');
  const [creating, setCreating] = useState(false);
  const [completing, setCompleting] = useState<StaffActingAppointment | null>(null);
  const [completionNotes, setCompletionNotes] = useState('');
  const [editing, setEditing] = useState<StaffActingAppointment | null>(null);
  const [endingEarly, setEndingEarly] = useState<StaffActingAppointment | null>(null);
  const [endEarlyReason, setEndEarlyReason] = useState('');
  const [edit, setEdit] = useState({
    endDate: '',
    receivesActingAllowance: false,
    actingAllowance: '',
    allowanceCalculation: '' as HRAllowanceCalculationMethod | '',
    status: 'Active' as StaffActingStatus,
    notes: '',
  });

  const form = useForm<FormValues>({
    defaultValues: {
      employeeId: '',
      actingPositionId: '',
      actingForEmployeeId: '',
      startDate: '',
      endDate: '',
      reason: 'IncumbentOnLeave',
      receivesActingAllowance: false,
      actingAllowance: 0,
      notes: '',
    },
  });

  const { data: items = [], isLoading } = useQuery({
    queryKey: ['hr', 'acting-appointments', view],
    queryFn: () => {
      switch (view) {
        case 'expiring':
          return actingAppointmentService.getExpiring(14);
        case 'converted':
          return actingAppointmentService.getConvertedToPermanent();
        case 'all':
          return actingAppointmentService.getAll();
        default:
          return actingAppointmentService.getActive();
      }
    },
  });

  const { data: positions = [] } = useQuery({
    queryKey: ['hr', 'positions', 'all'],
    queryFn: () => employeePositionService.getAll(),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'acting-appointments'] });

  /**
   * ⚠ The one movement sub-type where the EDIT was missing rather than the delete. Create,
   * complete, extend, convert and delete were all wired and correcting an appointment was not —
   * so an allowance typed wrongly could only be fixed by completing the appointment and raising
   * another, which changes what the record says happened.
   *
   * ⚠ Seeded from the row, which is safe here only because the list read is the full record —
   * `getAll` and friends return `StaffActingAppointmentDto`, not a summary. `allowanceCalculation`
   * was missing from the TypeScript type until this slice, which is why section E listed it as
   * settable by no form.
   */
  const openEdit = (a: StaffActingAppointment) => {
    setEdit({
      endDate: a.endDate ? a.endDate.slice(0, 10) : '',
      receivesActingAllowance: a.receivesActingAllowance,
      actingAllowance: a.actingAllowance == null ? '' : String(a.actingAllowance),
      allowanceCalculation: a.allowanceCalculation ?? '',
      status: a.status,
      notes: a.notes ?? '',
    });
    setEditing(a);
  };

  const saveEdit = useMutation({
    mutationFn: ({ id }: { id: string }) =>
      actingAppointmentService.update(id, {
        endDate: edit.endDate ? new Date(`${edit.endDate}T00:00:00`).toISOString() : null,
        receivesActingAllowance: edit.receivesActingAllowance,
        actingAllowance: edit.actingAllowance === '' ? null : Number(edit.actingAllowance),
        allowanceCalculation: edit.allowanceCalculation || null,
        // status is deliberately not sent — the server ignores it; see the note in the dialog.
        notes: edit.notes.trim() || null,
      }),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Appointment updated' });
      setEditing(null);
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'Could not update the appointment',
        description: e?.response?.data?.message ?? e?.response?.data ?? e?.message,
      }),
  });

  const create = useMutation({
    mutationFn: (values: FormValues) =>
      actingAppointmentService.create({
        employeeId: values.employeeId,
        actingPositionId: values.actingPositionId,
        actingForEmployeeId: values.actingForEmployeeId || null,
        startDate: new Date(values.startDate).toISOString(),
        endDate: values.endDate ? new Date(values.endDate).toISOString() : null,
        reason: values.reason,
        receivesActingAllowance: values.receivesActingAllowance,
        actingAllowance: values.receivesActingAllowance ? Number(values.actingAllowance) || 0 : null,
        notes: values.notes || null,
      }),
    onSuccess: (created) => {
      toast({ title: 'Acting appointment created', description: created.appointmentNumber });
      setCreating(false);
      form.reset();
      refresh();
    },
    onError: (error: any) =>
      toast({ title: 'Refused', description: error?.message, variant: 'destructive' }),
  });

  /**
   * ⚠ The only route to TerminatedEarly. It was previously reachable only by setting Status on the
   * plain edit — the same field that could reach Completed and strand the record with no completion
   * date. The endpoint sets the completion date with the status and records why.
   */
  const endEarly = useMutation({
    mutationFn: () => {
      if (!endingEarly) throw new Error('No appointment selected');
      return actingAppointmentService.terminateEarly(endingEarly.id, endEarlyReason.trim());
    },
    onSuccess: async () => {
      await refresh();
      setEndingEarly(null);
      setEndEarlyReason('');
      toast({ title: 'Acting appointment ended early' });
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'Could not end the appointment',
        description: e?.response?.data?.message ?? e?.message,
      }),
  });

  const complete = useMutation({
    mutationFn: () => {
      if (!completing) throw new Error('No appointment selected.');
      return actingAppointmentService.complete(completing.id, completionNotes || undefined);
    },
    onSuccess: () => {
      toast({ title: 'Acting appointment completed' });
      setCompleting(null);
      setCompletionNotes('');
      refresh();
    },
    onError: (error: any) =>
      toast({ title: 'Refused', description: error?.message, variant: 'destructive' }),
  });

  const receivesAllowance = form.watch('receivesActingAllowance');

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Acting appointments"
        description="Temporary cover for a post — while the incumbent is away, or while it is vacant."
        backHref="/hr/movements"
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus className="mr-2 h-4 w-4" />
            New appointment
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2">
        {([
          ['active', 'Active'],
          ['expiring', 'Ending soon'],
          ['converted', 'Made permanent'],
          ['all', 'All'],
        ] as [View, string][]).map(([key, label]) => (
          <Button
            key={key}
            size="sm"
            variant={view === key ? 'default' : 'outline'}
            onClick={() => setView(key)}
          >
            {key === 'expiring' && <CalendarClock className="mr-2 h-4 w-4" />}
            {label}
          </Button>
        ))}
      </div>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : items.length === 0 ? (
            <EmptyState title="No acting appointments" description="Nothing in this view." />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Acting as</TableHead>
                  <TableHead>Covering for</TableHead>
                  <TableHead>Period</TableHead>
                  <TableHead>Allowance</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((a) => (
                  <TableRow key={a.id}>
                    <TableCell className="font-medium">{a.appointmentNumber}</TableCell>
                    <TableCell>
                      <div>{a.employeeName}</div>
                      {a.employeeNumber && (
                        <div className="text-xs text-muted-foreground">{a.employeeNumber}</div>
                      )}
                    </TableCell>
                    <TableCell>{a.actingPositionTitle}</TableCell>
                    <TableCell>{a.actingForEmployeeName ?? '—'}</TableCell>
                    <TableCell>
                      {fmtDate(a.startDate)} → {a.endDate ? fmtDate(a.endDate) : 'open-ended'}
                    </TableCell>
                    <TableCell>
                      {a.receivesActingAllowance ? money(a.actingAllowance) : '—'}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={a.status} />
                      {a.convertedToPermanent && (
                        <Badge variant="outline" className="ml-2">
                          Permanent
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-2">
                        {/* Completed is where an appointment's terms are fixed; the API refuses
                            an edit after that, so the control is not offered. */}
                        {a.status !== 'Completed' && (
                          <Button size="sm" variant="ghost" onClick={() => openEdit(a)}>
                            <Pencil className="mr-2 h-4 w-4" />
                            Edit
                          </Button>
                        )}
                        {(a.status === 'Active' || a.status === 'Extended') && (
                          <>
                            <Button size="sm" variant="outline" onClick={() => setCompleting(a)}>
                              <CheckCircle2 className="mr-2 h-4 w-4" />
                              Complete
                            </Button>
                            {/* Ending early is a different act from completing: the appointment
                                stops before the date it was given. It needs a reason, which the
                                dialog requires and appends to the notes. */}
                            <Button size="sm" variant="ghost" onClick={() => setEndingEarly(a)}>
                              <XCircle className="mr-2 h-4 w-4" />
                              End early
                            </Button>
                          </>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <p className="text-xs text-muted-foreground">
        Payroll validates an acting allowance only after ten days of acting (FR-PAY011). The dates
        recorded here are what that rule reads — this module does not compute the allowance itself.
      </p>

      {/* ── New appointment ─────────────────────────────────────────────────── */}
      <Dialog open={creating} onOpenChange={setCreating}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>New acting appointment</DialogTitle>
            <DialogDescription>
              One person cannot hold two overlapping acting appointments — each would separately
              qualify them for an allowance.
            </DialogDescription>
          </DialogHeader>

          <form
            onSubmit={form.handleSubmit((values) => create.mutate(values))}
            className="grid gap-4 sm:grid-cols-2"
          >
            <EmployeePickerField form={form} name="employeeId" label="Employee acting" required />
            <EmployeePickerField
              form={form}
              name="actingForEmployeeId"
              label="Covering for"
              placeholder="Leave empty if the post is vacant"
            />

            <div className="space-y-2">
              <Label>
                Acting in position<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Select
                value={form.watch('actingPositionId')}
                onValueChange={(v) => form.setValue('actingPositionId', v)}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select the post being covered" />
                </SelectTrigger>
                <SelectContent>
                  {positions.map((p) => (
                    <SelectItem key={p.id} value={p.id}>
                      {p.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Reason</Label>
              <Select
                value={form.watch('reason')}
                onValueChange={(v) => form.setValue('reason', v as StaffActingReason)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {ACTING_REASONS.map((r) => (
                    <SelectItem key={r} value={r}>
                      {r.replace(/([A-Z])/g, ' $1').trim()}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="startDate">
                Starts<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Input id="startDate" type="date" {...form.register('startDate', { required: true })} />
            </div>

            <div className="space-y-2">
              <Label htmlFor="endDate">Ends</Label>
              <Input id="endDate" type="date" {...form.register('endDate')} />
            </div>

            <div className="sm:col-span-2 flex items-center justify-between rounded-md border p-3">
              <Label>Receives an acting allowance</Label>
              <Switch
                checked={receivesAllowance}
                onCheckedChange={(v) => form.setValue('receivesActingAllowance', v)}
              />
            </div>

            {receivesAllowance && (
              <div className="space-y-2">
                <Label htmlFor="actingAllowance">Allowance</Label>
                <Input
                  id="actingAllowance"
                  type="number"
                  step="0.01"
                  {...form.register('actingAllowance', { valueAsNumber: true })}
                />
              </div>
            )}

            <div className="space-y-2 sm:col-span-2">
              <Label htmlFor="notes">Notes</Label>
              <Textarea id="notes" rows={2} {...form.register('notes')} />
            </div>

            <DialogFooter className="sm:col-span-2">
              <Button type="button" variant="outline" onClick={() => setCreating(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={create.isPending}>
                {create.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Create
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Edit ────────────────────────────────────────────────────────────── */}
      <Dialog open={editing !== null} onOpenChange={(open) => !open && setEditing(null)}>
        <DialogContent className="max-h-[85vh] max-w-lg overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit {editing?.appointmentNumber}</DialogTitle>
            <DialogDescription>
              How long it runs, what it pays and where it has got to. Who is acting, in which post,
              from when is what the appointment is — change those and it is a different one.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="ae-end">Ends</Label>
                <Input
                  id="ae-end"
                  type="date"
                  value={edit.endDate}
                  onChange={(e) => setEdit((f) => ({ ...f, endDate: e.target.value }))}
                />
              </div>
              {/*
                ⚠ This was a Status select offering Active / Extended / TerminatedEarly, and the
                server now ignores the field entirely — so leaving the control would show something
                that appears to work and silently does nothing. It was never safe: assigning Status
                here also reached Completed, which left completionDate null and locked the record out
                of both this route and `complete`. Every one of these states now has a door that
                maintains what goes with it, so the status is shown rather than set.
              */}
              <div className="space-y-2">
                <Label>Status</Label>
                <div className="flex h-10 items-center">
                  <StatusBadge status={editing?.status ?? 'Active'} />
                </div>
                <p className="text-xs text-muted-foreground">
                  Status moves through its own actions — extend, end early, complete or convert —
                  never by editing. Each records what belongs with the change.
                </p>
              </div>
            </div>

            <div className="flex items-start gap-2 rounded-md border p-3">
              <Checkbox
                id="ae-allowance"
                checked={edit.receivesActingAllowance}
                onCheckedChange={(v) =>
                  setEdit((f) => ({ ...f, receivesActingAllowance: v === true }))
                }
              />
              <Label htmlFor="ae-allowance" className="cursor-pointer">
                Receives an acting allowance
              </Label>
            </div>

            {edit.receivesActingAllowance && (
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="ae-amount">Amount</Label>
                  <Input
                    id="ae-amount"
                    type="number"
                    min={0}
                    step="0.01"
                    value={edit.actingAllowance}
                    onChange={(e) => setEdit((f) => ({ ...f, actingAllowance: e.target.value }))}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="ae-calc">Worked out as</Label>
                  <Select
                    value={edit.allowanceCalculation || NONE}
                    onValueChange={(v) =>
                      setEdit((f) => ({
                        ...f,
                        allowanceCalculation: v === NONE ? '' : (v as HRAllowanceCalculationMethod),
                      }))
                    }
                  >
                    <SelectTrigger id="ae-calc">
                      <SelectValue placeholder="Not stated" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={NONE}>Not stated</SelectItem>
                      {ALLOWANCE_CALCULATIONS.map((c) => (
                        <SelectItem key={c} value={c}>
                          {c.replace(/([a-z])([A-Z])/g, '$1 $2')}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
            )}

            <div className="space-y-2">
              <Label htmlFor="ae-notes">Notes</Label>
              <Textarea
                id="ae-notes"
                rows={3}
                value={edit.notes}
                onChange={(e) => setEdit((f) => ({ ...f, notes: e.target.value }))}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(null)} disabled={saveEdit.isPending}>
              Cancel
            </Button>
            <Button
              onClick={() => editing && saveEdit.mutate({ id: editing.id })}
              disabled={saveEdit.isPending}
            >
              {saveEdit.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Complete ────────────────────────────────────────────────────────── */}
      <Dialog open={completing !== null} onOpenChange={(open) => !open && setCompleting(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Complete {completing?.appointmentNumber}</DialogTitle>
            <DialogDescription>
              The employee stops acting. Making the appointment permanent instead is a promotion
              movement, raised from the movement register.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="completionNotes">Notes</Label>
            <Textarea
              id="completionNotes"
              rows={3}
              value={completionNotes}
              onChange={(e) => setCompletionNotes(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCompleting(null)}>
              Cancel
            </Button>
            <Button onClick={() => complete.mutate()} disabled={complete.isPending}>
              {complete.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Complete
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={endingEarly !== null}
        onOpenChange={(open) => {
          if (!open) { setEndingEarly(null); setEndEarlyReason(''); }
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>End {endingEarly?.appointmentNumber} early</DialogTitle>
            <DialogDescription>
              The appointment stops before {endingEarly?.endDate
                ? new Date(endingEarly.endDate).toLocaleDateString()
                : 'the date it was given'}. Use Complete instead when it has simply run its course —
              the two say different things about what happened, and the allowance is worked out from
              the period actually served.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="endEarlyReason">Reason</Label>
            <Textarea
              id="endEarlyReason"
              rows={3}
              value={endEarlyReason}
              onChange={(e) => setEndEarlyReason(e.target.value)}
              placeholder="Why the appointment is ending before its end date"
            />
            {/* Required by the server, and required here so the refusal never has to explain it. */}
            <p className="text-xs text-muted-foreground">
              Recorded against the appointment. Ending someone&rsquo;s acting appointment early is a
              decision, so the record carries why.
            </p>
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => { setEndingEarly(null); setEndEarlyReason(''); }}
            >
              Cancel
            </Button>
            <Button
              onClick={() => endEarly.mutate()}
              disabled={endEarly.isPending || endEarlyReason.trim() === ''}
            >
              {endEarly.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              End early
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
