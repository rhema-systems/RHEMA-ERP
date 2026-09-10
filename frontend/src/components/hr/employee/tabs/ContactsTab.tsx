'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { employeeService } from '@/services/hr/employee.service';
import {
  EMPLOYEE_CONTACT_TYPE_OPTIONS,
  type EmployeeContact,
  type EmployeeContactType,
} from '@/types/hr/employee-subresources';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import { AddressCascadeField } from './address-fields';
import { SelectField, SwitchField, TextField } from './fields';

const schema = z.object({
  contactType: z.enum(['Home', 'Postal', 'Temporary', 'Other']),
  addressLine1: z.string().max(200).optional().or(z.literal('')),
  addressLine2: z.string().max(200).optional().or(z.literal('')),
  city: z.string().max(100).optional().or(z.literal('')),
  region: z.string().max(100).optional().or(z.literal('')),
  digitalAddress: z.string().max(30).optional().or(z.literal('')),
  countryId: z.string().optional().or(z.literal('')),
  geoAreaId: z.string().optional().or(z.literal('')),
  isPrimary: z.boolean(),
});

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  contactType: 'Home',
  addressLine1: '',
  addressLine2: '',
  city: '',
  region: '',
  digitalAddress: '',
  countryId: '',
  geoAreaId: '',
  isPrimary: false,
};

const toPayload = (employeeId: string, v: FormValues) => ({
  employeeId,
  contactType: v.contactType as EmployeeContactType,
  addressLine1: v.addressLine1 || null,
  addressLine2: v.addressLine2 || null,
  city: v.city || null,
  region: v.region || null,
  digitalAddress: v.digitalAddress || null,
  countryId: v.countryId || null,
  // ⚠ Null CLEARS the link on the update DTO, which is what emptying the cascade has to mean —
  // this DTO's address fields are all full-replace.
  geoAreaId: v.geoAreaId || null,
  isPrimary: v.isPrimary,
});

export function ContactsTab({ employeeId }: { employeeId: string }) {
  return (
    <EmployeeSubResourceTab<EmployeeContact, FormValues>
      employeeId={employeeId}
      title="addresses"
      singular="address"
      queryKey="contacts"
      getId={(c) => c.id}
      list={employeeService.getContacts.bind(employeeService)}
      create={(id, v) => employeeService.addContact(id, toPayload(id, v))}
      update={(id, contactId, v) =>
        employeeService.updateContact(id, contactId, { id: contactId, ...toPayload(id, v) })
      }
      remove={employeeService.removeContact.bind(employeeService)}
      actions={[
        {
          label: 'Set as primary',
          visible: (c) => !c.isPrimary,
          run: (c) => employeeService.setPrimaryContact(employeeId, c.id),
        },
      ]}
      columns={[
        { header: 'Type', cell: (c) => c.contactType },
        {
          header: 'Address',
          cell: (c) => [c.addressLine1, c.addressLine2].filter(Boolean).join(', ') || '—',
        },
        { header: 'City', cell: (c) => c.city || '—' },
        { header: 'Region', cell: (c) => c.region || '—' },
        { header: 'Digital address', cell: (c) => c.digitalAddress || '—' },
        {
          header: 'Primary',
          cell: (c) => (c.isPrimary ? <Badge variant="secondary">Primary</Badge> : '—'),
        },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={(c) => ({
        contactType: c.contactType,
        addressLine1: c.addressLine1 ?? '',
        addressLine2: c.addressLine2 ?? '',
        city: c.city ?? '',
        region: c.region ?? '',
        digitalAddress: c.digitalAddress ?? '',
        countryId: c.countryId ?? '',
        geoAreaId: c.geoAreaId ?? '',
        isPrimary: c.isPrimary,
      })}
      dialogClassName="sm:max-w-[640px]"
      renderFields={(form) => (
        <>
          <SelectField
            form={form}
            name="contactType"
            label="Address type"
            required
            options={EMPLOYEE_CONTACT_TYPE_OPTIONS}
          />
          <TextField form={form} name="addressLine1" label="Address line 1" />
          <TextField form={form} name="addressLine2" label="Address line 2" />
          {/*
            The address cascade. Its dropdown LABELS come from the selected country's scheme —
            Region / District / Town / Community for Ghana — so this block carries no
            country-specific code and never should.

            ⚠ City and Region are only editable when the country has no scheme loaded. With one,
            the server rewrites both from the chosen area on every save, so an editable box would
            be a field that silently discards what you type.
          */}
          <AddressCascadeField
            form={form}
            countryName="countryId"
            geoAreaName="geoAreaId"
            cityName="city"
            regionName="region"
          >
            <TextField
              form={form}
              name="digitalAddress"
              label="Digital address"
              placeholder="GA-123-4567"
            />
          </AddressCascadeField>
          <SwitchField
            form={form}
            name="isPrimary"
            label="Primary address"
            description="Used as the employee's main address."
          />
        </>
      )}
    />
  );
}
