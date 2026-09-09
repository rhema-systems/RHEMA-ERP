'use client';

import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Badge } from '@/components/ui/badge';
import { employeeService } from '@/services/hr/employee.service';
import { EMPLOYMENT_TYPE_OPTIONS } from '@/types/hr/employee';
import { contractTypeService } from '@/services/hr/contract-type.service';
import type { EmploymentType } from '@/types/hr/employee';
import { CONTRACT_STATUS_OPTIONS, type EmployeeContract } from '@/types/hr/employee-subresources';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import {
  DateField,
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextField,
  TextareaField,
} from './fields';

const schema = z
  .object({
    contractNumber: z.string().min(1, 'Contract number is required').max(50),
    employmentType: z.string().min(1, 'Employment type is required'),
    contractTypeId: z.string().optional().or(z.literal('')),
    startDate: z.string().min(1, 'Start date is required'),
    effectiveDate: z.string().optional().or(z.literal('')),
    endDate: z.string().optional().or(z.literal('')),
    contractEndDate: z.string().optional().or(z.literal('')),
    workingHoursPerWeek: z.coerce.number().int().min(0).max(168),
    annualLeaveEntitlementDays: z.coerce.number().int().min(0).max(365),
    vacationDaysPerYear: z.coerce.number().int().min(0),
    sickDaysPerYear: z.coerce.number().int().min(0),
    probationPeriodDays: z.string().optional().or(z.literal('')),
    confirmationDate: z.string().optional().or(z.literal('')),
    workSchedule: z.enum(['FullTime', 'PartTime', 'Shift', 'Flexi', 'Remote', 'Hybrid']),
    contractStatus: z.enum(['Active', 'Expired', 'Terminated']),
    terms: z.string().max(4000).optional().or(z.literal('')),
    specialConditions: z.string().max(2000).optional().or(z.literal('')),
    notes: z.string().max(500).optional().or(z.literal('')),
    isActive: z.boolean(),
  })
  .refine((v) => !v.endDate || v.endDate >= v.startDate, {
    message: 'End date cannot be before the start date',
    path: ['endDate'],
  })
  .refine((v) => !v.contractEndDate || v.contractEndDate >= v.startDate, {
    message: 'The scheduled end cannot be before the start date',
    path: ['contractEndDate'],
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  contractNumber: '',
  // ⚠ Was 'FullTime', which is not an EmploymentType member at all (Permanent, Contract,
  // FixedTerm, Internship, Casual, PartTime, Temporary, Consultant, Freelance). It belongs to
  // WorkArrangementType — the `workSchedule` field below, which until 2026-09-01 was on the entity
  // and on no DTO. So the dialog opened with a value its own select could not show and the API
  // could not parse, and an untouched Add failed.
  employmentType: 'Permanent',
  contractTypeId: '',
  startDate: '',
  effectiveDate: '',
  endDate: '',
  contractEndDate: '',
  workingHoursPerWeek: 40,
  annualLeaveEntitlementDays: 20,
  vacationDaysPerYear: 0,
  sickDaysPerYear: 0,
  probationPeriodDays: '',
  confirmationDate: '',
  workSchedule: 'FullTime',
  contractStatus: 'Active',
  terms: '',
  specialConditions: '',
  notes: '',
  isActive: true,
};

const toPayload = (employeeId: string, v: FormValues) => ({
  employeeId,
  contractNumber: v.contractNumber,
  employmentType: v.employmentType as EmploymentType,
  contractTypeId: v.contractTypeId || null,
  startDate: v.startDate,
  // Silence means "when it starts", which is what the server assumes too.
  effectiveDate: v.effectiveDate || null,
  endDate: v.endDate || null,
  contractEndDate: v.contractEndDate || null,
  workingHoursPerWeek: v.workingHoursPerWeek,
  annualLeaveEntitlementDays: v.annualLeaveEntitlementDays,
  vacationDaysPerYear: v.vacationDaysPerYear,
  sickDaysPerYear: v.sickDaysPerYear,
  probationPeriodDays: v.probationPeriodDays ? Number(v.probationPeriodDays) : null,
  confirmationDate: v.confirmationDate || null,
  workSchedule: v.workSchedule,
  contractStatus: v.contractStatus,
  terms: v.terms || null,
  specialConditions: v.specialConditions || null,
  notes: v.notes || null,
  isActive: v.isActive,
});

const WORK_SCHEDULE_OPTIONS = [
  { value: 'FullTime', label: 'Full time' },
  { value: 'PartTime', label: 'Part time' },
  { value: 'Shift', label: 'Shift' },
  { value: 'Flexi', label: 'Flexi' },
  { value: 'Remote', label: 'Remote' },
  { value: 'Hybrid', label: 'Hybrid' },
];

export function ContractsTab({ employeeId }: { employeeId: string }) {

  // The tenant's own vocabulary, a different axis from the EmploymentType enum below it.
  const { data: contractTypes } = useQuery({
    queryKey: ['hr', 'contract-types', 'active'],
    queryFn: () => contractTypeService.getActive(),
  });

  return (
    <EmployeeSubResourceTab<EmployeeContract, FormValues>
      employeeId={employeeId}
      title="contracts"
      singular="contract"
      queryKey="contracts"
      getId={(c) => c.id}
      list={employeeService.getContracts.bind(employeeService)}
      create={(id, v) => employeeService.addContract(id, toPayload(id, v))}
      update={(id, contractId, v) =>
        employeeService.updateContract(id, contractId, { id: contractId, ...toPayload(id, v) })
      }
      remove={employeeService.removeContract.bind(employeeService)}
      actions={[
        {
          label: (c) => (c.isActive ? 'Deactivate' : 'Activate'),
          run: (c) =>
            c.isActive
              ? employeeService.deactivateContract(employeeId, c.id)
              : employeeService.activateContract(employeeId, c.id),
        },
        {
          label: 'Terminate',
          destructive: true,
          visible: (c) => c.contractStatus !== 'Terminated',
          confirm: {
            title: 'Terminate contract?',
            description:
              'The contract is terminated as of today. Record the agreed reason in the contract notes if it differs.',
          },
          run: (c) =>
            employeeService.terminateContract(employeeId, c.id, {
              terminationDate: new Date().toISOString().slice(0, 10),
              reason: 'Terminated from the employee profile',
            }),
        },
      ]}
      columns={[
        { header: 'Contract', cell: (c) => c.contractNumber },
        { header: 'Type', cell: (c) => c.contractTypeName ?? c.employmentType },
        {
          // ⚠ The EFFECTIVE date, not the start date. A row added before 2026-09-09 carries
          // 0001-01-01 here because the writer never set it — shown as a dash rather than as a
          // date from the first century, which is what a bare format would print.
          header: 'In force from',
          cell: (c) => {
            const d = c.effectiveDate?.slice(0, 10);
            return !d || d.startsWith('0001') ? '—' : d;
          },
        },
        {
          // Scheduled end first, because that is the fact a renewal report asks for; the actual end
          // only exists once it has happened.
          header: 'Until',
          cell: (c) =>
            c.contractEndDate?.slice(0, 10) ?? c.endDate?.slice(0, 10) ?? 'Open-ended',
        },
        {
          // ⚠ History, not the pay. The Salary tab is where basic pay lives since lane E1; this
          // column shows what a contract row recorded at the time and is no longer edited here.
          header: 'Salary (as recorded)',
          cell: (c) =>
            c.salary
              ? `${c.currencyCode ?? 'GHS'} ${c.salary.toLocaleString()}${c.payFrequencyType ? ` / ${c.payFrequencyType}` : ''}`
              : '—',
        },
        {
          header: 'Status',
          cell: (c) => (
            <div className="flex gap-1">
              {/* The terms in force. Exactly one row carries it — adding a contract closes
                  whichever one it replaces. */}
              {c.isCurrent && <Badge>In force</Badge>}
              <Badge variant={c.contractStatus === 'Active' ? 'secondary' : 'outline'}>
                {c.contractStatus ?? '—'}
              </Badge>
              {!c.isActive && !c.isCurrent && <Badge variant="outline">Inactive</Badge>}
            </div>
          ),
        },
      ]}
      schema={schema}
      emptyForm={empty}
      dialogClassName="sm:max-w-[680px]"
      toForm={(c) => ({
        contractNumber: c.contractNumber,
        employmentType: c.employmentType,
        contractTypeId: c.contractTypeId ?? '',
        startDate: c.startDate?.slice(0, 10) ?? '',
        // A pre-2026-09-09 row reads 0001-01-01; offer it blank so a save does not re-file it.
        effectiveDate: c.effectiveDate?.startsWith('0001')
          ? ''
          : (c.effectiveDate?.slice(0, 10) ?? ''),
        endDate: c.endDate?.slice(0, 10) ?? '',
        contractEndDate: c.contractEndDate?.slice(0, 10) ?? '',
        workingHoursPerWeek: c.workingHoursPerWeek ?? 40,
        annualLeaveEntitlementDays: c.annualLeaveEntitlementDays ?? 20,
        vacationDaysPerYear: c.vacationDaysPerYear ?? 0,
        sickDaysPerYear: c.sickDaysPerYear ?? 0,
        // ⚠ Both were hardcoded blank because the read DTO carried neither — they were settable
        // and unreadable. Now bound, so an edit shows the probation term instead of hiding it.
        probationPeriodDays: c.probationPeriodDays != null ? String(c.probationPeriodDays) : '',
        confirmationDate: c.confirmationDate?.slice(0, 10) ?? '',
        workSchedule: c.workSchedule ?? 'FullTime',
        contractStatus: c.contractStatus ?? 'Active',
        terms: c.terms ?? '',
        specialConditions: c.specialConditions ?? '',
        notes: c.notes ?? '',
        isActive: c.isActive,
      })}
      renderFields={(form) => (
        <>
          <FieldRow>
            <TextField form={form} name="contractNumber" label="Contract number" required />
            <SelectField
              form={form}
              name="employmentType"
              label="Employment type"
              required
              options={EMPLOYMENT_TYPE_OPTIONS}
            />
          </FieldRow>
          <FieldRow>
            {/* The organisation's own vocabulary — a different axis from Employment type above.
                Its duration fills in the scheduled end when that is left blank. */}
            <SelectField
              form={form}
              name="contractTypeId"
              label="Contract kind"
              options={(contractTypes ?? []).map((t) => ({
                value: t.id,
                label: t.duration > 0 ? `${t.name} — ${t.duration} months` : `${t.name} — open-ended`,
              }))}
            />
            <DateField form={form} name="startDate" label="Start date" required />
          </FieldRow>
          <FieldRow>
            {/* ⚠ The date supersession runs on: adding a contract closes the one in force the day
                before this. Blank means the start date. */}
            <DateField form={form} name="effectiveDate" label="In force from" />
            <DateField form={form} name="contractEndDate" label="Scheduled end" />
          </FieldRow>
          <FieldRow>
            {/* When it ACTUALLY ended — normally written by terminating, not typed. */}
            <DateField form={form} name="endDate" label="Actual end date" />
            <NumberField
              form={form}
              name="annualLeaveEntitlementDays"
              label="Annual leave (days)"
            />
          </FieldRow>
          {/* ⚠ No salary, currency, pay frequency, tax treatment, withholding, pension or
              tax-exempt here since lane E1 (§ 6.5.4): basic pay and its treatment are payroll's,
              hosted on the Salary tab. The columns stay on the entity for the rows that hold them. */}
          <FieldRow>
            {/* A different axis from employment type: permanent vs contract is one question,
                full time vs shift vs remote is another. */}
            <SelectField
              form={form}
              name="workSchedule"
              label="Work schedule"
              required
              options={WORK_SCHEDULE_OPTIONS}
            />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="workingHoursPerWeek" label="Hours per week" />
            <NumberField form={form} name="probationPeriodDays" label="Probation (days)" />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="vacationDaysPerYear" label="Vacation days / year" />
            <NumberField form={form} name="sickDaysPerYear" label="Sick days / year" />
          </FieldRow>
          <FieldRow>
            <DateField form={form} name="confirmationDate" label="Confirmation date" />
            <SelectField
              form={form}
              name="contractStatus"
              label="Contract status"
              required
              options={CONTRACT_STATUS_OPTIONS}
            />
          </FieldRow>
          <SwitchField form={form} name="isActive" label="Active" />
          <TextareaField form={form} name="terms" label="Terms" rows={4} />
          <TextareaField
            form={form}
            name="specialConditions"
            label="Special conditions"
            rows={3}
          />
          <TextareaField form={form} name="notes" label="Notes" rows={2} />
        </>
      )}
    />
  );
}
