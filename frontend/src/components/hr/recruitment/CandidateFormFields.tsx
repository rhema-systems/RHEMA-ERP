'use client';

import { useQuery } from '@tanstack/react-query';
import type { UseFormReturn } from 'react-hook-form';
import { z } from 'zod';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  DateField,
  FieldRow,
  SelectField,
  SwitchField,
  TextField,
} from '@/components/hr/employee/tabs/fields';
import { countryService } from '@/services/hr/country.service';
import { GENDERS } from '@/types/hr/recruitment-pipeline';

export const candidateSchema = z.object({
  firstName: z.string().min(1, 'First name is required').max(100),
  middleName: z.string().max(100).optional().nullable(),
  lastName: z.string().min(1, 'Last name is required').max(100),
  dateOfBirth: z.string().min(1, 'Date of birth is required'),
  gender: z.string().min(1, 'Gender is required'),
  email: z.string().min(1, 'Email is required').email('Enter a valid email'),
  phone: z.string().min(1, 'Phone is required').max(20),
  alternatePhone: z.string().max(20).optional().nullable(),
  postalAddress: z.string().max(200).optional().nullable(),
  digitalAddress: z.string().max(30).optional().nullable(),
  city: z.string().min(1, 'City is required').max(100),
  countryId: z.string().min(1, 'Country is required'),
  linkedInProfile: z.string().max(200).optional().nullable(),
  portfolioUrl: z.string().max(200).optional().nullable(),
  gitHubUrl: z.string().max(200).optional().nullable(),
  isInTalentPool: z.boolean(),
});

export type CandidateFormValues = z.infer<typeof candidateSchema>;

export const emptyCandidate: CandidateFormValues = {
  firstName: '',
  middleName: null,
  lastName: '',
  dateOfBirth: '',
  gender: 'PreferNotToSay',
  email: '',
  phone: '',
  alternatePhone: null,
  postalAddress: null,
  digitalAddress: null,
  city: '',
  countryId: '',
  linkedInProfile: null,
  portfolioUrl: null,
  gitHubUrl: null,
  isInTalentPool: false,
};

/**
 * The editable half of a candidate record.
 *
 * ⚠ Deliberately does **not** cover headline, current role, expected salary, notice period,
 * preferred work arrangement or work-authorization status. Those are on the read DTO but not on
 * `CreateJobCandidateDto`/`UpdateJobCandidateDto` — they belong to the candidate, who supplies them
 * through the portal or an external application. Saving from HR leaves them as they were; adding
 * inputs for them here would silently discard what the user typed.
 */
export function CandidateFormFields({ form }: { form: UseFormReturn<CandidateFormValues> }) {
  // Active only — an inactive country should not be offered on a new record.
  const countries = useQuery({
    queryKey: ['hr', 'countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Identity</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <TextField form={form} name="firstName" label="First name" required />
            <TextField form={form} name="lastName" label="Last name" required />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="middleName" label="Middle name" />
            <DateField form={form} name="dateOfBirth" label="Date of birth" required />
          </FieldRow>
          <FieldRow>
            <SelectField
              form={form}
              name="gender"
              label="Gender"
              required
              options={GENDERS.map((g) => ({
                value: g,
                label: g === 'PreferNotToSay' ? 'Prefer not to say' : g,
              }))}
            />
            <div />
          </FieldRow>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Contact</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <TextField form={form} name="email" label="Email" type="email" required />
            <TextField form={form} name="phone" label="Phone" type="tel" required />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="alternatePhone" label="Alternate phone" type="tel" />
            <TextField form={form} name="digitalAddress" label="Digital address" />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="postalAddress" label="Postal address" />
            <TextField form={form} name="city" label="City" required />
          </FieldRow>
          <FieldRow>
            <SelectField
              form={form}
              name="countryId"
              label="Country"
              required
              options={(countries.data ?? []).map((c: any) => ({ value: c.id, label: c.name }))}
            />
            <div />
          </FieldRow>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Online presence</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <TextField form={form} name="linkedInProfile" label="LinkedIn" />
            <TextField form={form} name="portfolioUrl" label="Portfolio" />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="gitHubUrl" label="GitHub" />
            <div />
          </FieldRow>
          <SwitchField
            form={form}
            name="isInTalentPool"
            label="Keep in the talent pool"
            description="Talent-pool candidates stay searchable for future vacancies after this one closes."
          />
        </CardContent>
      </Card>
    </div>
  );
}
