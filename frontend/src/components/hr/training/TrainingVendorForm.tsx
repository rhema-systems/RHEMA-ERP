'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  TextField,
  NumberField,
  DateField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import {
  TRAINING_VENDOR_TYPE_OPTIONS,
  VENDOR_ACCREDITATION_STATUS_OPTIONS,
} from '@/types/hr/training';

export const trainingVendorSchema = z.object({
  vendorCode: z.string().min(1, 'Vendor code is required').max(50),
  name: z.string().min(1, 'Name is required').max(200),
  vendorType: z.enum([
    'IndividualConsultant',
    'TrainingFirm',
    'AccreditedInstitution',
    'University',
    'GovernmentAgency',
    'NGO',
    'Other',
  ]),
  isActive: z.boolean(),
  isPreferred: z.boolean(),
  preferredSince: z.string().optional().or(z.literal('')),
  accreditationBody: z.string().max(200).optional().or(z.literal('')),
  accreditationNumber: z.string().max(100).optional().or(z.literal('')),
  accreditationStatus: z
    .enum(['Active', 'Pending', 'Expired', 'Suspended', 'Revoked', ''])
    .optional(),
  accreditationExpiryDate: z.string().optional().or(z.literal('')),
  primaryContactName: z.string().max(200).optional().or(z.literal('')),
  primaryContactEmail: z.string().email('Enter a valid email').max(256).optional().or(z.literal('')),
  primaryContactPhone: z.string().max(20).optional().or(z.literal('')),
  website: z.string().max(500).optional().or(z.literal('')),
  address: z.string().max(500).optional().or(z.literal('')),
  currency: z.string().min(1).max(3),
  defaultDailyRate: z.coerce.number().min(0).optional(),
  contractReference: z.string().max(100).optional().or(z.literal('')),
  contractStartDate: z.string().optional().or(z.literal('')),
  contractEndDate: z.string().optional().or(z.literal('')),
  notes: z.string().max(2000).optional().or(z.literal('')),
});

export type TrainingVendorFormValues = z.infer<typeof trainingVendorSchema>;

export const emptyTrainingVendor: TrainingVendorFormValues = {
  vendorCode: '',
  name: '',
  vendorType: 'TrainingFirm',
  isActive: true,
  isPreferred: false,
  preferredSince: '',
  accreditationBody: '',
  accreditationNumber: '',
  accreditationStatus: '',
  accreditationExpiryDate: '',
  primaryContactName: '',
  primaryContactEmail: '',
  primaryContactPhone: '',
  website: '',
  address: '',
  currency: 'GHS',
  defaultDailyRate: undefined,
  contractReference: '',
  contractStartDate: '',
  contractEndDate: '',
  notes: '',
};

interface TrainingVendorFormProps {
  defaultValues: TrainingVendorFormValues;
  onSubmit: (values: TrainingVendorFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
  /** The vendor code is immutable after creation. */
  vendorCodeEditable?: boolean;
}

export function TrainingVendorForm({
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
  vendorCodeEditable = true,
}: TrainingVendorFormProps) {
  const form = useForm<TrainingVendorFormValues>({
    resolver: zodResolver(trainingVendorSchema) as any,
    defaultValues,
  });

  const isPreferred = !!form.watch('isPreferred');

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Vendor</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            {vendorCodeEditable ? (
              <TextField form={form} name="vendorCode" label="Vendor code" required placeholder="e.g. VND-001" />
            ) : (
              <div className="space-y-2">
                <Label htmlFor="vendorCode">Vendor code</Label>
                <Input id="vendorCode" value={form.watch('vendorCode')} disabled />
                <p className="text-xs text-muted-foreground">Cannot be changed after creation.</p>
              </div>
            )}
            <TextField form={form} name="name" label="Name" required />
          </FieldRow>
          <FieldRow>
            <SelectField form={form} name="vendorType" label="Vendor type" required options={TRAINING_VENDOR_TYPE_OPTIONS} />
            <TextField form={form} name="currency" label="Currency" required placeholder="GHS" />
          </FieldRow>
          <FieldRow>
            <SwitchField form={form} name="isActive" label="Active" />
            <SwitchField form={form} name="isPreferred" label="Preferred vendor" />
          </FieldRow>
          {isPreferred && <DateField form={form} name="preferredSince" label="Preferred since" />}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Accreditation</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <TextField form={form} name="accreditationBody" label="Accreditation body" placeholder="e.g. ACCA, HRCI" />
            <TextField form={form} name="accreditationNumber" label="Accreditation number" />
          </FieldRow>
          <FieldRow>
            <SelectField
              form={form}
              name="accreditationStatus"
              label="Accreditation status"
              options={VENDOR_ACCREDITATION_STATUS_OPTIONS}
              allowEmpty
              emptyLabel="Not accredited"
            />
            <DateField form={form} name="accreditationExpiryDate" label="Accreditation expiry" />
          </FieldRow>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Contact</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <TextField form={form} name="primaryContactName" label="Contact name" />
            <TextField form={form} name="primaryContactEmail" label="Contact email" type="email" />
          </FieldRow>
          <FieldRow>
            <TextField form={form} name="primaryContactPhone" label="Contact phone" />
            <TextField form={form} name="website" label="Website" />
          </FieldRow>
          <TextareaField form={form} name="address" label="Address" rows={2} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Contract &amp; rate</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <NumberField form={form} name="defaultDailyRate" label="Default daily rate" step="0.01" />
            <TextField form={form} name="contractReference" label="Contract reference" />
          </FieldRow>
          <FieldRow>
            <DateField form={form} name="contractStartDate" label="Contract start" />
            <DateField form={form} name="contractEndDate" label="Contract end" />
          </FieldRow>
          <TextareaField form={form} name="notes" label="Notes" rows={3} />
        </CardContent>
      </Card>

      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onCancel} disabled={submitting}>
          Cancel
        </Button>
        <Button type="submit" disabled={submitting}>
          {submitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
          {!submitting && <Save className="mr-2 h-4 w-4" />}
          {submitLabel}
        </Button>
      </div>
    </form>
  );
}
