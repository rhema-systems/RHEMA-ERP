'use client';

import { useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useQuery } from '@tanstack/react-query';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { AddressFields } from '@/components/reference/AddressFields';
import { employeeService } from '@/services/hr/employee.service';
import { referenceDimensionService } from '@/services/hr/lookup.service';
import { policySettingsService } from '@/services/hr/policy-settings.service';
import { contractTypeService } from '@/services/hr/contract-type.service';
import { disabilityTypeService } from '@/services/hr/disability-type.service';
import { disabilityTypeOptions } from '@/types/hr/disability-type';
import {
  GENDER_OPTIONS,
  MARITAL_STATUS_OPTIONS,
  STAFF_STATUS_OPTIONS,
  EMPLOYMENT_TYPE_OPTIONS,
  BLOOD_TYPE_OPTIONS,
  OFF_PAYROLL_REASON_OPTIONS,
  MAINTENANCE_ASSIGNMENT_OPTIONS,
  EXPERIENCE_LEVEL_OPTIONS,
  type MaintenanceAssignmentMode,
} from '@/types/hr/employee';
import type { EmployeePosition } from '@/types/hr/position';
import type { OrganizationLevel } from '@/types/hr/organization';
import type { Location, LocationLevel } from '@/types/hr/location';

const opt = z.string().optional().or(z.literal(''));

/** The disability picker's stand-in for an empty optional — a Radix Select item cannot carry ''. */
const NO_DISABILITY_TYPE = '__none__';

export const employeeSchema = z.object({
  employeeNumber: opt,
  firstName: z.string().min(1, 'First name is required').max(100),
  middleName: opt,
  lastName: z.string().min(1, 'Last name is required').max(100),
  title: opt,
  gender: opt,
  dateOfBirth: opt,
  maritalStatus: opt,
  religion: opt,
  genderDescription: opt,
  hometown: opt,
  hasDisability: z.boolean(),
  disabilityTypeId: opt,
  disabilityDescription: opt,
  bloodType: opt,
  isExpatriate: z.boolean(),
  // Optional since 2026-09-03: a register never has an address for everyone. Format-checked when given.
  emailAddress: z.string().email('Invalid email address').optional().or(z.literal('')),
  mobileNumber: opt,
  telephoneNumber: opt,
  address: opt,
  // ⚠ Snapshot fields since 2026-09-03. When geoAreaId is set the server overwrites both from the
  // geography tree, so what is typed here only survives for a country with no scheme loaded.
  city: opt,
  state: opt,
  postalCode: opt,
  digitalAddress: opt,
  countryId: opt,
  /** The deepest administrative area chosen — one id, whatever the scheme's depth. */
  geoAreaId: opt,
  positionId: z.string().min(1, 'Position is required'),
  organizationUnitId: z.string().min(1, 'Select a position to set the organization unit'),
  locationId: z.string().min(1, 'Location is required'),
  managerId: opt,
  employmentType: z.string().min(1),
  staffStatus: z.string().min(1),
  dateEmployed: opt,
  // ⚠ Derived, not typed. The server settles the term against the POSITION and refuses a value
  // that contradicts it, so the form holds whatever it is going to show and only SENDS it where the
  // position is silent. Kept in the schema because the field is still editable in that case.
  probationPeriodDays: z.coerce.number().int('Must be a whole number').min(0),
  /** Import mode only: somebody confirmed before this system existed. */
  confirmationDate: z.string().optional().or(z.literal('')),
  /** The kind of engagement the first contract records. Optional. */
  contractTypeId: z.string().optional().or(z.literal('')),
  isFullTime: z.boolean(),
  // Payroll MEMBERSHIP stays on the form: it is HR's fact and the create needs it to enrol the
  // person. The amount and the five switches do not (round-2 plan Q-3) — all remuneration lives on
  // the Salary tab, and a person is created with no pay basis; the reconciliation reports
  // `NoPayBasis` until the tab is filled in.
  isOnPayroll: z.boolean(),
  offPayrollReason: opt,
  offPayrollNote: opt,
  taxNumber: opt,
  socialSecurityNumber: opt,
  tinNumber: opt,
  badgeNumber: opt,
  notes: opt,
  // Round 4, lane O — whether Maintenance may assign this person work, and their trade.
  maintenanceAssignment: z.enum(['FollowPosition', 'Include', 'Exclude']),
  specialization: z.string().max(100, 'At most 100 characters').optional().or(z.literal('')),
  certificationLevel: z.string().max(50, 'At most 50 characters').optional().or(z.literal('')),
  experienceLevel: opt,
}).superRefine((v, ctx) => {
  if (!v.isOnPayroll && !v.offPayrollReason) {
    ctx.addIssue({
      code: z.ZodIssueCode.custom,
      path: ['offPayrollReason'],
      message: 'Say how this employee is paid instead.',
    });
  }
});

export type EmployeeFormValues = z.infer<typeof employeeSchema>;

