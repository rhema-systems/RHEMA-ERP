'use client';

import { z } from 'zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { BadgeCheck } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  TextareaField,
  SelectField,
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { medicalFacilityService } from '@/services/hr/medical-reference.service';
import type { PhysicianSummary } from '@/types/hr/medical';

/**
 * Doctors practising at the registered facilities.
 *
 * Verification is a one-way record that someone checked the licence — there is no un-verify, so
 * the action only appears on unverified rows. Like the facility register, reading is open to any
 * authenticated user because a claimant may name the doctor they saw.
 */
const physicianSchema = z.object({
  firstName: z.string().min(1, 'Required').max(100),
  lastName: z.string().min(1, 'Required').max(100),
  middleName: z.string().max(100).optional(),
  title: z.string().max(20).optional(),
  specialization: z.string().max(200).optional(),
  medicalLicenseNumber: z.string().max(100).optional(),
  licenseExpiryDate: z.string().optional(),
  phoneNumber: z.string().max(50).optional(),
  email: z.string().email('Not a valid email').max(255).optional().or(z.literal('')),
  facilityId: z.string().optional(),
  isActive: z.boolean(),
  notes: z.string().max(1000).optional(),
});

type PhysicianForm = z.input<typeof physicianSchema>;

const emptyPhysician: PhysicianForm = {
  firstName: '',
  lastName: '',
  middleName: '',
  title: '',
  specialization: '',
  medicalLicenseNumber: '',
  licenseExpiryDate: '',
  phoneNumber: '',
  email: '',
  facilityId: '',
  isActive: true,
  notes: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);

export default function MedicalPhysiciansPage() {
  const queryClient = useQueryClient();

  const { data: facilities = [] } = useQuery({
    queryKey: ['hr', 'medical-facilities'],
    queryFn: () => medicalFacilityService.getFacilities(),
  });

  const facilityOptions = [
    { value: '', label: 'Not attached to a facility' },
    ...facilities.map((f) => ({ value: f.id, label: f.facilityName })),
  ];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Physicians"
        description="Doctors at the registered facilities, with licence details. Referrals, appointments and expense claims can name a physician from this register."
        backHref="/hr/medical"
      />

      <ResourceListPanel<PhysicianSummary, PhysicianForm>
        title="physicians"
        singular="physician"
        queryKey={['hr', 'medical-physicians']}
        invalidateKeys={[['hr', 'medical-facilities']]}
        dialogHint="Attach the doctor to a facility where you can — it is how staff will find them when filing a claim."
        emptyDescription="No physicians have been registered yet. Add the doctors staff are commonly referred to."
        list={() => medicalFacilityService.getPhysicians()}
        create={(values) => {
          const v = physicianSchema.parse(values);
          return medicalFacilityService.createPhysician({
            ...v,
            middleName: blank(v.middleName),
            title: blank(v.title),
            specialization: blank(v.specialization),
            medicalLicenseNumber: blank(v.medicalLicenseNumber),
            licenseExpiryDate: blank(v.licenseExpiryDate),
            phoneNumber: blank(v.phoneNumber),
            email: blank(v.email),
            facilityId: blank(v.facilityId),
            notes: blank(v.notes),
          });
        }}
        update={(id, values) => {
          const v = physicianSchema.parse(values);
          return medicalFacilityService.updatePhysician(id, {
            id,
            ...v,
            middleName: blank(v.middleName),
            title: blank(v.title),
            specialization: blank(v.specialization),
            medicalLicenseNumber: blank(v.medicalLicenseNumber),
            licenseExpiryDate: blank(v.licenseExpiryDate),
            phoneNumber: blank(v.phoneNumber),
            email: blank(v.email),
            facilityId: blank(v.facilityId),
            notes: blank(v.notes),
          });
        }}
        remove={(id) => medicalFacilityService.removePhysician(id)}
        getId={(p) => p.id}
        actions={[
          {
            label: 'Verify licence',
            // One-way: the API has no un-verify, so offering it on a verified row would be a lie.
            visible: (p) => !p.isVerified,
            run: async (p) => {
              await medicalFacilityService.verifyPhysician(p.id);
              await queryClient.invalidateQueries({ queryKey: ['hr', 'medical-physicians'] });
            },
            confirm: {
              title: 'Record this licence as verified?',
              description:
                'Confirms someone has checked the practising licence. This cannot be undone.',
            },
          },
        ]}
        columns={[
          {
            header: 'Name',
            cell: (p) => (
              <span className="font-medium">
                {p.title ? `${p.title} ` : ''}
                {p.fullName}
              </span>
            ),
          },
          { header: 'Specialization', cell: (p) => p.specialization || '—' },
          { header: 'Facility', cell: (p) => p.facilityName || '—' },
          { header: 'Phone', cell: (p) => p.phoneNumber || '—' },
          {
            header: 'Licence',
            cell: (p) =>
              p.isVerified ? (
                <Badge variant="secondary" className="gap-1">
                  <BadgeCheck className="h-3 w-3" /> Verified
                </Badge>
              ) : (
                <span className="text-muted-foreground">Unverified</span>
              ),
          },
          { header: 'Status', cell: (p) => <StatusBadge active={p.isActive} /> },
        ]}
        schema={physicianSchema as any}
        emptyForm={emptyPhysician}
        toForm={(p) => ({
          ...emptyPhysician,
          // The summary row carries a single fullName; first/last are re-collected in the dialog.
          firstName: p.fullName.split(' ')[0] ?? '',
          lastName: p.fullName.split(' ').slice(1).join(' '),
          title: p.title ?? '',
          specialization: p.specialization ?? '',
          phoneNumber: p.phoneNumber ?? '',
          isActive: p.isActive,
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="title" label="Title" placeholder="Dr, Prof" />
              <TextField form={form} name="specialization" label="Specialization" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="firstName" label="First name" required />
              <TextField form={form} name="lastName" label="Last name" required />
            </FieldRow>
            <TextField form={form} name="middleName" label="Middle name" />
            <SelectField
              form={form}
              name="facilityId"
              label="Facility"
              options={facilityOptions}
            />
            <FieldRow>
              <TextField form={form} name="medicalLicenseNumber" label="Licence number" />
              <DateField form={form} name="licenseExpiryDate" label="Licence expires" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="phoneNumber" label="Phone" />
              <TextField form={form} name="email" label="Email" />
            </FieldRow>
            <SwitchField form={form} name="isActive" label="Active" />
            <TextareaField form={form} name="notes" label="Notes" rows={2} />
          </>
        )}
      />
    </div>
  );
}
