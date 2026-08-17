'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { medicalFacilityService } from '@/services/hr/medical-reference.service';
import { HEALTH_FACILITY_TYPE_OPTIONS } from '@/types/hr/medical';
import type { HealthcareFacilitySummary } from '@/types/hr/medical';

/**
 * The register of hospitals, clinics and pharmacies the organisation deals with.
 *
 * Everything downstream in this module points at a facility — a medical exam names where it was
 * done, and an expense claim cannot be filed without one — so this register is the first thing
 * to populate. Reading it does not need medical permissions (employees file their own claims and
 * must be able to pick a facility); creating and editing does, and deleting needs admin.
 */
const facilitySchema = z.object({
  facilityName: z.string().min(1, 'Required').max(300),
  shortName: z.string().max(100).optional(),
  facilityCode: z.string().min(1, 'Required').max(50),
  facilityType: z.enum([
    'GeneralHospital',
    'SpecializedHospital',
    'TeachingHospital',
    'Clinic',
    'Polyclinic',
    'MedicalCenter',
    'Pharmacy',
    'DiagnosticCenter',
    'Laboratory',
    'ImagingCenter',
    'UrgentCare',
    'DaySurgeryCenter',
    'RehabilitationCenter',
    'MaternityHome',
    'DentalClinic',
    'OpticalCenter',
  ]),
  licenseNumber: z.string().max(100).optional(),
  physicalAddress: z.string().min(1, 'Required').max(500),
  digitalAddress: z.string().max(50).optional(),
  city: z.string().max(100).optional(),
  primaryPhone: z.string().max(50).optional(),
  emergencyPhone: z.string().max(50).optional(),
  email: z.string().email('Not a valid email').max(255).optional().or(z.literal('')),
  hasEmergencyServices: z.boolean(),
  has24HourService: z.boolean(),
  hasAmbulanceService: z.boolean(),
  hasLaboratory: z.boolean(),
  hasPharmacy: z.boolean(),
  acceptsNHIS: z.boolean(),
  nhisAccreditationNumber: z.string().max(100).optional(),
  contactPersonName: z.string().max(200).optional(),
  contactPersonPhone: z.string().max(50).optional(),
  operatingHours: z.string().max(100).optional(),
  isActive: z.boolean(),
  notes: z.string().max(1000).optional(),
});

type FacilityForm = z.input<typeof facilitySchema>;

const emptyFacility: FacilityForm = {
  facilityName: '',
  shortName: '',
  facilityCode: '',
  facilityType: 'GeneralHospital',
  licenseNumber: '',
  physicalAddress: '',
  digitalAddress: '',
  city: '',
  primaryPhone: '',
  emergencyPhone: '',
  email: '',
  hasEmergencyServices: false,
  has24HourService: false,
  hasAmbulanceService: false,
  hasLaboratory: false,
  hasPharmacy: false,
  acceptsNHIS: false,
  nhisAccreditationNumber: '',
  contactPersonName: '',
  contactPersonPhone: '',
  operatingHours: '',
  isActive: true,
  notes: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);

const typeLabel = (v: string) =>
  HEALTH_FACILITY_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;

