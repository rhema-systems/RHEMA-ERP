'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  TextField,
  SelectField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { safetyInspectionService } from '@/services/hr/safety-inspection.service';
import { safetyChecklistService } from '@/services/hr/safety-checklist.service';
import { locationService } from '@/services/hr/location.service';
import { OrganizationUnitPickerField } from '@/components/hr/common/OrganizationUnitPickerField';
import {
  SHE_INSPECTION_TYPE_OPTIONS,
  SHE_INSPECTION_CATEGORY_OPTIONS,
} from '@/types/hr/safety-inspections';

/**
 * Schedule a new inspection. It lands in Scheduled with a server-generated INSP-YYYY-NNNN
 * number; findings, hazards and close-out run on the inspection page.
 */
const createSchema = z.object({
  inspectionDate: z.string().min(1, 'When is it happening?'),
  locationId: z.string().optional().or(z.literal('')),
  specificArea: z.string().max(200).optional().or(z.literal('')),
  organizationUnitId: z.string().optional().or(z.literal('')),
  type: z.enum(['Routine', 'Planned', 'Unplanned', 'FollowUp', 'PreTask', 'PostIncident', 'Regulatory', 'Management']),
  category: z.enum(['General', 'FireSafety', 'Construction', 'Equipment', 'Chemical', 'Electrical', 'WorkingAtHeight', 'ConfinedSpace', 'ManualHandling', 'Environmental', 'Housekeeping']),
  checklistId: z.string().optional().or(z.literal('')),
  inspectorId: z.string().min(1, 'Choose the inspector'),
  externalInspectorName: z.string().max(200).optional().or(z.literal('')),
  externalInspectorOrganization: z.string().max(200).optional().or(z.literal('')),
  nextInspectionDueDate: z.string().optional().or(z.literal('')),
});

type CreateForm = z.input<typeof createSchema>;

const emptyCreate: CreateForm = {
  inspectionDate: new Date().toISOString().slice(0, 10),
  locationId: '',
  specificArea: '',
  organizationUnitId: '',
  type: 'Routine',
  category: 'General',
  checklistId: '',
  inspectorId: '',
  externalInspectorName: '',
  externalInspectorOrganization: '',
  nextInspectionDueDate: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);

export default function NewInspectionPage() {
  const router = useRouter();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });


  const { data: checklists = [] } = useQuery({
    queryKey: ['hr', 'safety-checklists', 'active'],
    queryFn: () => safetyChecklistService.getAll(true),
  });

  const form = useForm<CreateForm>({
    resolver: zodResolver(createSchema) as any,
    defaultValues: emptyCreate,
  });

  const onSubmit = async (values: CreateForm) => {
    const v = createSchema.parse(values);
    setSubmitting(true);
    try {
      const created = await safetyInspectionService.create({
        inspectionDate: v.inspectionDate,
        locationId: blank(v.locationId),
        specificArea: blank(v.specificArea),
        organizationUnitId: blank(v.organizationUnitId),
        type: v.type,
        category: v.category,
        checklistId: blank(v.checklistId),
        inspectorId: v.inspectorId,
        externalInspectorName: blank(v.externalInspectorName),
        externalInspectorOrganization: blank(v.externalInspectorOrganization),
        nextInspectionDueDate: blank(v.nextInspectionDueDate),
      });
      toast({
        title: 'Inspection scheduled',
        description: `${created.inspectionNumber} — record findings as you go.`,
      });
      router.push(`/hr/safety/inspections/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to schedule the inspection.',
        variant: 'destructive',
      });
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Schedule Inspection"
        description="The number is generated automatically. Choosing a checklist loads its items onto the inspection and starts the walk; without one the inspection stays Scheduled and findings are recorded by hand."
        backHref="/hr/safety/inspections"
      />

      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">What & where</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <SelectField
                form={form}
                name="type"
                label="Type"
                required
                options={SHE_INSPECTION_TYPE_OPTIONS}
              />
              <SelectField
                form={form}
                name="category"
                label="Category"
                required
                options={SHE_INSPECTION_CATEGORY_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={form}
                name="locationId"
                label="Location"
                allowEmpty
                options={locations.map((l) => ({ value: l.id, label: l.name }))}
              />
              <TextField form={form} name="specificArea" label="Specific area" />
            </FieldRow>
            <FieldRow>
              <OrganizationUnitPickerField form={form} name="organizationUnitId" label="Organization unit" allowEmpty />
              <SelectField
                form={form}
                name="checklistId"
                label="Checklist"
                allowEmpty
                emptyLabel="No checklist — free-form"
                options={checklists.map((c) => ({
                  value: c.id,
                  label: `${c.checklistNumber} — ${c.name}`,
                }))}
              />
            </FieldRow>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Who & when</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <EmployeePickerField form={form} name="inspectorId" label="Inspector" required />
              <DateField form={form} name="inspectionDate" label="Inspection date" required />
            </FieldRow>
            <FieldRow>
              <TextField
                form={form}
                name="externalInspectorName"
                label="External inspector (if any)"
              />
              <TextField
                form={form}
                name="externalInspectorOrganization"
                label="External organization"
              />
            </FieldRow>
            <DateField form={form} name="nextInspectionDueDate" label="Next inspection due" />
          </CardContent>
        </Card>

        <div className="flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={() => router.push('/hr/safety/inspections')}
            disabled={submitting}
          >
            Cancel
          </Button>
          <Button type="submit" disabled={submitting}>
            {submitting ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            Schedule inspection
          </Button>
        </div>
      </form>
    </div>
  );
}
