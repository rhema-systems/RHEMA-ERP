'use client';

import { z } from 'zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { BadgeCheck } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import { useAuth } from '@/hooks/use-auth';
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

/** What both writes carry. Neither touches verification — the "Verify licence" action owns that. */
const toRequest = (values: PhysicianForm) => {
  const v = physicianSchema.parse(values);
  return {
    firstName: v.firstName,
    lastName: v.lastName,
    middleName: blank(v.middleName),
    title: blank(v.title),
    specialization: blank(v.specialization),
    medicalLicenseNumber: blank(v.medicalLicenseNumber),
    licenseExpiryDate: blank(v.licenseExpiryDate),
    phoneNumber: blank(v.phoneNumber),
    email: blank(v.email),
    facilityId: blank(v.facilityId),
    isActive: v.isActive,
    notes: blank(v.notes),
  };
};

export default function MedicalPhysiciansPage() {
  const { hasAnyPermission } = useAuth();
  const canWrite = hasAnyPermission(['HR.Medical.Write', 'HR.Medical.Admin']);
  const canAdmin = hasAnyPermission(['HR.Medical.Admin']);
  const queryClient = useQueryClient();

  const { data: facilities = [] } = useQuery({
    queryKey: ['hr', 'medical-facilities'],
    queryFn: () => medicalFacilityService.getFacilities(),
  });

  // The "no facility" choice is SelectField's allowEmpty item: Radix throws on an item whose value is ''.
  const facilityOptions = facilities.map((f) => ({ value: f.id, label: f.facilityName }));

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
        create={(values) => medicalFacilityService.createPhysician(toRequest(values))}
        update={(id, values) =>
          medicalFacilityService.updatePhysician(id, { id, ...toRequest(values) })
        }
        // 2026-09-03: reads are open by design; create/edit are HR.Medical.Write, delete is Admin.
        allowCreate={canWrite}
        allowUpdate={canWrite}
        remove={canAdmin ? (id) => medicalFacilityService.removePhysician(id) : undefined}
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
        // The list row is a SUMMARY: one fullName, and no facility, licence, email, middle name or
        // notes. This only seeds the dialog until `loadForEdit` below replaces it with the record.
        toForm={(p) => ({
          ...emptyPhysician,
          firstName: p.fullName.split(' ')[0] ?? '',
          lastName: p.fullName.split(' ').slice(1).join(' '),
          title: p.title ?? '',
          specialization: p.specialization ?? '',
          phoneNumber: p.phoneNumber ?? '',
          isActive: p.isActive,
        })}
        // ⚠ Without this an edit saved the summary: the update is a full replace, so it cleared the
        // facility, licence, expiry, email, middle name and notes.
        loadForEdit={async (p) => {
          const full = await medicalFacilityService.getPhysician(p.id);
          return {
            firstName: full.firstName,
            lastName: full.lastName,
            middleName: full.middleName ?? '',
            title: full.title ?? '',
            specialization: full.specialization ?? '',
            medicalLicenseNumber: full.medicalLicenseNumber ?? '',
            licenseExpiryDate: full.licenseExpiryDate?.slice(0, 10) ?? '',
            phoneNumber: full.phoneNumber ?? '',
            email: full.email ?? '',
            facilityId: full.facilityId ?? '',
            isActive: full.isActive,
            notes: full.notes ?? '',
          };
        }}
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
              allowEmpty
              emptyLabel="Not attached to a facility"
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
