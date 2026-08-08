'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { employeeService } from '@/services/hr/employee.service';
import { REFEREE_TYPE_OPTIONS, type EmployeeReferee } from '@/types/hr/employee-subresources';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import { FieldRow, SelectField, SwitchField, TextField } from './fields';

const schema = z.object({
  refereeType: z.enum(['Professional', 'Academic', 'Personal']),
  fullName: z.string().min(1, 'Name is required').max(200),
  organization: z.string().max(200).optional().or(z.literal('')),
  positionOrTitle: z.string().max(200).optional().or(z.literal('')),
  relationship: z.string().min(1, 'Relationship is required').max(100),
  phoneNumber: z.string().min(1, 'Phone number is required').max(30),
  emailAddress: z.string().email('Enter a valid email').optional().or(z.literal('')),
  isPrimary: z.boolean(),
  isActive: z.boolean(),
});

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  refereeType: 'Professional',
  fullName: '',
  organization: '',
  positionOrTitle: '',
  relationship: '',
  phoneNumber: '',
  emailAddress: '',
  isPrimary: false,
  isActive: true,
};

const toPayload = (employeeId: string, v: FormValues) => ({
  employeeId,
  refereeType: v.refereeType,
  fullName: v.fullName,
  organization: v.organization || null,
  positionOrTitle: v.positionOrTitle || null,
  relationship: v.relationship,
  phoneNumber: v.phoneNumber,
  emailAddress: v.emailAddress || null,
  isPrimary: v.isPrimary,
  isActive: v.isActive,
});

export function RefereesTab({ employeeId }: { employeeId: string }) {
  return (
    <EmployeeSubResourceTab<EmployeeReferee, FormValues>
      employeeId={employeeId}
      title="referees"
      singular="referee"
      queryKey="referees"
      getId={(r) => r.id}
      list={employeeService.getReferees.bind(employeeService)}
      create={(id, v) => employeeService.addReferee(id, toPayload(id, v))}
      update={(id, refereeId, v) =>
        employeeService.updateReferee(id, refereeId, { id: refereeId, ...toPayload(id, v) })
      }
      remove={employeeService.removeReferee.bind(employeeService)}
      actions={[
        {
          label: 'Set as primary',
          visible: (r) => !r.isPrimary,
          run: (r) => employeeService.setPrimaryReferee(employeeId, r.id),
        },
        {
          label: (r) => (r.isActive ? 'Deactivate' : 'Activate'),
          run: (r) =>
            r.isActive
              ? employeeService.deactivateReferee(employeeId, r.id)
              : employeeService.activateReferee(employeeId, r.id),
        },
      ]}
      columns={[
        { header: 'Name', cell: (r) => r.fullName },
        { header: 'Type', cell: (r) => r.refereeType },
        { header: 'Organization', cell: (r) => r.organization || '—' },
        { header: 'Relationship', cell: (r) => r.relationship },
        { header: 'Phone', cell: (r) => r.phoneNumber },
        {
          header: 'Status',
          cell: (r) => (
            <div className="flex gap-1">
              {r.isPrimary && <Badge variant="secondary">Primary</Badge>}
              {r.isContacted && <Badge variant="outline">Contacted</Badge>}
              {!r.isActive && <Badge variant="outline">Inactive</Badge>}
            </div>
          ),
        },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={(r) => ({
        refereeType: r.refereeType,
        fullName: r.fullName,
        organization: r.organization ?? '',
        positionOrTitle: r.positionOrTitle ?? '',
        relationship: r.relationship,
        phoneNumber: r.phoneNumber,
        emailAddress: r.emailAddress ?? '',
        isPrimary: r.isPrimary,
        isActive: r.isActive,
      })}
      renderFields={(form) => (
        <>
          <FieldRow>
            <TextField form={form} name="fullName" label="Full name" required />
            <SelectField
              form={form}
              name="refereeType"
              label="Referee type"
              required
              options={REFEREE_TYPE_OPTIONS}
            />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="organization" label="Organization" />
            <TextField form={form} name="positionOrTitle" label="Position / title" />
          </FieldRow>
          <TextField
            form={form}
            name="relationship"
            label="Relationship"
            placeholder="Former manager"
            required
          />
          <FieldRow>
            <TextField form={form} name="phoneNumber" label="Phone" type="tel" required />
            <TextField form={form} name="emailAddress" label="Email" type="email" />
          </FieldRow>
          <FieldRow>
            <SwitchField form={form} name="isPrimary" label="Primary referee" />
            <SwitchField form={form} name="isActive" label="Active" />
          </FieldRow>
        </>
      )}
    />
  );
}
