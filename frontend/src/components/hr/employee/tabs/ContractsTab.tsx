'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { employeeService } from '@/services/hr/employee.service';
import { EMPLOYMENT_TYPE_OPTIONS } from '@/types/hr/employee';
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
    contractStatus: z.enum(['Active', 'Expired', 'Terminated']),
    terms: z.string().max(4000).optional().or(z.literal('')),
    isActive: z.boolean(),
  })
  .refine((v) => !v.endDate || v.endDate >= v.startDate, {
    message: 'End date cannot be before the start date',
    path: ['endDate'],
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  contractNumber: '',
  employmentType: 'FullTime',
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
  contractStatus: 'Active',
  terms: '',
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
  contractStatus: v.contractStatus,
  terms: v.terms || null,
  isActive: v.isActive,
});

export function ContractsTab({ employeeId }: { employeeId: string }) {
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
        probationPeriodDays: '',
        confirmationDate: '',
        contractStatus: c.contractStatus ?? 'Active',
        terms: c.terms ?? '',
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
            <SelectField
              form={form}
              name="payFrequency"
              label="Pay frequency"
              required
              options={PAY_FREQUENCY_OPTIONS}
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
        </>
      )}
    />
  );
}
