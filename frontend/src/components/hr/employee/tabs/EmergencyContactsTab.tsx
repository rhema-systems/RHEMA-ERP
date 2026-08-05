'use client';

import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Badge } from '@/components/ui/badge';
import { countryService } from '@/services/hr/country.service';
import { employeeService } from '@/services/hr/employee.service';
import {
  EMERGENCY_CONTACT_TYPE_OPTIONS,
  type EmergencyContactType,
  type EmployeeEmergencyContact,
} from '@/types/hr/employee-subresources';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import { FieldRow, SelectField, SwitchField, TextField, TextareaField } from './fields';

const schema = z.object({
  firstName: z.string().min(1, 'First name is required').max(100),
  middleName: z.string().max(100).optional().or(z.literal('')),
  lastName: z.string().min(1, 'Last name is required').max(100),
  relationship: z.string().min(1, 'Relationship is required').max(100),
  contactType: z.enum(['EmergencyContact', 'NextOfKin', 'Both']),
  phoneNumber: z.string().min(1, 'Phone number is required').max(30),
  alternatePhoneNumber: z.string().max(30).optional().or(z.literal('')),
  emailAddress: z.string().email('Enter a valid email').optional().or(z.literal('')),
  address: z.string().max(300).optional().or(z.literal('')),
  city: z.string().max(100).optional().or(z.literal('')),
  countryId: z.string().optional().or(z.literal('')),
  digitalAddress: z.string().max(30).optional().or(z.literal('')),
  isPrimary: z.boolean(),
  isActive: z.boolean(),
  notes: z.string().max(1000).optional().or(z.literal('')),
});

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  firstName: '',
  middleName: '',
  lastName: '',
  relationship: '',
  contactType: 'EmergencyContact',
  phoneNumber: '',
  alternatePhoneNumber: '',
  emailAddress: '',
  address: '',
  city: '',
  countryId: '',
  digitalAddress: '',
  isPrimary: false,
  isActive: true,
};

const toPayload = (employeeId: string, v: FormValues) => ({
  employeeId,
  firstName: v.firstName,
  middleName: v.middleName || null,
  lastName: v.lastName,
  relationship: v.relationship,
  contactType: v.contactType as EmergencyContactType,
  phoneNumber: v.phoneNumber,
  alternatePhoneNumber: v.alternatePhoneNumber || null,
  emailAddress: v.emailAddress || null,
  address: v.address || null,
  city: v.city || null,
  countryId: v.countryId || null,
  digitalAddress: v.digitalAddress || null,
  isPrimary: v.isPrimary,
  isActive: v.isActive,
  notes: v.notes || null,
});

export function EmergencyContactsTab({ employeeId }: { employeeId: string }) {
  const { data: countries } = useQuery({
    queryKey: ['hr', 'countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  const countryOptions = (countries ?? []).map((c) => ({ value: c.id, label: c.name }));

  return (
    <EmployeeSubResourceTab<EmployeeEmergencyContact, FormValues>
      employeeId={employeeId}
      title="emergency contacts"
      singular="emergency contact"
      queryKey="emergency-contacts"
      getId={(c) => c.id}
      list={employeeService.getEmergencyContacts.bind(employeeService)}
      create={(id, v) => employeeService.addEmergencyContact(id, toPayload(id, v))}
      update={(id, contactId, v) =>
        employeeService.updateEmergencyContact(id, contactId, {
          id: contactId,
          ...toPayload(id, v),
        })
      }
      remove={employeeService.removeEmergencyContact.bind(employeeService)}
      actions={[
        {
          label: 'Set as primary',
          visible: (c) => !c.isPrimary,
          run: (c) => employeeService.setPrimaryEmergencyContact(employeeId, c.id),
        },
        {
          label: (c) => (c.isActive ? 'Deactivate' : 'Activate'),
          run: (c) =>
            c.isActive
              ? employeeService.deactivateEmergencyContact(employeeId, c.id)
              : employeeService.activateEmergencyContact(employeeId, c.id),
        },
      ]}
      columns={[
        {
          header: 'Name',
          cell: (c) => [c.firstName, c.middleName, c.lastName].filter(Boolean).join(' '),
        },
        { header: 'Relationship', cell: (c) => c.relationship },
        { header: 'Type', cell: (c) => c.contactType },
        { header: 'Phone', cell: (c) => c.phoneNumber },
        { header: 'Email', cell: (c) => c.emailAddress || '—' },
        {
          header: 'Status',
          cell: (c) => (
            <div className="flex gap-1">
              {c.isPrimary && <Badge variant="secondary">Primary</Badge>}
              {!c.isActive && <Badge variant="outline">Inactive</Badge>}
            </div>
          ),
        },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={(c) => ({
        firstName: c.firstName,
        middleName: c.middleName ?? '',
        lastName: c.lastName,
        relationship: c.relationship,
        contactType: c.contactType,
        phoneNumber: c.phoneNumber,
        alternatePhoneNumber: c.alternatePhoneNumber ?? '',
        emailAddress: c.emailAddress ?? '',
        address: c.address ?? '',
        city: c.city ?? '',
        countryId: c.countryId ?? '',
        digitalAddress: c.digitalAddress ?? '',
        isPrimary: c.isPrimary,
        isActive: c.isActive,
        notes: c.notes ?? '',
      })}
      renderFields={(form) => (
        <>
          <FieldRow>
            <TextField form={form} name="firstName" label="First name" required />
            <TextField form={form} name="lastName" label="Last name" required />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="middleName" label="Middle name" />
            <TextField
              form={form}
              name="relationship"
              label="Relationship"
              placeholder="Spouse"
              required
            />
          </FieldRow>
          <SelectField
            form={form}
            name="contactType"
            label="Contact type"
            required
            options={EMERGENCY_CONTACT_TYPE_OPTIONS}
          />
          <FieldRow>
            <TextField form={form} name="phoneNumber" label="Phone" type="tel" required />
            <TextField
              form={form}
              name="alternatePhoneNumber"
              label="Alternate phone"
              type="tel"
            />
          </FieldRow>
          <TextField form={form} name="emailAddress" label="Email" type="email" />
          <TextField form={form} name="address" label="Address" />
          <FieldRow>
            <TextField form={form} name="city" label="City" />
            <TextField form={form} name="digitalAddress" label="Digital address" />
          </FieldRow>
          <SelectField
            form={form}
            name="countryId"
            label="Country"
            options={countryOptions}
            allowEmpty
          />
          <TextareaField form={form} name="notes" label="Notes" />
          <FieldRow>
            <SwitchField form={form} name="isPrimary" label="Primary contact" />
            <SwitchField form={form} name="isActive" label="Active" />
          </FieldRow>
        </>
      )}
    />
  );
}
