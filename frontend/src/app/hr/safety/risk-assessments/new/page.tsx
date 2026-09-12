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
  TextareaField,
  SelectField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { safetyRiskAssessmentService } from '@/services/hr/safety-risk-assessment.service';
import { locationService } from '@/services/hr/location.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { SHE_RISK_ASSESSMENT_TYPE_OPTIONS } from '@/types/hr/safety-hazards';

/**
 * Prepare a new risk assessment. It lands in Draft with a server-generated RA-YYYY-NNNN number;
 * hazard lines are added on the detail page, and approval is a separate, guarded step.
 */
const createSchema = z.object({
  title: z.string().min(1, 'Give the assessment a title').max(200),
  type: z.enum(['HIRA', 'JHA', 'PreTask', 'COSHH', 'FireRisk', 'EnvironmentalImpact', 'ErgoAssessment', 'Other']),
  scope: z.string().max(1000).optional().or(z.literal('')),
  locationId: z.string().optional().or(z.literal('')),
  specificActivity: z.string().max(200).optional().or(z.literal('')),
  organizationUnitId: z.string().optional().or(z.literal('')),
  preparedById: z.string().min(1, 'Choose who prepared it'),
  preparedDate: z.string().min(1, 'A preparation date is required'),
  validFrom: z.string().optional().or(z.literal('')),
  validUntil: z.string().optional().or(z.literal('')),
  nextReviewDate: z.string().optional().or(z.literal('')),
});

type CreateForm = z.input<typeof createSchema>;

const emptyCreate: CreateForm = {
  title: '',
  type: 'HIRA',
  scope: '',
  locationId: '',
  specificActivity: '',
  organizationUnitId: '',
  preparedById: '',
  preparedDate: new Date().toISOString().slice(0, 10),
  validFrom: '',
  validUntil: '',
  nextReviewDate: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);

export default function NewRiskAssessmentPage() {
  const router = useRouter();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const { data: orgUnits = [] } = useQuery({
    queryKey: ['hr', 'organization-units'],
    queryFn: () => organizationUnitService.getAll(),
  });

  const form = useForm<CreateForm>({
    resolver: zodResolver(createSchema) as any,
    defaultValues: emptyCreate,
  });

  const onSubmit = async (values: CreateForm) => {
    const v = createSchema.parse(values);
    setSubmitting(true);
    try {
      const created = await safetyRiskAssessmentService.create({
        title: v.title,
        type: v.type,
        scope: blank(v.scope),
        locationId: blank(v.locationId),
        specificActivity: blank(v.specificActivity),
        organizationUnitId: blank(v.organizationUnitId),
        preparedById: v.preparedById,
        preparedDate: v.preparedDate,
        validFrom: blank(v.validFrom),
        validUntil: blank(v.validUntil),
        nextReviewDate: blank(v.nextReviewDate),
      });
      toast({
        title: 'Assessment created',
        description: `${created.assessmentNumber} is in Draft — add its hazard lines next.`,
      });
      router.push(`/hr/safety/risk-assessments/${created.id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create the assessment.',
        variant: 'destructive',
      });
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Risk Assessment"
        description="The number is generated automatically and the assessment starts in Draft. Hazard lines, approval and sign-off follow on the assessment page."
        backHref="/hr/safety/risk-assessments"
      />

      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">What is being assessed</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <TextField
                form={form}
                name="title"
                label="Title"
                required
                placeholder="e.g. Working at height — warehouse racking"
              />
              <SelectField
                form={form}
                name="type"
                label="Type"
                required
                options={SHE_RISK_ASSESSMENT_TYPE_OPTIONS}
              />
            </FieldRow>
            <TextareaField
              form={form}
              name="scope"
              label="Scope"
              rows={3}
              placeholder="The tasks, area and people this assessment covers."
            />
            <FieldRow>
              <SelectField
                form={form}
                name="locationId"
                label="Location"
                allowEmpty
                options={locations.map((l) => ({ value: l.id, label: l.name }))}
              />
              <TextField form={form} name="specificActivity" label="Specific activity" />
            </FieldRow>
            <SelectField
              form={form}
              name="organizationUnitId"
              label="Organization unit"
              allowEmpty
              options={orgUnits.map((u) => ({ value: u.id, label: u.name }))}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Authorship & validity</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <EmployeePickerField form={form} name="preparedById" label="Prepared by" required />
              <DateField form={form} name="preparedDate" label="Prepared date" required />
            </FieldRow>
            <FieldRow>
              <DateField form={form} name="validFrom" label="Valid from" />
              <DateField form={form} name="validUntil" label="Valid until" />
            </FieldRow>
            <DateField form={form} name="nextReviewDate" label="Next review" />
          </CardContent>
        </Card>

        <div className="flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={() => router.push('/hr/safety/risk-assessments')}
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
            Create assessment
          </Button>
        </div>
      </form>
    </div>
  );
}
