'use client';

import { useState } from 'react';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Camera, Paperclip } from 'lucide-react';
import { hrCurrencyService } from '@/services/hr/hr-currency.service';
import { Badge } from '@/components/ui/badge';
import { employeeService } from '@/services/hr/employee.service';
import { identificationTypeService } from '@/services/hr/lookup.service';
import { GENDER_OPTIONS } from '@/types/hr/employee';
import type { Gender } from '@/types/hr/employee';
import type { EmployeeGuarantor } from '@/types/hr/employee-subresources';
import { RELATIONSHIP_SCOPES } from '@/types/hr/relationship-type';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import { AddressCascadeField, RelationshipField } from './address-fields';
import { GuarantorFilesDialog } from './GuarantorFilesDialog';
import {
  DateField,
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextField,
  TextareaField,
} from './fields';

const schema = z.object({
  firstName: z.string().min(1, 'First name is required').max(100),
  middleName: z.string().max(100).optional().or(z.literal('')),
  lastName: z.string().min(1, 'Last name is required').max(100),
  title: z.string().max(50).optional().or(z.literal('')),
  relationship: z.string().min(1, 'Relationship is required').max(100),
  relationshipTypeId: z.string().optional().or(z.literal('')),
  gender: z.string().optional().or(z.literal('')),
  dateOfBirth: z.string().optional().or(z.literal('')),
  address: z.string().min(1, 'Address is required').max(500),
  city: z.string().max(100).optional().or(z.literal('')),
  region: z.string().max(100).optional().or(z.literal('')),
  digitalAddress: z.string().max(50).optional().or(z.literal('')),
  countryId: z.string().optional().or(z.literal('')),
  geoAreaId: z.string().optional().or(z.literal('')),
  phoneNumber: z.string().max(30).optional().or(z.literal('')),
  emailAddress: z.string().email('Enter a valid email').optional().or(z.literal('')),
  jobTitle: z.string().max(200).optional().or(z.literal('')),
  employerName: z.string().max(200).optional().or(z.literal('')),
  employerAddress: z.string().max(300).optional().or(z.literal('')),
  employerPhone: z.string().max(30).optional().or(z.literal('')),
  monthlyIncome: z.string().optional().or(z.literal('')),
  amountGuaranteed: z.string().optional().or(z.literal('')),
  amountGuaranteedCurrencyCode: z.string().optional().or(z.literal('')),
  genderDescription: z.string().optional().or(z.literal('')),
  // The catalogue kind. When it is empty the free-text box below is offered instead.
  nationalIdTypeId: z.string().optional().or(z.literal('')),
  nationalIdType: z.string().max(50).optional().or(z.literal('')),
  nationalIdNumber: z.string().max(50).optional().or(z.literal('')),
  nationalIdExpiryDate: z.string().optional().or(z.literal('')),
  hasSignedGuarantorForm: z.boolean(),
  dateFormSigned: z.string().optional().or(z.literal('')),
  isPrimary: z.boolean(),
  isActive: z.boolean(),
  notes: z.string().max(1000).optional().or(z.literal('')),
});

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  firstName: '',
  middleName: '',
  lastName: '',
  title: '',
  relationship: '',
  relationshipTypeId: '',
  gender: '',
  dateOfBirth: '',
  address: '',
  city: '',
  region: '',
  digitalAddress: '',
  countryId: '',
  geoAreaId: '',
  phoneNumber: '',
  emailAddress: '',
  jobTitle: '',
  employerName: '',
  employerAddress: '',
  employerPhone: '',
  monthlyIncome: '',
  amountGuaranteed: '',
  amountGuaranteedCurrencyCode: '',
  genderDescription: '',
  nationalIdTypeId: '',
  nationalIdType: '',
  nationalIdNumber: '',
  nationalIdExpiryDate: '',
  hasSignedGuarantorForm: false,
  dateFormSigned: '',
  isPrimary: false,
  isActive: true,
  notes: '',
};

