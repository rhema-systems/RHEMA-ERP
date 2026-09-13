'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import { jobPostingService } from '@/services/hr/recruitment.service';
import { jobApplicationService, jobCandidateService } from '@/services/hr/recruitment-pipeline.service';
import { APPLICATION_SOURCES, type ApplicationSource, type JobCandidate } from '@/types/hr/recruitment-pipeline';

const NONE = '__none__';

/**
 * HR records an application that did not arrive through the careers site (round 3, lane A;
 * register row R-4): a walk-in, an agency's submission, a referral. The source is typed by hand
 * and the advert it came through, if any, is one of the vacancy's own — the server refuses any
 * other. `POST api/job-applications` existed with no caller until this dialog.
 */
export function RecordApplicationDialog({
  open,
  onOpenChange,
  vacancies,
  defaultVacancyId,
  onRecorded,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  vacancies: { id: string; jobTitle?: string | null; vacancyNumber: string }[];
  defaultVacancyId?: string | null;
  onRecorded?: (applicationId: string) => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [vacancyId, setVacancyId] = useState(defaultVacancyId ?? '');
  const [email, setEmail] = useState('');
  const [lookupEmail, setLookupEmail] = useState('');
  const [candidate, setCandidate] = useState<JobCandidate | null>(null);
  const [source, setSource] = useState<ApplicationSource>('WalkIn');
  const [postingId, setPostingId] = useState('');
  const [years, setYears] = useState('');
  const [availableFrom, setAvailableFrom] = useState('');
  const [coverLetter, setCoverLetter] = useState('');

  useEffect(() => {
    if (open) setVacancyId(defaultVacancyId ?? '');
  }, [open, defaultVacancyId]);

  const lookup = useQuery({
    queryKey: ['hr', 'candidate-by-email', lookupEmail],
    queryFn: () => jobCandidateService.getByEmail(lookupEmail),
    enabled: lookupEmail.length > 0,
  });
  useEffect(() => {
    if (lookup.isSuccess) setCandidate(lookup.data ?? null);
  }, [lookup.isSuccess, lookup.data]);

  const postings = useQuery({
    queryKey: ['hr', 'postings', 'vacancy', vacancyId],
    queryFn: () => jobPostingService.getByVacancy(vacancyId),
    enabled: !!vacancyId,
  });
  // A posting picked for one vacancy cannot ride along to another.
  useEffect(() => setPostingId(''), [vacancyId]);

  const record = useMutation({
    mutationFn: () =>
      jobApplicationService.create({
        jobVacancyId: vacancyId,
        jobCandidateId: candidate!.id,
        source,
        jobPostingId: postingId || null,
        yearsOfExperience: years === '' ? null : Number(years),
        availableFrom: availableFrom || null,
        coverLetter: coverLetter.trim() || null,
      }),
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'applications'] });
      toast({ title: 'Application recorded', description: created.applicationNumber });
      onOpenChange(false);
      setCandidate(null);
      setEmail('');
      setLookupEmail('');
      setCoverLetter('');
      setYears('');
      setAvailableFrom('');
      onRecorded?.(created.id);
    },
    onError: (e: any) =>
      toast({ title: 'Could not record it', description: e?.data?.message ?? e?.message, variant: 'destructive' }),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Record an application</DialogTitle>
          <DialogDescription>
            For an applicant who did not apply through the careers site — a walk-in, an agency
            submission, a referral. The candidate must already exist; create them first if not.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 py-2">
          <div className="space-y-1.5">
            <Label htmlFor="record-vacancy">Vacancy</Label>
            <Select value={vacancyId || NONE} onValueChange={(v) => setVacancyId(v === NONE ? '' : v)}>
              <SelectTrigger id="record-vacancy">
                <SelectValue placeholder="Choose the vacancy" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>Choose the vacancy</SelectItem>
                {vacancies.map((v) => (
                  <SelectItem key={v.id} value={v.id}>
                    {v.jobTitle || v.vacancyNumber}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="record-email">Candidate (by email)</Label>
            <div className="flex gap-2">
              <Input
                id="record-email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && setLookupEmail(email.trim())}
                placeholder="candidate@example.com"
              />
              <Button type="button" variant="secondary" onClick={() => setLookupEmail(email.trim())} disabled={!email.trim()}>
                {lookup.isFetching ? <Loader2 className="h-4 w-4 animate-spin" /> : <Search className="h-4 w-4" />}
              </Button>
            </div>
            {lookupEmail && !lookup.isFetching && (
              <p className="text-xs text-muted-foreground">
                {candidate ? `${candidate.fullName} · ${candidate.candidateNumber}` : 'No candidate has that email — create the candidate first.'}
              </p>
            )}
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="record-source">How they arrived</Label>
              <Select value={source} onValueChange={(v) => setSource(v as ApplicationSource)}>
                <SelectTrigger id="record-source">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {APPLICATION_SOURCES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {humanizeEnum(s)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="record-posting">Through which advert</Label>
              <Select value={postingId || NONE} onValueChange={(v) => setPostingId(v === NONE ? '' : v)} disabled={!vacancyId}>
                <SelectTrigger id="record-posting">
                  <SelectValue placeholder="None" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>None</SelectItem>
                  {(postings.data ?? []).map((p) => (
                    <SelectItem key={p.id} value={p.id}>
                      {humanizeEnum(p.channel)} · {p.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="record-years">Years of experience</Label>
              <Input id="record-years" type="number" min={0} max={60} value={years} onChange={(e) => setYears(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="record-available">Available from</Label>
              <Input id="record-available" type="date" value={availableFrom} onChange={(e) => setAvailableFrom(e.target.value)} />
            </div>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="record-cover">Cover letter / notes</Label>
            <Textarea id="record-cover" rows={3} maxLength={5000} value={coverLetter} onChange={(e) => setCoverLetter(e.target.value)} />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={() => record.mutate()} disabled={!vacancyId || !candidate || record.isPending}>
            {record.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Record
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
