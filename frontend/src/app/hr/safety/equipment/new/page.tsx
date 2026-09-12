'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { z } from 'zod';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyEquipmentService } from '@/services/hr/safety-equipment.service';
import { locationService } from '@/services/hr/location.service';
import { SHE_SAFETY_EQUIPMENT_TYPE_OPTIONS } from '@/types/hr/safety-equipment';
import type { SheSafetyEquipmentType } from '@/types/hr/safety-equipment';

/**
 * Registers a piece of safety equipment. The equipment number is assigned server-side
 * (SEQ-YYYY-NNNN) — there is no number field here on purpose. New equipment starts Operational;
 * status changes happen on the detail page.
 */
const equipmentSchema = z.object({
  name: z.string().min(1, 'A name is required').max(200),
  description: z.string().max(500).optional().or(z.literal('')),
  type: z.string().min(1, 'A type is required'),
  locationId: z.string().min(1, 'A location is required'),
  specificArea: z.string().max(200).optional().or(z.literal('')),
  manufacturer: z.string().max(100).optional().or(z.literal('')),
  model: z.string().max(100).optional().or(z.literal('')),
  serialNumber: z.string().max(100).optional().or(z.literal('')),
  purchaseDate: z.string().optional().or(z.literal('')),
  installationDate: z.string().optional().or(z.literal('')),
  requiresRegularInspection: z.boolean(),
  inspectionFrequencyDays: z.coerce.number().min(0).max(3650),
  requiresCertification: z.boolean(),
  certificationExpiryDate: z.string().optional().or(z.literal('')),
  expiryDate: z.string().optional().or(z.literal('')),
  notes: z.string().max(500).optional().or(z.literal('')),
});

type EquipmentForm = z.input<typeof equipmentSchema>;

const blank = (v?: string) => (v && v.length > 0 ? v : null);
const dateOrNull = (v?: string) => (v && v.length > 0 ? new Date(v).toISOString() : null);

export default function NewSafetyEquipmentPage() {
  const router = useRouter();
  const { toast } = useToast();
  const [busy, setBusy] = useState(false);
  const [responsiblePersonId, setResponsiblePersonId] = useState<string | null>(null);

  const form = useForm<EquipmentForm>({
    resolver: zodResolver(equipmentSchema),
    defaultValues: {
      name: '',
      description: '',
      type: 'FireExtinguisher',
      locationId: '',
      specificArea: '',
      manufacturer: '',
      model: '',
      serialNumber: '',
      purchaseDate: '',
      installationDate: '',
      requiresRegularInspection: true,
      inspectionFrequencyDays: 90,
      requiresCertification: false,
      certificationExpiryDate: '',
      expiryDate: '',
      notes: '',
    },
  });

  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const requiresCertification = !!form.watch('requiresCertification');
  const requiresInspection = !!form.watch('requiresRegularInspection');

  const submit = form.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = equipmentSchema.parse(values);
      const created = await safetyEquipmentService.create({
        name: v.name,
        description: blank(v.description),
        type: v.type as SheSafetyEquipmentType,
        locationId: v.locationId,
        specificArea: blank(v.specificArea),
        manufacturer: blank(v.manufacturer),
        model: blank(v.model),
        serialNumber: blank(v.serialNumber),
        purchaseDate: dateOrNull(v.purchaseDate),
        installationDate: dateOrNull(v.installationDate),
        requiresRegularInspection: v.requiresRegularInspection,
        inspectionFrequencyDays: v.inspectionFrequencyDays,
        requiresCertification: v.requiresCertification,
        certificationExpiryDate: v.requiresCertification
          ? dateOrNull(v.certificationExpiryDate)
          : null,
        expiryDate: dateOrNull(v.expiryDate),
        responsiblePersonId,
        notes: blank(v.notes),
      });
      toast({ title: `Equipment registered as ${created.equipmentNumber}` });
      router.push(`/hr/safety/equipment/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Registering the equipment failed.',
        variant: 'destructive',
      });
      setBusy(false);
    }
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Register Safety Equipment"
        description="The number is assigned automatically on save (SEQ-YYYY-NNNN)."
        backHref="/hr/safety/equipment"
      />

      <form onSubmit={submit} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Identity</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <TextField form={form} name="name" label="Name" required />
              <SelectField
                form={form}
                name="type"
                label="Type"
                required
                options={SHE_SAFETY_EQUIPMENT_TYPE_OPTIONS}
              />
            </FieldRow>
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <FieldRow>
              <SelectField
                form={form}
                name="locationId"
                label="Location"
                required
                options={locations.map((l) => ({ value: l.id, label: l.name }))}
              />
              <TextField form={form} name="specificArea" label="Specific area" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="manufacturer" label="Manufacturer" />
              <TextField form={form} name="model" label="Model" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="serialNumber" label="Serial number" />
              <div className="space-y-2">
                <Label>Responsible person</Label>
                <EmployeePicker
                  value={responsiblePersonId}
                  onChange={(id) => setResponsiblePersonId(id)}
                />
              </div>
            </FieldRow>
            <FieldRow>
              <DateField form={form} name="purchaseDate" label="Purchase date" />
              <DateField form={form} name="installationDate" label="Installation date" />
            </FieldRow>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Inspection, certification & expiry</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <SwitchField
              form={form}
              name="requiresRegularInspection"
              label="Requires regular inspection"
              description="Due dates are computed from the last inspection; the due-inspection view on the register is the reminder — nothing fires automatically yet."
            />
            {requiresInspection && (
              <NumberField
                form={form}
                name="inspectionFrequencyDays"
                label="Inspection frequency (days)"
              />
            )}
            <SwitchField
              form={form}
              name="requiresCertification"
              label="Requires certification"
            />
            {requiresCertification && (
              <DateField
                form={form}
                name="certificationExpiryDate"
                label="Certification expiry"
              />
            )}
            <DateField form={form} name="expiryDate" label="Equipment expiry date" />
            <TextareaField form={form} name="notes" label="Notes" rows={2} />
          </CardContent>
        </Card>

        <div className="flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            disabled={busy}
            onClick={() => router.push('/hr/safety/equipment')}
          >
            Cancel
          </Button>
          <Button type="submit" disabled={busy}>
            {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Register
          </Button>
        </div>
      </form>
    </div>
  );
}
