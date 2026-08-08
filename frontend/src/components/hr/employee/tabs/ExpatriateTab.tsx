'use client';

import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Badge } from '@/components/ui/badge';
import { countryService } from '@/services/hr/country.service';
import { employeeService } from '@/services/hr/employee.service';
import type { ExpatriateAssignment } from '@/types/hr/employee-subresources';
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
    homeCountryId: z.string().min(1, 'Home country is required'),
    startDate: z.string().min(1, 'Start date is required'),
    endDate: z.string().optional().or(z.literal('')),
    relocationAllowance: z.string().optional().or(z.literal('')),
    relocationDate: z.string().optional().or(z.literal('')),
    familyAccompanying: z.boolean(),
    assignmentObjective: z.string().max(2000).optional().or(z.literal('')),
    visaType: z.string().max(100).optional().or(z.literal('')),
    visaExpiryDate: z.string().optional().or(z.literal('')),
    workPermitNumber: z.string().max(100).optional().or(z.literal('')),
    workPermitExpiryDate: z.string().optional().or(z.literal('')),
  })
  .refine((v) => !v.endDate || v.endDate >= v.startDate, {
    message: 'End date cannot be before the start date',
    path: ['endDate'],
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  homeCountryId: '',
  startDate: '',
  endDate: '',
  relocationAllowance: '',
  relocationDate: '',
  familyAccompanying: false,
  assignmentObjective: '',
  visaType: '',
  visaExpiryDate: '',
  workPermitNumber: '',
  workPermitExpiryDate: '',
};

const toPayload = (employeeId: string, v: FormValues) => ({
  employeeId,
  homeCountryId: v.homeCountryId,
  startDate: v.startDate,
  endDate: v.endDate || null,
  relocationAllowance: v.relocationAllowance ? Number(v.relocationAllowance) : null,
  relocationDate: v.relocationDate || null,
  familyAccompanying: v.familyAccompanying,
  assignmentObjective: v.assignmentObjective || null,
  visaType: v.visaType || null,
  visaExpiryDate: v.visaExpiryDate || null,
  workPermitNumber: v.workPermitNumber || null,
  workPermitExpiryDate: v.workPermitExpiryDate || null,
});

const expiringSoon = (date?: string | null) => {
  if (!date) return false;
  const days = (new Date(date).getTime() - Date.now()) / 86_400_000;
  return days < 90;
};

export function ExpatriateTab({ employeeId }: { employeeId: string }) {
  const { data: countries } = useQuery({
    queryKey: ['hr', 'countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  const countryOptions = (countries ?? []).map((c) => ({ value: c.id, label: c.name }));

  return (
    <EmployeeSubResourceTab<ExpatriateAssignment, FormValues>
      employeeId={employeeId}
      title="expatriate assignments"
      singular="expatriate assignment"
      queryKey="expatriate-assignments"
      emptyDescription="Only applies to employees on an international assignment."
      getId={(a) => a.id}
      list={employeeService.getExpatriateAssignments.bind(employeeService)}
      create={(id, v) => employeeService.addExpatriateAssignment(id, toPayload(id, v))}
      update={(id, assignmentId, v) =>
        employeeService.updateExpatriateAssignment(id, assignmentId, {
          id: assignmentId,
          ...toPayload(id, v),
        })
      }
      remove={employeeService.removeExpatriateAssignment.bind(employeeService)}
      columns={[
        { header: 'Home country', cell: (a) => a.homeCountryName || '—' },
        { header: 'From', cell: (a) => a.startDate?.slice(0, 10) || '—' },
        { header: 'To', cell: (a) => a.endDate?.slice(0, 10) || 'Open-ended' },
        { header: 'Visa', cell: (a) => a.visaType || '—' },
        {
          header: 'Visa expiry',
          cell: (a) =>
            a.visaExpiryDate ? (
              <span className={expiringSoon(a.visaExpiryDate) ? 'text-red-600' : undefined}>
                {a.visaExpiryDate.slice(0, 10)}
              </span>
            ) : (
              '—'
            ),
        },
        {
          header: 'Work permit expiry',
          cell: (a) =>
            a.workPermitExpiryDate ? (
              <span className={expiringSoon(a.workPermitExpiryDate) ? 'text-red-600' : undefined}>
                {a.workPermitExpiryDate.slice(0, 10)}
              </span>
            ) : (
              '—'
            ),
        },
        {
          header: 'Family',
          cell: (a) => (a.familyAccompanying ? <Badge variant="secondary">Accompanying</Badge> : '—'),
        },
      ]}
      schema={schema}
      emptyForm={empty}
      dialogClassName="sm:max-w-[640px]"
      toForm={(a) => ({
        homeCountryId: a.homeCountryId,
        startDate: a.startDate?.slice(0, 10) ?? '',
        endDate: a.endDate?.slice(0, 10) ?? '',
        relocationAllowance: a.relocationAllowance != null ? String(a.relocationAllowance) : '',
        relocationDate: a.relocationDate?.slice(0, 10) ?? '',
        familyAccompanying: a.familyAccompanying,
        assignmentObjective: a.assignmentObjective ?? '',
        visaType: a.visaType ?? '',
        visaExpiryDate: a.visaExpiryDate?.slice(0, 10) ?? '',
        workPermitNumber: a.workPermitNumber ?? '',
        workPermitExpiryDate: a.workPermitExpiryDate?.slice(0, 10) ?? '',
      })}
      renderFields={(form) => (
        <>
          <SelectField
            form={form}
            name="homeCountryId"
            label="Home country"
            required
            options={countryOptions}
          />
          <FieldRow>
            <DateField form={form} name="startDate" label="Assignment start" required />
            <DateField form={form} name="endDate" label="Assignment end" />
          </FieldRow>
          <FieldRow>
            <NumberField
              form={form}
              name="relocationAllowance"
              label="Relocation allowance"
              step="0.01"
            />
            <DateField form={form} name="relocationDate" label="Relocation date" />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="visaType" label="Visa type" />
            <DateField form={form} name="visaExpiryDate" label="Visa expiry" />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="workPermitNumber" label="Work permit number" />
            <DateField form={form} name="workPermitExpiryDate" label="Work permit expiry" />
          </FieldRow>
          <SwitchField
            form={form}
            name="familyAccompanying"
            label="Family accompanying"
            description="Affects relocation and benefit entitlements."
          />
          <TextareaField form={form} name="assignmentObjective" label="Assignment objective" />
        </>
      )}
    />
  );
}