export default function HealthcareFacilitiesPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Healthcare Facilities"
        description="Hospitals, clinics, laboratories and pharmacies the organisation deals with. Medical exams, appointments and expense claims all name a facility from this register."
        backHref="/hr/medical"
      />

      <ResourceListPanel<HealthcareFacilitySummary, FacilityForm>
        title="facilities"
        singular="facility"
        queryKey={['hr', 'medical-facilities']}
        dialogHint="The facility code is how staff will recognise it on a claim — keep it short and stable."
        emptyDescription="No facilities have been registered yet. Add the hospitals and clinics staff actually attend; claims cannot be filed without one."
        list={() => medicalFacilityService.getFacilities()}
        create={(values) => {
          const v = facilitySchema.parse(values);
          return medicalFacilityService.createFacility({
            ...v,
            shortName: blank(v.shortName),
            licenseNumber: blank(v.licenseNumber),
            digitalAddress: blank(v.digitalAddress),
            city: blank(v.city),
            primaryPhone: blank(v.primaryPhone),
            emergencyPhone: blank(v.emergencyPhone),
            email: blank(v.email),
            nhisAccreditationNumber: blank(v.nhisAccreditationNumber),
            contactPersonName: blank(v.contactPersonName),
            contactPersonPhone: blank(v.contactPersonPhone),
            operatingHours: blank(v.operatingHours),
            notes: blank(v.notes),
          });
        }}
        update={(id, values) => {
          const v = facilitySchema.parse(values);
          return medicalFacilityService.updateFacility(id, {
            id,
            ...v,
            shortName: blank(v.shortName),
            licenseNumber: blank(v.licenseNumber),
            digitalAddress: blank(v.digitalAddress),
            city: blank(v.city),
            primaryPhone: blank(v.primaryPhone),
            emergencyPhone: blank(v.emergencyPhone),
            email: blank(v.email),
            nhisAccreditationNumber: blank(v.nhisAccreditationNumber),
            contactPersonName: blank(v.contactPersonName),
            contactPersonPhone: blank(v.contactPersonPhone),
            operatingHours: blank(v.operatingHours),
            notes: blank(v.notes),
          });
        }}
        remove={(id) => medicalFacilityService.removeFacility(id)}
        getId={(f) => f.id}
        columns={[
          {
            header: 'Facility',
            cell: (f) => <span className="font-medium">{f.facilityName}</span>,
          },
          {
            header: 'Code',
            cell: (f) => <span className="font-mono text-sm text-muted-foreground">{f.facilityCode}</span>,
          },
          { header: 'Type', cell: (f) => typeLabel(f.facilityType) },
          { header: 'City', cell: (f) => f.city || '—' },
          { header: 'Phone', cell: (f) => f.primaryPhone || '—' },
          {
            header: 'Emergency',
            cell: (f) => (f.hasEmergencyServices ? <Badge variant="secondary">A&E</Badge> : '—'),
          },
          {
            header: 'NHIS',
            cell: (f) => (f.acceptsNHIS ? <Badge variant="secondary">Accepted</Badge> : '—'),
          },
          { header: 'Status', cell: (f) => <StatusBadge active={f.isActive} /> },
        ]}
        schema={facilitySchema as any}
        emptyForm={emptyFacility}
        toForm={(f) => ({
          ...emptyFacility,
          facilityName: f.facilityName,
          facilityCode: f.facilityCode,
          facilityType: f.facilityType,
          city: f.city ?? '',
          primaryPhone: f.primaryPhone ?? '',
          hasEmergencyServices: f.hasEmergencyServices,
          acceptsNHIS: f.acceptsNHIS,
          isActive: f.isActive,
          // physicalAddress is required by the API but absent from the summary row the table
          // holds; the dialog re-collects it rather than silently sending an empty string.
          physicalAddress: '',
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="facilityName" label="Facility name" required />
              <TextField form={form} name="facilityCode" label="Facility code" required />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={form}
                name="facilityType"
                label="Type"
                required
                options={HEALTH_FACILITY_TYPE_OPTIONS}
              />
              <TextField form={form} name="shortName" label="Short name" />
            </FieldRow>
            <TextField
              form={form}
              name="physicalAddress"
              label="Physical address"
              required
              placeholder="e.g. Liberation Road, Airport Residential Area"
            />
            <FieldRow>
              <TextField form={form} name="city" label="City" />
              <TextField form={form} name="digitalAddress" label="Digital address (GhanaPost GPS)" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="primaryPhone" label="Primary phone" />
              <TextField form={form} name="emergencyPhone" label="Emergency phone" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="email" label="Email" />
              <TextField form={form} name="licenseNumber" label="Licence number" />
            </FieldRow>

            <div className="grid gap-2 sm:grid-cols-2">
              <SwitchField form={form} name="hasEmergencyServices" label="Accident & emergency" />
              <SwitchField form={form} name="has24HourService" label="Open 24 hours" />
              <SwitchField form={form} name="hasAmbulanceService" label="Ambulance service" />
              <SwitchField form={form} name="hasLaboratory" label="Laboratory on site" />
              <SwitchField form={form} name="hasPharmacy" label="Pharmacy on site" />
              <SwitchField form={form} name="acceptsNHIS" label="Accepts NHIS" />
            </div>

            <FieldRow>
              <TextField
                form={form}
                name="nhisAccreditationNumber"
                label="NHIS accreditation number"
              />
              <TextField form={form} name="operatingHours" label="Operating hours" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="contactPersonName" label="Contact person" />
              <TextField form={form} name="contactPersonPhone" label="Contact phone" />
            </FieldRow>
            <SwitchField form={form} name="isActive" label="Active" />
            <TextareaField form={form} name="notes" label="Notes" rows={2} />
          </>
        )}
      />
    </div>
  );
}
