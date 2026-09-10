'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { employeeService } from '@/services/hr/employee.service';
import type { EmployeeWorkHistory } from '@/types/hr/employee-subresources';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import { AddressCascadeField } from './address-fields';
import {
  DateField,
  FieldRow,
  NumberField,
  SwitchField,
  TextField,
  TextareaField,
} from './fields';

/**
 * ⚠ Three of these maxima used to exceed their columns (finding X-5, and two more found with it):
 * companyAddress said 300 against a 200 column, jobTitle 200 against 100, jobDescription 2000
 * against 1000. The form accepted a value, the create refused it with a 400 the user could not
 * have predicted, and the update — whose DTO carried no lengths at all until this lane — reached
 * SQL Server and failed as a truncation 500. All three now state what the column actually holds.
 */
const schema = z
  .object({
    companyName: z.string().min(1, 'Company name is required').max(200),
    companyAddress: z.string().max(200).optional().or(z.literal('')),
    countryId: z.string().optional().or(z.literal('')),
    city: z.string().max(100).optional().or(z.literal('')),
    region: z.string().max(100).optional().or(z.literal('')),
    geoAreaId: z.string().optional().or(z.literal('')),
    jobTitle: z.string().min(1, 'Job title is required').max(100),
    jobDescription: z.string().max(1000).optional().or(z.literal('')),
    startDate: z.string().min(1, 'Start date is required'),
    endDate: z.string().optional().or(z.literal('')),
    salary: z.string().optional().or(z.literal('')),
    reasonForLeaving: z.string().max(500).optional().or(z.literal('')),
    supervisorName: z.string().max(200).optional().or(z.literal('')),
    supervisorPhone: z.string().max(30).optional().or(z.literal('')),
    canContact: z.boolean(),
  })
  .refine((v) => !v.endDate || v.endDate >= v.startDate, {
    message: 'End date cannot be before the start date',
    path: ['endDate'],
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  companyName: '',
  companyAddress: '',
  countryId: '',
  city: '',
  region: '',
  geoAreaId: '',
  jobTitle: '',
  jobDescription: '',
  startDate: '',
  endDate: '',
  salary: '',
  reasonForLeaving: '',
  supervisorName: '',
  supervisorPhone: '',
  canContact: true,
};

const toPayload = (employeeId: string, v: FormValues) => ({
  employeeId,
  companyName: v.companyName,
  companyAddress: v.companyAddress || null,
  countryId: v.countryId || null,
  city: v.city || null,
  region: v.region || null,
  // ⚠ Null CLEARS the link on the update DTO — its address fields are all full-replace.
  geoAreaId: v.geoAreaId || null,
  jobTitle: v.jobTitle,
  jobDescription: v.jobDescription || null,
  startDate: v.startDate,
  endDate: v.endDate || null,
  salary: v.salary ? Number(v.salary) : null,
  reasonForLeaving: v.reasonForLeaving || null,
  supervisorName: v.supervisorName || null,
  supervisorPhone: v.supervisorPhone || null,
  canContact: v.canContact,
});

export function WorkHistoryTab({ employeeId }: { employeeId: string }) {
  return (
    <EmployeeSubResourceTab<EmployeeWorkHistory, FormValues>
      employeeId={employeeId}
      title="previous roles"
      singular="previous role"
      queryKey="work-histories"
      emptyDescription="Record employment held before joining."
      getId={(w) => w.id}
      list={employeeService.getWorkHistories.bind(employeeService)}
      create={(id, v) => employeeService.addWorkHistory(id, toPayload(id, v))}
      update={(id, historyId, v) =>
        employeeService.updateWorkHistory(id, historyId, { id: historyId, ...toPayload(id, v) })
      }
      remove={employeeService.removeWorkHistory.bind(employeeService)}
      columns={[
        { header: 'Company', cell: (w) => w.companyName },
        { header: 'Where', cell: (w) => [w.city, w.region].filter(Boolean).join(', ') || '—' },
        { header: 'Job title', cell: (w) => w.jobTitle },
        { header: 'From', cell: (w) => w.startDate?.slice(0, 10) || '—' },
        { header: 'To', cell: (w) => w.endDate?.slice(0, 10) || 'Present' },
        { header: 'Reason for leaving', cell: (w) => w.reasonForLeaving || '—' },
        {
          header: 'Reference',
          cell: (w) =>
            w.canContact ? <Badge variant="secondary">Contactable</Badge> : <span>Do not contact</span>,
        },
      ]}
      schema={schema}
      emptyForm={empty}
      dialogClassName="sm:max-w-[640px]"
      toForm={(w) => ({
        companyName: w.companyName,
        companyAddress: w.companyAddress ?? '',
        countryId: w.countryId ?? '',
        city: w.city ?? '',
        region: w.region ?? '',
        geoAreaId: w.geoAreaId ?? '',
        jobTitle: w.jobTitle,
        jobDescription: w.jobDescription ?? '',
        startDate: w.startDate?.slice(0, 10) ?? '',
        endDate: w.endDate?.slice(0, 10) ?? '',
        salary: w.salary != null ? String(w.salary) : '',
        reasonForLeaving: w.reasonForLeaving ?? '',
        supervisorName: w.supervisorName ?? '',
        supervisorPhone: w.supervisorPhone ?? '',
        canContact: w.canContact ?? true,
      })}
      renderFields={(form) => (
        <>
          <FieldRow>
            <TextField form={form} name="companyName" label="Company" required />
            <TextField form={form} name="jobTitle" label="Job title" required />
          </FieldRow>
          <TextField form={form} name="companyAddress" label="Company address" />
          {/*
            Where the employer is. Until round 2 the address was one free-text line with no
            country, city or geography at all (register row E-6) — so "every engineer who worked at
            a Tema firm" was unanswerable.
          */}
          <AddressCascadeField
            form={form}
            countryName="countryId"
            geoAreaName="geoAreaId"
            cityName="city"
            regionName="region"
          />
          <TextareaField form={form} name="jobDescription" label="Job description" />
          <FieldRow>
            <DateField form={form} name="startDate" label="Start date" required />
            <DateField form={form} name="endDate" label="End date" />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="salary" label="Salary" step="0.01" />
            <TextField form={form} name="reasonForLeaving" label="Reason for leaving" />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="supervisorName" label="Supervisor" />
            <TextField form={form} name="supervisorPhone" label="Supervisor phone" type="tel" />
          </FieldRow>
          <SwitchField
            form={form}
            name="canContact"
            label="May contact this employer"
            description="Whether this employer can be approached for a reference."
          />
        </>
      )}
    />
  );
}
