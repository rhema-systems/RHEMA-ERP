'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Briefcase, CheckCircle2, Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { formatDate } from '@/lib/hr/attendance-format';
import { jobVacancyService } from '@/services/hr/recruitment.service';
import { jobApplicationService } from '@/services/hr/recruitment-pipeline.service';
import type { JobVacancy } from '@/types/hr/recruitment';
import type { JobApplicationSummary } from '@/types/hr/recruitment-pipeline';

interface ApplyForm {
  yearsOfExperience: string;
  availableFrom: string;
  coverLetter: string;
}

const BLANK_FORM: ApplyForm = { yearsOfExperience: '', availableFrom: '', coverLetter: '' };

function toPayload(vacancyId: string, form: ApplyForm) {
  return {
    vacancyId,
    yearsOfExperience: form.yearsOfExperience ? Number(form.yearsOfExperience) : null,
    availableFrom: form.availableFrom || null,
    coverLetter: form.coverLetter.trim() || null,
  };
}

/**
 * The internal job board — every employee's own view, not HR's.
 *
 * Backed by `api/job-applications`' internal endpoints, the one part of that controller left
 * open to any authenticated employee (see its class doc comment): each takes the employee from
 * the token and acts only on their own applications. Lives under `/hr/...` rather than a separate
 * portal — same placement as `/hr/recruitment/my-panel` for interview panelists, and for the same
 * reason: this is the main app, not the external candidate portal.
 */
