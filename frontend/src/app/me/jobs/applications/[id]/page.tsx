'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Briefcase, CalendarClock, ClipboardCheck, MapPin, XCircle } from 'lucide-react';
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
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { formatDate } from '@/lib/hr/attendance-format';
import { jobApplicationService } from '@/services/hr/recruitment-pipeline.service';

/**
 * One of my internal applications (area 25 slice 13b).
 *
 * ⚠ Backed by `my-applications/{id}`, a **lean applicant projection** — not the recruiter's
 * detail. See `MyJobApplication`'s note for what is deliberately absent and why.
 *
 * Withdrawing is the point of the page: until this slice an employee could put their name forward
 * for an internal job and had no way to take it back, because the only withdraw route was
 * `RecruitmentWrite`. `canWithdraw` is computed server-side so this screen does not keep its own
 * copy of which states are terminal.
 */
export default function MyApplicationPage() {
  const params = useParams();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const id = params.id as string;

  const [confirming, setConfirming] = useState(false);
  const [reason, setReason] = useState('');

  const { data: app, isLoading, isError, error } = useQuery({
    queryKey: ['me', 'jobs', 'application', id],
    queryFn: () => jobApplicationService.getMyApplication(id),
    retry: false,
  });

  const withdraw = useMutation({
    mutationFn: () => jobApplicationService.withdrawMyApplication(id, reason.trim() || null),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['me', 'jobs'] });
      setConfirming(false);
      setReason('');
      toast({ title: 'Application withdrawn' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not withdraw', description: e?.message, variant: 'destructive' }),
  });

  const notFound = isError && (error as { status?: number })?.status === 404;

  if (isLoading) {
    return (
      <div className="space-y-6">
        <PageHeader title="My application" backHref="/me/jobs/applications" />
        <Skeleton className="h-48" />
      </div>
    );
  }

  if (notFound || !app) {
    return (
      <div className="space-y-6">
        <PageHeader title="My application" backHref="/me/jobs/applications" />
        <EmptyState
          icon={ClipboardCheck}
          title="No such application"
          description="This application is not one of yours, or no longer exists."
        />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={app.jobTitle || app.positionTitle}
        description={`Application ${app.applicationNumber}`}
        backHref="/me/jobs/applications"
        actions={
          app.canWithdraw ? (
            <Button variant="outline" onClick={() => setConfirming(true)}>
              <XCircle className="mr-2 h-4 w-4" /> Withdraw
            </Button>
          ) : undefined
        }
      />

      <Card>
        <CardContent className="space-y-4 p-5">
          <div className="flex flex-wrap items-center gap-3">
            <StatusBadge status={app.status} />
            <span className="text-sm text-muted-foreground">
              applied {formatDate(app.applicationDate)}
            </span>
            {app.isShortlisted && app.shortlistedDate && (
              <Badge variant="secondary">shortlisted {formatDate(app.shortlistedDate)}</Badge>
            )}
          </div>

          <div className="flex flex-wrap gap-x-5 gap-y-2 text-sm text-muted-foreground">
            <span className="flex items-center gap-1.5">
              <Briefcase className="h-3.5 w-3.5" /> {app.positionTitle}
            </span>
            {app.orgUnitName && (
              <span className="flex items-center gap-1.5">
                <MapPin className="h-3.5 w-3.5" /> {app.orgUnitName}
              </span>
            )}
            {app.applicationDeadline && (
              <span className="flex items-center gap-1.5">
                <CalendarClock className="h-3.5 w-3.5" /> closes {formatDate(app.applicationDeadline)}
              </span>
            )}
            <span className="font-mono text-xs">{app.vacancyNumber}</span>
          </div>

          {/* The outcome, when there is one. A rejection reason belongs to the person rejected. */}
          {app.rejectedDate && (
            <div className="rounded-md border border-destructive/30 bg-destructive/5 p-3">
              <p className="text-sm font-medium">Not taken forward · {formatDate(app.rejectedDate)}</p>
              {app.rejectionReason && (
                <p className="mt-1 whitespace-pre-line text-sm text-muted-foreground">
                  {app.rejectionReason}
                </p>
              )}
            </div>
          )}
          {app.withdrawnDate && (
            <div className="rounded-md border bg-muted/40 p-3">
              <p className="text-sm font-medium">Withdrawn · {formatDate(app.withdrawnDate)}</p>
              {app.withdrawalReason && (
                <p className="mt-1 whitespace-pre-line text-sm text-muted-foreground">
                  {app.withdrawalReason}
                </p>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium">What you sent</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <p className="text-xs text-muted-foreground">Relevant experience</p>
              <p className="text-sm">
                {app.yearsOfExperience != null ? `${app.yearsOfExperience} years` : '—'}
              </p>
            </div>
            <div>
              <p className="text-xs text-muted-foreground">Available from</p>
              <p className="text-sm">{app.availableFrom ? formatDate(app.availableFrom) : '—'}</p>
            </div>
          </div>
          <div>
            <p className="text-xs text-muted-foreground">Supporting statement</p>
            {app.coverLetter ? (
              <p className="mt-1 whitespace-pre-line text-sm leading-relaxed">{app.coverLetter}</p>
            ) : (
              <p className="mt-1 text-sm text-muted-foreground">Nothing added.</p>
            )}
          </div>
        </CardContent>
      </Card>

      <p className="text-sm text-muted-foreground">
        Looking for other openings?{' '}
        <Link href="/me/jobs" className="text-primary hover:underline">
          Back to the job board
        </Link>
        .
      </p>

      <Dialog open={confirming} onOpenChange={(o) => !o && setConfirming(false)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Withdraw this application?</DialogTitle>
            <DialogDescription>
              Your application for {app.jobTitle || app.positionTitle} will be taken out of the
              process. You can apply again later while the vacancy is still open.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-1.5">
            <Label htmlFor="why">Reason (optional)</Label>
            <Textarea
              id="why"
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Only if you want to say."
            />
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirming(false)} disabled={withdraw.isPending}>
              Keep it
            </Button>
            <Button variant="destructive" onClick={() => withdraw.mutate()} disabled={withdraw.isPending}>
              Withdraw
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
