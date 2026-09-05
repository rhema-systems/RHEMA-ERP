'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { employeeService } from '@/services/hr/employee.service';
import { useQuery } from '@tanstack/react-query';
import { EMPLOYMENT_TYPE_OPTIONS } from '@/types/hr/employee';
import { hrCurrencyService } from '@/services/hr/hr-currency.service';
import type { EmploymentType } from '@/types/hr/employee';
import {
  CONTRACT_STATUS_OPTIONS,
  PAY_FREQUENCY_OPTIONS,
  TAX_TREATMENT_OPTIONS,
  type EmployeeContract,
} from '@/types/hr/employee-subresources';
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
    startDate: z.string().min(1, 'Start date is required'),
    endDate: z.string().optional().or(z.literal('')),
    salary: z.coerce.number().min(0, 'Cannot be negative'),
    payFrequency: z.enum(['Weekly', 'BiWeekly', 'Monthly', 'Quarterly', 'Annually', 'OneTime']),
    taxTreatmentType: z.enum(['None', 'PAYE', 'WithholdingTax']),
    withholdingTaxRate: z.string().optional().or(z.literal('')),
    isPensionApplicable: z.boolean(),
    isTaxExempt: z.boolean(),
    workingHoursPerWeek: z.coerce.number().int().min(0).max(168),
    vacationDaysPerYear: z.coerce.number().int().min(0),
    sickDaysPerYear: z.coerce.number().int().min(0),
    probationPeriodDays: z.string().optional().or(z.literal('')),
    confirmationDate: z.string().optional().or(z.literal('')),
    currencyCode: z.string().optional().or(z.literal('')),
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
  startDate: '',
  endDate: '',
  salary: 0,
  payFrequency: 'Monthly',
  taxTreatmentType: 'PAYE',
  withholdingTaxRate: '',
  isPensionApplicable: true,
  isTaxExempt: false,
  workingHoursPerWeek: 40,
  vacationDaysPerYear: 0,
  sickDaysPerYear: 0,
  probationPeriodDays: '',
  confirmationDate: '',
  currencyCode: '',
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
  startDate: v.startDate,
  endDate: v.endDate || null,
  salary: v.salary,
  payFrequency: v.payFrequency,
  taxTreatmentType: v.taxTreatmentType,
  withholdingTaxRate: v.withholdingTaxRate ? Number(v.withholdingTaxRate) : null,
  isPensionApplicable: v.isPensionApplicable,
  isTaxExempt: v.isTaxExempt,
  workingHoursPerWeek: v.workingHoursPerWeek,
  vacationDaysPerYear: v.vacationDaysPerYear,
  sickDaysPerYear: v.sickDaysPerYear,
  probationPeriodDays: v.probationPeriodDays ? Number(v.probationPeriodDays) : null,
  confirmationDate: v.confirmationDate || null,
  currencyCode: v.currencyCode || 'GHS',
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
  // Only currencies Finance actually holds — the server refuses anything else, so a free-text box
  // would be offering a way to fail. ⚠ Read through hrCurrencyService, not Finance's own endpoint,
  // which answers HR with a 403 and renders the picker empty.
  const { data: currencies } = useQuery({
    queryKey: ['finance', 'currencies'],
    queryFn: () => hrCurrencyService.getActive(),
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
        { header: 'Type', cell: (c) => c.employmentType },
        { header: 'From', cell: (c) => c.startDate?.slice(0, 10) || '—' },
        { header: 'To', cell: (c) => c.endDate?.slice(0, 10) || 'Open-ended' },
        {
          header: 'Salary',
          cell: (c) =>
            c.salary != null
              ? `${c.salary.toLocaleString()}${c.payFrequencyType ? ` / ${c.payFrequencyType}` : ''}`
              : '—',
        },
        {
          header: 'Status',
          cell: (c) => (
            <div className="flex gap-1">
              <Badge variant={c.contractStatus === 'Active' ? 'secondary' : 'outline'}>
                {c.contractStatus ?? '—'}
              </Badge>
              {!c.isActive && <Badge variant="outline">Inactive</Badge>}
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
        startDate: c.startDate?.slice(0, 10) ?? '',
        endDate: c.endDate?.slice(0, 10) ?? '',
        salary: c.salary ?? 0,
        payFrequency: c.payFrequencyType ?? 'Monthly',
        taxTreatmentType: c.taxTreatmentType ?? 'PAYE',
        withholdingTaxRate: c.withholdingTaxRate != null ? String(c.withholdingTaxRate) : '',
        isPensionApplicable: c.isPensionApplicable ?? true,
        isTaxExempt: c.isTaxExempt ?? false,
        workingHoursPerWeek: c.workingHoursPerWeek ?? 40,
        vacationDaysPerYear: c.vacationDaysPerYear ?? 0,
        sickDaysPerYear: c.sickDaysPerYear ?? 0,
        // ⚠ Both were hardcoded blank because the read DTO carried neither — they were settable
        // and unreadable. Now bound, so an edit shows the probation term instead of hiding it.
        probationPeriodDays: c.probationPeriodDays != null ? String(c.probationPeriodDays) : '',
        confirmationDate: c.confirmationDate?.slice(0, 10) ?? '',
        currencyCode: c.currencyCode ?? '',
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
            <DateField form={form} name="startDate" label="Start date" required />
            <DateField form={form} name="endDate" label="End date" />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="salary" label="Salary" step="0.01" required />
            {/* A salary with no currency was displayed as a bare number and assumed GHS. */}
            <SelectField
              form={form}
              name="currencyCode"
              label="Currency"
              options={(currencies ?? []).map((c) => ({
                value: c.code,
                label: `${c.code} — ${c.name}`,
              }))}
            />
          </FieldRow>
          <FieldRow>
            <SelectField
              form={form}
              name="payFrequency"
              label="Pay frequency"
              required
              options={PAY_FREQUENCY_OPTIONS}
            />
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
            <SelectField
              form={form}
              name="taxTreatmentType"
              label="Tax treatment"
              required
              options={TAX_TREATMENT_OPTIONS}
            />
            <NumberField
              form={form}
              name="withholdingTaxRate"
              label="Withholding tax rate (%)"
              step="0.01"
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
          <FieldRow>
            <SwitchField form={form} name="isPensionApplicable" label="Pension applicable" />
            <SwitchField form={form} name="isTaxExempt" label="Tax exempt" />
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
