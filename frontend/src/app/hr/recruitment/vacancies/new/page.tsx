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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { useToast } from '@/hooks/use-toast';
import { dateOffset, humanizeEnum } from '@/lib/hr/attendance-format';
import { jobVacancyService, staffRequisitionService } from '@/services/hr/recruitment.service';
import { recruitmentPipelineService } from '@/services/hr/recruitment-pipeline.service';
import {
  EMPLOYMENT_TYPES,
  WORK_MODES,
  type EmploymentType,
  type WorkMode,
} from '@/types/hr/recruitment';

/**
 * Opening a vacancy from an approved requisition.
 *
 * The requisition is the parent, not a field: the position, the organisation unit and the
 * internal/external audience are all snapshotted from it server-side. What is set here is what
 * makes it an *advert* — the title candidates see, the deadline, the salary range, who is hiring.
 */
export default function NewVacancyPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const requisitionId = searchParams.get('requisitionId') ?? '';
  const { toast } = useToast();

  // Only active pipelines are offered — an inactive one stays attached where it already is, but
  // should not be picked for something new.
  const pipelines = useQuery({
    queryKey: ['hr', 'recruitment-pipelines'],
    queryFn: () => recruitmentPipelineService.getAll(),
    select: (all) => all.filter((p) => p.isActive),
  });

  const requisition = useQuery({
    queryKey: ['hr', 'requisitions', requisitionId],
    queryFn: () => staffRequisitionService.getById(requisitionId),
    enabled: !!requisitionId,
  });

  const [form, setForm] = useState({
    customAdvertTitle: '',
    numberOfPositions: 1,
    hiringManagerId: null as string | null,
    hiringManagerName: null as string | null,
    recruiterId: null as string | null,
    recruiterName: null as string | null,
    applicationDeadline: dateOffset(21),
    shortlistingDeadline: '',
    numberOfInterviewRounds: 1,
    targetStartDate: '',
    employmentType: 'Permanent' as EmploymentType,
    workMode: 'OnSite' as WorkMode,
    isSalaryVisible: false,
    salaryRangeMin: '',
    salaryRangeMax: '',
    salaryCurrencyCode: 'GHS',
    requiredMinExperienceYears: '',
    keyBenefitsSummary: '',
    requiresWrittenTest: false,
    requiresPracticalTest: false,
    recruitmentPipelineId: '' as string,
    isBlindScreeningEnabled: false,
    autoShortlistMinScore: '',
    autoShortlistRequireAllMandatory: true,
  });

  const [seeded, setSeeded] = useState(false);
  if (requisition.data && !seeded) {
    setSeeded(true);
    setForm((p) => ({ ...p, numberOfPositions: requisition.data.numberOfPositions }));
  }

  const create = useMutation({
    mutationFn: () =>
      jobVacancyService.create({
        staffRequisitionId: requisitionId,
        customAdvertTitle: form.customAdvertTitle.trim() || null,
        numberOfPositions: form.numberOfPositions,
        hiringManagerId: form.hiringManagerId,
        recruiterId: form.recruiterId,
        applicationDeadline: form.applicationDeadline || null,
        shortlistingDeadline: form.shortlistingDeadline || null,
        numberOfInterviewRounds: form.numberOfInterviewRounds || null,
        targetStartDate: form.targetStartDate || null,
        isSalaryVisible: form.isSalaryVisible,
        employmentType: form.employmentType,
        workMode: form.workMode,
        salaryRangeMin: form.salaryRangeMin ? Number(form.salaryRangeMin) : null,
        salaryRangeMax: form.salaryRangeMax ? Number(form.salaryRangeMax) : null,
        salaryCurrencyCode: form.salaryCurrencyCode.trim() || null,
        requiredMinExperienceYears: form.requiredMinExperienceYears
          ? Number(form.requiredMinExperienceYears)
          : null,
        keyBenefitsSummary: form.keyBenefitsSummary.trim() || null,
        requiresWrittenTest: form.requiresWrittenTest,
        requiresPracticalTest: form.requiresPracticalTest,
        // Without a pipeline the vacancy has no board and its applications cannot be moved through
        // stages at all, so this is asked for up front rather than left to be discovered later.
        recruitmentPipelineId: form.recruitmentPipelineId || null,
        isBlindScreeningEnabled: form.isBlindScreeningEnabled,
        autoShortlistMinScore: form.autoShortlistMinScore ? Number(form.autoShortlistMinScore) : null,
        autoShortlistRequireAllMandatory: form.autoShortlistRequireAllMandatory,
      }),
    onSuccess: (created) => {
      toast({
        title: 'Vacancy opened',
        description: `${created.vacancyNumber} saved as a draft.`,
      });
      router.push(`/hr/recruitment/vacancies/${created.id}`);
    },
    onError: (e: any) =>
      toast({ title: 'Could not open it', description: e?.message, variant: 'destructive' }),
  });

  if (!requisitionId) {
    return (
      <div className="p-6">
        <PageHeader title="Open a vacancy" backHref="/hr/recruitment/vacancies" />
        <EmptyState
          title="Start from a requisition"
          description="A vacancy is opened from an approved requisition, so it inherits the position and the audience. Open the requisition and use “Open a vacancy”."
          action={
            <Button onClick={() => router.push('/hr/recruitment/requisitions')}>
              Go to requisitions
            </Button>
          }
        />
      </div>
    );
  }

  if (requisition.isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const req = requisition.data;
  if (!req) {
    return (
      <div className="p-6">
        <EmptyState title="Requisition not found" description="It may have been removed." />
      </div>
    );
  }

  const notApproved = req.status !== 'Approved';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Open a vacancy"
        description={`${req.requisitionNumber} · ${req.positionTitle}`}
        backHref={`/hr/recruitment/requisitions/${requisitionId}`}
        actions={
          <Button onClick={() => create.mutate()} disabled={notApproved || create.isPending}>
            <Save className="mr-2 h-4 w-4" />
            {create.isPending ? 'Opening…' : 'Open vacancy'}
          </Button>
        }
      />

      {notApproved ? (
        <EmptyState
          title={`This requisition is ${humanizeEnum(req.status)}`}
          description="A vacancy can only be opened once the headcount has been approved."
        />
      ) : (
        <>
          <Alert>
            <Info className="h-4 w-4" />
            <AlertTitle>Inherited from the requisition</AlertTitle>
            <AlertDescription>
              Position <strong>{req.positionTitle}</strong>, unit{' '}
              <strong>{req.organizationUnitName ?? '—'}</strong>, open to{' '}
              <strong>
                {[req.allowInternalCandidates && 'internal', req.allowExternalCandidates && 'external']
                  .filter(Boolean)
                  .join(' and ') || 'nobody'}
              </strong>{' '}
              candidates. Those audience flags decide which adverts are raised when you publish.
            </AlertDescription>
          </Alert>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">The advert</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-2">
              <div className="space-y-1.5 md:col-span-2">
                <Label htmlFor="customAdvertTitle">Advert title</Label>
                <Input
                  id="customAdvertTitle"
                  value={form.customAdvertTitle}
                  onChange={(e) => setForm({ ...form, customAdvertTitle: e.target.value })}
                  placeholder={req.jobDescriptionTitle ?? req.positionTitle}
                />
                <p className="text-xs text-muted-foreground">
                  Leave blank to use the job description&apos;s title
                  {req.jobDescriptionTitle ? ` (“${req.jobDescriptionTitle}”)` : ''}.
                </p>
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="numberOfPositions">How many heads</Label>
                <Input
                  id="numberOfPositions"
                  type="number"
                  min={1}
                  value={form.numberOfPositions}
                  onChange={(e) =>
                    setForm({ ...form, numberOfPositions: Number(e.target.value) || 1 })
                  }
                />
              </div>

              <div className="space-y-1.5">
                <Label>Employment type</Label>
                <Select
                  value={form.employmentType}
                  onValueChange={(v) => setForm({ ...form, employmentType: v as EmploymentType })}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {EMPLOYMENT_TYPES.map((t) => (
                      <SelectItem key={t} value={t}>
                        {humanizeEnum(t)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-1.5">
                <Label>Work mode</Label>
                <Select
                  value={form.workMode}
                  onValueChange={(v) => setForm({ ...form, workMode: v as WorkMode })}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {WORK_MODES.map((m) => (
                      <SelectItem key={m} value={m}>
                        {humanizeEnum(m)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="requiredMinExperienceYears">Minimum experience (years)</Label>
                <Input
                  id="requiredMinExperienceYears"
                  type="number"
                  min={0}
                  value={form.requiredMinExperienceYears}
                  onChange={(e) =>
                    setForm({ ...form, requiredMinExperienceYears: e.target.value })
                  }
                />
              </div>

              <div className="space-y-1.5 md:col-span-2">
                <Label htmlFor="keyBenefitsSummary">Key benefits</Label>
                <Textarea
                  id="keyBenefitsSummary"
                  rows={3}
                  value={form.keyBenefitsSummary}
                  onChange={(e) => setForm({ ...form, keyBenefitsSummary: e.target.value })}
                />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">Dates and people</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-2">
              <div className="space-y-1.5">
                <Label htmlFor="applicationDeadline">Application deadline</Label>
                <Input
                  id="applicationDeadline"
                  type="date"
                  value={form.applicationDeadline}
                  onChange={(e) => setForm({ ...form, applicationDeadline: e.target.value })}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="shortlistingDeadline">Shortlisting deadline</Label>
                <Input
                  id="shortlistingDeadline"
                  type="date"
                  value={form.shortlistingDeadline}
                  onChange={(e) => setForm({ ...form, shortlistingDeadline: e.target.value })}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="targetStartDate">Target start date</Label>
                <Input
                  id="targetStartDate"
                  type="date"
                  value={form.targetStartDate}
                  onChange={(e) => setForm({ ...form, targetStartDate: e.target.value })}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="numberOfInterviewRounds">Interview rounds</Label>
                <Input
                  id="numberOfInterviewRounds"
                  type="number"
                  min={0}
                  value={form.numberOfInterviewRounds}
                  onChange={(e) =>
                    setForm({ ...form, numberOfInterviewRounds: Number(e.target.value) || 0 })
                  }
                />
              </div>
              <div className="space-y-1.5">
                <Label>Hiring manager</Label>
                <EmployeePicker
                  value={form.hiringManagerId}
                  initialLabel={form.hiringManagerName}
                  onChange={(id, label) =>
                    setForm({ ...form, hiringManagerId: id, hiringManagerName: label })
                  }
                />
              </div>
              <div className="space-y-1.5">
                <Label>Recruiter</Label>
                <EmployeePicker
                  value={form.recruiterId}
                  initialLabel={form.recruiterName}
                  onChange={(id, label) =>
                    setForm({ ...form, recruiterId: id, recruiterName: label })
                  }
                />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">Salary and assessment</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-3">
                <div className="space-y-1.5">
                  <Label htmlFor="salaryRangeMin">Salary from</Label>
                  <Input
                    id="salaryRangeMin"
                    type="number"
                    value={form.salaryRangeMin}
                    onChange={(e) => setForm({ ...form, salaryRangeMin: e.target.value })}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="salaryRangeMax">Salary to</Label>
                  <Input
                    id="salaryRangeMax"
                    type="number"
                    value={form.salaryRangeMax}
                    onChange={(e) => setForm({ ...form, salaryRangeMax: e.target.value })}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="salaryCurrencyCode">Currency</Label>
                  <Input
                    id="salaryCurrencyCode"
                    maxLength={3}
                    value={form.salaryCurrencyCode}
                    onChange={(e) =>
                      setForm({ ...form, salaryCurrencyCode: e.target.value.toUpperCase() })
                    }
                  />
                </div>
              </div>

              <div className="flex items-center gap-2">
                <Checkbox
                  id="isSalaryVisible"
                  checked={form.isSalaryVisible}
                  onCheckedChange={(c) => setForm({ ...form, isSalaryVisible: c === true })}
                />
                <Label htmlFor="isSalaryVisible" className="font-normal">
                  Show the salary range on the advert
                </Label>
              </div>

              <div className="flex items-center gap-2">
                <Checkbox
                  id="requiresWrittenTest"
                  checked={form.requiresWrittenTest}
                  onCheckedChange={(c) => setForm({ ...form, requiresWrittenTest: c === true })}
                />
                <Label htmlFor="requiresWrittenTest" className="font-normal">
                  Requires a written test
                </Label>
              </div>

              <div className="flex items-center gap-2">
                <Checkbox
                  id="requiresPracticalTest"
                  checked={form.requiresPracticalTest}
                  onCheckedChange={(c) => setForm({ ...form, requiresPracticalTest: c === true })}
                />
                <Label htmlFor="requiresPracticalTest" className="font-normal">
                  Requires a practical test
                </Label>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">Screening</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-1.5">
                <Label htmlFor="recruitmentPipelineId">Recruitment pipeline</Label>
                <Select
                  value={form.recruitmentPipelineId}
                  onValueChange={(v) => setForm({ ...form, recruitmentPipelineId: v })}
                >
                  <SelectTrigger id="recruitmentPipelineId">
                    <SelectValue placeholder="Select a pipeline" />
                  </SelectTrigger>
                  <SelectContent>
                    {(pipelines.data ?? []).map((p) => (
                      <SelectItem key={p.id} value={p.id}>
                        {p.name}
                        {p.isDefault ? ' (default)' : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  Without a pipeline this vacancy has no board, and its applications cannot be moved
                  through stages.
                </p>
              </div>

              <div className="flex items-center gap-2">
                <Checkbox
                  id="isBlindScreeningEnabled"
                  checked={form.isBlindScreeningEnabled}
                  onCheckedChange={(c) =>
                    setForm({ ...form, isBlindScreeningEnabled: c === true })
                  }
                />
                <Label htmlFor="isBlindScreeningEnabled" className="font-normal">
                  Enable blind screening — reviewers see no name, gender, age or contact details
                </Label>
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-1.5">
                  <Label htmlFor="autoShortlistMinScore">Auto-shortlist threshold</Label>
                  <Input
                    id="autoShortlistMinScore"
                    type="number"
                    min={1}
                    max={100}
                    value={form.autoShortlistMinScore}
                    onChange={(e) => setForm({ ...form, autoShortlistMinScore: e.target.value })}
                    placeholder="Optional"
                  />
                </div>
                <div className="flex items-end gap-2 pb-2">
                  <Checkbox
                    id="autoShortlistRequireAllMandatory"
                    checked={form.autoShortlistRequireAllMandatory}
                    onCheckedChange={(c) =>
                      setForm({ ...form, autoShortlistRequireAllMandatory: c === true })
                    }
                  />
                  <Label htmlFor="autoShortlistRequireAllMandatory" className="font-normal">
                    Require every mandatory criterion to pass
                  </Label>
                </div>
              </div>
            </CardContent>
          </Card>

          <div className="flex justify-end gap-2">
            <Button
              variant="outline"
              onClick={() => router.push(`/hr/recruitment/requisitions/${requisitionId}`)}
            >
              Cancel
            </Button>
            <Button onClick={() => create.mutate()} disabled={create.isPending}>
              <Save className="mr-2 h-4 w-4" />
              {create.isPending ? 'Opening…' : 'Open vacancy'}
            </Button>
          </div>
        </>
      )}
    </div>
  );
}
