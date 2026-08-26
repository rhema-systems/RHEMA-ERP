'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Send, CheckCircle2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { toast } from 'sonner';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  TextField,
  TextareaField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import {
  LIKELIHOOD_OPTIONS,
  SEVERITY_OPTIONS,
  previewHazardLevel,
} from '@/components/hr/safety/RiskBadge';
import { safetyHazardService } from '@/services/hr/safety-hazard.service';
import { locationService } from '@/services/hr/location.service';
import { SHE_HAZARD_CATEGORY_OPTIONS } from '@/types/hr/safety-hazards';

/**
 * The open hazard-reporting surface — any authenticated employee can flag a hazard, no SHE role
 * needed, and the server records the token's employee as the reporter. The 1–5 ratings are the
 * reporter's first estimate; SHE refines them (and the residual side) during assessment, so the
 * form keeps residual = inherent unless the reporter says controls already exist.
 *
 * Area 25 slice 8: re-homed from /hr/safety/report-hazard under the portal's unified
 * report-a-concern surface (D3 — moved, not redirected). There is no "hazards I reported" read —
 * the hazard entity carries no reporter column (recorded residual), so this form is
 * deliberately fire-and-forget.
 */
const reportSchema = z.object({
  name: z.string().min(1, 'Give the hazard a short name').max(200),
  category: z.enum([
    'Physical',
    'Chemical',
    'Biological',
    'Ergonomic',
    'Psychosocial',
    'Electrical',
    'Mechanical',
    'FireExplosion',
    'SlipTripFall',
    'WorkingAtHeight',
    'ConfinedSpace',
    'Radiation',
    'Environmental',
    'Traffic',
    'Other',
  ]),
  description: z.string().min(1, 'Describe the hazard').max(2000),
  locationId: z.string().optional().or(z.literal('')),
  specificArea: z.string().max(200).optional().or(z.literal('')),
  inherentLikelihood: z.enum(['1', '2', '3', '4', '5']),
  inherentSeverity: z.enum(['1', '2', '3', '4', '5']),
});

type ReportForm = z.input<typeof reportSchema>;

const emptyReport: ReportForm = {
  name: '',
  category: 'Physical',
  description: '',
  locationId: '',
  specificArea: '',
  inherentLikelihood: '3',
  inherentSeverity: '3',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);

export default function ReportHazardPage() {
  const router = useRouter();
  const [submitting, setSubmitting] = useState(false);
  const [reportedName, setReportedName] = useState<string | null>(null);

  // May be gated for a plain employee — the picker then quietly hides instead of erroring.
  const { data: locations = [] } = useQuery({
    queryKey: ['me', 'safety', 'locations'],
    queryFn: () => locationService.getAll(),
    retry: false,
  });

  const form = useForm<ReportForm>({
    resolver: zodResolver(reportSchema) as any,
    defaultValues: emptyReport,
  });

  const likelihood = Number(form.watch('inherentLikelihood'));
  const severity = Number(form.watch('inherentSeverity'));
  const score = likelihood * severity;

  const onSubmit = async (values: ReportForm) => {
    const v = reportSchema.parse(values);
    setSubmitting(true);
    try {
      const created = await safetyHazardService.create({
        name: v.name,
        category: v.category,
        description: v.description,
        locationId: blank(v.locationId),
        specificArea: blank(v.specificArea),
        inherentLikelihood: Number(v.inherentLikelihood),
        inherentSeverity: Number(v.inherentSeverity),
        // The reporter estimates the uncontrolled risk only — SHE assesses controls and sets
        // the residual side properly during triage.
        residualLikelihood: Number(v.inherentLikelihood),
        residualSeverity: Number(v.inherentSeverity),
        isActive: true,
      });
      setReportedName(created.name);
      toast.success('Hazard reported — the SHE team will assess it.');
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Failed to submit the report.');
    } finally {
      setSubmitting(false);
    }
  };

  if (reportedName) {
    return (
      <div className="space-y-6">
        <PageHeader title="Hazard reported" backHref="/me/safety" />
        <Card>
          <CardContent className="flex flex-col items-center gap-3 py-12 text-center">
            <CheckCircle2 className="h-10 w-10 text-green-600" />
            <p className="text-lg font-medium">&ldquo;{reportedName}&rdquo; is on the register</p>
            <p className="text-muted-foreground max-w-md text-sm">
              The SHE team will assess it and put controls in place. If the danger is immediate,
              also tell your supervisor now — this form does not replace that.
            </p>
            <div className="mt-2 flex gap-2">
              <Button
                variant="outline"
                onClick={() => {
                  setReportedName(null);
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
    <div className="space-y-6">
      <PageHeader
        title="Report a Hazard"
        description="See something that could hurt someone? Anyone can report it — you don't need it to have caused an incident, and you are recorded as the reporter."
        backHref="/me/safety/report"
      />

      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">The hazard</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <TextField
                form={form}
                name="name"
                label="Short name"
                required
                placeholder="e.g. Exposed cable run by loading bay"
              />
              <SelectField
                form={form}
                name="category"
                label="Category"
                required
                options={SHE_HAZARD_CATEGORY_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              {locations.length > 0 ? (
                <SelectField
                  form={form}
                  name="locationId"
                  label="Location"
                  allowEmpty
                  emptyLabel="Not sure"
                  options={locations.map((l) => ({ value: l.id, label: l.name }))}
                />
              ) : (
                <div />
              )}
              <TextField
                form={form}
                name="specificArea"
                label="Where exactly"
                placeholder="e.g. Walkway between stores and workshop"
              />
            </FieldRow>
            <TextareaField
              form={form}
              name="description"
              label="Describe the hazard"
              rows={4}
              placeholder="What is dangerous about it, who is exposed, when."
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">How risky does it look?</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <SelectField
                form={form}
                name="inherentLikelihood"
                label="How likely is it to hurt someone?"
                required
                options={LIKELIHOOD_OPTIONS}
              />
              <SelectField
                form={form}
                name="inherentSeverity"
                label="How bad could it be?"
                required
                options={SEVERITY_OPTIONS}
              />
            </FieldRow>
            <p className="text-muted-foreground text-sm">
              Your estimate: score <span className="font-medium tabular-nums">{score}</span> (
              {previewHazardLevel(score)}). A best guess is fine — the SHE team reassesses every
              report.
            </p>
          </CardContent>
        </Card>

        <div className="flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={() => router.push('/me/safety/report')}
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
