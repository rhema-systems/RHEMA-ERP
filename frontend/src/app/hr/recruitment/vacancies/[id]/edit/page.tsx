'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Info, Loader2, Save, TriangleAlert } from 'lucide-react';
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
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import { jobVacancyService } from '@/services/hr/recruitment.service';
import { recruitmentPipelineService } from '@/services/hr/recruitment-pipeline.service';
import {
  EMPLOYMENT_TYPES,
  WORK_MODES,
  type EmploymentType,
  type JobVacancy,
  type WorkMode,
} from '@/types/hr/recruitment';

/** `2026-09-15T00:00:00Z` → `2026-09-15`, which is what `<input type="date">` wants. */
function toDateInput(value?: string | null): string {
  if (!value) return '';
  return value.slice(0, 10);
}

/**
 * Editing a vacancy after it has been opened.
 *
 * ⚠ **This page did not exist until 2026-09-15 (G-5.1), and its absence was the module's
 * single worst gap.** `PUT /api/job-vacancies/{id}` had always existed, and so had
 * `jobVacancyService.update()` — nothing called either. There was no `[id]/edit` route and the
 * detail page had no Edit button, so once a vacancy was created these were fixed for its entire
 * life: advert title, heads, employment type, work mode, minimum experience, key benefits,
 * **application deadline**, **shortlisting deadline**, target start date, interview rounds, hiring
 * manager, recruiter, salary range and visibility, test requirements, **recruitment pipeline**,
 * **blind screening** and the auto-shortlist threshold.
 *
 * Three screens gave instructions that depended on this page and were therefore false:
 *
 * | Screen | Said | Reality before this page |
 * |---|---|---|
 * | Pipeline (none assigned) | *"Assign one on the vacancy, then come back."* | could not be assigned after creation |
 * | Screening → Blind screening | *"Switch it on in the vacancy's settings."* | there were no vacancy settings |
 * | Screening (deadline passed) | *"…until HR extends the deadline."* | HR could not extend it |
 *
 * The pipeline case was the worst of the three: a vacancy created without a pipeline could never
 * have one, so its applications could never be moved through stages **at all**.
 *
 * ⚠ **Every field is sent on every save.** `UpdateJobVacancy` is a whole-record payload and the
 * server's `UpdateEntity` assigns each property unconditionally, so a field omitted here is a field
 * cleared on the vacancy. That is why the form seeds from the loaded record rather than starting
 * empty, and why there is no "only send what changed" optimisation — it is the replace-set trap
 * that has bitten this repo before.
 */
