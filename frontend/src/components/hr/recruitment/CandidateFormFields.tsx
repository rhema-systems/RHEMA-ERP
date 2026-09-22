'use client';

import { useQuery } from '@tanstack/react-query';
import type { UseFormReturn } from 'react-hook-form';
import { z } from 'zod';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  DateField,
  FieldRow,
  // ⚠ NumberField, not `TextField type="number"` — TextField's `type` union is text|email|tel only.
  NumberField,
  SelectField,
  SwitchField,
  TextField,
} from '@/components/hr/employee/tabs/fields';
import { AddressCascadeField } from '@/components/hr/employee/tabs/address-fields';
import { identificationTypeService } from '@/services/hr/lookup.service';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import { GENDERS, PREFERRED_WORK_ARRANGEMENTS } from '@/types/hr/recruitment-pipeline';

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
  // Round 4, lane A. No longer `min(1)`: when the chosen country has a geography scheme the
  // cascade below owns the address, the city box is disabled, and the server rewrites it from the
  // tree on save. A required-but-uneditable field is a form that cannot be submitted. The address
  // stays compulsory through the refine at the bottom — an area, or a city, never neither.
  city: z.string().max(100).optional().nullable(),
  region: z.string().max(100).optional().nullable(),
  geoAreaId: z.string().optional().nullable(),
  // G-7.3: free text, and optional. Nationality is not the same question as country of residence —
  // a dual national or a stateless applicant is not served by a single FK into the country table.
  nationality: z.string().max(100).optional().nullable(),
  // Optional since 2026-09-14, matching the entity. It was `min(1)`, which meant a candidate
  // with no country — every shadow record minted from a countryless employee by the internal job
  // board — could not be saved from this form at all, whatever else you were trying to change.
  countryId: z.string().optional().nullable(),
  linkedInProfile: z.string().max(200).optional().nullable(),
  portfolioUrl: z.string().max(200).optional().nullable(),
  gitHubUrl: z.string().max(200).optional().nullable(),
  // National identity (round 3, lanes C1/C2) — the employee's trio, carried across at hire.
  nationalIdTypeId: z.string().optional().nullable(),
  nationalIdNumber: z.string().max(50).optional().nullable(),
  nationalIdExpiryDate: z.string().optional().nullable(),
  // Round 4, lane B - the professional profile HR could not record. See the card below.
  headline: z.string().max(300).optional().nullable(),
  professionalSummary: z.string().max(4000).optional().nullable(),
  currentJobTitle: z.string().max(200).optional().nullable(),
  currentEmployer: z.string().max(200).optional().nullable(),
  // Kept as strings: an <input type="number"> yields '' for empty, and coercing '' to 0 would
  // record "no experience" for "not asked". The page maps '' to null on the way out.
  totalYearsExperience: z.string().optional().nullable(),
  noticePeriodDays: z.string().optional().nullable(),
  availableFrom: z.string().optional().nullable(),
  preferredWorkArrangement: z.string().optional().nullable(),
  isInTalentPool: z.boolean(),
}).refine((v) => !v.nationalIdNumber?.trim() || !!v.nationalIdTypeId, {
  message: 'Say which document the number is from',
  path: ['nationalIdTypeId'],
}).refine((v) => !!v.geoAreaId || !!v.city?.trim(), {
  // Mirrors the server's JobCandidateAddressRule. Most of the world has no scheme loaded, so
  // demanding an area would make the form unfillable outside Ghana; demanding a city would make it
  // unfillable inside Ghana, where the cascade writes it. One or the other, never neither.
  message: 'Say where the candidate is: pick an area, or type a city.',
  path: ['city'],
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
  region: null,
  geoAreaId: null,
  nationality: null,
  countryId: '',
  linkedInProfile: null,
  portfolioUrl: null,
  gitHubUrl: null,
  nationalIdTypeId: null,
  nationalIdNumber: null,
  nationalIdExpiryDate: null,
  headline: null,
  professionalSummary: null,
  currentJobTitle: null,
  currentEmployer: null,
  totalYearsExperience: null,
  noticePeriodDays: null,
  availableFrom: null,
  // 'Any' is the entity's default and means "no preference stated". The pool rubric scores it as
  // genuine flexibility rather than as a match, which is the G-13.2 distinction.
  preferredWorkArrangement: 'Any',
  isInTalentPool: false,
};

/**
 * The editable half of a candidate record.
 *
 * ⚠ Round 4, lane B: the professional profile and availability are now HERE as well as on the
 * candidate's portal profile. They used to be portal-only, on the reasoning that they belong to the
 * candidate — which is right for someone who applied online and wrong for everybody else. A career
 * fair, a referral and an unsolicited CV all reach the talent pool through HR typing them in, and
 * the pool's own match rubric scores on experience, work arrangement and availability. So the
 * people HR knew most about scored lowest, because HR had nowhere to put what it knew.
 *
 * ⚠ Still not covered: expected salary and work-authorization status. Those remain candidate-only
 * and the DTOs do not carry them, so an input here would silently discard what was typed.
 */
export function CandidateFormFields({ form }: { form: UseFormReturn<CandidateFormValues> }) {
  // ⚠ Round 4, lane A: the country list is no longer fetched here. AddressCascadeField owns the
  // country picker now, because the scheme it loads is keyed on the country — splitting the two
  // across two components meant the cascade could not react to the country changing.
  // The tenant's identity documents (Ghana Card, passport, …) — the server refuses any other type.
  const idTypes = useQuery({
    queryKey: ['hr', 'identification-types', 'active'],
    queryFn: () => identificationTypeService.getActive(),
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
          {/* Round 3, lane C1/C2 (register row R-3a): the national identity document. At hire it
              becomes the employee's first identification card, unverified. */}
          <FieldRow>
            <SelectField
              form={form}
              name="nationalIdTypeId"
              label="Identity document"
              allowEmpty
              emptyLabel="Not recorded"
              options={(idTypes.data ?? []).map((t) => ({ value: t.id, label: t.name }))}
            />
            <TextField form={form} name="nationalIdNumber" label="Document number" />
          </FieldRow>
          <FieldRow>
            <DateField form={form} name="nationalIdExpiryDate" label="Document expiry" />
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
          {/* Round 4, lane A. The country select and the free-text city used to sit here as two
              unrelated inputs; they are now one control, the same cascade the employee register
              uses. City and Region render underneath — disabled when the chosen country has a
              scheme, because the server rewrites both from the tree on save, and as ordinary text
              inputs when it has none, which is most countries. The area is what lets a vacancy's
              Location criterion match "Greater Accra" against somebody recorded in Tema. */}
          <AddressCascadeField
            form={form}
            countryName="countryId"
            geoAreaName="geoAreaId"
            cityName="city"
            regionName="region"
          >
            <FieldRow>
              <TextField form={form} name="postalAddress" label="Postal address" />
              {/* G-7.3: displayed on the Personal card since the module was built and settable by
                  nothing — the demo seeder was its only writer, which is why it read "Ghanaian" in
                  every walkthrough and "—" on every real tenant. */}
              <TextField form={form} name="nationality" label="Nationality" />
            </FieldRow>
          </AddressCascadeField>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Professional profile</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <TextField form={form} name="headline" label="Headline" />
            <NumberField form={form} name="totalYearsExperience" label="Years of experience" />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="currentJobTitle" label="Current job title" />
            <TextField form={form} name="currentEmployer" label="Current employer" />
          </FieldRow>
          <FieldRow>
            <DateField form={form} name="availableFrom" label="Available from" />
            <NumberField form={form} name="noticePeriodDays" label="Notice period (days)" />
          </FieldRow>
          <FieldRow>
            <SelectField
              form={form}
              name="preferredWorkArrangement"
              label="Preferred work arrangement"
              options={PREFERRED_WORK_ARRANGEMENTS.map((w) => ({ value: w, label: humanizeEnum(w) }))}
            />
            <div />
          </FieldRow>
          <TextField form={form} name="professionalSummary" label="Professional summary" />
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
