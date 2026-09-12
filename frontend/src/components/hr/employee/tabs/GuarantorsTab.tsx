'use client';

import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { hrCurrencyService } from '@/services/hr/hr-currency.service';
import { Badge } from '@/components/ui/badge';
import { countryService } from '@/services/hr/country.service';
import { employeeService } from '@/services/hr/employee.service';
import { GENDER_OPTIONS } from '@/types/hr/employee';
import type { Gender } from '@/types/hr/employee';
import type { EmployeeGuarantor } from '@/types/hr/employee-subresources';
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

const schema = z.object({
  firstName: z.string().min(1, 'First name is required').max(100),
  middleName: z.string().max(100).optional().or(z.literal('')),
  lastName: z.string().min(1, 'Last name is required').max(100),
  title: z.string().max(50).optional().or(z.literal('')),
  relationship: z.string().min(1, 'Relationship is required').max(100),
  gender: z.string().optional().or(z.literal('')),
  dateOfBirth: z.string().optional().or(z.literal('')),
  address: z.string().min(1, 'Address is required').max(300),
  city: z.string().max(100).optional().or(z.literal('')),
  digitalAddress: z.string().max(30).optional().or(z.literal('')),
  countryId: z.string().optional().or(z.literal('')),
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
  gender: '',
  dateOfBirth: '',
  address: '',
  city: '',
  digitalAddress: '',
  countryId: '',
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
  firstName: v.firstName,
  middleName: v.middleName || null,
  lastName: v.lastName,
  title: v.title || null,
  gender: (v.gender || null) as Gender | null,
  dateOfBirth: v.dateOfBirth || null,
  address: v.address,
  city: v.city || null,
  digitalAddress: v.digitalAddress || null,
  countryId: v.countryId || null,
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
  nationalIdType: v.nationalIdType || null,
  nationalIdNumber: v.nationalIdNumber || null,
  nationalIdExpiryDate: v.nationalIdExpiryDate || null,
  hasSignedGuarantorForm: v.hasSignedGuarantorForm,
  dateFormSigned: v.dateFormSigned || null,
  notes: v.notes || null,
  isActive: v.isActive,
});

export function GuarantorsTab({ employeeId }: { employeeId: string }) {
  const { data: countries } = useQuery({
    queryKey: ['hr', 'countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  // Only currencies Finance actually holds — the server refuses anything else, so a free-text box
  // would be offering a way to fail. Same read the succession development panel uses.
  const { data: currencies } = useQuery({
    queryKey: ['finance', 'currencies'],
    queryFn: () => hrCurrencyService.getActive(),
  });

  const countryOptions = (countries ?? []).map((c) => ({ value: c.id, label: c.name }));

  return (
    <EmployeeSubResourceTab<EmployeeGuarantor, FormValues>
      employeeId={employeeId}
      title="guarantors"
      singular="guarantor"
      queryKey="guarantors"
      getId={(g) => g.id}
      list={employeeService.getGuarantors.bind(employeeService)}
      create={(id, v) => employeeService.addGuarantor(id, toPayload(id, v))}
      update={(id, guarantorId, v) =>
        employeeService.updateGuarantor(id, guarantorId, { id: guarantorId, ...toPayload(id, v) })
      }
      remove={employeeService.removeGuarantor.bind(employeeService)}
      actions={[
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
        { header: 'Employer', cell: (g) => g.employerName || '—' },
        // The API masks the national ID; the raw value is never returned.
        { header: 'National ID', cell: (g) => g.nationalIdNumberMasked || '—' },
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
      toForm={(g) => ({
        firstName: g.firstName,
        middleName: g.middleName ?? '',
        lastName: g.lastName,
        title: g.title ?? '',
        relationship: g.relationship,
        gender: g.gender ?? '',
        dateOfBirth: g.dateOfBirth?.slice(0, 10) ?? '',
        address: g.address ?? '',
        city: g.city ?? '',
        digitalAddress: g.digitalAddress ?? '',
        countryId: g.countryId ?? '',
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
      })}
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
          <FieldRow>
            <TextField form={form} name="relationship" label="Relationship" required />
            <SelectField
              form={form}
              name="gender"
              label="Gender"
              options={GENDER_OPTIONS}
              allowEmpty
            />
          </FieldRow>
          <DateField form={form} name="dateOfBirth" label="Date of birth" />
          <TextField form={form} name="address" label="Address" required />
          <FieldRow>
            <TextField form={form} name="city" label="City" />
            <TextField form={form} name="digitalAddress" label="Digital address" />
          </FieldRow>
          <FieldRow>
            <SelectField
              form={form}
              name="countryId"
              label="Country"
              options={countryOptions}
              allowEmpty
            />
            <TextField form={form} name="phoneNumber" label="Phone" type="tel" />
          </FieldRow>
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
            <TextField
              form={form}
              name="nationalIdType"
              label="National ID type"
              placeholder="Ghana Card"
            />
            <TextField
              form={form}
              name="nationalIdNumber"
              label="National ID number"
              placeholder="Leave blank to keep the stored number"
            />
          </FieldRow>
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
        </>
      )}
    />
  );
}
