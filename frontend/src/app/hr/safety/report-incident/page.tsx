'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Send, CheckCircle2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  TextField,
  TextareaField,
  SelectField,
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyIncidentService } from '@/services/hr/safety-incident.service';
import { safetyReferenceService } from '@/services/hr/safety-reference.service';
import { SHE_INCIDENT_CATEGORY_OPTIONS } from '@/types/hr/safety';
import { SHE_INCIDENT_SEVERITY_OPTIONS } from '@/types/hr/safety-incidents';

/**
 * The open employee reporting surface (FR-SHE-100 / FR-ENV-025) — every authenticated employee
 * can file here, no SHE role needed, and the server records the TOKEN's employee as the reporter
 * regardless of what is sent. The incident-type picker is a nicety loaded from an HR-gated
 * lookup, so for a plain employee it quietly disappears instead of erroring; classification can
 * be refined by SHE during review.
 */
const reportSchema = z.object({
  category: z.enum([
    'Accident',
    'NearMiss',
    'DangerousOccurrence',
    'OccupationalIllness',
    'EnvironmentalIncident',
    'PropertyDamage',
    'SecurityIncident',
    'FireIncident',
  ]),
  severity: z.enum(['Negligible', 'Minor', 'Moderate', 'Major', 'Catastrophic']),
  incidentTypeId: z.string().optional().or(z.literal('')),
  incidentDate: z.string().min(1, 'When did it happen?'),
  specificArea: z.string().max(200).optional().or(z.literal('')),
  description: z.string().min(1, 'Describe what happened').max(4000),
  immediateCause: z.string().max(2000).optional().or(z.literal('')),
  immediateActionTaken: z.string().max(500).optional().or(z.literal('')),
  couldHaveCausedInjury: z.boolean(),
  potentialConsequence: z.string().max(500).optional().or(z.literal('')),
});

type ReportForm = z.input<typeof reportSchema>;

const emptyReport: ReportForm = {
  category: 'NearMiss',
  severity: 'Minor',
  incidentTypeId: '',
  incidentDate: new Date().toISOString().slice(0, 10),
  specificArea: '',
  description: '',
  immediateCause: '',
  immediateActionTaken: '',
  couldHaveCausedInjury: false,
  potentialConsequence: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);

export default function ReportIncidentPage() {
  const router = useRouter();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);
  const [reportedNumber, setReportedNumber] = useState<string | null>(null);

  // HR-gated lookup: a plain employee gets a 403 here, which is fine — the picker just hides.
  const { data: incidentTypes = [] } = useQuery({
    queryKey: ['hr', 'safety-reference', 'incident-types', 'active'],
    queryFn: () => safetyReferenceService.getIncidentTypes(true),
    retry: false,
  });

  const form = useForm<ReportForm>({
    resolver: zodResolver(reportSchema) as any,
    defaultValues: emptyReport,
  });

  const onSubmit = async (values: ReportForm) => {
    const v = reportSchema.parse(values);
    setSubmitting(true);
    try {
      const created = await safetyIncidentService.create({
        category: v.category,
        severity: v.severity,
        incidentTypeId: blank(v.incidentTypeId),
        incidentDate: v.incidentDate,
        specificArea: blank(v.specificArea),
        description: v.description,
        immediateCause: blank(v.immediateCause),
        immediateActionTaken: blank(v.immediateActionTaken),
        couldHaveCausedInjury: v.couldHaveCausedInjury,
        potentialConsequence: blank(v.potentialConsequence),
        requiresInvestigation: false,
        reportableToAuthority: false,
      });
      setReportedNumber(created.incidentNumber);
      toast({
        title: 'Incident reported',
        description: `Reference ${created.incidentNumber}. Thank you — the SHE team has it from here.`,
      });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to submit the report.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  if (reportedNumber) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="Incident reported" backHref="/hr/safety" />
        <Card>
          <CardContent className="flex flex-col items-center gap-3 py-12 text-center">
            <CheckCircle2 className="h-10 w-10 text-green-600" />
            <p className="text-lg font-medium">
              Reference <span className="font-mono">{reportedNumber}</span>
            </p>
            <p className="text-muted-foreground max-w-md text-sm">
              Your report is with the SHE team. If anyone was hurt or the area is still unsafe,
              also tell your supervisor immediately — this form does not replace that.
            </p>
            <div className="mt-2 flex gap-2">
              <Button
                variant="outline"
                onClick={() => {
                  setReportedNumber(null);
                  form.reset(emptyReport);
                }}
              >
                Report another
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Report an Incident"
        description="Accidents, near misses, dangerous occurrences, environmental incidents — anyone can report, and near misses matter as much as injuries. You are recorded as the reporter."
        backHref="/hr/safety"
      />

      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">What happened</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <SelectField
                form={form}
                name="category"
                label="Category"
                required
                options={SHE_INCIDENT_CATEGORY_OPTIONS}
              />
              <SelectField
                form={form}
                name="severity"
                label="How serious"
                required
                options={SHE_INCIDENT_SEVERITY_OPTIONS}
              />
            </FieldRow>
            {incidentTypes.length > 0 && (
              <SelectField
                form={form}
                name="incidentTypeId"
                label="Incident type"
                allowEmpty
                emptyLabel="Not sure — let SHE classify it"
                options={incidentTypes.map((t) => ({ value: t.id, label: t.name }))}
              />
            )}
            <FieldRow>
              <DateField form={form} name="incidentDate" label="Date" required />
              <TextField
                form={form}
                name="specificArea"
                label="Where exactly"
                placeholder="e.g. Plant room B, walkway by the generator"
              />
            </FieldRow>
            <TextareaField
              form={form}
              name="description"
              label="Describe what happened"
              rows={4}
              required
              placeholder="What you saw, in your own words. Who, what, when, where."
            />
            <TextareaField
              form={form}
              name="immediateCause"
              label="What do you think caused it?"
              rows={2}
            />
            <TextareaField
              form={form}
              name="immediateActionTaken"
              label="What was done immediately"
              rows={2}
              placeholder="First aid given, area cordoned off, machine stopped…"
            />
            <SwitchField
              form={form}
              name="couldHaveCausedInjury"
              label="Could this have injured someone?"
              description="For near misses — say what the worst case could have been below."
            />
            <TextareaField
              form={form}
              name="potentialConsequence"
              label="Worst case, what could have happened?"
              rows={2}
            />
          </CardContent>
        </Card>

        <div className="flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={() => router.push('/hr/safety')}
            disabled={submitting}
          >
            Cancel
          </Button>
          <Button type="submit" disabled={submitting}>
            {submitting ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Send className="mr-2 h-4 w-4" />
            )}
            Submit report
          </Button>
        </div>
      </form>
    </div>
  );
}
