'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Banknote,
  Briefcase,
  CalendarClock,
  CheckCircle2,
  ClipboardCheck,
  MapPin,
  Users,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { useToast } from '@/hooks/use-toast';
import { formatDate } from '@/lib/hr/attendance-format';
import { jobVacancyService } from '@/services/hr/recruitment.service';
import { jobApplicationService } from '@/services/hr/recruitment-pipeline.service';
import type { PublicVacancy } from '@/types/hr/recruitment';

/**
 * The internal job board (area 25 slice 13b).
 *
 * ⚠ **`allowInternalCandidates` is the SERVER's rule now.** This screen used to filter it in the
 * browser — `vacancies.filter(v => v.allowInternalCandidates)` — while the API happily served the
 * excluded vacancies and `apply-internal` happily accepted an application against one. A rule that
 * lives only in the client is not a rule; the field is no longer even on the payload.
 *
 * ⚠ **The payload is `PublicVacancy`, not `JobVacancy`.** See the type's note: the full record
 * carries the scoring thresholds and boost points an applicant is about to be judged on, and used
 * to reach this screen. Salary is null unless the vacancy says it is visible — render the absence,
 * never a zero.
 */

const BLANK = { yearsOfExperience: '', availableFrom: '', coverLetter: '' };
type ApplyForm = typeof BLANK;

function salaryLine(v: PublicVacancy) {
  if (!v.isSalaryVisible || v.salaryRangeMin == null) return null;
  const cur = v.salaryCurrencyCode ? `${v.salaryCurrencyCode} ` : '';
  const min = v.salaryRangeMin.toLocaleString();
  const max = v.salaryRangeMax != null ? ` – ${v.salaryRangeMax.toLocaleString()}` : '';
  return `${cur}${min}${max}`;
}

