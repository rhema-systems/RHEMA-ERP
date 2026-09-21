'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { useQuery, useMutation } from '@tanstack/react-query';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { SalaryScalePicker, type SalaryScaleSelection } from '@/components/hr/common/SalaryScalePicker';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { movementService } from '@/services/hr/movement.service';
import { employeeService } from '@/services/hr/employee.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { useToast } from '@/hooks/use-toast';
import {
  MOVEMENT_TYPES,
  MOVEMENT_CATEGORIES,
  TEMPORARY_BY_NATURE,
  type StaffMovementType,
  type StaffMovementCategory,
} from '@/types/hr/movements';

interface FormValues {
  employeeId: string;
  movementType: StaffMovementType;
  category: StaffMovementCategory;
  newPositionId: string;
  newSupervisorId: string;
  newSalary: number;
  reason: string;
  justification: string;
  effectiveDate: string;
  isTemporary: boolean;
  temporaryEndDate: string;
  temporaryArrangementDetails: string;
  requiresEmployeeAcceptance: boolean;
  requiresHandover: boolean;
  additionalNotes: string;
}

const money = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2 });

/**
 * Raise a movement.
 *
 * The "current" half of the record is a SNAPSHOT taken from the employee, not something the user
 * types: position, unit, location, reporting line and salary are read off their record when they
 * are chosen, so the movement records where they actually were on the day it was raised. The user
 * only describes the destination.
 */