const toPayload = (employeeId: string, v: FormValues) => ({
  employeeId,
  isPrimary: v.isPrimary,
  relationship: v.relationship,
  // ⚠ When an id is sent the server OVERWRITES `relationship` with the catalogue row's name.
  // A guarantor accepts all three categories — an employer, a brother and a landlord can each
  // stand surety.
  relationshipTypeId: v.relationshipTypeId || null,
  // ⚠ Nulls mean "not supplied" on the guarantor UPDATE DTO, so unlinking has to say so
  // explicitly. The tab sends the whole form every save; without these an emptied picker would
  // save successfully and change nothing.
  clearRelationshipType: !v.relationshipTypeId,
  clearGeoArea: !v.geoAreaId,
  firstName: v.firstName,
  middleName: v.middleName || null,
  lastName: v.lastName,
  title: v.title || null,
  gender: (v.gender || null) as Gender | null,
  dateOfBirth: v.dateOfBirth || null,
  address: v.address,
  city: v.city || null,
  region: v.region || null,
  digitalAddress: v.digitalAddress || null,
  countryId: v.countryId || null,
  geoAreaId: v.geoAreaId || null,
  phoneNumber: v.phoneNumber || null,
  emailAddress: v.emailAddress || null,
  jobTitle: v.jobTitle || null,
  employerName: v.employerName || null,
  employerAddress: v.employerAddress || null,
  employerPhone: v.employerPhone || null,
  monthlyIncome: v.monthlyIncome ? Number(v.monthlyIncome) : null,
  amountGuaranteed: v.amountGuaranteed ? Number(v.amountGuaranteed) : null,
  amountGuaranteedCurrencyCode: v.amountGuaranteedCurrencyCode || null,
  genderDescription: v.genderDescription || null,
  // Catalogue kind wins; when one is chosen the free text is blanked rather than left beside it
  // (an empty string IS a supplied value on the update DTO, unlike null).
  nationalIdTypeId: v.nationalIdTypeId || null,
  nationalIdType: v.nationalIdTypeId ? '' : v.nationalIdType || null,
  nationalIdNumber: v.nationalIdNumber || null,
  nationalIdExpiryDate: v.nationalIdExpiryDate || null,
  hasSignedGuarantorForm: v.hasSignedGuarantorForm,
  dateFormSigned: v.dateFormSigned || null,
  notes: v.notes || null,
  isActive: v.isActive,
});

const toForm = (g: EmployeeGuarantor): FormValues => ({
  firstName: g.firstName,
  middleName: g.middleName ?? '',
  lastName: g.lastName,
  title: g.title ?? '',
  relationship: g.relationship,
  relationshipTypeId: g.relationshipTypeId ?? '',
  gender: g.gender ?? '',
  dateOfBirth: g.dateOfBirth?.slice(0, 10) ?? '',
  address: g.address ?? '',
  city: g.city ?? '',
  region: g.region ?? '',
  digitalAddress: g.digitalAddress ?? '',
  countryId: g.countryId ?? '',
  geoAreaId: g.geoAreaId ?? '',
  phoneNumber: g.phoneNumber ?? '',
  emailAddress: g.emailAddress ?? '',
  jobTitle: g.jobTitle ?? '',
  employerName: g.employerName ?? '',
  employerAddress: g.employerAddress ?? '',
  employerPhone: g.employerPhone ?? '',
  monthlyIncome: g.monthlyIncome != null ? String(g.monthlyIncome) : '',
  amountGuaranteed: g.amountGuaranteed != null ? String(g.amountGuaranteed) : '',
  amountGuaranteedCurrencyCode: g.amountGuaranteedCurrencyCode ?? '',
  genderDescription: g.genderDescription ?? '',
  nationalIdTypeId: g.nationalIdTypeId ?? '',
  nationalIdType: g.nationalIdType ?? '',
  // Masked on read — leave blank so an edit does not overwrite the stored value
  // with the mask. Enter a new number only to replace it.
  nationalIdNumber: '',
  nationalIdExpiryDate: g.nationalIdExpiryDate?.slice(0, 10) ?? '',
  hasSignedGuarantorForm: g.hasSignedGuarantorForm ?? false,
  dateFormSigned: g.dateFormSigned?.slice(0, 10) ?? '',
  isPrimary: g.isPrimary,
  isActive: g.isActive,
  notes: g.notes ?? '',
});

/**
 * Guarantors — who stands surety for the employee, for how much, with what papers.
 *
 * Round 2 (lanes A-5, A-6, A-8, A-9): the national-ID kind comes from the identification-type
 * catalogue; the photograph and documents are reachable from the row's "Files…" action; and the
 * edit dialog hydrates from the DETAIL read, because the list is a summary and a save off it
 * would have blanked address, employer, national ID and surety.
 */
