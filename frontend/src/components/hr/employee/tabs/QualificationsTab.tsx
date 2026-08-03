'use client';

import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Badge } from '@/components/ui/badge';
import { countryService } from '@/services/hr/country.service';
import { qualificationService } from '@/services/hr/lookup.service';
import { employeeService } from '@/services/hr/employee.service';
import type { EmployeeQualification } from '@/types/hr/employee-subresources';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import { DateField, FieldRow, SelectField, TextField, TextareaField } from './fields';

const schema = z
  .object({
    qualificationId: z.string().optional().or(z.literal('')),
    customQualificationName: z.string().max(200).optional().or(z.literal('')),
    institution: z.string().min(1, 'Institution is required').max(200),
    fieldOfStudy: z.string().max(200).optional().or(z.literal('')),
    startDate: z.string().optional().or(z.literal('')),
    completionDate: z.string().optional().or(z.literal('')),
    grade: z.string().max(50).optional().or(z.literal('')),
    countryId: z.string().optional().or(z.literal('')),
    description: z.string().max(1000).optional().or(z.literal('')),
    notes: z.string().max(1000).optional().or(z.literal('')),
  })
  // The backend accepts either a catalogue reference or a free-text name; one is required.
  .refine((v) => !!v.qualificationId || !!v.customQualificationName, {
    message: 'Pick a qualification or enter a custom name',
    path: ['qualificationId'],
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  qualificationId: '',
  customQualificationName: '',
  institution: '',
  fieldOfStudy: '',
  startDate: '',
  completionDate: '',
  grade: '',
  countryId: '',
  description: '',
  notes: '',
};

const toPayload = (employeeId: string, v: FormValues) => ({
  employeeId,
  qualificationId: v.qualificationId || null,
  customQualificationName: v.customQualificationName || null,
  institution: v.institution,
  fieldOfStudy: v.fieldOfStudy || null,
  startDate: v.startDate || null,
  completionDate: v.completionDate || null,
  grade: v.grade || null,
  countryId: v.countryId || null,
  description: v.description || null,
  notes: v.notes || null,
});

export function QualificationsTab({ employeeId }: { employeeId: string }) {
  const { data: catalogue } = useQuery({
    queryKey: ['hr', 'qualifications', 'active'],
    queryFn: () => qualificationService.getActive(),
  });

  const { data: countries } = useQuery({
    queryKey: ['hr', 'countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  const qualificationOptions = (catalogue ?? []).map((q) => ({ value: q.id, label: q.name }));
  const countryOptions = (countries ?? []).map((c) => ({ value: c.id, label: c.name }));

  return (
    <EmployeeSubResourceTab<EmployeeQualification, FormValues>
      employeeId={employeeId}
      title="qualifications"
      singular="qualification"
      queryKey="qualifications"
      getId={(q) => q.id}
      list={employeeService.getQualifications.bind(employeeService)}
      create={(id, v) => employeeService.addQualification(id, toPayload(id, v))}
      update={(id, qualId, v) =>
        employeeService.updateQualification(id, qualId, { id: qualId, ...toPayload(id, v) })
      }
      remove={employeeService.removeQualification.bind(employeeService)}
      actions={[
        {
          label: (q) => (q.isVerified ? 'Mark unverified' : 'Mark verified'),
          run: (q) =>
            q.isVerified
              ? employeeService.unverifyQualification(employeeId, q.id)
              : employeeService.verifyQualification(employeeId, q.id),
        },
      ]}
      columns={[
        {
          header: 'Qualification',
          cell: (q) => q.qualificationName || q.customQualificationName || '—',
        },
        { header: 'Institution', cell: (q) => q.institution },
        { header: 'Field of study', cell: (q) => q.fieldOfStudy || '—' },
        { header: 'Completed', cell: (q) => q.completionDate?.slice(0, 10) || '—' },
        { header: 'Grade', cell: (q) => q.grade || '—' },
        {
          header: 'Verified',
          cell: (q) =>
            q.isVerified ? <Badge variant="secondary">Verified</Badge> : <span>—</span>,
        },
      ]}
      schema={schema}
      emptyForm={empty}
      dialogClassName="sm:max-w-[640px]"
      toForm={(q) => ({
        qualificationId: q.qualificationId ?? '',
        customQualificationName: q.customQualificationName ?? '',
        institution: q.institution,
        fieldOfStudy: q.fieldOfStudy ?? '',
        startDate: q.startDate?.slice(0, 10) ?? '',
        completionDate: q.completionDate?.slice(0, 10) ?? '',
        grade: q.grade ?? '',
        countryId: q.countryId ?? '',
        description: q.description ?? '',
        notes: q.notes ?? '',
      })}
      renderFields={(form) => (
        <>
          <SelectField
            form={form}
            name="qualificationId"
            label="Qualification"
            options={qualificationOptions}
            allowEmpty
            emptyLabel="Not in the catalogue"
          />
          <TextField
            form={form}
            name="customQualificationName"
            label="Custom qualification name"
            placeholder="Only if not in the catalogue"
          />
          <FieldRow>
            <TextField form={form} name="institution" label="Institution" required />
            <TextField form={form} name="fieldOfStudy" label="Field of study" />
          </FieldRow>
          <FieldRow>
            <DateField form={form} name="startDate" label="Start date" />
            <DateField form={form} name="completionDate" label="Completion date" />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="grade" label="Grade" placeholder="First Class" />
            <SelectField
              form={form}
              name="countryId"
              label="Country"
              options={countryOptions}
              allowEmpty
            />
          </FieldRow>
          <TextareaField form={form} name="description" label="Description" />
          <TextareaField form={form} name="notes" label="Notes" />
        </>
      )}
    />
  );
}