export default function JobBoardPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [applyTo, setApplyTo] = useState<JobVacancy | null>(null);
  const [form, setForm] = useState<ApplyForm>(BLANK_FORM);
  const [continuing, setContinuing] = useState<JobApplicationSummary | null>(null);
  const [continueForm, setContinueForm] = useState<ApplyForm>(BLANK_FORM);

  const vacancies = useQuery({
    queryKey: ['hr', 'job-board', 'published'],
    queryFn: () => jobVacancyService.getPublished(),
  });

  const myApplications = useQuery({
    queryKey: ['hr', 'job-board', 'my-applications'],
    queryFn: () => jobApplicationService.getMyApplications(),
  });

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['hr', 'job-board', 'my-applications'] }),
    ]);

  const fail = (e: any) =>
    toast({ title: 'Refused', description: e?.message || 'Action failed.', variant: 'destructive' });

  const applyNow = useMutation({
    mutationFn: () => {
      if (!applyTo) throw new Error('No vacancy selected.');
      return jobApplicationService.applyInternal(toPayload(applyTo.id, form));
    },
    onSuccess: async () => {
      await refresh();
      setApplyTo(null);
      setForm(BLANK_FORM);
      toast({ title: 'Application submitted' });
    },
    onError: fail,
  });

  const saveDraft = useMutation({
    mutationFn: () => {
      if (!applyTo) throw new Error('No vacancy selected.');
      return jobApplicationService.saveInternalDraft(toPayload(applyTo.id, form));
    },
    onSuccess: async () => {
      await refresh();
      setApplyTo(null);
      setForm(BLANK_FORM);
      toast({ title: 'Draft saved', description: 'Continue it any time from My applications.' });
    },
    onError: fail,
  });

  const submitDraft = useMutation({
    mutationFn: () => {
      if (!continuing) throw new Error('No draft selected.');
      return jobApplicationService.submitInternalDraft(continuing.id, {
        yearsOfExperience: continueForm.yearsOfExperience ? Number(continueForm.yearsOfExperience) : null,
        availableFrom: continueForm.availableFrom || null,
        coverLetter: continueForm.coverLetter.trim() || null,
      });
    },
    onSuccess: async () => {
      await refresh();
      setContinuing(null);
      setContinueForm(BLANK_FORM);
      toast({ title: 'Application submitted' });
    },
    onError: fail,
  });

  const appliedVacancyIds = new Set((myApplications.data ?? []).map((a) => a.jobVacancyId));
  const eligible = (vacancies.data ?? []).filter((v) => v.allowInternalCandidates);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Internal job board"
        description="Open roles you can apply for, and the applications you have already made."
        backHref="/hr/recruitment"
      />

      <Tabs defaultValue="open">
        <TabsList>
          <TabsTrigger value="open">Open roles</TabsTrigger>
          <TabsTrigger value="mine">My applications</TabsTrigger>
        </TabsList>

        <TabsContent value="open" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {vacancies.isLoading ? (
                <div className="flex items-center justify-center py-16">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : eligible.length === 0 ? (
                <div className="py-10">
                  <EmptyState
                    icon={Briefcase}
                    title="No open roles right now"
                    description="Vacancies open to internal candidates appear here while published."
                  />
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Role</TableHead>
                      <TableHead>Organisation unit</TableHead>
                      <TableHead>Deadline</TableHead>
                      <TableHead className="w-40" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {eligible.map((v) => {
                      const applied = appliedVacancyIds.has(v.id);
                      return (
                        <TableRow key={v.id}>
                          <TableCell className="font-medium">{v.jobTitle || v.positionTitle}</TableCell>
                          <TableCell className="text-sm text-muted-foreground">{v.orgUnitName ?? '—'}</TableCell>
                          <TableCell className="text-sm">{formatDate(v.applicationDeadline)}</TableCell>
                          <TableCell className="text-right">
                            {applied ? (
                              <Badge variant="secondary" className="gap-1">
                                <CheckCircle2 className="h-3 w-3" /> Already applied
                              </Badge>
                            ) : (
                              <Button size="sm" onClick={() => setApplyTo(v)}>
                                Apply
                              </Button>
                            )}
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="mine" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {myApplications.isLoading ? (
                <div className="flex items-center justify-center py-16">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : (myApplications.data ?? []).length === 0 ? (
                <div className="py-10">
                  <EmptyState
                    title="No applications yet"
                    description="Applications you make from the internal job board appear here."
                  />
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Reference</TableHead>
                      <TableHead>Vacancy</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Applied</TableHead>
                      <TableHead className="w-32" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(myApplications.data ?? []).map((a) => (
                      <TableRow key={a.id}>
                        <TableCell className="font-mono text-xs">{a.applicationNumber}</TableCell>
                        <TableCell>{a.jobTitle}</TableCell>
                        <TableCell>
                          <StatusBadge status={a.status} />
                        </TableCell>
                        <TableCell className="text-sm">{formatDate(a.applicationDate)}</TableCell>
                        <TableCell className="text-right">
                          {a.status === 'Draft' && (
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => {
                                setContinueForm(BLANK_FORM);
                                setContinuing(a);
                              }}
                            >
                              Continue
                            </Button>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <Dialog open={applyTo !== null} onOpenChange={(o) => !o && setApplyTo(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Apply — {applyTo?.jobTitle || applyTo?.positionTitle}</DialogTitle>
            <DialogDescription>
              Save as a draft to finish later, or submit now. Both are visible to HR once submitted.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2">
            <div className="space-y-1.5">
              <Label htmlFor="yoe">Years of relevant experience</Label>
              <Input
                id="yoe"
                type="number"
                min={0}
                max={60}
                value={form.yearsOfExperience}
                onChange={(e) => setForm({ ...form, yearsOfExperience: e.target.value })}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="availableFrom">Available from</Label>
              <Input
                id="availableFrom"
                type="date"
                value={form.availableFrom}
                onChange={(e) => setForm({ ...form, availableFrom: e.target.value })}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="coverLetter">Cover letter</Label>
              <Textarea
                id="coverLetter"
                rows={5}
                value={form.coverLetter}
                onChange={(e) => setForm({ ...form, coverLetter: e.target.value })}
              />
            </div>
          </div>
          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="outline" onClick={() => setApplyTo(null)}>
              Cancel
            </Button>
            <Button
              variant="secondary"
              disabled={saveDraft.isPending || applyNow.isPending}
              onClick={() => saveDraft.mutate()}
            >
              {saveDraft.isPending ? 'Saving…' : 'Save as draft'}
            </Button>
            <Button disabled={saveDraft.isPending || applyNow.isPending} onClick={() => applyNow.mutate()}>
              {applyNow.isPending ? 'Submitting…' : 'Submit application'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={continuing !== null} onOpenChange={(o) => !o && setContinuing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Submit draft — {continuing?.jobTitle}</DialogTitle>
            <DialogDescription>
              Leave a field blank to keep what you saved earlier; submitting moves this out of Draft.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2">
            <div className="space-y-1.5">
              <Label htmlFor="cyoe">Years of relevant experience</Label>
              <Input
                id="cyoe"
                type="number"
                min={0}
                max={60}
                value={continueForm.yearsOfExperience}
                onChange={(e) => setContinueForm({ ...continueForm, yearsOfExperience: e.target.value })}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="cavailableFrom">Available from</Label>
              <Input
                id="cavailableFrom"
                type="date"
                value={continueForm.availableFrom}
                onChange={(e) => setContinueForm({ ...continueForm, availableFrom: e.target.value })}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="ccoverLetter">Cover letter</Label>
              <Textarea
                id="ccoverLetter"
                rows={5}
                value={continueForm.coverLetter}
                onChange={(e) => setContinueForm({ ...continueForm, coverLetter: e.target.value })}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setContinuing(null)}>
              Cancel
            </Button>
            <Button disabled={submitDraft.isPending} onClick={() => submitDraft.mutate()}>
              {submitDraft.isPending ? 'Submitting…' : 'Submit application'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