export function GuarantorsTab({ employeeId }: { employeeId: string }) {
  const [filesFor, setFilesFor] = useState<EmployeeGuarantor | null>(null);

  // Only currencies Finance actually holds — the server refuses anything else, so a free-text box
  // would be offering a way to fail. Same read the succession development panel uses.
  const { data: currencies } = useQuery({
    queryKey: ['finance', 'currencies'],
    queryFn: () => hrCurrencyService.getActive(),
  });

  // The same catalogue the employee's own Identification tab offers (round 2, E-13).
  const { data: idTypes } = useQuery({
    queryKey: ['hr', 'identification-types', 'active'],
    queryFn: () => identificationTypeService.getActive(),
  });

  const idTypeOptions = (idTypes ?? []).map((t) => ({ value: t.id, label: t.name }));

  return (
    <>
      <EmployeeSubResourceTab<EmployeeGuarantor, FormValues>
        employeeId={employeeId}
        title="guarantors"
        singular="guarantor"
        queryKey="guarantors"
        getId={(g) => g.id}
        list={employeeService.getGuarantors.bind(employeeService)}
        create={(id, v) => employeeService.addGuarantor(id, toPayload(id, v))}
        update={(id, guarantorId, v) =>
          employeeService.updateGuarantor(id, guarantorId, {
            id: guarantorId,
            ...toPayload(id, v),
            // A null id means "not supplied" on the update DTO; unlinking has to be explicit.
            clearNationalIdType: !v.nationalIdTypeId,
          })
        }
        remove={employeeService.removeGuarantor.bind(employeeService)}
        actions={[
          {
            label: 'Files…',
            run: async (g) => setFilesFor(g),
          },
          {
            label: 'Set as primary',
            visible: (g) => !g.isPrimary,
            run: (g) => employeeService.setPrimaryGuarantor(employeeId, g.id),
          },
          {
            label: (g) => (g.isVerified ? 'Mark unverified' : 'Mark verified'),
            run: (g) =>
              g.isVerified
                ? employeeService.unverifyGuarantor(employeeId, g.id)
                : employeeService.verifyGuarantor(employeeId, g.id, {
                    // The API records who verified; the employee being viewed stands in
                    // until a "current employee" claim is wired through the portal.
                    verifiedByEmployeeId: employeeId,
                    verifiedDate: new Date().toISOString(),
                  }),
          },
          {
            label: (g) => (g.isActive ? 'Deactivate' : 'Activate'),
            run: (g) =>
              g.isActive
                ? employeeService.deactivateGuarantor(employeeId, g.id)
                : employeeService.activateGuarantor(employeeId, g.id),
          },
        ]}
        columns={[
          {
            header: 'Name',
            cell: (g) => [g.title, g.firstName, g.middleName, g.lastName].filter(Boolean).join(' '),
          },
          { header: 'Relationship', cell: (g) => g.relationship },
          { header: 'Phone', cell: (g) => g.phoneNumber || '—' },
          { header: 'ID type', cell: (g) => g.nationalIdTypeName || g.nationalIdType || '—' },
          {
            header: 'Files',
            cell: (g) => (
              <div className="flex items-center gap-2 text-xs text-muted-foreground">
                {g.hasPhoto && (
                  <span className="inline-flex items-center gap-1" title="Photograph on file">
                    <Camera className="h-3.5 w-3.5" /> Photo
                  </span>
                )}
                {(g.documentCount ?? 0) > 0 && (
                  <span className="inline-flex items-center gap-1" title="Documents on file">
                    <Paperclip className="h-3.5 w-3.5" /> {g.documentCount}
                  </span>
                )}
                {!g.hasPhoto && !(g.documentCount ?? 0) && '—'}
              </div>
            ),
          },
          {
            header: 'Status',
            cell: (g) => (
              <div className="flex gap-1">
                {g.isPrimary && <Badge variant="secondary">Primary</Badge>}
                {g.isVerified && <Badge variant="secondary">Verified</Badge>}
                {!g.isActive && <Badge variant="outline">Inactive</Badge>}
              </div>
            ),
          },
        ]}
        schema={schema}
        emptyForm={empty}
        dialogClassName="sm:max-w-[720px]"
        toForm={toForm}
        // ⚠ The list is a SUMMARY: address, employer, national ID, surety and the photo metadata
        // are absent from it. Without this, opening a guarantor and pressing Save blanked every
        // one of those — silently, because they are optional. See ResourceCollectionTab.loadForEdit.
        loadForEdit={async (g) => toForm(await employeeService.getGuarantor(employeeId, g.id))}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="firstName" label="First name" required />
              <TextField form={form} name="lastName" label="Last name" required />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="middleName" label="Middle name" />
              <TextField form={form} name="title" label="Title" placeholder="Mr / Mrs / Dr" />
            </FieldRow>
            {/*
              ⚠ All three categories, unlike the referee and next-of-kin screens: an employer, a
              brother and a landlord can each stand surety, and refusing any of them would be
              inventing a rule the business does not have.
            */}
            <RelationshipField
              form={form}
              typeIdName="relationshipTypeId"
              textName="relationship"
              categories={RELATIONSHIP_SCOPES.guarantor}
              required
            />
            <SelectField
              form={form}
              name="gender"
              label="Gender"
              options={GENDER_OPTIONS}
              allowEmpty
            />
            {form.watch('gender') === 'Other' && (
              <TextField form={form} name="genderDescription" label="Describe gender" />
            )}
            <DateField form={form} name="dateOfBirth" label="Date of birth" />
            <TextField form={form} name="address" label="Address" required />
            <AddressCascadeField
              form={form}
              countryName="countryId"
              geoAreaName="geoAreaId"
              cityName="city"
              regionName="region"
            >
              <FieldRow>
                <TextField form={form} name="digitalAddress" label="Digital address" />
                <TextField form={form} name="phoneNumber" label="Phone" type="tel" />
              </FieldRow>
            </AddressCascadeField>
            <TextField form={form} name="emailAddress" label="Email" type="email" />
            <FieldRow>
              <TextField form={form} name="jobTitle" label="Job title" />
              <TextField form={form} name="employerName" label="Employer" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="employerAddress" label="Employer address" />
              <TextField form={form} name="employerPhone" label="Employer phone" type="tel" />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="monthlyIncome" label="Monthly income" step="0.01" />
              {/* ⚠ Distinct from income: what they EARN versus what they have UNDERTAKEN. The row
                  could say how solvent a guarantor was and never what they stood surety for — the
                  only figure that matters if the guarantee is ever called. */}
              <NumberField
                form={form}
                name="amountGuaranteed"
                label="Amount guaranteed"
                step="0.01"
              />
              <SelectField
                form={form}
                name="amountGuaranteedCurrencyCode"
                label="Currency"
                // Finance owns this list; the server refuses a code it does not hold.
                options={(currencies ?? []).map((c) => ({
                  value: c.code,
                  label: `${c.code} — ${c.name}`,
                }))}
                allowEmpty
                emptyLabel="HR default"
              />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={form}
                name="nationalIdTypeId"
                label="National ID type"
                options={idTypeOptions}
                allowEmpty
                emptyLabel="Not in the list"
              />
              <TextField
                form={form}
                name="nationalIdNumber"
                label="National ID number"
                placeholder="Leave blank to keep the stored number"
              />
            </FieldRow>
            {/* The free-text kind is only offered when the catalogue has no entry — the lane-3b
                idiom, so rows recorded before the lookup existed are not blanked. */}
            {!form.watch('nationalIdTypeId') && (
              <TextField
                form={form}
                name="nationalIdType"
                label="ID type (not in the list)"
                placeholder="Say what kind of ID it is"
              />
            )}
            <DateField form={form} name="nationalIdExpiryDate" label="National ID expiry" />
            <FieldRow>
              <SwitchField
                form={form}
                name="hasSignedGuarantorForm"
                label="Guarantor form signed"
              />
              <DateField form={form} name="dateFormSigned" label="Date signed" />
            </FieldRow>
            <FieldRow>
              <SwitchField form={form} name="isPrimary" label="Primary guarantor" />
              <SwitchField form={form} name="isActive" label="Active" />
            </FieldRow>
            <TextareaField form={form} name="notes" label="Notes" />
            <p className="text-xs text-muted-foreground">
              The photograph and the signed form are attached from the row's “Files…” action after
              saving — they go through the document store, not this form.
            </p>
          </>
        )}
      />

      <GuarantorFilesDialog
        employeeId={employeeId}
        guarantor={filesFor}
        open={filesFor !== null}
        onOpenChange={(o) => !o && setFilesFor(null)}
      />
    </>
  );
}