export default function NewMovementPage() {
  const router = useRouter();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const form = useForm<FormValues>({
    defaultValues: {
      employeeId: '',
      movementType: 'Promotion',
      category: 'CareerDevelopment',
      newPositionId: '',
      newSupervisorId: '',
      newSalary: 0,
      reason: '',
      justification: '',
      effectiveDate: '',
      isTemporary: false,
      temporaryEndDate: '',
      temporaryArrangementDetails: '',
      requiresEmployeeAcceptance: false,
      requiresHandover: false,
      additionalNotes: '',
    },
  });

  const employeeId = form.watch('employeeId');
  const movementType = form.watch('movementType');
  const isTemporary = form.watch('isTemporary');
  const newPositionId = form.watch('newPositionId');

  // getDetails, not getById: the list DTO carries only display NAMES, and the snapshot needs the
  // ids — position, unit, level, location level, manager — plus the salary.
  const { data: employee, isFetching: loadingEmployee } = useQuery({
    queryKey: ['hr', 'employee', 'details', employeeId],
    queryFn: () => employeeService.getDetails(employeeId),
    enabled: !!employeeId,
  });

  const { data: positions = [] } = useQuery({
    queryKey: ['hr', 'positions', 'all'],
    queryFn: () => employeePositionService.getAll(),
  });

  const newPosition = positions.find((p) => p.id === newPositionId);

  // The new placement on the scale (round 3, lane H). Grade follows the chosen position until the
  // user picks otherwise; the notch amount is offered as the new salary when none is typed yet.
  const [scale, setScale] = useState<SalaryScaleSelection>({ gradeId: '', levelId: '', notchId: '' });
  useEffect(() => {
    const g = newPosition?.salaryGradeId ?? '';
    if (g && !scale.gradeId) setScale({ gradeId: g, levelId: '', notchId: '' });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [newPosition?.salaryGradeId]);

  // Secondments and acting appointments are temporary by definition; tick it for the user rather
  // than letting a permanent secondment be raised by omission.
  useEffect(() => {
    if (TEMPORARY_BY_NATURE.includes(movementType)) form.setValue('isTemporary', true);
  }, [movementType, form]);

  const mutation = useMutation({
    mutationFn: async (values: FormValues) => {
      if (!employee) throw new Error('Select the employee being moved.');
      if (!newPosition) throw new Error('Select the position they are moving into.');

      return movementService.create({
        employeeId: values.employeeId,
        movementType: values.movementType,
        category: values.category,

        // Snapshot of where they are now, read from the employee record.
        currentPositionId: employee.positionId,
        currentOrganizationUnitId: employee.organizationUnitId ?? newPosition.organizationUnitId,
        currentOrganizationLevelId: employee.organizationLevelId ?? null,
        currentLocationId: employee.locationId ?? null,
        currentLocationLevelId: employee.locationLevelId ?? null,
        currentSupervisorId: employee.managerId ?? null,
        currentSalary: employee.salary ?? 0,

        newPositionId: values.newPositionId,
        newOrganizationUnitId: newPosition.organizationUnitId,
        newOrganizationLevelId: newPosition.organizationLevelId ?? null,
        newLocationId: employee.locationId ?? null,
        newSupervisorId: values.newSupervisorId || null,
        newSalary: Number(values.newSalary) || 0,
        // Round 3, lane H: the placement the movement writes at implementation. The grade defaults
        // to the position's; the notch is the user's — it is what the person will be paid.
        newSalaryGradeId: scale.gradeId || newPosition.salaryGradeId || null,
        newSalaryLevelId: scale.levelId || null,
        newSalaryNotchId: scale.notchId || null,

        reason: values.reason,
        justification: values.justification || null,
        effectiveDate: new Date(values.effectiveDate).toISOString(),
        isTemporary: values.isTemporary,
        temporaryEndDate: values.isTemporary && values.temporaryEndDate
          ? new Date(values.temporaryEndDate).toISOString()
          : null,
        temporaryArrangementDetails: values.temporaryArrangementDetails || null,
        requiresEmployeeAcceptance: values.requiresEmployeeAcceptance,
        requiresHandover: values.requiresHandover,
        additionalNotes: values.additionalNotes || null,
      });
    },
    onSuccess: (created) => {
      toast({ title: 'Movement raised', description: `${created.movementNumber} saved as a draft.` });
      router.push(`/hr/movements/${created.id}`);
    },
    onError: (error: any) => {
      toast({
        title: 'Could not raise the movement',
        description: error?.message ?? 'Unexpected error.',
        variant: 'destructive',
      });
    },
    onSettled: () => setSubmitting(false),
  });

  const onSubmit = (values: FormValues) => {
    setSubmitting(true);
    mutation.mutate(values);
  };

  const salaryDelta =
    employee && form.watch('newSalary')
      ? Number(form.watch('newSalary')) - (employee.salary ?? 0)
      : null;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Raise a staff movement"
        description="Saved as a draft. Nothing changes on the employee record until the movement is approved and implemented."
        backHref="/hr/movements"
      />

      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle>Who is moving</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <EmployeePickerField
              form={form}
              name="employeeId"
              label="Employee"
              required
              placeholder="Search by name or employee number"
            />

            {loadingEmployee && (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" /> Reading their current record…
              </div>
            )}

            {employee && (
              <div className="rounded-md border bg-muted/40 p-4 text-sm">
                <p className="mb-2 font-medium">Recorded as their position now</p>
                <dl className="grid gap-2 sm:grid-cols-2">
                  <div>
                    <dt className="text-muted-foreground">Position</dt>
                    <dd>{employee.positionTitle ?? '—'}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">Organisation unit</dt>
                    <dd>{employee.organizationUnitName ?? '—'}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">Location</dt>
                    <dd>{employee.locationName ?? '—'}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">Salary</dt>
                    <dd>{money(employee.salary)}</dd>
                  </div>
                </dl>
                <p className="mt-3 text-xs text-muted-foreground">
                  Captured with the movement as a snapshot, so the record stays accurate even if
                  their details change later.
                </p>
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>The move</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>
                Type<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Select
                value={movementType}
                onValueChange={(v) => form.setValue('movementType', v as StaffMovementType)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MOVEMENT_TYPES.map((t) => (
                    <SelectItem key={t} value={t}>
                      {t.replace(/([A-Z])/g, ' $1').trim()}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>
                Category<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Select
                value={form.watch('category')}
                onValueChange={(v) => form.setValue('category', v as StaffMovementCategory)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MOVEMENT_CATEGORIES.map((c) => (
                    <SelectItem key={c} value={c}>
                      {c.replace(/([A-Z])/g, ' $1').trim()}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2 sm:col-span-2">
              <Label>
                New position<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Select
                value={newPositionId}
                onValueChange={(v) => form.setValue('newPositionId', v)}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select the position they are moving into" />
                </SelectTrigger>
                <SelectContent>
                  {positions.map((p) => (
                    <SelectItem key={p.id} value={p.id}>
                      {p.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {newPosition && (
                <p className="text-xs text-muted-foreground">
                  Organisation unit and salary grade come from the position.
                </p>
              )}
            </div>

            <EmployeePickerField
              form={form}
              name="newSupervisorId"
              label="New reporting line"
              placeholder="Who they will report to"
            />

            <div className="space-y-2 md:col-span-2">
              <SalaryScalePicker
                value={scale}
                onChange={setScale}
                idPrefix="movement-scale"
                gradeLabel="New salary grade"
                onResolved={(r) => {
                  if (r.amount && !Number(form.getValues('newSalary'))) form.setValue('newSalary', r.amount);
                }}
              />
              <p className="text-xs text-muted-foreground">
                Implementing the movement places the employee here on the effective date; the
                previous placement ends the day before. Leave the notch empty to place on the grade
                alone.
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="newSalary">
                New salary<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Input
                id="newSalary"
                type="number"
                step="0.01"
                {...form.register('newSalary', { required: true, valueAsNumber: true })}
              />
              {salaryDelta !== null && employee && (
                <p className="text-xs text-muted-foreground">
                  {salaryDelta === 0
                    ? 'No change to salary.'
                    : `${salaryDelta > 0 ? 'Increase' : 'Decrease'} of ${money(Math.abs(salaryDelta))} on ${money(employee.salary)}.`}{' '}
                  The percentage is computed and stored by the server.
                </p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="effectiveDate">
                Effective date<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Input id="effectiveDate" type="date" {...form.register('effectiveDate', { required: true })} />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Why</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="reason">
                Reason<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Input id="reason" maxLength={500} {...form.register('reason', { required: true })} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="justification">Justification</Label>
              <Textarea id="justification" rows={3} maxLength={500} {...form.register('justification')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="additionalNotes">Additional notes</Label>
              <Textarea id="additionalNotes" rows={3} maxLength={2000} {...form.register('additionalNotes')} />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Arrangements</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex items-center justify-between rounded-md border p-3">
              <div>
                <Label>Temporary assignment</Label>
                <p className="text-xs text-muted-foreground">
                  Secondments and acting appointments are always temporary. A temporary movement has
                  a return to process at the end.
                </p>
              </div>
              <Switch
                checked={isTemporary}
                onCheckedChange={(v) => form.setValue('isTemporary', v)}
                disabled={TEMPORARY_BY_NATURE.includes(movementType)}
              />
            </div>

            {isTemporary && (
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="temporaryEndDate">Ends on</Label>
                  <Input id="temporaryEndDate" type="date" {...form.register('temporaryEndDate')} />
                </div>
                <div className="space-y-2 sm:col-span-2">
                  <Label htmlFor="temporaryArrangementDetails">Arrangement details</Label>
                  <Textarea
                    id="temporaryArrangementDetails"
                    rows={2}
                    maxLength={1000}
                    {...form.register('temporaryArrangementDetails')}
                  />
                </div>
              </div>
            )}

            <div className="flex items-center justify-between rounded-md border p-3">
              <div>
                <Label>Requires the employee&apos;s acceptance</Label>
                <p className="text-xs text-muted-foreground">
                  They accept or decline it themselves — nobody, HR included, can record that for
                  them.
                </p>
              </div>
              <Switch
                checked={form.watch('requiresEmployeeAcceptance')}
                onCheckedChange={(v) => form.setValue('requiresEmployeeAcceptance', v)}
              />
            </div>

            <div className="flex items-center justify-between rounded-md border p-3">
              <div>
                <Label>Requires a handover</Label>
                <p className="text-xs text-muted-foreground">
                  Recorded on the movement once the outgoing duties have been handed over.
                </p>
              </div>
              <Switch
                checked={form.watch('requiresHandover')}
                onCheckedChange={(v) => form.setValue('requiresHandover', v)}
              />
            </div>
          </CardContent>
        </Card>

        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => router.push('/hr/movements')}>
            Cancel
          </Button>
          <Button type="submit" disabled={submitting}>
            {submitting ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            Save draft
          </Button>
        </div>
      </form>
    </div>
  );
}
