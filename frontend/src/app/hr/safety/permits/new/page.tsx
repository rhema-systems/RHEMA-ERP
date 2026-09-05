'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  TextField,
  TextareaField,
  SelectField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { safetyPermitService } from '@/services/hr/safety-permit.service';
import { safetyRiskAssessmentService } from '@/services/hr/safety-risk-assessment.service';
import { locationService } from '@/services/hr/location.service';
import {
  SHE_PERMIT_TYPE_OPTIONS,
  GAS_TEST_MANDATORY_TYPES,
  type ShePermitType,
} from '@/types/hr/safety-permits';

/**
 * Request a new permit. It lands in Draft with a server-generated PTW-YYYY-NNNN number.
 * The safety sections can be completed here or later on the permit page — but approval is
 * blocked until hazards, controls (and gas testing where mandatory) are recorded.
 */
const createSchema = z.object({
  permitType: z.enum(['HotWork', 'ConfinedSpaceEntry', 'WorkingAtHeight', 'Excavation', 'ElectricalIsolation', 'ChemicalHandling', 'CriticalLift', 'Demolition', 'General', 'RoadClosure']),
  workDescription: z.string().min(1, 'Describe the work').max(300),
  locationId: z.string().optional().or(z.literal('')),
  specificArea: z.string().max(200).optional().or(z.literal('')),
  requestedById: z.string().min(1, 'Who is requesting the permit?'),
  plannedStartDate: z.string().min(1, 'When does the work start?'),
  plannedStartTime: z.string().min(1, 'Start time?'),
  plannedEndDate: z.string().min(1, 'When does the window end?'),
  plannedEndTime: z.string().min(1, 'End time?'),
  hazardsIdentified: z.string().max(2000).optional().or(z.literal('')),
  controlMeasures: z.string().max(2000).optional().or(z.literal('')),
  ppeRequired: z.string().max(1000).optional().or(z.literal('')),
  gasTestResults: z.string().max(500).optional().or(z.literal('')),
  isolationDetails: z.string().max(500).optional().or(z.literal('')),
  riskAssessmentId: z.string().optional().or(z.literal('')),
});

type CreateForm = z.input<typeof createSchema>;

const emptyCreate: CreateForm = {
  permitType: 'General',
  workDescription: '',
  locationId: '',
  specificArea: '',
  requestedById: '',
  plannedStartDate: new Date().toISOString().slice(0, 10),
  plannedStartTime: '08:00',
  plannedEndDate: new Date().toISOString().slice(0, 10),
  plannedEndTime: '17:00',
  hazardsIdentified: '',
  controlMeasures: '',
  ppeRequired: '',
  gasTestResults: '',
  isolationDetails: '',
  riskAssessmentId: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);
const toTimeSpan = (v: string) => (v.length === 5 ? `${v}:00` : v);

export default function NewPermitPage() {
  const router = useRouter();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const { data: riskAssessments = [] } = useQuery({
    queryKey: ['hr', 'safety-risk-assessments'],
    queryFn: () => safetyRiskAssessmentService.getAll(),
  });

  const form = useForm<CreateForm>({
    resolver: zodResolver(createSchema) as any,
    defaultValues: emptyCreate,
  });

  const permitType = form.watch('permitType') as ShePermitType;
  const gasMandatory = GAS_TEST_MANDATORY_TYPES.includes(permitType);

  const linkableAssessments = riskAssessments.filter(
    (r) => r.status === 'Approved' || r.status === 'Active',
  );

  const onSubmit = async (values: CreateForm) => {
    const v = createSchema.parse(values);
    setSubmitting(true);
    try {
      const created = await safetyPermitService.create({
        permitType: v.permitType,
        workDescription: v.workDescription,
        locationId: blank(v.locationId),
        specificArea: blank(v.specificArea),
        requestedById: v.requestedById,
        plannedStartDate: v.plannedStartDate,
        plannedStartTime: toTimeSpan(v.plannedStartTime),
        plannedEndDate: v.plannedEndDate,
        plannedEndTime: toTimeSpan(v.plannedEndTime),
        hazardsIdentified: blank(v.hazardsIdentified),
        controlMeasures: blank(v.controlMeasures),
        ppeRequired: blank(v.ppeRequired),
        gasTestResults: blank(v.gasTestResults),
        isolationDetails: blank(v.isolationDetails),
        riskAssessmentId: blank(v.riskAssessmentId),
      });
      toast({
        title: 'Permit requested',
        description: `${created.permitNumber} is in Draft — complete the safety sections and approve.`,
      });
      router.push(`/hr/safety/permits/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create the permit.',
        variant: 'destructive',
      });
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Request Permit to Work"
        description="The number is generated automatically and the permit starts in Draft."
        backHref="/hr/safety/permits"
      />

      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">The work</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <SelectField
                form={form}
                name="permitType"
                label="Permit type"
                required
                options={SHE_PERMIT_TYPE_OPTIONS}
              />
              <EmployeePickerField form={form} name="requestedById" label="Requested by" required />
            </FieldRow>
            <TextareaField
              form={form}
              name="workDescription"
              label="Work description"
              rows={2}
              placeholder="What is being done, with what, by whom."
            />
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
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Validity window</CardTitle>
            <CardDescription>
              Expiry is automatic — once approved, a permit past this window is expired by the
              hourly reminder engine, with warnings at 3 and 1 day(s) out.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <DateField form={form} name="plannedStartDate" label="Start date" required />
              <TextField form={form} name="plannedStartTime" label="Start time (HH:mm)" required />
            </FieldRow>
            <FieldRow>
              <DateField form={form} name="plannedEndDate" label="End date" required />
              <TextField form={form} name="plannedEndTime" label="End time (HH:mm)" required />
            </FieldRow>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Safety sections</CardTitle>
            <CardDescription>
              Approval is blocked until hazards and controls are recorded
              {gasMandatory ? ' — and gas test results are mandatory for this permit type.' : '.'}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <TextareaField form={form} name="hazardsIdentified" label="Hazards identified" rows={3} />
            <TextareaField form={form} name="controlMeasures" label="Control measures" rows={3} />
            <TextareaField form={form} name="ppeRequired" label="PPE required" rows={2} />
            <FieldRow>
              <TextField
                form={form}
                name="gasTestResults"
                label={gasMandatory ? 'Gas test results (mandatory)' : 'Gas test results'}
              />
              <TextField form={form} name="isolationDetails" label="Isolation details (LOTO)" />
            </FieldRow>
            <SelectField
              form={form}
              name="riskAssessmentId"
              label="Linked risk assessment"
              allowEmpty
              emptyLabel="None"
              options={linkableAssessments.map((r) => ({
                value: r.id,
                label: `${r.assessmentNumber} — ${r.title}`,
              }))}
            />
          </CardContent>
        </Card>

        <div className="flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={() => router.push('/hr/safety/permits')}
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
            Create permit
          </Button>
        </div>
      </form>
    </div>
  );
}
