'use client';

import { z } from 'zod';
import type { UseFormReturn } from 'react-hook-form';
import {
  TextField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { HEALTH_FACILITY_TYPE_OPTIONS } from '@/types/hr/medical';
import type { HealthcareFacility } from '@/types/hr/medical';

/**
 * One statement of the healthcare-facility form, shared by the register's create dialog and the
 * facility detail screen's edit card.
 *
 * ⚠ It lives here because the two were about to disagree. The register's dialog builds its values
 * from a **summary** row, and the summary does not carry `physicalAddress` — measured in areas
 * 19-23 slice 9, where the payload has ten keys and that is not one of them. So its edit path
 * blanked a required field on open and made the user retype an address they could not see. Editing
 * now happens on the detail screen, which loads the whole record; the register keeps only create,
 * where every field is typed fresh anyway and nothing can be lost.
 */
export const facilitySchema = z.object({
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

export type FacilityForm = z.input<typeof facilitySchema>;

export const emptyFacility: FacilityForm = {
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

/** Form values → the shape both the create and update endpoints take (update adds its own id). */
export function facilityFormToRequest(values: FacilityForm) {
  const v = facilitySchema.parse(values);
  return {
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
  };
}

/** A loaded facility → form values. Every field, because the detail read carries every field. */
export function facilityToForm(f: HealthcareFacility): FacilityForm {
  return {
    facilityName: f.facilityName,
    shortName: f.shortName ?? '',
    facilityCode: f.facilityCode,
    facilityType: f.facilityType as FacilityForm['facilityType'],
    licenseNumber: f.licenseNumber ?? '',
    physicalAddress: f.physicalAddress,
    digitalAddress: f.digitalAddress ?? '',
    city: f.city ?? '',
    primaryPhone: f.primaryPhone ?? '',
    emergencyPhone: f.emergencyPhone ?? '',
    email: f.email ?? '',
    hasEmergencyServices: f.hasEmergencyServices,
    has24HourService: f.has24HourService,
    hasAmbulanceService: f.hasAmbulanceService,
    hasLaboratory: f.hasLaboratory,
    hasPharmacy: f.hasPharmacy,
    acceptsNHIS: f.acceptsNHIS,
    nhisAccreditationNumber: f.nhisAccreditationNumber ?? '',
    contactPersonName: f.contactPersonName ?? '',
    contactPersonPhone: f.contactPersonPhone ?? '',
    operatingHours: f.operatingHours ?? '',
    isActive: f.isActive,
    notes: f.notes ?? '',
  };
}

export function FacilityFormFields({ form }: { form: UseFormReturn<FacilityForm> }) {
  return (
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
        <TextField form={form} name="nhisAccreditationNumber" label="NHIS accreditation number" />
        <TextField form={form} name="operatingHours" label="Operating hours" />
      </FieldRow>
      <FieldRow>
        <TextField form={form} name="contactPersonName" label="Contact person" />
        <TextField form={form} name="contactPersonPhone" label="Contact phone" />
      </FieldRow>
      <SwitchField form={form} name="isActive" label="Active" />
      <TextareaField form={form} name="notes" label="Notes" rows={2} />
    </>
  );
}
