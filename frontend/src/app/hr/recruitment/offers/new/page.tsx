'use client';

import { useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Info, Loader2, Save } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { formatMoney } from '@/lib/hr/attendance-format';
import { jobOfferService } from '@/services/hr/offers.service';
import { jobVacancyService } from '@/services/hr/recruitment.service';
import { jobApplicationService } from '@/services/hr/recruitment-pipeline.service';
import type { CreateJobOffer } from '@/types/hr/offers';

/**
 * Raising an offer against an application.
 *
 * ⚠ **Only the negotiated terms are set here.** The role snapshot — position, reporting line,
 * grade, employment type, work mode — is taken server-side from the application's vacancy and
 * position and cannot be sent from this form; the create DTO refuses any of those fields with a
 * 400. The vacancy's salary range is shown as a hint only, not enforced client-side — the real
 * grade-band guard lives on the server and comes back as a 422 naming the band if the figure is
 * outside it.
 */
export default function NewJobOfferPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const applicationId = searchParams.get('applicationId') ?? '';
  const { toast } = useToast();

  const application = useQuery({
    queryKey: ['hr', 'application', applicationId],
    queryFn: () => jobApplicationService.getById(applicationId),
    enabled: !!applicationId,
  });

  const vacancyId = application.data?.jobVacancyId ?? null;

  const vacancy = useQuery({
    queryKey: ['hr', 'vacancies', vacancyId],
    queryFn: () => jobVacancyService.getById(vacancyId as string),
    enabled: !!vacancyId,
  });

  const [form, setForm] = useState({
    baseSalary: '',
    currencyCode: 'GHS',
    bonus: '',
    bonusTerms: '',
    commission: '',
    commissionStructure: '',
    proposedStartDate: '',
    expiryDate: '',
    contractDurationMonths: '',
    probationPeriodMonths: '',
    noticePeriodMonths: '',
    annualLeaveDays: '',
    weeklyHours: '',
    ndaRequired: false,
    isConditional: false,
    locationId: null as string | null,
    additionalTerms: '',
  });

  const create = useMutation({
    mutationFn: () => {
      const payload: CreateJobOffer = {
        jobApplicationId: applicationId,
        baseSalary: form.baseSalary ? Number(form.baseSalary) : null,
        currencyCode: form.currencyCode.trim() || null,
        bonus: form.bonus ? Number(form.bonus) : null,
        bonusTerms: form.bonusTerms.trim() || null,
        commission: form.commission ? Number(form.commission) : null,
        commissionStructure: form.commissionStructure.trim() || null,
        proposedStartDate: form.proposedStartDate || null,
        expiryDate: form.expiryDate || null,
        contractDurationMonths: form.contractDurationMonths ? Number(form.contractDurationMonths) : null,
        probationPeriodMonths: form.probationPeriodMonths ? Number(form.probationPeriodMonths) : null,
        noticePeriodMonths: form.noticePeriodMonths ? Number(form.noticePeriodMonths) : null,
        annualLeaveDays: form.annualLeaveDays ? Number(form.annualLeaveDays) : null,
        weeklyHours: form.weeklyHours ? Number(form.weeklyHours) : null,
        ndaRequired: form.ndaRequired,
        isConditional: form.isConditional,
        locationId: form.locationId,
        additionalTerms: form.additionalTerms.trim() || null,
      };
      return jobOfferService.create(payload);
    },
    onSuccess: (created) => {
      toast({ title: 'Offer created', description: `${created.offerNumber} saved as a draft.` });
      router.push(`/hr/recruitment/offers/${created.id}`);
    },
    onError: (e: any) =>
      toast({ title: 'Could not raise the offer', description: e?.message, variant: 'destructive' }),
  });

  if (!applicationId) {
    return (
      <div className="p-6">
        <PageHeader title="Raise an offer" backHref="/hr/recruitment/offers" />
        <EmptyState
          title="Start from an application"
          description="An offer is raised against an application, so it inherits the position and reporting line. Open the candidate's application and use “Extend an offer”."
          action={
            <Button onClick={() => router.push('/hr/recruitment/applications')}>
              Go to applications
            </Button>
          }
        />
      </div>
    );
  }

  if (application.isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const app = application.data;
  if (!app) {
    return (
      <div className="p-6">
        <EmptyState title="Application not found" description="It may have been removed." />
      </div>
    );
  }

  const v = vacancy.data;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Raise an offer"
        description={`${app.candidateName} · ${app.jobTitle || app.vacancyNumber}`}
        backHref={`/hr/recruitment/applications/${applicationId}`}
        actions={
          <Button onClick={() => create.mutate()} disabled={create.isPending}>
            <Save className="mr-2 h-4 w-4" />
            {create.isPending ? 'Raising…' : 'Raise offer'}
          </Button>
        }
      />

      {app.hasOffer && (
        <Alert variant="destructive">
          <Info className="h-4 w-4" />
          <AlertTitle>An offer already exists for this application</AlertTitle>
          <AlertDescription>
            Creating another raises a second, independent offer rather than revising the first —
            use the existing offer's Revise action instead if this is a counter-offer.
          </AlertDescription>
        </Alert>
      )}

      <Alert>
        <Info className="h-4 w-4" />
        <AlertTitle>Taken from the application</AlertTitle>
        <AlertDescription>
          Position, reporting line, grade and employment type come from{' '}
          <strong>{app.jobTitle || app.vacancyNumber}</strong>&apos;s vacancy and are set automatically
          — they cannot be entered here.
          {v && v.isSalaryVisible && (v.salaryRangeMin != null || v.salaryRangeMax != null) && (
            <>
              {' '}
              The advertised range is{' '}
              <strong>
                {v.salaryRangeMin != null ? formatMoney(v.salaryRangeMin, v.salaryCurrencyCode ?? 'GHS') : '—'} to{' '}
                {v.salaryRangeMax != null ? formatMoney(v.salaryRangeMax, v.salaryCurrencyCode ?? 'GHS') : '—'}
              </strong>
              .
            </>
          )}
        </AlertDescription>
      </Alert>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Compensation</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-3">
          <div className="space-y-1.5">
            <Label htmlFor="baseSalary">Base salary</Label>
            <Input
              id="baseSalary"
              type="number"
              step="0.01"
              value={form.baseSalary}
              onChange={(e) => setForm({ ...form, baseSalary: e.target.value })}
            />
            <p className="text-xs text-muted-foreground">
              Checked against the position grade&apos;s band server-side.
            </p>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="currencyCode">Currency</Label>
            <Input
              id="currencyCode"
              maxLength={10}
              value={form.currencyCode}
              onChange={(e) => setForm({ ...form, currencyCode: e.target.value.toUpperCase() })}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="weeklyHours">Weekly hours</Label>
            <Input
              id="weeklyHours"
              type="number"
              step="0.5"
              value={form.weeklyHours}
              onChange={(e) => setForm({ ...form, weeklyHours: e.target.value })}
              placeholder="Defaults from the employment type"
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="bonus">Bonus</Label>
            <Input
              id="bonus"
              type="number"
              step="0.01"
              value={form.bonus}
              onChange={(e) => setForm({ ...form, bonus: e.target.value })}
            />
          </div>
          <div className="space-y-1.5 md:col-span-2">
            <Label htmlFor="bonusTerms">Bonus terms</Label>
            <Input
              id="bonusTerms"
              value={form.bonusTerms}
              onChange={(e) => setForm({ ...form, bonusTerms: e.target.value })}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="commission">Commission</Label>
            <Input
              id="commission"
              type="number"
              step="0.01"
              value={form.commission}
              onChange={(e) => setForm({ ...form, commission: e.target.value })}
            />
          </div>
          <div className="space-y-1.5 md:col-span-2">
            <Label htmlFor="commissionStructure">Commission structure</Label>
            <Input
              id="commissionStructure"
              value={form.commissionStructure}
              onChange={(e) => setForm({ ...form, commissionStructure: e.target.value })}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Dates and terms</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-3">
          <div className="space-y-1.5">
            <Label htmlFor="proposedStartDate">Proposed start date</Label>
            <Input
              id="proposedStartDate"
              type="date"
              value={form.proposedStartDate}
              onChange={(e) => setForm({ ...form, proposedStartDate: e.target.value })}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="expiryDate">Offer expires</Label>
            <Input
              id="expiryDate"
              type="date"
              value={form.expiryDate}
              onChange={(e) => setForm({ ...form, expiryDate: e.target.value })}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="contractDurationMonths">Contract length (months)</Label>
            <Input
              id="contractDurationMonths"
              type="number"
              min={0}
              value={form.contractDurationMonths}
              onChange={(e) => setForm({ ...form, contractDurationMonths: e.target.value })}
              placeholder="Leave blank if permanent"
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="probationPeriodMonths">Probation (months)</Label>
            <Input
              id="probationPeriodMonths"
              type="number"
              min={0}
              value={form.probationPeriodMonths}
              onChange={(e) => setForm({ ...form, probationPeriodMonths: e.target.value })}
              placeholder="Defaults from the position"
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="noticePeriodMonths">Notice period (months)</Label>
            <Input
              id="noticePeriodMonths"
              type="number"
              min={0}
              value={form.noticePeriodMonths}
              onChange={(e) => setForm({ ...form, noticePeriodMonths: e.target.value })}
              placeholder="Defaults from the position"
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="annualLeaveDays">Annual leave (days)</Label>
            <Input
              id="annualLeaveDays"
              type="number"
              min={0}
              value={form.annualLeaveDays}
              onChange={(e) => setForm({ ...form, annualLeaveDays: e.target.value })}
            />
          </div>

          <div className="flex items-center gap-2">
            <Checkbox
              id="ndaRequired"
              checked={form.ndaRequired}
              onCheckedChange={(c) => setForm({ ...form, ndaRequired: c === true })}
            />
            <Label htmlFor="ndaRequired" className="font-normal">
              NDA required
            </Label>
          </div>
          <div className="flex items-center gap-2">
            <Checkbox
              id="isConditional"
              checked={form.isConditional}
              onCheckedChange={(c) => setForm({ ...form, isConditional: c === true })}
            />
            <Label htmlFor="isConditional" className="font-normal">
              Conditional on pre-employment checks
            </Label>
          </div>

          <div className="space-y-1.5 md:col-span-3">
            <Label htmlFor="additionalTerms">Additional terms</Label>
            <Textarea
              id="additionalTerms"
              rows={4}
              value={form.additionalTerms}
              onChange={(e) => setForm({ ...form, additionalTerms: e.target.value })}
            />
          </div>
        </CardContent>
      </Card>

      <div className="flex justify-end gap-2">
        <Button variant="outline" onClick={() => router.push(`/hr/recruitment/applications/${applicationId}`)}>
          Cancel
        </Button>
        <Button onClick={() => create.mutate()} disabled={create.isPending}>
          <Save className="mr-2 h-4 w-4" />
          {create.isPending ? 'Raising…' : 'Raise offer'}
        </Button>
      </div>
    </div>
  );
}