export default function EditVacancyPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyPermission } = useAuth();
  const canEdit = hasAnyPermission(['HR.Recruitment.Write', 'HR.Recruitment.Admin']);

  const vacancy = useQuery({
    queryKey: ['hr', 'vacancy-detail', id],
    queryFn: () => jobVacancyService.getById(id),
    enabled: !!id,
  });

  const pipelines = useQuery({
    queryKey: ['hr', 'recruitment-pipelines'],
    queryFn: () => recruitmentPipelineService.getAll(),
    // An inactive pipeline stays attached where it already is, but should not be newly picked.
    // The vacancy's current one is added back below so editing something else cannot silently
    // detach a pipeline that has since been retired.
    select: (all) => all.filter((p) => p.isActive),
  });

  const [form, setForm] = useState<{
    customAdvertTitle: string;
    numberOfPositions: number;
    hiringManagerId: string | null;
    hiringManagerName: string | null;
    recruiterId: string | null;
    recruiterName: string | null;
    applicationDeadline: string;
    shortlistingDeadline: string;
    numberOfInterviewRounds: number;
    targetStartDate: string;
    employmentType: EmploymentType;
    workMode: WorkMode;
    isSalaryVisible: boolean;
    salaryRangeMin: string;
    salaryRangeMax: string;
    salaryCurrencyCode: string;
    requiredMinExperienceYears: string;
    keyBenefitsSummary: string;
    requiresWrittenTest: boolean;
    requiresPracticalTest: boolean;
    recruitmentPipelineId: string;
    isBlindScreeningEnabled: boolean;
    autoShortlistMinScore: string;
    autoShortlistRequireAllMandatory: boolean;
    testScoreWeight: string;
    internalCandidateBoostPoints: string;
  } | null>(null);

  // Seed once, from the record. Deliberately not a useEffect: this mirrors the `seeded` pattern the
  // new-vacancy page uses, and re-seeding on every refetch would discard a half-typed edit.
  if (vacancy.data && form === null) {
    const v: JobVacancy = vacancy.data;
    setForm({
      customAdvertTitle: v.customAdvertTitle ?? '',
      numberOfPositions: v.numberOfPositions ?? 1,
      hiringManagerId: v.hiringManagerId ?? null,
      hiringManagerName: v.hiringManagerName ?? null,
      recruiterId: v.recruiterId ?? null,
      recruiterName: v.recruiterName ?? null,
      applicationDeadline: toDateInput(v.applicationDeadline),
      shortlistingDeadline: toDateInput(v.shortlistingDeadline),
      numberOfInterviewRounds: v.numberOfInterviewRounds ?? 1,
      targetStartDate: toDateInput(v.targetStartDate),
      employmentType: (v.employmentType ?? 'Permanent') as EmploymentType,
      workMode: (v.workMode ?? 'OnSite') as WorkMode,
      isSalaryVisible: v.isSalaryVisible ?? false,
      salaryRangeMin: v.salaryRangeMin != null ? String(v.salaryRangeMin) : '',
      salaryRangeMax: v.salaryRangeMax != null ? String(v.salaryRangeMax) : '',
      salaryCurrencyCode: v.salaryCurrencyCode ?? 'GHS',
      requiredMinExperienceYears:
        v.requiredMinExperienceYears != null ? String(v.requiredMinExperienceYears) : '',
      keyBenefitsSummary: v.keyBenefitsSummary ?? '',
      requiresWrittenTest: v.requiresWrittenTest ?? false,
      requiresPracticalTest: v.requiresPracticalTest ?? false,
      recruitmentPipelineId: v.recruitmentPipelineId ?? '',
      isBlindScreeningEnabled: v.isBlindScreeningEnabled ?? false,
      autoShortlistMinScore:
        v.autoShortlistMinScore != null ? String(v.autoShortlistMinScore) : '',
      autoShortlistRequireAllMandatory: v.autoShortlistRequireAllMandatory ?? true,
      testScoreWeight: v.testScoreWeight ? String(v.testScoreWeight) : '',
      internalCandidateBoostPoints: v.internalCandidateBoostPoints
        ? String(v.internalCandidateBoostPoints)
        : '',
    });
  }

  const save = useMutation({
    mutationFn: () => {
      if (!form) throw new Error('Nothing to save.');
      return jobVacancyService.update(id, {
        id,
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
        recruitmentPipelineId: form.recruitmentPipelineId || null,
        isBlindScreeningEnabled: form.isBlindScreeningEnabled,
        autoShortlistMinScore: form.autoShortlistMinScore
          ? Number(form.autoShortlistMinScore)
          : null,
        autoShortlistRequireAllMandatory: form.autoShortlistRequireAllMandatory,
        testScoreWeight: Number(form.testScoreWeight) || 0,
        internalCandidateBoostPoints: Number(form.internalCandidateBoostPoints) || 0,
      });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'vacancy-detail', id] });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'vacancies'] });
      toast({ title: 'Vacancy updated' });
      router.push(`/hr/recruitment/vacancies/${id}`);
    },
    onError: (e: any) =>
      toast({
        title: 'Could not save the changes',
        description: e?.body?.message ?? e?.message,
        variant: 'destructive',
      }),
  });

  if (vacancy.isLoading || (vacancy.data && !form)) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const v = vacancy.data;
  if (!v || !form) {
    return (
      <div className="p-6">
        <PageHeader title="Edit vacancy" backHref="/hr/recruitment/vacancies" />
        <EmptyState
          title="Vacancy not found"
          description="It may have been removed, or the link may be wrong."
        />
      </div>
    );
  }

  if (!canEdit) {
    return (
      <div className="p-6">
        <PageHeader title="Edit vacancy" backHref={`/hr/recruitment/vacancies/${id}`} />
        <EmptyState
          title="You cannot edit this vacancy"
          description="Editing a vacancy needs recruitment write access."
        />
      </div>
    );
  }

  // The server refuses a write to a terminal vacancy ("A closed or filled vacancy cannot be
  // edited"), so say so here rather than let a full form be filled in and then rejected.
  const terminal = v.vacancyStatus === 'Filled' || v.vacancyStatus === 'Cancelled';
  if (terminal) {
    return (
      <div className="p-6">
        <PageHeader
          title="Edit vacancy"
          description={`${v.vacancyNumber} · ${humanizeEnum(v.vacancyStatus)}`}
          backHref={`/hr/recruitment/vacancies/${id}`}
        />
        <EmptyState
          title={`This vacancy is ${humanizeEnum(v.vacancyStatus)}`}
          description="A filled or cancelled vacancy is a closed record and can no longer be edited."
        />
      </div>
    );
  }

  const published = v.vacancyStatus === 'Published';
  const currentPipelineMissing =
    !!form.recruitmentPipelineId &&
    !(pipelines.data ?? []).some((p) => p.id === form.recruitmentPipelineId);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Edit vacancy"
        description={`${v.vacancyNumber} · ${v.positionTitle ?? ''}`}
        backHref={`/hr/recruitment/vacancies/${id}`}
        actions={
          <Button onClick={() => save.mutate()} disabled={save.isPending}>
            <Save className="mr-2 h-4 w-4" />
            {save.isPending ? 'Saving…' : 'Save changes'}
          </Button>
        }
      />

      <Alert>
        <Info className="h-4 w-4" />
        <AlertTitle>What is not edited here</AlertTitle>
        <AlertDescription>
          The position, the organisation unit and the internal/external audience come from the
          requisition and are fixed for the vacancy&apos;s life. Status changes, shortlisting
          criteria, stage owners, adverts and attachments are all managed on the vacancy page
          itself.
        </AlertDescription>
      </Alert>

      {published && (
        <Alert>
          <TriangleAlert className="h-4 w-4" />
          <AlertTitle>This vacancy is published</AlertTitle>
          <AlertDescription>
            Candidates can see it now. Changing the advert title, the salary range or the
            application deadline changes what they are being told — the adverts already raised keep
            their own text until you edit them on the Adverts tab.
          </AlertDescription>
        </Alert>
      )}

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
              placeholder={v.positionTitle ?? ''}
            />
            <p className="text-xs text-muted-foreground">
              Leave blank to use the position&apos;s own title.
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
              onValueChange={(val) => setForm({ ...form, employmentType: val as EmploymentType })}
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
              onValueChange={(val) => setForm({ ...form, workMode: val as WorkMode })}
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
              onChange={(e) => setForm({ ...form, requiredMinExperienceYears: e.target.value })}
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
            <p className="text-xs text-muted-foreground">
              Extending this is what the screening page means by &ldquo;until HR extends the
              deadline&rdquo;.
            </p>
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
              onChange={(pickedId, label) =>
                setForm({ ...form, hiringManagerId: pickedId, hiringManagerName: label })
              }
            />
          </div>
          <div className="space-y-1.5">
            <Label>Recruiter</Label>
            <EmployeePicker
              value={form.recruiterId}
              initialLabel={form.recruiterName}
              onChange={(pickedId, label) =>
                setForm({ ...form, recruiterId: pickedId, recruiterName: label })
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
              onValueChange={(val) => setForm({ ...form, recruitmentPipelineId: val })}
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
                {/* The vacancy's own pipeline, if it has since been deactivated. Without this the
                    Select would show an empty trigger and the next save would detach it. */}
                {currentPipelineMissing && (
                  <SelectItem value={form.recruitmentPipelineId}>
                    Current pipeline (no longer active)
                  </SelectItem>
                )}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              Assigning one here is what the pipeline board means by &ldquo;assign one on the
              vacancy, then come back&rdquo;. Without a pipeline this vacancy has no board and its
              applications cannot be moved through stages at all.
            </p>
          </div>

          <div className="flex items-center gap-2">
            <Checkbox
              id="isBlindScreeningEnabled"
              checked={form.isBlindScreeningEnabled}
              onCheckedChange={(c) => setForm({ ...form, isBlindScreeningEnabled: c === true })}
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

          {/* G-5.2 — see the new-vacancy page for why these two were unreachable until now. */}
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="testScoreWeight">Weight given to test scores (%)</Label>
              <Input
                id="testScoreWeight"
                type="number"
                min={0}
                max={100}
                value={form.testScoreWeight}
                onChange={(e) => setForm({ ...form, testScoreWeight: e.target.value })}
                placeholder="0 — ignore test scores"
              />
              <p className="text-xs text-muted-foreground">
                Blends written and practical test results into the shortlist score. At 0 the tests
                are still recorded but count for nothing.
              </p>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="internalCandidateBoostPoints">Internal candidate bonus</Label>
              <Input
                id="internalCandidateBoostPoints"
                type="number"
                min={0}
                max={20}
                value={form.internalCandidateBoostPoints}
                onChange={(e) =>
                  setForm({ ...form, internalCandidateBoostPoints: e.target.value })
                }
                placeholder="0 — no preference"
              />
              <p className="text-xs text-muted-foreground">
                Flat points added to an existing employee&apos;s score, up to 20.
              </p>
            </div>
          </div>
        </CardContent>
      </Card>

      <div className="flex justify-end gap-2">
        <Button variant="outline" onClick={() => router.push(`/hr/recruitment/vacancies/${id}`)}>
          Cancel
        </Button>
        <Button onClick={() => save.mutate()} disabled={save.isPending}>
          <Save className="mr-2 h-4 w-4" />
          {save.isPending ? 'Saving…' : 'Save changes'}
        </Button>
      </div>
    </div>
  );
}