export default function InternalJobBoardPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [applyTo, setApplyTo] = useState<PublicVacancy | null>(null);
  const [form, setForm] = useState<ApplyForm>(BLANK);

  const vacancies = useQuery({
    queryKey: ['me', 'jobs', 'published'],
    queryFn: () => jobVacancyService.getPublished(),
  });

  const myApplications = useQuery({
    queryKey: ['me', 'jobs', 'my-applications'],
    queryFn: () => jobApplicationService.getMyApplications(),
  });

  const fail = (e: any) =>
    toast({ title: 'Refused', description: e?.message || 'Action failed.', variant: 'destructive' });

  const done = async (title: string, description?: string) => {
    await queryClient.invalidateQueries({ queryKey: ['me', 'jobs', 'my-applications'] });
    setApplyTo(null);
    setForm(BLANK);
    toast({ title, description });
  };

  const payload = (vacancyId: string) => ({
    vacancyId,
    yearsOfExperience: form.yearsOfExperience ? Number(form.yearsOfExperience) : null,
    availableFrom: form.availableFrom || null,
    coverLetter: form.coverLetter.trim() || null,
  });

  const applyNow = useMutation({
    mutationFn: () => {
      if (!applyTo) throw new Error('No vacancy selected.');
      return jobApplicationService.applyInternal(payload(applyTo.id));
    },
    onSuccess: () => done('Application submitted'),
    onError: fail,
  });

  const saveDraft = useMutation({
    mutationFn: () => {
      if (!applyTo) throw new Error('No vacancy selected.');
      return jobApplicationService.saveInternalDraft(payload(applyTo.id));
    },
    onSuccess: () => done('Draft saved', 'Finish it any time from My applications.'),
    onError: fail,
  });

  const byVacancy = new Map(
    (myApplications.data ?? []).map((a) => [a.jobVacancyId, a] as const),
  );
  const roles = vacancies.data ?? [];
  const busy = applyNow.isPending || saveDraft.isPending;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Internal job board"
        description="Roles open to internal candidates, and the applications you have already made."
        backHref="/me"
        actions={
          <Button variant="outline" asChild>
            <Link href="/me/jobs/applications">
              <ClipboardCheck className="mr-2 h-4 w-4" />
              My applications
              {byVacancy.size > 0 && <Badge variant="secondary" className="ml-2">{byVacancy.size}</Badge>}
            </Link>
          </Button>
        }
      />

      {vacancies.isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-40" />
          <Skeleton className="h-40" />
        </div>
      ) : vacancies.isError ? (
        <p className="text-sm text-muted-foreground">
          The job board could not be loaded right now. Try again in a moment.
        </p>
      ) : roles.length === 0 ? (
        <EmptyState
          icon={Briefcase}
          title="No open roles right now"
          description="Vacancies open to internal candidates appear here while they are published and still accepting applications."
        />
      ) : (
        <div className="space-y-4">
          {roles.map((v) => {
            const applied = byVacancy.get(v.id);
            const pay = salaryLine(v);
            return (
              <Card key={v.id}>
                <CardContent className="space-y-4 p-5">
                  <div className="flex flex-wrap items-start gap-3">
                    <div className="min-w-0 flex-1">
                      <h2 className="text-base font-semibold">{v.jobTitle || v.positionTitle}</h2>
                      <p className="font-mono text-xs text-muted-foreground">{v.vacancyNumber}</p>
                    </div>
                    {applied ? (
                      <Button variant="outline" size="sm" asChild>
                        <Link href={`/me/jobs/applications/${applied.id}`}>
                          {applied.status === 'Draft' ? (
                            <>Continue draft</>
                          ) : (
                            <>
                              <CheckCircle2 className="mr-1.5 h-3.5 w-3.5 text-green-600" />
                              Applied · {applied.statusName ?? applied.status}
                            </>
                          )}
                        </Link>
                      </Button>
                    ) : (
                      <Button size="sm" onClick={() => { setApplyTo(v); setForm(BLANK); }}>
                        Apply
                      </Button>
                    )}
                  </div>

                  <div className="flex flex-wrap gap-x-5 gap-y-2 text-sm text-muted-foreground">
                    {v.departmentName && (
                      <span className="flex items-center gap-1.5">
                        <Briefcase className="h-3.5 w-3.5" /> {v.departmentName}
                      </span>
                    )}
                    {v.locationName && (
                      <span className="flex items-center gap-1.5">
                        <MapPin className="h-3.5 w-3.5" /> {v.locationName}
                      </span>
                    )}
                    <span className="flex items-center gap-1.5">
                      <Users className="h-3.5 w-3.5" />
                      {v.numberOfPositions} {v.numberOfPositions === 1 ? 'position' : 'positions'}
                    </span>
                    {/* Only when the vacancy says the salary may be shown — the server nulls it otherwise. */}
                    {pay && (
                      <span className="flex items-center gap-1.5">
                        <Banknote className="h-3.5 w-3.5" /> {pay}
                      </span>
                    )}
                    {v.applicationDeadline && (
                      <span className="flex items-center gap-1.5">
                        <CalendarClock className="h-3.5 w-3.5" /> closes {formatDate(v.applicationDeadline)}
                      </span>
                    )}
                  </div>

                  <div className="flex flex-wrap gap-2">
                    <Badge variant="secondary">{v.employmentTypeName}</Badge>
                    <Badge variant="secondary">{v.workModeName}</Badge>
                    {v.requiredMinExperienceYears != null && (
                      <Badge variant="outline">{v.requiredMinExperienceYears}+ years</Badge>
                    )}
                    {v.requiresWrittenTest && <Badge variant="outline">Written test</Badge>}
                    {v.requiresPracticalTest && <Badge variant="outline">Practical test</Badge>}
                  </div>

                  {/* The advert body. The board could not show this at all before 13b — the full
                      vacancy record it used to serve has no job description on it. */}
                  {v.jobDescription && (
                    <p className="whitespace-pre-line border-t pt-3 text-sm leading-relaxed">
                      {v.jobDescription}
                    </p>
                  )}
                  {v.keyBenefitsSummary && (
                    <p className="text-sm text-muted-foreground">{v.keyBenefitsSummary}</p>
                  )}
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}

      {/* ── Apply ──────────────────────────────────────────────────────────── */}
      <Dialog open={!!applyTo} onOpenChange={(o) => !o && setApplyTo(null)}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Apply for {applyTo?.jobTitle || applyTo?.positionTitle}</DialogTitle>
            <DialogDescription>
              Your name, staff number and work email come from your employee record. Add anything
              that supports your application.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label htmlFor="yrs">Relevant experience (years)</Label>
                <Input
                  id="yrs"
                  type="number"
                  min={0}
                  value={form.yearsOfExperience}
                  onChange={(e) => setForm((f) => ({ ...f, yearsOfExperience: e.target.value }))}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="from">Available from</Label>
                <Input
                  id="from"
                  type="date"
                  value={form.availableFrom}
                  onChange={(e) => setForm((f) => ({ ...f, availableFrom: e.target.value }))}
                />
              </div>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="cover">Supporting statement</Label>
              <Textarea
                id="cover"
                rows={6}
                value={form.coverLetter}
                onChange={(e) => setForm((f) => ({ ...f, coverLetter: e.target.value }))}
                placeholder="Why you, for this role."
              />
            </div>
          </div>

          <DialogFooter className="gap-2 sm:justify-between">
            <Button variant="outline" disabled={busy} onClick={() => saveDraft.mutate()}>
              Save as draft
            </Button>
            <Button disabled={busy} onClick={() => applyNow.mutate()}>
              Submit application
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
