'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter, useSearchParams } from 'next/navigation';
import { useMutation, useQuery } from '@tanstack/react-query';
import { ArrowLeft, Loader2, MapPin, Send } from 'lucide-react';
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
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { isCandidateUser } from '@/lib/auth-routing';
import { formatDate } from '@/lib/hr/attendance-format';
import { authService } from '@/services/auth';
import { candidateService, publicCareersService } from '@/services/hr/careers.service';

/**
 * Public vacancy detail. Anyone can read it; the Apply button requires a signed-in candidate
 * (browse public, apply logged-in). An application needs a saved candidate profile — the server
 * says so with a message, and the dialog routes the candidate to their profile when it does.
 */
export default function CareersVacancyPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';
  // The advert this link was shared from (round 3, lane A): `/careers/{id}?posting={postingId}`.
  // Carried on the application so HR sees which advert brought the applicant, and the source
  // follows the advert's channel. Only one of this vacancy's live adverts counts.
  const searchParams = useSearchParams();
  const postingParam = searchParams?.get('posting') ?? '';
  const router = useRouter();
  const { toast } = useToast();

  const user = typeof window !== 'undefined' ? authService.getStoredUser() : null;
  const signedInCandidate = isCandidateUser(user);

  const [applyOpen, setApplyOpen] = useState(false);
  const [coverLetter, setCoverLetter] = useState('');
  const [years, setYears] = useState('');
  const [availableFrom, setAvailableFrom] = useState('');

  const tenant = useQuery({
    queryKey: ['careers', 'tenant'],
    queryFn: () => publicCareersService.resolveTenant(),
    staleTime: Infinity,
  });

  const vacancy = useQuery({
    queryKey: ['careers', 'vacancy', tenant.data?.id, id],
    queryFn: () => publicCareersService.getVacancy(tenant.data?.id ?? '', id),
    enabled: !!tenant.data?.id && !!id,
  });

  const posting = (vacancy.data?.postings ?? []).find((p) => p.id === postingParam) ?? null;

  const apply = useMutation({
    mutationFn: () =>
      candidateService.apply({
        vacancyId: id,
        jobPostingId: posting?.id ?? null,
        coverLetter: coverLetter.trim() || null,
        yearsOfExperience: years === '' ? null : Number(years),
        availableFrom: availableFrom || null,
      }),
    onSuccess: (result) => {
      setApplyOpen(false);
      toast({
        title: 'Application submitted',
        description: `Your application number is ${result.applicationNumber}.`,
      });
      router.push('/external-portal/careers');
    },
    onError: (e: any) => {
      const message: string = e?.message ?? 'Could not submit the application.';
      if (message.toLowerCase().includes('profile')) {
        toast({
          title: 'Complete your profile first',
          description: 'Applications carry your profile — finish it, then apply.',
        });
        router.push('/external-portal/careers/profile');
        return;
      }
      toast({ title: 'Could not apply', description: message, variant: 'destructive' });
    },
  });

  if (vacancy.isLoading || tenant.isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const v = vacancy.data;
  if (vacancy.isError || !v) {
    return (
      <div className="py-16 text-center text-muted-foreground">
        This vacancy is no longer available.
        <div className="mt-4">
          <Button asChild variant="outline">
            <Link href="/careers">
              <ArrowLeft className="mr-2 h-4 w-4" />
              All open positions
            </Link>
          </Button>
        </div>
      </div>
    );
  }

  const deadlinePassed = !!v.applicationDeadline && new Date(v.applicationDeadline) < new Date();

  return (
    <div className="space-y-6">
      <Link href="/careers" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:underline">
        <ArrowLeft className="h-4 w-4" />
        All open positions
      </Link>

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">{v.jobTitle}</h1>
          <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-muted-foreground">
            {v.departmentName && <span>{v.departmentName}</span>}
            {v.locationName && (
              <span className="flex items-center gap-1">
                <MapPin className="h-3.5 w-3.5" />
                {v.locationName}
              </span>
            )}
            <span>{v.employmentTypeName}</span>
            <span>{v.workModeName}</span>
            {v.numberOfPositions > 1 && <span>{v.numberOfPositions} positions</span>}
          </div>
        </div>
        <div className="flex flex-col items-end gap-2">
          {deadlinePassed ? (
            <Badge variant="secondary">Applications closed</Badge>
          ) : signedInCandidate ? (
            <Button onClick={() => setApplyOpen(true)}>
              <Send className="mr-2 h-4 w-4" />
              Apply now
            </Button>
          ) : (
            <div className="flex flex-col items-end gap-1.5">
              <Button asChild>
                <Link href={`/login?redirect=${encodeURIComponent(`/careers/${id}`)}`}>Sign in to apply</Link>
              </Button>
              <span className="text-xs text-muted-foreground">
                New here?{' '}
                <Link href="/careers/register" className="underline">
                  Create an account
                </Link>
              </span>
            </div>
          )}
          {v.applicationDeadline && !deadlinePassed && (
            <span className="text-xs text-muted-foreground">Apply by {formatDate(v.applicationDeadline)}</span>
          )}
        </div>
      </div>

      {v.isSalaryVisible && v.salaryRangeMin != null && (
        <Badge variant="secondary">
          {v.salaryCurrencyCode ?? ''} {v.salaryRangeMin?.toLocaleString()} – {v.salaryRangeMax?.toLocaleString()}
        </Badge>
      )}

      {v.jobDescription && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">About the role</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="whitespace-pre-wrap text-sm leading-relaxed">{v.jobDescription}</p>
          </CardContent>
        </Card>
      )}

      {v.keyBenefitsSummary && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Benefits</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="whitespace-pre-wrap text-sm">{v.keyBenefitsSummary}</p>
          </CardContent>
        </Card>
      )}

      <div className="text-xs text-muted-foreground">
        {v.requiredMinExperienceYears != null && (
          <span>Minimum experience: {v.requiredMinExperienceYears} years. </span>
        )}
        {(v.requiresWrittenTest || v.requiresPracticalTest) && (
          <span>
            This role includes {[v.requiresWrittenTest && 'a written test', v.requiresPracticalTest && 'a practical test']
              .filter(Boolean)
              .join(' and ')}
            .
          </span>
        )}
      </div>

      <Dialog open={applyOpen} onOpenChange={setApplyOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Apply — {v.jobTitle}</DialogTitle>
            <DialogDescription>
              Your saved candidate profile goes with this application. Keep it up to date.
              {posting && ` You came through the ${posting.channelName} advert; we record that.`}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="apply-cover">Cover letter</Label>
              <Textarea
                id="apply-cover"
                rows={6}
                maxLength={5000}
                value={coverLetter}
                onChange={(e) => setCoverLetter(e.target.value)}
                placeholder="Why you, for this role…"
              />
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="apply-years">Years of relevant experience</Label>
                <Input
                  id="apply-years"
                  type="number"
                  min={0}
                  max={60}
                  value={years}
                  onChange={(e) => setYears(e.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="apply-available">Available from</Label>
                <Input
                  id="apply-available"
                  type="date"
                  value={availableFrom}
                  onChange={(e) => setAvailableFrom(e.target.value)}
                />
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setApplyOpen(false)}>
              Cancel
            </Button>
            <Button disabled={apply.isPending} onClick={() => apply.mutate()}>
              {apply.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Submit application
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