export const emptyEmployee: EmployeeFormValues = {
  employeeNumber: '',
  firstName: '',
  middleName: '',
  lastName: '',
  title: '',
  gender: '',
  dateOfBirth: '',
  maritalStatus: '',
  religion: '',
  genderDescription: '',
  hometown: '',
  hasDisability: false,
  disabilityTypeId: '',
  disabilityDescription: '',
  bloodType: '',
  isExpatriate: false,
  emailAddress: '',
  mobileNumber: '',
  telephoneNumber: '',
  address: '',
  city: '',
  state: '',
  postalCode: '',
  digitalAddress: '',
  countryId: '',
  geoAreaId: '',
  positionId: '',
  organizationUnitId: '',
  locationId: '',
  managerId: '',
  employmentType: 'Permanent',
  staffStatus: 'Active',
  dateEmployed: '',
  probationPeriodDays: 90,
  confirmationDate: '',
  contractTypeId: '',
  isFullTime: true,
  isOnPayroll: true,
  offPayrollReason: '',
  offPayrollNote: '',
  taxNumber: '',
  socialSecurityNumber: '',
  tinNumber: '',
  badgeNumber: '',
  notes: '',
  maintenanceAssignment: 'FollowPosition',
  specialization: '',
  certificationLevel: '',
  experienceLevel: '',
};

interface EmployeeFormProps {
  positions: EmployeePosition[];
  orgLevels: OrganizationLevel[];
  locations: Location[];
  locationLevels: LocationLevel[];
  defaultValues: EmployeeFormValues;
  /**
   * @param meta.probationIsDerived
   *   Whether the selected position states the probation term. ⚠ The page needs it because the
   *   mapper must then send `null` rather than the number on screen — the server refuses a value
   *   that contradicts the post, and the form is showing the post's own figure.
   */
  onSubmit: (
    values: EmployeeFormValues,
    meta: { probationIsDerived: boolean },
  ) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
  initialManagerLabel?: string | null;
  /** The employee's saved location level, to seed the location cascade on edit. */
  initialLocationLevelId?: string | null;
  /**
   * Show what the tenant's numbering rules will do with the staff-number field. Create only.
   *
   * ⚠ On edit the number already exists and the rule has nothing to say about it, so the field
   * carries no claim at all rather than a claim that is false.
   */
  showNumberingRule?: boolean;
  /**
   * Present when the caller can record an employee who already HAS a staff number, i.e. a data
   * load rather than a hire. Providing the handler is what puts the choice on the form.
   */
  importMode?: boolean;
  onImportModeChange?: (value: boolean) => void;
  /**
   * Whether this is a create rather than an edit.
   *
   * ⚠ The contract kind is only offered on a create, because that is the only path that opens a
   * contract. On an edit the employee already HAS contracts and the kind belongs to whichever one
   * is in force — the Contracts tab edits it. Offering the field here would be a control that
   * looks like it changes something and does not.
   */
  isCreate?: boolean;
}

// Sort comparators: levels by number then name; everything else alphabetical.
const byLevelThenName = (a: { levelNumber: number; name: string }, b: { levelNumber: number; name: string }) =>
  a.levelNumber - b.levelNumber || a.name.localeCompare(b.name);
const byName = (a: { name: string }, b: { name: string }) => a.name.localeCompare(b.name);
const byTitle = (a: { title: string }, b: { title: string }) => a.title.localeCompare(b.title);

// --- small presentational helpers ---
function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="space-y-5 border-t pt-6 first:border-t-0 first:pt-0">
      <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">{title}</h3>
      {children}
    </section>
  );
}

function Field({
  label,
  htmlFor,
  error,
  hint,
  children,
  className,
}: {
  label: string;
  htmlFor?: string;
  error?: string;
  hint?: string;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div className={`space-y-2 ${className ?? ''}`}>
      <Label htmlFor={htmlFor}>{label}</Label>
      {children}
      {hint && !error && <p className="text-xs text-muted-foreground">{hint}</p>}
      {error && <p className="text-sm text-red-500">{error}</p>}
    </div>
  );
}

function SwitchRow({
  id,
  label,
  checked,
  onChange,
}: {
  id: string;
  label: string;
  checked: boolean;
  onChange: (v: boolean) => void;
}) {
  return (
    <div className="flex items-center justify-between rounded-md border px-3 py-2.5">
      <Label htmlFor={id} className="cursor-pointer">
        {label}
      </Label>
      <Switch id={id} checked={checked} onCheckedChange={onChange} />
    </div>
  );
}

