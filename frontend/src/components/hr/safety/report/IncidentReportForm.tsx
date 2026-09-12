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
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { safetyIncidentService } from '@/services/hr/safety-incident.service';
import { safetyReferenceService } from '@/services/hr/safety-reference.service';
import { locationService } from '@/services/hr/location.service';
import { SHE_INCIDENT_CATEGORY_OPTIONS } from '@/types/hr/safety';
import { SHE_INCIDENT_SEVERITY_OPTIONS } from '@/types/hr/safety-incidents';

/**
 * One incident form, two doors (FR-SHE-100 / FR-ENV-025).
 *
 * `mode="self"` is the open employee reporting surface under /me/safety/report/incident: every
 * authenticated employee can file, no SHE role needed, and the server records the TOKEN's
 * employee as the reporter regardless of what is sent. The incident-type picker is loaded from
 * an HR-gated lookup, so for a plain employee it quietly disappears instead of erroring;
 * classification can be refined by SHE during review.
 *
 * `mode="desk"` (2026-09-03) is the SHE desk's own door under /hr/safety/incidents/new. Until
 * then the desk register's "Report incident" button pointed at the self-service form, so a
 * walk-in or phoned-in report could only be filed AS the officer, and the register named the
 * officer as the reporter. The desk door names the reporter: the API honours `reportedById`
 * when the caller holds HR.She.Write (SafetyIncidentController.Create), and forces the token's
 * employee for everyone else — so the picker is only offered here, and only the desk can use it.
 */
const baseSchema = z.object({
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
  locationId: z.string().optional().or(z.literal('')),
  specificArea: z.string().max(200).optional().or(z.literal('')),
  description: z.string().min(1, 'Describe what happened').max(4000),
  immediateCause: z.string().max(2000).optional().or(z.literal('')),
  immediateActionTaken: z.string().max(500).optional().or(z.literal('')),
  couldHaveCausedInjury: z.boolean(),
  potentialConsequence: z.string().max(500).optional().or(z.literal('')),
  reportedById: z.string().optional().or(z.literal('')),
});

const deskSchema = baseSchema.extend({
  reportedById: z.string().min(1, 'Who reported it?'),
});

type ReportForm = z.input<typeof baseSchema>;

const emptyReport: ReportForm = {
  category: 'NearMiss',
  severity: 'Minor',
  incidentTypeId: '',
  incidentDate: new Date().toISOString().slice(0, 10),
  locationId: '',
  specificArea: '',
  description: '',
  immediateCause: '',
  immediateActionTaken: '',
  couldHaveCausedInjury: false,
  potentialConsequence: '',
  reportedById: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);

export function IncidentReportForm({ mode }: { mode: 'self' | 'desk' }) {
  const router = useRouter();
  const desk = mode === 'desk';
  const [submitting, setSubmitting] = useState(false);
  const [reportedNumber, setReportedNumber] = useState<string | null>(null);

  // HR-gated lookups: a plain employee gets a 403 here, which is fine — the pickers just hide.
  const { data: incidentTypes = [] } = useQuery({
    queryKey: ['me', 'safety', 'incident-types-active'],
    queryFn: () => safetyReferenceService.getIncidentTypes(true),
    retry: false,
  });
  const { data: locations = [] } = useQuery({
    queryKey: ['me', 'safety', 'locations'],
    queryFn: () => locationService.getAll(),
    retry: false,
  });

  const form = useForm<ReportForm>({
    resolver: zodResolver(desk ? deskSchema : baseSchema) as any,
    defaultValues: emptyReport,
  });

  const onSubmit = async (values: ReportForm) => {
    const v = (desk ? deskSchema : baseSchema).parse(values);
    setSubmitting(true);
    try {
      const created = await safetyIncidentService.create({
        category: v.category,
        severity: v.severity,
        incidentTypeId: blank(v.incidentTypeId),
        incidentDate: v.incidentDate,
        locationId: blank(v.locationId),
        specificArea: blank(v.specificArea),
        description: v.description,
        immediateCause: blank(v.immediateCause),
        immediateActionTaken: blank(v.immediateActionTaken),
        couldHaveCausedInjury: v.couldHaveCausedInjury,
        potentialConsequence: blank(v.potentialConsequence),
        requiresInvestigation: false,
        reportableToAuthority: false,
        // Self mode never sends a reporter: the server takes it from the token.
        ...(desk ? { reportedById: blank(v.reportedById) } : {}),
      });
      if (desk) {
        toast.success(`Incident ${created.incidentNumber} recorded.`);
        router.push(`/hr/safety/incidents/${created.id}`);
        return;
      }
      setReportedNumber(created.incidentNumber);
      toast.success(
        `Incident reported — reference ${created.incidentNumber}. The SHE team has it from here.`,
      );
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Failed to submit the report.');
    } finally {
      setSubmitting(false);
    }
  };

  if (reportedNumber) {
    return (
      <div className="space-y-6">
        <PageHeader title="Incident reported" backHref="/me/safety" />
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
              <Button onClick={() => router.push('/me/safety/reports')}>My reports</Button>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  const cancelHref = desk ? '/hr/safety/incidents' : '/me/safety/report';

  return (
    <div className={desk ? 'space-y-6 p-6' : 'space-y-6'}>
      <PageHeader
        title={desk ? 'Record an Incident' : 'Report an Incident'}
        description={
          desk
            ? 'For reports that reach the SHE desk in person, by phone or on paper. Name the person who reported it — the register shows them as the reporter, not you.'
            : 'Accidents, near misses, dangerous occurrences, environmental incidents — anyone can report, and near misses matter as much as injuries. You are recorded as the reporter.'
        }
        backHref={cancelHref}
      />

      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        {desk && (
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Who reported it</CardTitle>
            </CardHeader>
            <CardContent>
              <EmployeePickerField
                form={form}
                name="reportedById"
                label="Reported by"
                required
                placeholder="Search the person who reported the incident"
              />
            </CardContent>
          </Card>
        )}

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
            </FieldRow>
            <TextField
              form={form}
              name="specificArea"
              label="Where exactly"
              placeholder="e.g. Plant room B, walkway by the generator"
            />
            <TextareaField
              form={form}
              name="description"
              label="Describe what happened"
              rows={4}
              required
              placeholder={
                desk
                  ? "The reporter's account, as told to you. Who, what, when, where."
                  : 'What you saw, in your own words. Who, what, when, where.'
              }
            />
            <TextareaField
              form={form}
              name="immediateCause"
              label={desk ? 'Immediate cause, as reported' : 'What do you think caused it?'}
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
            onClick={() => router.push(cancelHref)}
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
            {desk ? 'Record incident' : 'Submit report'}
          </Button>
        </div>
      </form>
    </div>
  );
}
