'use client';

import { use, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, Save, Stethoscope } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  FacilityFormFields,
  facilitySchema,
  facilityFormToRequest,
  facilityToForm,
  emptyFacility,
  type FacilityForm,
} from '@/components/hr/medical/facility-form';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { medicalFacilityService } from '@/services/hr/medical-reference.service';
import { MEDICAL_SERVICE_TYPE_OPTIONS, HEALTH_FACILITY_TYPE_OPTIONS } from '@/types/hr/medical';
import type { FacilityService } from '@/types/hr/medical';

/**
 * One healthcare facility, and the services it offers.
 *
 * The services register had five endpoints and no screen at all — this is the screen. It sits here
 * rather than anywhere else because a service is meaningless without the facility that offers it:
 * "X-ray, GHS 200" is a price list entry, "X-ray at Tema General, GHS 200" is a fact a claim can be
 * checked against.
 *
 * ⚠ Deleting a facility is refused while it still lists services, and this screen is where that
 * becomes visible. The delete is a soft delete, so nothing cascades — before slice 9 the services
 * simply stopped appearing while continuing to exist, which is the worst of both.
 */
const serviceSchema = z.object({
  name: z.string().min(1, 'Required').max(200),
  serviceType: z.string().min(1, 'Required'),
  estimatedCost: z.coerce.number().min(0, 'Cannot be negative').optional(),
  requiresAppointment: z.boolean(),
  isEmergencyService: z.boolean(),
  requiresPreAuthorization: z.boolean(),
  isActive: z.boolean(),
  notes: z.string().max(500).optional(),
});

type ServiceForm = z.input<typeof serviceSchema>;

const emptyService: ServiceForm = {
  name: '',
  serviceType: 'Consultation',
  estimatedCost: undefined,
  requiresAppointment: false,
  isEmergencyService: false,
  requiresPreAuthorization: false,
  isActive: true,
  notes: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);
const money = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2 });
const serviceLabel = (v: string) =>
  MEDICAL_SERVICE_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;
const facilityTypeLabel = (v: string) =>
  HEALTH_FACILITY_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;

