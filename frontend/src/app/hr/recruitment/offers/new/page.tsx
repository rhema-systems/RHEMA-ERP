'use client';

import { useEffect, useState } from 'react';
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
import { CurrencyPicker } from '@/components/hr/common/CurrencyPicker';
import { LocationPicker } from '@/components/hr/common/LocationPicker';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { salaryGradeService } from '@/services/hr/salary-grade.service';
import type { CreateJobOffer, JobOfferDefaults } from '@/types/hr/offers';

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
    // ⚠ `locationId` was already here before round 4 — in state, sent on create, and bound to
    // NO control (§ 3 defect 13). It therefore sent null on every offer ever raised, while the
    // offer letter printed {{LocationName}}. `locationLevelId` is its partner and was absent.
    locationLevelId: null as string | null,
    locationId: null as string | null,
    salaryLevelId: null as string | null,
    salaryNotchId: null as string | null,
    additionalTerms: '',
  });

  // Round 4, lane G1. The proposal the form opens with. Writes nothing, and every value is
  // editable — the server’s own rules still run on create and still win.
  const defaultsQuery = useQuery({
    queryKey: ['hr', 'offer-defaults', applicationId],
    queryFn: () => jobOfferService.getDefaults(applicationId),
    enabled: !!applicationId,
  });
  const defaults: JobOfferDefaults | undefined = defaultsQuery.data;

  // ⚠ Seeded ONCE, and only into boxes the user has not touched. Re-seeding on every render of
  // the query would overwrite a recruiter mid-edit; seeding unconditionally on refetch would
  // silently revert a deliberate override back to the system’s suggestion.
  const [seeded, setSeeded] = useState(false);
  useEffect(() => {
    if (seeded || !defaults) return;
    const n = (v: number | null | undefined) => (v === null || v === undefined ? '' : String(v));
    setForm((prev) => ({
      ...prev,
      baseSalary: n(defaults.baseSalary),
      currencyCode: defaults.currencyCode ?? prev.currencyCode,
      proposedStartDate: defaults.proposedStartDate?.slice(0, 10) ?? prev.proposedStartDate,
      expiryDate: defaults.expiryDate?.slice(0, 10) ?? prev.expiryDate,
      contractDurationMonths: n(defaults.contractDurationMonths),
      probationPeriodMonths: n(defaults.probationPeriodMonths),
      noticePeriodMonths: n(defaults.noticePeriodMonths),
      annualLeaveDays: n(defaults.annualLeaveDays),
      weeklyHours: n(defaults.weeklyHours),
      isConditional: defaults.isConditional,
      locationLevelId: defaults.locationLevelId ?? null,
      locationId: defaults.locationId ?? null,
      salaryLevelId: defaults.salaryLevelId ?? null,
      salaryNotchId: defaults.salaryNotchId ?? null,
    }));
    setSeeded(true);
  }, [defaults, seeded]);

  // The grade’s ladder, for the two controls that had no UI at all.
  const levels = useQuery({
    queryKey: ['hr', 'salary-levels', defaults?.salaryGradeId],
    queryFn: () => salaryGradeService.getLevels(defaults!.salaryGradeId as string),
    enabled: !!defaults?.salaryGradeId,
  });
  const notches = useQuery({
    queryKey: ['hr', 'salary-notches', form.salaryLevelId],
    queryFn: () => salaryGradeService.getNotches(form.salaryLevelId as string),
    enabled: !!form.salaryLevelId,
  });

  /** The one-line provenance under a box, or nothing when the value was not derived. */
  const Source = ({ field }: { field: string }) => {
    const why = defaults?.sources?.[field];
    return why ? <p className="text-xs text-muted-foreground">{why}</p> : null;
  };
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
        locationLevelId: form.locationLevelId,
        locationId: form.locationId,
        salaryLevelId: form.salaryLevelId,
        salaryNotchId: form.salaryNotchId,
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

      {/* ⚠ Round 4, lane G1. Named explicitly rather than left as empty boxes. The endpoint
          returns a source line only for values it actually derived, so anything it could not
          work out is listed here instead of being given a confident caption it does not
          deserve. */}
      {defaults && defaults.unresolved.length > 0 && (
        <Alert>
          <Info className="h-4 w-4" />
          <AlertTitle>You will need to supply these</AlertTitle>
          <AlertDescription>
            <ul className="ml-4 list-disc space-y-0.5">
              {defaults.unresolved.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Placement</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* ⚠ The control that never existed. `locationId` has been in this form’s state and on
              its create payload all along, bound to nothing — so every offer ever raised sent
              null, while the offer letter printed {{LocationName}}. The picker carries the level
              too, which the payload also accepts and nothing ever set. */}
          <LocationPicker
            value={form.locationId ?? ''}
            onChange={(locationId) => setForm({ ...form, locationId: locationId || null })}
            onLevelChange={(levelId) => setForm({ ...form, locationLevelId: levelId || null })}
            allowNone="Not stated"
            levelLabel="Location level"
            locationLabel="Duty station"
            hint="Printed on the offer letter as the place of work."
            idPrefix="offer"
          />
          <Source field="locationId" />
        </CardContent>
      </Card>

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
            <Source field="baseSalary" />
            <p className="text-xs text-muted-foreground">
              Checked against the position grade&apos;s band server-side.
            </p>
          </div>
          <div className="space-y-1.5">
            {/* ⚠ Was a free-text box accepting ten characters, validated by nothing (§ 3 defect
                14). The server now refuses a code Finance does not hold, so a typo that used to
                reach the offer letter comes back as a 422 instead. */}
            <Label htmlFor="currencyCode">Currency</Label>
            <CurrencyPicker
              id="currencyCode"
              value={form.currencyCode}
              onChange={(code) => setForm({ ...form, currencyCode: code })}
            />
            <Source field="currencyCode" />
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
            <Source field="weeklyHours" />
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

          {/* ⚠ Two more fields the create payload has always accepted with no UI behind them.
              They place the salary on the grade’s ladder, which is what makes a figure
              defensible later — "notch 3 of level II" rather than a number somebody typed. */}
          {defaults?.salaryGradeId && (
            <>
              <div className="space-y-1.5">
                <Label htmlFor="salaryLevelId">Salary level</Label>
                <Select
                  value={form.salaryLevelId ?? 'none'}
                  onValueChange={(value) =>
                    // Changing the level invalidates the notch: a notch belongs to one level, and
                    // keeping it would post a notch from a level no longer selected.
                    setForm({
                      ...form,
                      salaryLevelId: value === 'none' ? null : value,
                      salaryNotchId: null,
                    })
                  }
                >
                  <SelectTrigger id="salaryLevelId">
                    <SelectValue placeholder="Not placed" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Not placed</SelectItem>
                    {(levels.data ?? []).map((level) => (
                      <SelectItem key={level.id} value={level.id}>
                        {level.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Source field="salaryLevelId" />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="salaryNotchId">Notch</Label>
                <Select
                  value={form.salaryNotchId ?? 'none'}
                  disabled={!form.salaryLevelId}
                  onValueChange={(value) => {
                    const notchId = value === 'none' ? null : value;
                    const notch = (notches.data ?? []).find((n) => n.id === notchId);
                    // A notch IS an amount. Picking one and leaving a different figure in the
                    // box would put the offer outside the ladder it claims to sit on.
                    setForm({
                      ...form,
                      salaryNotchId: notchId,
                      baseSalary: notch ? String(notch.salaryAmount) : form.baseSalary,
                    });
                  }}
                >
                  <SelectTrigger id="salaryNotchId">
                    <SelectValue placeholder={form.salaryLevelId ? 'Not placed' : 'Choose a level first'} />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Not placed</SelectItem>
                    {(notches.data ?? []).map((notch) => (
                      <SelectItem key={notch.id} value={notch.id}>
                        Notch {notch.notchNumber}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  Choosing a notch sets the base salary to its amount.
                </p>
              </div>
            </>
          )}
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
            <Source field="proposedStartDate" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="expiryDate">Offer expires</Label>
            <Input
              id="expiryDate"
              type="date"
              value={form.expiryDate}
              onChange={(e) => setForm({ ...form, expiryDate: e.target.value })}
            />
            <Source field="expiryDate" />
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
            <Source field="contractDurationMonths" />
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
            <Source field="probationPeriodMonths" />
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
            <Source field="noticePeriodMonths" />
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
            <Source field="annualLeaveDays" />
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
