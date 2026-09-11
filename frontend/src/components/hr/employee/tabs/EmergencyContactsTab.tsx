'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { employeeService } from '@/services/hr/employee.service';
import {
  EMERGENCY_CONTACT_TYPE_OPTIONS,
  type EmergencyContactType,
  type EmployeeEmergencyContact,
} from '@/types/hr/employee-subresources';
import { RELATIONSHIP_SCOPES } from '@/types/hr/relationship-type';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import { AddressCascadeField, RelationshipField } from './address-fields';
import { FieldRow, SelectField, SwitchField, TextField, TextareaField } from './fields';

const schema = z.object({
  firstName: z.string().min(1, 'First name is required').max(100),
  middleName: z.string().max(100).optional().or(z.literal('')),
  lastName: z.string().min(1, 'Last name is required').max(100),
  relationship: z.string().min(1, 'Relationship is required').max(100),
  relationshipTypeId: z.string().optional().or(z.literal('')),
  contactType: z.enum(['EmergencyContact', 'NextOfKin', 'Both']),
  phoneNumber: z.string().min(1, 'Phone number is required').max(30),
  alternatePhoneNumber: z.string().max(30).optional().or(z.literal('')),
  emailAddress: z.string().email('Enter a valid email').optional().or(z.literal('')),
  address: z.string().max(500).optional().or(z.literal('')),
  city: z.string().max(100).optional().or(z.literal('')),
  region: z.string().max(100).optional().or(z.literal('')),
  countryId: z.string().optional().or(z.literal('')),
  geoAreaId: z.string().optional().or(z.literal('')),
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
  relationshipTypeId: '',
  contactType: 'EmergencyContact',
  phoneNumber: '',
  alternatePhoneNumber: '',
  emailAddress: '',
  address: '',
  city: '',
  region: '',
  countryId: '',
  geoAreaId: '',
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
  // ⚠ When an id is sent the server OVERWRITES `relationship` with the catalogue row's name, so
  // the two can never disagree. Null clears the link and the typed words stand alone.
  relationshipTypeId: v.relationshipTypeId || null,
  contactType: v.contactType as EmergencyContactType,
  phoneNumber: v.phoneNumber,
  alternatePhoneNumber: v.alternatePhoneNumber || null,
  emailAddress: v.emailAddress || null,
  address: v.address || null,
  city: v.city || null,
  region: v.region || null,
  countryId: v.countryId || null,
  geoAreaId: v.geoAreaId || null,
  digitalAddress: v.digitalAddress || null,
  isPrimary: v.isPrimary,
  isActive: v.isActive,
  notes: v.notes || null,
});

export function EmergencyContactsTab({ employeeId }: { employeeId: string }) {
  return (
    <EmployeeSubResourceTab<EmployeeEmergencyContact, FormValues>
      employeeId={employeeId}
      title="emergency contacts"
      singular="emergency contact"
      itemLabel={(c) => [c.firstName, c.middleName, c.lastName].filter(Boolean).join(' ')}
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
      dialogClassName="sm:max-w-[640px]"
      toForm={(c) => ({
        firstName: c.firstName,
        middleName: c.middleName ?? '',
        lastName: c.lastName,
        relationship: c.relationship,
        relationshipTypeId: c.relationshipTypeId ?? '',
        contactType: c.contactType,
        phoneNumber: c.phoneNumber,
        alternatePhoneNumber: c.alternatePhoneNumber ?? '',
        emailAddress: c.emailAddress ?? '',
        address: c.address ?? '',
        city: c.city ?? '',
        region: c.region ?? '',
        countryId: c.countryId ?? '',
        geoAreaId: c.geoAreaId ?? '',
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
          <TextField form={form} name="middleName" label="Middle name" />
          {/*
            ⚠ Familial and other only. A next of kin is not a former manager, and a dropdown that
            offered one would be as useless as the free text it replaces.
          */}
          <RelationshipField
            form={form}
            typeIdName="relationshipTypeId"
            textName="relationship"
            categories={RELATIONSHIP_SCOPES.nextOfKin}
            required
          />
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
          <AddressCascadeField
            form={form}
            countryName="countryId"
            geoAreaName="geoAreaId"
            cityName="city"
            regionName="region"
          >
            <TextField form={form} name="digitalAddress" label="Digital address" />
          </AddressCascadeField>
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
