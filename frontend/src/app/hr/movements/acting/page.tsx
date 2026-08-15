'use client';

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { Loader2, Plus, CalendarClock, CheckCircle2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
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
  type StaffActingReason,
  type StaffActingAppointment,
} from '@/types/hr/movement-subtypes';

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
                      {(a.status === 'Active' || a.status === 'Extended') && (
                        <Button size="sm" variant="outline" onClick={() => setCompleting(a)}>
                          <CheckCircle2 className="mr-2 h-4 w-4" />
                          Complete
                        </Button>
                      )}
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
    </div>
  );
}
