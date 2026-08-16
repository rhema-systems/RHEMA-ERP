'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { useQuery, useMutation } from '@tanstack/react-query';
import { Loader2, Save, AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { disciplineService, disciplineLookupService } from '@/services/hr/discipline.service';
import { useToast } from '@/hooks/use-toast';
import { SEVERITY_OPTIONS, type StaffOffenseSeverity } from '@/types/hr/discipline';

interface FormValues {
  staffOffenseId: string;
  severity: StaffOffenseSeverity;
  incidentDate: string;
  reportedDate: string;
  incidentDescription: string;
  requiresInvestigation: boolean;
  hearingRequired: boolean;
}

const today = () => new Date().toISOString().slice(0, 10);

/**
 * Raise a disciplinary case.
 *
 * Two deliberate absences from this form:
 *
 * The REPORTER is not a field. The server takes it from the caller's token, and only HR may name
 * someone else — which is a different act (recording a report made to them) and belongs on the edit
 * screen, not here. Offering it as a free choice on the raise form invites attributing an allegation
 * to a colleague who never made it.
 *
 * The CASE NUMBER is not a field either, though the API accepts one. It is generated from the
 * highest already issued, and letting a user type one is how two cases end up sharing a reference.
 */
export default function NewDisciplineCasePage() {
  const router = useRouter();
  const { toast } = useToast();
  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [employeeLabel, setEmployeeLabel] = useState<string | null>(null);
  const [reportedToId, setReportedToId] = useState<string | null>(null);

  const { register, handleSubmit, watch, setValue, formState: { errors } } = useForm<FormValues>({
    defaultValues: {
      staffOffenseId: '',
      severity: 'Minor',
      incidentDate: today(),
      reportedDate: today(),
      incidentDescription: '',
      requiresInvestigation: false,
      hearingRequired: false,
    },
  });

  // Only active offences are offered: a retired entry in the catalog is one HR has withdrawn, and
  // filing a new case against it would resurrect it in every by-offence report.
  const { data: offenses, isLoading: offensesLoading } = useQuery({
    queryKey: ['hr', 'discipline', 'offenses', 'active'],
    queryFn: () => disciplineLookupService.getActiveOffenses(),
  });

  const createMutation = useMutation({
    mutationFn: (values: FormValues & { employeeId: string }) =>
      disciplineService.create({
        employeeId: values.employeeId,
        staffOffenseId: values.staffOffenseId,
        severity: values.severity,
        incidentDate: new Date(values.incidentDate).toISOString(),
        incidentDescription: values.incidentDescription,
        reportedDate: new Date(values.reportedDate).toISOString(),
        reportedToId: reportedToId ?? undefined,
        requiresInvestigation: values.requiresInvestigation,
        hearingRequired: values.hearingRequired,
      }),
    onSuccess: (created) => {
      toast({
        title: `Case ${created.caseNumber} raised`,
        description: 'It is a draft until you submit it.',
      });
      router.push(`/hr/discipline/${created.id}`);
    },
    onError: (e: Error) => {
      toast({ title: 'Could not raise the case', description: e.message, variant: 'destructive' });
    },
  });

  const onSubmit = (values: FormValues) => {
    if (!employeeId) {
      toast({ title: 'Choose the employee the case concerns', variant: 'destructive' });
      return;
    }
    // Narrowed here rather than asserted at the call site: the picker's value is nullable until a
    // selection is made, and the guard above is what makes it a string.
    createMutation.mutate({ ...values, employeeId });
  };

  const severity = watch('severity');

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Raise a disciplinary case"
        description="Records an allegation of misconduct. The case is a draft until you submit it."
        backHref="/hr/discipline"
      />

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle>Who and what</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-5 md:grid-cols-2">
            <div className="space-y-2 md:col-span-2">
              <Label>Employee the case concerns *</Label>
              <EmployeePicker
                value={employeeId}
                initialLabel={employeeLabel}
                onChange={(id, label) => {
                  setEmployeeId(id);
                  setEmployeeLabel(label);
                }}
                placeholder="Search for the employee…"
              />
            </div>

            <div className="space-y-2">
              <Label>Offence *</Label>
              <Select
                value={watch('staffOffenseId')}
                onValueChange={(v) => setValue('staffOffenseId', v, { shouldValidate: true })}
                disabled={offensesLoading}
              >
                <SelectTrigger>
                  <SelectValue placeholder={offensesLoading ? 'Loading…' : 'Choose an offence'} />
                </SelectTrigger>
                <SelectContent>
                  {(offenses ?? []).map((o) => (
                    <SelectItem key={o.id} value={o.id}>
                      {o.offenseCode ? `${o.offenseCode} — ${o.offenseName}` : o.offenseName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <input type="hidden" {...register('staffOffenseId', { required: true })} />
              {errors.staffOffenseId && (
                <p className="text-xs text-destructive">Choose the offence being alleged.</p>
              )}
            </div>

            <div className="space-y-2">
              <Label>Severity *</Label>
              <Select
                value={severity}
                onValueChange={(v) => setValue('severity', v as StaffOffenseSeverity)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {SEVERITY_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {severity === 'GrossMisconduct' && (
                <p className="flex items-start gap-2 text-xs text-muted-foreground">
                  <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                  Gross misconduct is the most serious classification available and shapes the route
                  the case takes. Set it because the allegation warrants it, not to signal urgency.
                </p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="incidentDate">Date of the incident *</Label>
              <Input id="incidentDate" type="date" {...register('incidentDate', { required: true })} />
            </div>

            <div className="space-y-2">
              <Label htmlFor="reportedDate">Date it was reported *</Label>
              <Input id="reportedDate" type="date" {...register('reportedDate', { required: true })} />
            </div>

            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="incidentDescription">What happened *</Label>
              <Textarea
                id="incidentDescription"
                rows={5}
                placeholder="Describe the incident: what occurred, when, where and who was present."
                {...register('incidentDescription', { required: true, maxLength: 4000 })}
              />
              {errors.incidentDescription && (
                <p className="text-xs text-destructive">
                  Describe the incident. This is the account the employee will be asked to answer.
                </p>
              )}
            </div>

            <div className="space-y-2 md:col-span-2">
              <Label>Reported to (optional)</Label>
              <EmployeePicker
                value={reportedToId}
                onChange={(id) => setReportedToId(id)}
                placeholder="The manager or officer the report was made to…"
              />
              <p className="text-xs text-muted-foreground">
                You are recorded as the reporter. Only the employee the report was made to is set here.
              </p>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Route</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex items-start justify-between gap-4">
              <div>
                <Label htmlFor="requiresInvestigation">Requires an investigation</Label>
                <p className="text-xs text-muted-foreground">
                  Puts the case in the &ldquo;needs investigation&rdquo; queue until one is opened.
                </p>
              </div>
              <Switch
                id="requiresInvestigation"
                checked={watch('requiresInvestigation')}
                onCheckedChange={(v) => setValue('requiresInvestigation', v)}
              />
            </div>

            <div className="flex items-start justify-between gap-4">
              <div>
                <Label htmlFor="hearingRequired">Requires a hearing</Label>
                <p className="text-xs text-muted-foreground">
                  Puts the case in the &ldquo;needs hearing&rdquo; queue until one is scheduled.
                </p>
              </div>
              <Switch
                id="hearingRequired"
                checked={watch('hearingRequired')}
                onCheckedChange={(v) => setValue('hearingRequired', v)}
              />
            </div>
          </CardContent>
        </Card>

        <div className="flex justify-end gap-3">
          <Button type="button" variant="outline" onClick={() => router.back()}>
            Cancel
          </Button>
          <Button type="submit" disabled={createMutation.isPending}>
            {createMutation.isPending
              ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              : <Save className="mr-2 h-4 w-4" />}
            Raise case
          </Button>
        </div>
      </form>
    </div>
  );
}
