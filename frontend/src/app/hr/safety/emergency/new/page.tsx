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
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TextField,
  TextareaField,
  SelectField,
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyEmergencyService } from '@/services/hr/safety-emergency.service';
import { locationService } from '@/services/hr/location.service';
import { SHE_EMERGENCY_TYPE_OPTIONS } from '@/types/hr/safety-equipment';
import type { SheEmergencyType } from '@/types/hr/safety-equipment';

/**
 * Creates an emergency plan. The plan number is a user-assigned code (e.g. EP-003) and must be
 * unique — a duplicate is refused. Assembly points, contacts, drills and the response team are
 * added on the plan's detail page after creation.
 */
const planSchema = z.object({
  planNumber: z.string().min(1, 'A plan number is required').max(100),
  planName: z.string().min(1, 'A name is required').max(200),
  type: z.string().min(1),
  description: z.string().max(2000).optional().or(z.literal('')),
  procedures: z.string().max(4000).optional().or(z.literal('')),
  locationId: z.string().optional().or(z.literal('')),
  lastReviewed: z.string().min(1, 'A last-reviewed date is required'),
  nextReviewDate: z.string().min(1, 'A next-review date is required'),
  planOwnerId: z.string().min(1, 'A plan owner is required'),
  isActive: z.boolean(),
});

type PlanForm = z.input<typeof planSchema>;

const blank = (v?: string) => (v && v.length > 0 ? v : null);

export default function NewEmergencyPlanPage() {
  const router = useRouter();
  const { toast } = useToast();
  const [busy, setBusy] = useState(false);

  const form = useForm<PlanForm>({
    resolver: zodResolver(planSchema),
    defaultValues: {
      planNumber: '',
      planName: '',
      type: 'Fire',
      description: '',
      procedures: '',
      locationId: '',
      lastReviewed: new Date().toISOString().slice(0, 10),
      nextReviewDate: '',
      planOwnerId: '',
      isActive: true,
    },
  });

  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const submit = form.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = planSchema.parse(values);
      const created = await safetyEmergencyService.createPlan({
        planNumber: v.planNumber,
        planName: v.planName,
        type: v.type as SheEmergencyType,
        description: v.description || '',
        procedures: v.procedures || '',
        locationId: blank(v.locationId),
        lastReviewed: new Date(v.lastReviewed).toISOString(),
        nextReviewDate: new Date(v.nextReviewDate).toISOString(),
        planOwnerId: v.planOwnerId,
        isActive: v.isActive,
      });
      toast({ title: `Plan ${created.planNumber} created` });
      router.push(`/hr/safety/emergency/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Creating the plan failed.',
        variant: 'destructive',
      });
      setBusy(false);
    }
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Emergency Plan"
        description="Assembly points, the contact tree, drills and the response team are added on the plan page after creation."
        backHref="/hr/safety/emergency"
      />

      <form onSubmit={submit} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Plan</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <TextField form={form} name="planNumber" label="Plan number (e.g. EP-003)" required />
              <TextField form={form} name="planName" label="Plan name" required />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={form}
                name="type"
                label="Emergency type"
                required
                options={SHE_EMERGENCY_TYPE_OPTIONS}
              />
              <SelectField
                form={form}
                name="locationId"
                label="Location"
                allowEmpty
                emptyLabel="All sites"
                options={locations.map((l) => ({ value: l.id, label: l.name }))}
              />
            </FieldRow>
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <TextareaField
              form={form}
              name="procedures"
              label="Procedures"
              rows={5}
            />
            <FieldRow>
              <EmployeePickerField form={form} name="planOwnerId" label="Plan owner" required />
              <div />
            </FieldRow>
            <FieldRow>
              <DateField form={form} name="lastReviewed" label="Last reviewed" required />
              <DateField form={form} name="nextReviewDate" label="Next review due" required />
            </FieldRow>
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="Inactive plans stay on record but drop off the active register and review chase."
            />
          </CardContent>
        </Card>

        <div className="flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            disabled={busy}
            onClick={() => router.push('/hr/safety/emergency')}
          >
            Cancel
          </Button>
          <Button type="submit" disabled={busy}>
            {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Create plan
          </Button>
        </div>
      </form>
    </div>
  );
}