export default function HealthcareFacilityDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);

  const { data: facility, isLoading, isError } = useQuery({
    queryKey: ['hr', 'medical-facilities', id],
    queryFn: () => medicalFacilityService.getFacility(id),
    enabled: !!id,
  });

  const form = useForm<FacilityForm>({
    resolver: zodResolver(facilitySchema) as any,
    defaultValues: emptyFacility,
  });

  // ⚠ Reset from the loaded record rather than seeding the form from a list row. The register's
  // summary does not carry `physicalAddress`, so a form built from it starts with a required field
  // blank — which is why editing moved here.
  useEffect(() => {
    if (facility) form.reset(facilityToForm(facility));
  }, [facility, form]);

  const handleSave = async (values: FacilityForm) => {
    setSaving(true);
    try {
      await medicalFacilityService.updateFacility(id, { id, ...facilityFormToRequest(values) });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['hr', 'medical-facilities'] }),
        queryClient.invalidateQueries({ queryKey: ['hr', 'medical-facilities', id] }),
      ]);
      toast({ title: 'Saved', description: 'The facility was updated.' });
    } catch (error) {
      toast({
        title: 'Could not save',
        description: (error as Error)?.message || 'The changes were not saved.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  if (isLoading) {
    return (
      <div className="space-y-6 p-6">
        <Skeleton className="h-10 w-72" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (isError || !facility) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="Healthcare Facility" backHref="/hr/medical/facilities" />
        <EmptyState
          icon={Stethoscope}
          title="Facility not found"
          description="This facility no longer exists, or it belongs to another tenant."
        />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={facility.facilityName}
        description={`${facilityTypeLabel(facility.facilityType)} · ${facility.facilityCode}`}
        backHref="/hr/medical/facilities"
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge active={facility.isActive} />
            {facility.acceptsNHIS && <Badge variant="secondary">NHIS</Badge>}
            {facility.hasEmergencyServices && <Badge variant="secondary">A&amp;E</Badge>}
          </div>
        }
      />

      <ResourceCollectionTab<FacilityService, ServiceForm>
        parentId={id}
        title="services"
        singular="service"
        queryKey={['hr', 'medical-facilities', id, 'services']}
        invalidateKeys={[['hr', 'medical-facilities']]}
        dialogHint="The estimated cost is what this facility usually charges — a reference for the person checking a claim, not a cap."
        emptyDescription="No services recorded for this facility. Adding them gives whoever checks a claim something to compare it against."
        list={(facilityId) => medicalFacilityService.getFacilityServices(facilityId)}
        create={(facilityId, values) => {
          const v = serviceSchema.parse(values);
          return medicalFacilityService.createFacilityService({
            facilityId,
            ...v,
            serviceType: v.serviceType as FacilityService['serviceType'],
            estimatedCost: v.estimatedCost ?? null,
            notes: blank(v.notes),
          });
        }}
        // ⚠ No facilityId on the update: a service cannot be moved between facilities, and the
        // update DTO does not model one. Sending a key the endpoint does not know is a hole, not
        // an error — setup.mjs learned that the expensive way with `hireDate`.
        update={(_facilityId, serviceId, values) => {
          const v = serviceSchema.parse(values);
          return medicalFacilityService.updateFacilityService(serviceId, {
            id: serviceId,
            ...v,
            serviceType: v.serviceType as FacilityService['serviceType'],
            estimatedCost: v.estimatedCost ?? null,
            notes: blank(v.notes),
          });
        }}
        remove={(_facilityId, serviceId) => medicalFacilityService.removeFacilityService(serviceId)}
        getId={(s) => s.id}
        columns={[
          { header: 'Service', cell: (s) => <span className="font-medium">{s.name}</span> },
          { header: 'Type', cell: (s) => serviceLabel(s.serviceType) },
          { header: 'Estimated cost', cell: (s) => money(s.estimatedCost), className: 'text-right' },
          {
            header: 'Booking',
            cell: (s) => (s.requiresAppointment ? 'By appointment' : 'Walk-in'),
          },
          {
            header: 'Flags',
            cell: (s) => (
              <div className="flex flex-wrap gap-1">
                {s.isEmergencyService && <Badge variant="secondary">Emergency</Badge>}
                {s.requiresPreAuthorization && <Badge variant="secondary">Pre-auth</Badge>}
                {!s.isEmergencyService && !s.requiresPreAuthorization && '—'}
              </div>
            ),
          },
          { header: 'Status', cell: (s) => <StatusBadge active={s.isActive} /> },
        ]}
        schema={serviceSchema as any}
        emptyForm={emptyService}
        toForm={(s) => ({
          name: s.name,
          serviceType: s.serviceType,
          estimatedCost: s.estimatedCost ?? undefined,
          requiresAppointment: s.requiresAppointment,
          isEmergencyService: s.isEmergencyService,
          requiresPreAuthorization: s.requiresPreAuthorization,
          isActive: s.isActive,
          notes: s.notes ?? '',
        })}
        renderFields={(serviceForm) => (
          <>
            <TextField form={serviceForm} name="name" label="Service" required />
            <FieldRow>
              <SelectField
                form={serviceForm}
                name="serviceType"
                label="Type"
                required
                options={MEDICAL_SERVICE_TYPE_OPTIONS}
              />
              <NumberField form={serviceForm} name="estimatedCost" label="Estimated cost" />
            </FieldRow>
            <div className="grid gap-2 sm:grid-cols-2">
              <SwitchField form={serviceForm} name="requiresAppointment" label="Needs an appointment" />
              <SwitchField form={serviceForm} name="isEmergencyService" label="Emergency service" />
              <SwitchField
                form={serviceForm}
                name="requiresPreAuthorization"
                label="Needs pre-authorisation"
              />
              <SwitchField form={serviceForm} name="isActive" label="Active" />
            </div>
            <TextareaField form={serviceForm} name="notes" label="Notes" rows={2} />
          </>
        )}
      />

      <Card>
        <form onSubmit={form.handleSubmit(handleSave)}>
          <CardHeader>
            <CardTitle>Facility Details</CardTitle>
            <CardDescription>
              Everything the register holds about this facility. Editing happens here rather than in
              the register&apos;s dialog, because the list row does not carry the physical address and
              a form built from it would start with a required field blank.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FacilityFormFields form={form} />
          </CardContent>
          <CardFooter className="flex justify-end space-x-2">
            <Button
              variant="outline"
              type="button"
              onClick={() => router.push('/hr/medical/facilities')}
              disabled={saving}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={saving}>
              {saving ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Save className="mr-2 h-4 w-4" />
              )}
              Save Changes
            </Button>
          </CardFooter>
        </form>
      </Card>
    </div>
  );
}
