'use client';

import { useState } from 'react';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { employeeService } from '@/services/hr/employee.service';
import { GENDER_OPTIONS } from '@/types/hr/employee';
import {
  DEPENDENT_RELATIONSHIP_OPTIONS,
  type DependentRelationship,
  type EmployeeDependent,
} from '@/types/hr/employee-subresources';
import type { Gender } from '@/types/hr/employee';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import { DependentBenefitsDialog } from './DependentBenefitsDialog';
import { DateField, FieldRow, SelectField, SwitchField, TextField, TextareaField } from './fields';

const relationships = DEPENDENT_RELATIONSHIP_OPTIONS.map((o) => o.value) as [
  DependentRelationship,
  ...DependentRelationship[],
];

const schema = z.object({
  firstName: z.string().min(1, 'First name is required').max(100),
  middleName: z.string().max(100).optional().or(z.literal('')),
  lastName: z.string().min(1, 'Last name is required').max(100),
  relationship: z.enum(relationships),
  relationshipDescription: z.string().max(100).optional().or(z.literal('')),
  dateOfBirth: z.string().optional().or(z.literal('')),
  gender: z.string().optional().or(z.literal('')),
  hasDisability: z.boolean(),
  disabilityDescription: z.string().max(500).optional().or(z.literal('')),
  ghanaCardNumber: z.string().max(50).optional().or(z.literal('')),
  phone: z.string().max(30).optional().or(z.literal('')),
  digitalAddress: z.string().max(30).optional().or(z.literal('')),
  occupation: z.string().max(100).optional().or(z.literal('')),
  isEligibleForBenefits: z.boolean(),
  isDeceased: z.boolean(),
  notes: z.string().max(1000).optional().or(z.literal('')),
});

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  firstName: '',
  middleName: '',
  lastName: '',
  relationship: 'Son',
  relationshipDescription: '',
  dateOfBirth: '',
  gender: '',
  hasDisability: false,
  disabilityDescription: '',
  ghanaCardNumber: '',
  phone: '',
  digitalAddress: '',
  occupation: '',
  isEligibleForBenefits: true,
  isDeceased: false,
  notes: '',
};

const toPayload = (employeeId: string, v: FormValues) => ({
  employeeId,
  firstName: v.firstName,
  middleName: v.middleName || null,
  lastName: v.lastName,
  relationship: v.relationship,
  relationshipDescription: v.relationshipDescription || null,
  dateOfBirth: v.dateOfBirth || null,
  gender: (v.gender || null) as Gender | null,
  hasDisability: v.hasDisability,
  disabilityDescription: v.disabilityDescription || null,
  ghanaCardNumber: v.ghanaCardNumber || null,
  phone: v.phone || null,
  digitalAddress: v.digitalAddress || null,
  occupation: v.occupation || null,
  isEligibleForBenefits: v.isEligibleForBenefits,
  isDeceased: v.isDeceased,
  notes: v.notes || null,
});

const age = (dob?: string | null) => {
  if (!dob) return '—';
  const born = new Date(dob);
  if (Number.isNaN(born.getTime())) return '—';
  const diff = Date.now() - born.getTime();
  return String(Math.floor(diff / (365.25 * 24 * 60 * 60 * 1000)));
};

export function DependentsTab({ employeeId }: { employeeId: string }) {
  const [benefitsFor, setBenefitsFor] = useState<EmployeeDependent | null>(null);

  return (
    <>
      <EmployeeSubResourceTab<EmployeeDependent, FormValues>
        employeeId={employeeId}
        title="dependents"
        singular="dependent"
        queryKey="dependents"
        getId={(d) => d.id}
        list={employeeService.getDependents.bind(employeeService)}
        create={(id, v) => employeeService.addDependent(id, toPayload(id, v))}
        update={(id, dependentId, v) =>
          employeeService.updateDependent(id, dependentId, { id: dependentId, ...toPayload(id, v) })
        }
        remove={employeeService.removeDependent.bind(employeeService)}
        actions={[
          {
            label: 'Benefits…',
            run: async (d) => setBenefitsFor(d),
          },
        ]}
        columns={[
          {
            header: 'Name',
            cell: (d) => [d.firstName, d.middleName, d.lastName].filter(Boolean).join(' '),
          },
          { header: 'Relationship', cell: (d) => d.relationship },
          { header: 'Date of birth', cell: (d) => d.dateOfBirth?.slice(0, 10) || '—' },
          { header: 'Age', cell: (d) => age(d.dateOfBirth) },
          { header: 'Gender', cell: (d) => d.gender || '—' },
          {
            header: 'Status',
            cell: (d) => (
              <div className="flex gap-1">
                {d.isEligibleForBenefits && <Badge variant="secondary">Benefits</Badge>}
                {d.hasDisability && <Badge variant="outline">Disability</Badge>}
                {d.isDeceased && <Badge variant="outline">Deceased</Badge>}
              </div>
            ),
          },
        ]}
        schema={schema}
        emptyForm={empty}
        dialogClassName="sm:max-w-[640px]"
        toForm={(d) => ({
          firstName: d.firstName,
          middleName: d.middleName ?? '',
          lastName: d.lastName,
          relationship: d.relationship,
          relationshipDescription: d.relationshipDescription ?? '',
          dateOfBirth: d.dateOfBirth?.slice(0, 10) ?? '',
          gender: d.gender ?? '',
          hasDisability: d.hasDisability,
          disabilityDescription: d.disabilityDescription ?? '',
          ghanaCardNumber: d.ghanaCardNumber ?? '',
          phone: d.phone ?? '',
          digitalAddress: d.digitalAddress ?? '',
          occupation: d.occupation ?? '',
          isEligibleForBenefits: d.isEligibleForBenefits,
          isDeceased: d.isDeceased,
          notes: d.notes ?? '',
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="firstName" label="First name" required />
              <TextField form={form} name="lastName" label="Last name" required />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="middleName" label="Middle name" />
              <SelectField
                form={form}
                name="relationship"
                label="Relationship"
                required
                options={DEPENDENT_RELATIONSHIP_OPTIONS}
              />
            </FieldRow>
            <TextField
              form={form}
              name="relationshipDescription"
              label="Relationship detail"
              placeholder="Only if 'Other'"
            />
            <FieldRow>
              <DateField form={form} name="dateOfBirth" label="Date of birth" />
              <SelectField
                form={form}
                name="gender"
                label="Gender"
                options={GENDER_OPTIONS}
                allowEmpty
              />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="ghanaCardNumber" label="Ghana Card number" />
              <TextField form={form} name="phone" label="Phone" type="tel" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="digitalAddress" label="Digital address" />
              <TextField form={form} name="occupation" label="Occupation" />
            </FieldRow>
            <SwitchField
              form={form}
              name="hasDisability"
              label="Has a disability"
              description="Recorded for benefit eligibility."
            />
            <TextareaField
              form={form}
              name="disabilityDescription"
              label="Disability description"
            />
            <FieldRow>
              <SwitchField
                form={form}
                name="isEligibleForBenefits"
                label="Eligible for benefits"
              />
              <SwitchField form={form} name="isDeceased" label="Deceased" />
            </FieldRow>
            <TextareaField form={form} name="notes" label="Notes" />
          </>
        )}
      />

      <DependentBenefitsDialog
        employeeId={employeeId}
        dependent={benefitsFor}
        open={benefitsFor !== null}
        onOpenChange={(open) => !open && setBenefitsFor(null)}
      />
    </>
  );
}