function OptionalSelect({
  value,
  onChange,
  placeholder,
  options,
  id,
}: {
  value: string;
  onChange: (v: string) => void;
  placeholder: string;
  options: { value: string; label: string }[];
  id?: string;
}) {
  const NONE = '__none__';
  return (
    <Select value={value || NONE} onValueChange={(v) => onChange(v === NONE ? '' : v)}>
      <SelectTrigger id={id}>
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={NONE}>{placeholder}</SelectItem>
        {options.map((o) => (
          <SelectItem key={o.value} value={o.value}>
            {o.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

const GRID3 = 'grid grid-cols-1 gap-x-6 gap-y-5 sm:grid-cols-2 lg:grid-cols-3';

export function EmployeeForm({
  positions,
  orgLevels,
  locations,
  locationLevels,
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
  initialManagerLabel,
  initialLocationLevelId,
  showNumberingRule = false,
  importMode = false,
  onImportModeChange,
  isCreate = false,
}: EmployeeFormProps) {
  // Round 3, lane P2: the disability catalogue the tick opens onto. Live rows only.
  const { data: disabilityTypes } = useQuery({
    queryKey: ['hr', 'disability-types', 'active'],
    queryFn: () => disabilityTypeService.getActive(),
  });
  const disabilityOptions = useMemo(() => disabilityTypeOptions(disabilityTypes ?? []), [disabilityTypes]);

  const form = useForm<EmployeeFormValues>({
    resolver: zodResolver(employeeSchema) as any,
    defaultValues,
  });

  const positionId = form.watch('positionId');
  const locationId = form.watch('locationId');
  const countryId = form.watch('countryId') ?? '';
  const geoAreaId = form.watch('geoAreaId') ?? '';
  const isOnPayroll = form.watch('isOnPayroll');
  const managerId = form.watch('managerId') || null;
  const selectedPosition = positions.find((p) => p.id === positionId);

  // Round 4, lane O — what the Maintenance choice means for the position chosen above, in words.
  // ⚠ The answer to "follow the position" is the POST's flag, read live, so changing the position
  // on this form changes the answer before anything is saved.
  const maintenanceAssignment = form.watch('maintenanceAssignment');
  const maintenanceHint = !selectedPosition
    ? 'Choose a position first. A technician role makes its holders available to Maintenance.'
    : maintenanceAssignment === 'FollowPosition'
      ? selectedPosition.isTechnicianRole
        ? `Available: ${selectedPosition.title} is a technician role, so Maintenance can assign this person work orders.`
        : `Not available: ${selectedPosition.title} is not a technician role.`
      : maintenanceAssignment === 'Include'
        ? 'Available by HR’s decision for this person, whatever the position says — for example, seconded in. Kept if they change position.'
        : 'Not available by HR’s decision for this person, even in a technician role — for example, long-term light duties. Kept if they change position.';

  // Whoever holds the position this one reports to — offered as the manager, rather than leaving
  // a free search over 1,507 people to guess at a line the org chart already knows.
  const [managerLabel, setManagerLabel] = useState<string | null>(initialManagerLabel ?? null);
  const reportsToPositionId = selectedPosition?.reportsToPositionId ?? null;
  const supervisors = useQuery({
    queryKey: ['hr', 'employees', 'by-position', reportsToPositionId],
    queryFn: () => employeeService.searchPaged({ positionId: reportsToPositionId as string }, 1, 10),
    enabled: !!reportsToPositionId,
  });
  const supervisorOptions = (supervisors.data?.items ?? []).map((e: any) => ({
    id: e.id as string,
    label: (e.displayName || e.fullName || `${e.firstName ?? ''} ${e.lastName ?? ''}`).trim(),
  }));

  // ── what the register's numbering rule will do with the staff number ──────
  //
  // ⚠ The hint here used to read "Auto-generated if left blank", which stopped being true the day
  // numbering became configuration. Whether a number is issued or typed is the REGISTER's
  // decision now, and a register with no rule REFUSES a blank rather than inventing one — so a
  // form promising to fill it in was sending people into a create that fails.
  const employmentType = form.watch('employmentType');
  const numberingRules = useQuery({
    queryKey: ['hr', 'staff-number-formats'],
    queryFn: () => referenceDimensionService.getStaffNumberFormats(),
    enabled: showNumberingRule,
  });

  // Own rule first, then the tenant default — the same resolution the server does, and only
  // ACTIVE rules compete, because retiring one stops it governing new hires immediately.
  const numberingRule = useMemo(() => {
    const active = (numberingRules.data ?? []).filter((r) => r.isActive);
    return (
      active.find((r) => r.appliesToEmploymentType === employmentType) ??
      active.find((r) => r.appliesToEmploymentType === null) ??
      null
    );
  }, [numberingRules.data, employmentType]);

  const registerLabel =
    EMPLOYMENT_TYPE_OPTIONS.find((o) => o.value === employmentType)?.label.toLowerCase() ?? 'these';

  // ── the probation term, which belongs to the POST ─────────────────────────
  //
  // ⚠ This box used to be a free number defaulted to 90, and 90 is nobody's probation at TDC:
  // junior posts run three months, senior and management six, and 123 of 146 positions carry the
  // term. Every create was overwriting a maintained figure with a form default. The server now
  // settles it against the position and REFUSES a contradicting value, so the form shows what the
  // post says and only opens the field where the post says nothing.
  const policySettings = useQuery({
    queryKey: ['hr', 'policy-settings'],
    queryFn: () => policySettingsService.get(),
  });

  const positionProbationMonths = selectedPosition?.probationPeriodMonths ?? null;
  const policyProbationMonths = policySettings.data?.defaultProbationMonths ?? null;

  const probationFromPosition = positionProbationMonths != null && positionProbationMonths > 0;
  const probationMonths = probationFromPosition
    ? positionProbationMonths
    : policyProbationMonths;

  // The stored unit is days, and months × 30 is the conversion the hire path established.
  const derivedProbationDays = probationMonths != null ? probationMonths * 30 : null;

  // Editable only where the post is silent — then the company default stands unless somebody
  // says otherwise for this person, which is the one case an override is a real answer.
  const probationIsDerived = probationFromPosition;

  useEffect(() => {
    if (probationIsDerived && derivedProbationDays != null) {
      form.setValue('probationPeriodDays', derivedProbationDays);
    }
  }, [probationIsDerived, derivedProbationDays, form]);

  const dateEmployed = form.watch('dateEmployed');
  const probationDaysShown = form.watch('probationPeriodDays');

  // What the profile will show as "expected confirmation" — the same arithmetic the server does on
  // read, put in front of whoever is filling the form in rather than discovered a quarter later.
  const expectedConfirmation = useMemo(() => {
    if (!dateEmployed || !probationDaysShown || probationDaysShown <= 0) return null;
    const start = new Date(dateEmployed);
    if (Number.isNaN(start.getTime())) return null;
    start.setDate(start.getDate() + Number(probationDaysShown));
    return start.toISOString().slice(0, 10);
  }, [dateEmployed, probationDaysShown]);

  const probationHint = !probationIsDerived
    ? policyProbationMonths != null
      ? `This position states no probation period, so the company default of ${policyProbationMonths} month(s) applies. Change it here only for this employee.`
      : 'This position states no probation period.'
    : `${positionProbationMonths} month(s), from the position${
        expectedConfirmation ? ` — expected confirmation ${expectedConfirmation}` : ''
      }. Change it on the position, not here.`;

  const contractTypes = useQuery({
    queryKey: ['hr', 'contract-types', 'active'],
    queryFn: () => contractTypeService.getActive(),
    enabled: isCreate,
  });
  const numberIsIssued = showNumberingRule && !importMode && !!numberingRule?.autoGenerate;

  // A disabled input still submits whatever react-hook-form holds. Without this, typing a number
  // and then switching to an auto-numbered register sends a value the server is obliged to refuse,
  // from a field the user can no longer see or clear.
  useEffect(() => {
    if (numberIsIssued && form.getValues('employeeNumber')) {
      form.setValue('employeeNumber', '');
    }
  }, [numberIsIssued, form]);

  const numberHint = !showNumberingRule
    ? undefined
    : importMode
      ? 'Required — the number this employee already has. It is kept exactly as given, and the register’s counter is moved past it so the next hire does not collide with it.'
      : numberingRule?.autoGenerate
        ? `Issued automatically from “${numberingRule.name}”, e.g. ${numberingRule.example}. A number cannot be supplied while that rule is on.`
        : numberingRule
          ? `Required — “${numberingRule.name}” is set to be entered by hand.`
          : `Required — no numbering rule is configured for ${registerLabel} staff, so the system does not issue one.`;

  // Org level name derived from the position (its DTO carries the level id, but
  // the level name isn't always populated — resolve it against the levels list).
  const orgLevelName = orgLevels.find((l) => l.id === selectedPosition?.organizationLevelId)?.name;

  // Sorted option lists for the dropdowns.
  const sortedPositions = useMemo(() => [...positions].sort(byTitle), [positions]);
  const sortedLocationLevels = useMemo(() => [...locationLevels].sort(byLevelThenName), [locationLevels]);

  // Location cascade: pick a location level, then the location is filtered to it.
  // The level itself is not submitted (server derives it from the location).
  // Seed it (edit mode) directly from the employee's saved location level.
  const [locationLevelId, setLocationLevelId] = useState(initialLocationLevelId ?? '');

  // Fallback: if no initial level was provided, derive it from the saved location
  // once the locations list has loaded.
  useEffect(() => {
    if (!locationLevelId && locationId) {
      const loc = locations.find((l) => l.id === locationId);
      if (loc?.locationLevelId) setLocationLevelId(loc.locationLevelId);
    }
  }, [locations, locationId, locationLevelId]);

  const locationsForLevel = useMemo(
    () => locations.filter((l) => l.locationLevelId === locationLevelId).sort(byName),
    [locations, locationLevelId],
  );

  const handlePositionChange = (value: string) => {
    form.setValue('positionId', value, { shouldValidate: true });
    const pos = positions.find((p) => p.id === value);
    // Employee's org unit must equal the position's org unit (backend-enforced).
    form.setValue('organizationUnitId', pos?.organizationUnitId ?? '', { shouldValidate: true });
  };

  const handleLocationLevelChange = (value: string) => {
    setLocationLevelId(value);
    const stillValid = locations.some((l) => l.id === locationId && l.locationLevelId === value);
    if (!stillValid) form.setValue('locationId', '', { shouldValidate: true });
  };

  const err = (name: keyof EmployeeFormValues) =>
    form.formState.errors[name]?.message as string | undefined;

  return (
    <Card>
      <form onSubmit={form.handleSubmit((values) => onSubmit(values, { probationIsDerived }))}>
        <CardHeader>
          <CardTitle>Employee Details</CardTitle>
        </CardHeader>
        <CardContent className="space-y-8">
          {/* Identity & Personal */}
          <Section title="Identity & Personal">
            <div className={GRID3}>
              <Field label="First Name" htmlFor="firstName" error={err('firstName')}>
                <Input id="firstName" {...form.register('firstName')} />
              </Field>
              <Field label="Middle Name" htmlFor="middleName">
                <Input id="middleName" {...form.register('middleName')} />
              </Field>
              <Field label="Last Name" htmlFor="lastName" error={err('lastName')}>
                <Input id="lastName" {...form.register('lastName')} />
              </Field>
            </div>
            <div className={GRID3}>
              <Field label="Employee Number" htmlFor="employeeNumber" hint={numberHint}>
                <Input
                  id="employeeNumber"
                  disabled={numberIsIssued}
                  placeholder={numberIsIssued ? numberingRule?.example : undefined}
                  {...form.register('employeeNumber')}
                />
                {onImportModeChange && (
                  <label className="flex items-start gap-2 pt-1 text-xs text-muted-foreground">
                    <Checkbox
                      id="importMode"
                      className="mt-0.5"
                      checked={importMode}
                      onCheckedChange={(v) => onImportModeChange(v === true)}
                    />
                    <span>
                      This person already has a staff number (recording an existing employee, not a
                      new hire)
                    </span>
                  </label>
                )}
              </Field>
              <Field label="Title" htmlFor="title">
                <Input id="title" placeholder="Mr / Ms / Dr" {...form.register('title')} />
              </Field>
              <Field label="Gender" htmlFor="gender">
                <OptionalSelect
                  id="gender"
                  value={form.watch('gender') ?? ''}
                  onChange={(v) => form.setValue('gender', v)}
                  placeholder="Not set"
                  options={GENDER_OPTIONS}
                />
              </Field>
            </div>
            {/* Only asked when it means something. The enum offered "Other" and then had nowhere
                to say what other meant, which makes the option a dead end for whoever picks it.
                Sits directly under Gender (demo feedback round 2, E-1) — it used to be two rows
                away, beside Hometown, where nobody connected it with the dropdown. */}
            {form.watch('gender') === 'Other' && (
              <div className={GRID3}>
                <Field label="Describe gender" htmlFor="genderDescription" className="lg:col-start-3">
                  <Input id="genderDescription" {...form.register('genderDescription')} />
                </Field>
              </div>
            )}
            <div className={GRID3}>
              <Field label="Date of Birth" htmlFor="dateOfBirth">
                <Input id="dateOfBirth" type="date" {...form.register('dateOfBirth')} />
              </Field>
              <Field label="Marital Status" htmlFor="maritalStatus">
                <OptionalSelect
                  id="maritalStatus"
                  value={form.watch('maritalStatus') ?? ''}
                  onChange={(v) => form.setValue('maritalStatus', v)}
                  placeholder="Not set"
                  options={MARITAL_STATUS_OPTIONS}
                />
              </Field>
              <Field label="Blood Type" htmlFor="bloodType">
                <OptionalSelect
                  id="bloodType"
                  value={form.watch('bloodType') ?? ''}
                  onChange={(v) => form.setValue('bloodType', v)}
                  placeholder="Not set"
                  options={BLOOD_TYPE_OPTIONS}
                />
              </Field>
            </div>
            <div className={GRID3}>
              <Field label="Religion" htmlFor="religion">
                <Input id="religion" {...form.register('religion')} />
              </Field>
              <Field label="Hometown" htmlFor="hometown">
                <Input id="hometown" {...form.register('hometown')} />
              </Field>
            </div>

            {/* ⚠ The EMPLOYEE's own disability. The one on a dependant is a different fact about a
                different person and is edited on the Dependants tab; neither replaces the other. */}
            <div className="space-y-3">
              <div className="flex items-center gap-2">
                <Checkbox
                  id="hasDisability"
                  checked={!!form.watch('hasDisability')}
                  onCheckedChange={(v) => form.setValue('hasDisability', v === true)}
                />
                <Label htmlFor="hasDisability" className="cursor-pointer">
                  This employee has a disability
                </Label>
              </div>
              {form.watch('hasDisability') && (
                <div className={GRID3}>
                  {/* Round 3, lane P2 (register row E-5): tick → pick from the catalogue, notes beside. */}
                  <Field label="Disability type" htmlFor="disabilityTypeId">
                    <Select
                      value={form.watch('disabilityTypeId') || NO_DISABILITY_TYPE}
                      onValueChange={(v) => form.setValue('disabilityTypeId', v === NO_DISABILITY_TYPE ? '' : v)}
                    >
                      <SelectTrigger id="disabilityTypeId" data-testid="employee-disability-type">
                        <SelectValue placeholder="Pick from the catalogue" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value={NO_DISABILITY_TYPE}>Not specified</SelectItem>
                        {disabilityOptions.map((o) => (
                          <SelectItem key={o.value} value={o.value}>
                            {o.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </Field>
                  <div className="sm:col-span-2">
                    <Field label="Notes" htmlFor="disabilityDescription">
                      <Input
                        id="disabilityDescription"
                        placeholder="In their words where possible — the accommodation needed, or a condition the list does not name"
                        {...form.register('disabilityDescription')}
                      />
                    </Field>
                  </div>
                </div>
              )}
            </div>
          </Section>

          {/* Contact */}
          <Section title="Contact">
            <div className={GRID3}>
              <Field label="Email" htmlFor="emailAddress" error={err('emailAddress')}>
                <Input id="emailAddress" type="email" {...form.register('emailAddress')} />
              </Field>
              <Field label="Mobile" htmlFor="mobileNumber">
                <Input id="mobileNumber" {...form.register('mobileNumber')} />
              </Field>
              <Field label="Telephone" htmlFor="telephoneNumber">
                <Input id="telephoneNumber" {...form.register('telephoneNumber')} />
              </Field>
            </div>
            {/*
              The address cascade. Its dropdown LABELS come from the selected country's scheme —
              Region / District / Town / Community for Ghana — so this block carries no
              country-specific code and never should.

              ⚠ City and Region below are only editable when the country has no scheme loaded.
              With one, the server rewrites both from the chosen area, so an editable box would be
              a field that silently discards what you type.
            */}
            <AddressFields
              countryId={countryId}
              onCountryChange={(value) =>
                form.setValue('countryId', value, { shouldValidate: true, shouldDirty: true })
              }
              geoAreaId={geoAreaId}
              onGeoAreaChange={(value) =>
                form.setValue('geoAreaId', value, { shouldValidate: true, shouldDirty: true })
              }
              fallback={(schemeLoaded) => (
                <>
                  <div className={`${GRID3} mt-4`}>
                    <Field label="Address" htmlFor="address" className="sm:col-span-2">
                      <Input id="address" {...form.register('address')} />
                    </Field>
                    <Field
                      label="City / Town"
                      htmlFor="city"
                      hint={schemeLoaded ? 'Set from the address above' : undefined}
                    >
                      <Input id="city" {...form.register('city')} readOnly={schemeLoaded} disabled={schemeLoaded} />
                    </Field>
                  </div>
                  <div className={GRID3}>
                    <Field
                      label="State / Region"
                      htmlFor="state"
                      hint={schemeLoaded ? 'Set from the address above' : undefined}
                    >
                      <Input id="state" {...form.register('state')} readOnly={schemeLoaded} disabled={schemeLoaded} />
                    </Field>
                    <Field label="Postal Code" htmlFor="postalCode">
                      <Input id="postalCode" {...form.register('postalCode')} />
                    </Field>
                    <Field label="Digital Address" htmlFor="digitalAddress">
                      <Input id="digitalAddress" {...form.register('digitalAddress')} />
                    </Field>
                  </div>
                </>
              )}
            />
          </Section>

          {/* Employment */}
          <Section title="Employment">
            <div className={GRID3}>
              <Field label="Position" htmlFor="positionId" error={err('positionId')}>
                <Select value={positionId || undefined} onValueChange={handlePositionChange}>
                  <SelectTrigger id="positionId">
                    <SelectValue placeholder="Select a position" />
                  </SelectTrigger>
                  <SelectContent>
                    {sortedPositions.map((p) => (
                      <SelectItem key={p.id} value={p.id}>
                        {p.title}
                        {p.organizationUnitName ? ` · ${p.organizationUnitName}` : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
              <Field label="Organization Level" hint="From the selected position">
                <Input value={orgLevelName ?? ''} placeholder="—" readOnly disabled />
              </Field>
              <Field label="Organization Unit" hint="From the selected position" error={err('organizationUnitId')}>
                <Input value={selectedPosition?.organizationUnitName ?? ''} placeholder="—" readOnly disabled />
              </Field>
            </div>
            <div className={GRID3}>
              <Field label="Location Level">
                <Select value={locationLevelId || undefined} onValueChange={handleLocationLevelChange}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select a location level" />
                  </SelectTrigger>
                  <SelectContent>
                    {sortedLocationLevels.length === 0 ? (
                      <div className="px-2 py-1.5 text-sm text-muted-foreground">No location levels.</div>
                    ) : (
                      sortedLocationLevels.map((l) => (
                        <SelectItem key={l.id} value={l.id}>
                          {l.name}
                        </SelectItem>
                      ))
                    )}
                  </SelectContent>
                </Select>
              </Field>
              <Field label="Location" htmlFor="locationId" error={err('locationId')}>
                <Select
                  value={locationId || undefined}
                  disabled={!locationLevelId}
                  onValueChange={(v) => form.setValue('locationId', v, { shouldValidate: true })}
                >
                  <SelectTrigger id="locationId">
                    <SelectValue placeholder={locationLevelId ? 'Select a location' : 'Select a level first'} />
                  </SelectTrigger>
                  <SelectContent>
                    {locationsForLevel.length === 0 ? (
                      <div className="px-2 py-1.5 text-sm text-muted-foreground">No locations at this level.</div>
                    ) : (
                      locationsForLevel.map((l) => (
                        <SelectItem key={l.id} value={l.id}>
                          {l.name}
                        </SelectItem>
                      ))
                    )}
                  </SelectContent>
                </Select>
              </Field>
              <Field label="Manager">
                <EmployeePicker
                  value={managerId}
                  // Driven by state, not the prop, so clicking a suggested supervisor updates the
                  // picker's own label instead of leaving it showing the previous manager.
                  initialLabel={managerLabel}
                  onChange={(id, label) => {
                    form.setValue('managerId', id ?? '');
                    setManagerLabel(label);
                  }}
                  placeholder="Search for a manager…"
                />
                {/*
                  ⚠ The reporting line was recorded and ignored. `EmployeePosition.ReportsToPositionId`
                  is populated on 121 of 174 positions and drives the organogram, while this picker
                  was a free search over 1,507 people — so the org chart and the manager on the
                  employee record could disagree with nothing to notice it.

                  Suggested, not enforced: a manager is not always the holder of the supervising
                  post (acting arrangements, matrix reporting), so the free search stays.
                */}
                {selectedPosition?.reportsToPositionId && (
                  <div className="mt-2 rounded-md border bg-muted/40 p-2 text-sm">
                    <p className="text-muted-foreground">
                      This position reports to{' '}
                      <span className="font-medium text-foreground">
                        {selectedPosition.reportsToPositionTitle ?? 'another position'}
                      </span>
                      .
                    </p>
                    {supervisors.isLoading ? (
                      <p className="mt-1 text-xs text-muted-foreground">Finding who holds it…</p>
                    ) : supervisorOptions.length === 0 ? (
                      // A vacant supervising post is worth saying out loud — it is why the
                      // suggestion is empty, and it is a real fact about the org chart.
                      <p className="mt-1 text-xs text-amber-600">
                        Nobody currently holds that position, so there is no one to suggest.
                      </p>
                    ) : (
                      <div className="mt-1.5 flex flex-wrap gap-1.5">
                        {supervisorOptions.map((sup) => (
                          <Button
                            key={sup.id}
                            type="button"
                            variant={managerId === sup.id ? 'default' : 'outline'}
                            size="sm"
                            onClick={() => {
                              form.setValue('managerId', sup.id);
                              setManagerLabel(sup.label);
                            }}
                          >
                            {sup.label}
                          </Button>
                        ))}
                      </div>
                    )}
                  </div>
                )}
              </Field>
            </div>
            <div className={GRID3}>
              <Field label="Employment Type" htmlFor="employmentType">
                <Select
                  value={form.watch('employmentType')}
                  onValueChange={(v) => form.setValue('employmentType', v)}
                >
                  <SelectTrigger id="employmentType">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {EMPLOYMENT_TYPE_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>
                        {o.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
              <Field label="Staff Status" htmlFor="staffStatus">
                <Select
                  value={form.watch('staffStatus')}
                  onValueChange={(v) => form.setValue('staffStatus', v)}
                >
                  <SelectTrigger id="staffStatus">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {STAFF_STATUS_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>
                        {o.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
              <Field label="Date Employed" htmlFor="dateEmployed">
                <Input id="dateEmployed" type="date" {...form.register('dateEmployed')} />
              </Field>
            </div>
            <div className={GRID3}>
              {/* A different axis from Employment Type above: that is the system's fixed set, this
                  is the organisation's own vocabulary, and its duration dates the first contract. */}
              {isCreate && (
              <Field
                label="Contract kind"
                htmlFor="contractTypeId"
                hint="What the appointment letter calls this engagement. Its duration dates the first contract."
              >
                <OptionalSelect
                  id="contractTypeId"
                  value={form.watch('contractTypeId') ?? ''}
                  onChange={(v) => form.setValue('contractTypeId', v)}
                  placeholder="Not stated"
                  options={(contractTypes.data ?? []).map((t) => ({
                    value: t.id,
                    label: t.duration > 0 ? `${t.name} — ${t.duration} months` : `${t.name} — open-ended`,
                  }))}
                />
              </Field>
              )}
              {/* ⚠ Import mode ONLY. A confirmation date is an outcome the probation record
                  produces, with a letter behind it, and the server refuses it from a hire and from
                  the ordinary employee edit. Somebody recorded from a register may have passed
                  probation years before this system existed and has no record to confirm through. */}
              {importMode && (
                <Field
                  label="Confirmation date"
                  htmlFor="confirmationDate"
                  hint="Only for staff already confirmed before this system. Leave blank if they are still on probation."
                >
                  <Input id="confirmationDate" type="date" {...form.register('confirmationDate')} />
                </Field>
              )}
            </div>
            <div className={GRID3}>
              <Field
                label="Probation (days)"
                htmlFor="probationPeriodDays"
                error={err('probationPeriodDays')}
                hint={probationHint}
              >
                <Input
                  id="probationPeriodDays"
                  type="number"
                  min={0}
                  readOnly={probationIsDerived}
                  disabled={probationIsDerived}
                  {...form.register('probationPeriodDays')}
                />
              </Field>
              <div className="grid grid-cols-2 gap-4 sm:col-span-2 lg:col-span-1 lg:self-end">
                <SwitchRow
                  id="isFullTime"
                  label="Full-time"
                  checked={form.watch('isFullTime')}
                  onChange={(v) => form.setValue('isFullTime', v)}
                />
                <SwitchRow
                  id="isExpatriate"
                  label="Expatriate"
                  checked={form.watch('isExpatriate')}
                  onChange={(v) => form.setValue('isExpatriate', v)}
                />
              </div>
            </div>
          </Section>

          {/* Maintenance — round 4, lane O. Who Maintenance may assign work to is decided by the
              position; this person can be an exception either way. The trade and the two levels are
              what Maintenance's technician list shows and filters on. */}
          <Section title="Maintenance">
            <div className={GRID3}>
              <Field
                label="Available to Maintenance"
                htmlFor="maintenanceAssignment"
                hint={maintenanceHint}
                className="sm:col-span-2 lg:col-span-3"
              >
                <Select
                  value={maintenanceAssignment}
                  onValueChange={(v) =>
                    form.setValue('maintenanceAssignment', v as MaintenanceAssignmentMode, { shouldDirty: true })
                  }
                >
                  <SelectTrigger id="maintenanceAssignment">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {MAINTENANCE_ASSIGNMENT_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>
                        {o.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
              <Field
                label="Specialization"
                htmlFor="specialization"
                error={err('specialization')}
                hint="The trade, e.g. Electrical installation"
              >
                <Input id="specialization" maxLength={100} {...form.register('specialization')} />
              </Field>
              <Field
                label="Certification level"
                htmlFor="certificationLevel"
                error={err('certificationLevel')}
                hint="E.g. Level 2"
              >
                <Input id="certificationLevel" maxLength={50} {...form.register('certificationLevel')} />
              </Field>
              <Field label="Experience level" htmlFor="experienceLevel">
                <OptionalSelect
                  id="experienceLevel"
                  value={form.watch('experienceLevel') ?? ''}
                  onChange={(v) => form.setValue('experienceLevel', v, { shouldDirty: true })}
                  placeholder="Not recorded"
                  options={EXPERIENCE_LEVEL_OPTIONS.map((level) => ({ value: level, label: level }))}
                />
              </Field>
            </div>
          </Section>

          {/* Compensation & Tax */}
          <Section title="Compensation & Tax">
            {/* Membership only. Off-payroll staff (consultants on invoice, interns on an
                allowance, secondees) keep their tax identifiers — those are facts about the person.
                The amount, the switches and the grade placement are the Salary tab's (round 2,
                lane E1): a person is created with no pay basis and the tab is where it is given. */}
            <div className="grid grid-cols-1 gap-x-6 gap-y-5 sm:grid-cols-2 lg:grid-cols-4">
              <div className="sm:col-span-2 lg:col-span-4">
                <SwitchRow
                  id="isOnPayroll"
                  label="On payroll — paid through the payroll run"
                  checked={isOnPayroll}
                  onChange={(v) => {
                    form.setValue('isOnPayroll', v, { shouldValidate: true });
                    if (v) {
                      form.setValue('offPayrollReason', '');
                      form.setValue('offPayrollNote', '');
                    }
                  }}
                />
              </div>
              {!isOnPayroll && (
                <>
                  <Field
                    label="How are they paid instead?"
                    htmlFor="offPayrollReason"
                    error={form.formState.errors.offPayrollReason?.message}
                    className="sm:col-span-2"
                  >
                    <OptionalSelect
                      id="offPayrollReason"
                      value={form.watch('offPayrollReason') ?? ''}
                      onChange={(v) => form.setValue('offPayrollReason', v, { shouldValidate: true })}
                      placeholder="Select a reason"
                      options={OFF_PAYROLL_REASON_OPTIONS}
                    />
                  </Field>
                  <Field
                    label="Note (who pays, under what arrangement)"
                    htmlFor="offPayrollNote"
                    className="sm:col-span-2"
                  >
                    <Input id="offPayrollNote" maxLength={500} {...form.register('offPayrollNote')} />
                  </Field>
                </>
              )}
              {isOnPayroll && (
                <p className="text-xs text-muted-foreground sm:col-span-2 lg:col-span-4">
                  The basic salary, payroll switches and grade placement are recorded on the Salary
                  tab after saving. Until then the payroll reconciliation lists this person as having
                  no pay basis.
                </p>
              )}
              <Field label="Tax Number" htmlFor="taxNumber">
                <Input id="taxNumber" {...form.register('taxNumber')} />
              </Field>
              <Field label="SSNIT Number" htmlFor="socialSecurityNumber">
                <Input id="socialSecurityNumber" {...form.register('socialSecurityNumber')} />
              </Field>
              <Field label="TIN" htmlFor="tinNumber">
                <Input id="tinNumber" {...form.register('tinNumber')} />
              </Field>
            </div>
            {!isOnPayroll && (
              <p className="text-xs text-muted-foreground">
                Not on payroll: no salary, payroll switches or grade placement are recorded. Switching
                this on later enrols the employee in Payroll; switching it off clears the pay figures
                and closes any open grade placement.
              </p>
            )}
            <Field label="Notes" htmlFor="notes">
              <Textarea id="notes" rows={3} {...form.register('notes')} />
            </Field>
          </Section>
        </CardContent>
        <CardFooter className="flex justify-end gap-2 border-t pt-6">
          <Button variant="outline" type="button" onClick={onCancel} disabled={submitting}>
            Cancel
          </Button>
          <Button type="submit" disabled={submitting}>
            {submitting ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            {submitLabel}
          </Button>
        </CardFooter>
      </form>
    </Card>
  );
}
