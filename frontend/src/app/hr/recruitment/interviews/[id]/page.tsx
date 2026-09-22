'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CalendarClock, CheckCheck, Loader2, Trash2, XCircle } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { InterviewCandidatesPanel } from '@/components/hr/recruitment/InterviewCandidatesPanel';
import { InterviewPanelPanel } from '@/components/hr/recruitment/InterviewPanelPanel';
import { InterviewQuestionsPanel } from '@/components/hr/recruitment/InterviewQuestionsPanel';
import { InterviewScoresPanel } from '@/components/hr/recruitment/InterviewScoresPanel';
import { InterviewSlotApportionPanel } from '@/components/hr/recruitment/InterviewSlotApportionPanel';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { HR_ROLES } from '@/components/hr/common/PermissionGate';
import { formatDate, formatTime, humanizeEnum } from '@/lib/hr/attendance-format';
import { jobInterviewService } from '@/services/hr/interviews.service';
import { TERMINAL_INTERVIEW_STATUSES } from '@/types/hr/interviews';

/**
 * One interview session.
 *
 * ⚠ **This screen serves two audiences with different rights**, which is why nearly everything is
 * conditioned on `canManage`. HR schedules, edits the panel and sends invitations; a panelist can
 * read the session and file their own scorecard and nothing else. The server enforces this per
 * record — the buttons are hidden to avoid offering an action that would come back a 403, not as
 * the security boundary.
 */
export default function InterviewDetailPage() {
  const params = useParams();
  const router = useRouter();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const { hasAnyRole, user } = useAuth();
  const interviewId = params.id as string;

  const [rescheduleOpen, setRescheduleOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [rescheduleForm, setRescheduleForm] = useState({ date: '', start: '', end: '', location: '', reason: '' });
  const [cancelReason, setCancelReason] = useState('');

  const canManage = hasAnyRole(HR_ROLES);

  const interview = useQuery({
    queryKey: ['hr', 'interview', interviewId],
    queryFn: () => jobInterviewService.getDetails(interviewId),
  });

  const data = interview.data;
  const myPanelist = data?.panelists.find((p) => p.employeeId === (user as any)?.employeeId);
  const canScore = !!myPanelist;

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['hr', 'interview', interviewId] });

  const complete = useMutation({
    mutationFn: () => jobInterviewService.complete(interviewId),
    onSuccess: () => {
      toast({ title: 'Interview closed' });
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Could not close the interview', description: error?.message, variant: 'destructive' }),
  });

  const reschedule = useMutation({
    mutationFn: () =>
      jobInterviewService.reschedule(interviewId, {
        interviewId,
        newDate: rescheduleForm.date,
        newStartTime: `${rescheduleForm.start}:00`,
        newEndTime: `${rescheduleForm.end}:00`,
        locationOrLink: rescheduleForm.location || null,
        rescheduleReason: rescheduleForm.reason,
      }),
    onSuccess: () => {
      toast({
        title: 'Interview rescheduled',
        description: 'Every candidate has been re-invited and their old confirmation link no longer works.',
      });
      setRescheduleOpen(false);
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Could not reschedule', description: error?.message, variant: 'destructive' }),
  });

  const cancel = useMutation({
    mutationFn: () => jobInterviewService.cancel(interviewId, { interviewId, cancellationReason: cancelReason }),
    onSuccess: () => {
      toast({ title: 'Interview cancelled' });
      setCancelOpen(false);
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Could not cancel', description: error?.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: () => jobInterviewService.remove(interviewId),
    onSuccess: () => {
      toast({ title: 'Interview deleted' });
      router.push('/hr/recruitment/interviews');
    },
    onError: (error: any) =>
      toast({ title: 'Could not delete the interview', description: error?.message, variant: 'destructive' }),
  });

  if (interview.isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (interview.isError || !data) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="Interview" backHref="/hr/recruitment/interviews" />
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            This interview could not be loaded. Only HR and members of its panel can open it.
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  const isTerminal = TERMINAL_INTERVIEW_STATUSES.includes(data.status);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${data.interviewNumber} — ${data.jobTitle || 'Interview'}`}
        description={`Round ${data.round} · ${humanizeEnum(data.type)} · ${humanizeEnum(data.mode)}`}
        backHref={canManage ? '/hr/recruitment/interviews' : '/me/panel'}
        actions={
          canManage && !isTerminal ? (
            <div className="flex gap-2">
              <Button
                variant="outline"
                onClick={() => {
                  setRescheduleForm({
                    date: data.scheduledDate,
                    start: data.startTime.slice(0, 5),
                    end: data.endTime.slice(0, 5),
                    location: data.locationOrLink ?? '',
                    reason: '',
                  });
                  setRescheduleOpen(true);
                }}
              >
                <CalendarClock className="mr-1.5 h-4 w-4" />
                Reschedule
              </Button>
              <Button variant="outline" onClick={() => setCancelOpen(true)}>
                <XCircle className="mr-1.5 h-4 w-4" />
                Cancel
              </Button>
              <Button onClick={() => complete.mutate()} disabled={complete.isPending}>
                {complete.isPending ? (
                  <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
                ) : (
                  <CheckCheck className="mr-1.5 h-4 w-4" />
                )}
                Close interview
              </Button>
            </div>
          ) : canManage && data.status === 'Cancelled' ? (
            <Button variant="outline" onClick={() => remove.mutate()} disabled={remove.isPending}>
              <Trash2 className="mr-1.5 h-4 w-4" />
              Delete
            </Button>
          ) : undefined
        }
      />

      <Card>
        <CardContent className="grid gap-4 p-6 sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <p className="text-sm text-muted-foreground">Status</p>
            <div className="mt-1">
              <StatusBadge status={data.status} />
            </div>
          </div>
          <div>
            <p className="text-sm text-muted-foreground">When</p>
            <p className="mt-1 font-medium">{formatDate(data.scheduledDate)}</p>
            <p className="text-sm text-muted-foreground">
              {formatTime(data.startTime)} – {formatTime(data.endTime)}
            </p>
          </div>
          <div>
            <p className="text-sm text-muted-foreground">Where</p>
            <p className="mt-1 font-medium">{data.locationOrLink || 'To be advised'}</p>
          </div>
          <div>
            <p className="text-sm text-muted-foreground">Vacancy</p>
            <p className="mt-1 font-medium">{data.vacancyNumber}</p>
          </div>

          {data.originalDate && (
            <div className="sm:col-span-2 lg:col-span-4">
              <Alert>
                <CalendarClock className="h-4 w-4" />
                <AlertDescription>
                  Moved from {formatDate(data.originalDate)}
                  {data.rescheduleReason ? ` — ${data.rescheduleReason}` : ''}
                </AlertDescription>
              </Alert>
            </div>
          )}

          {data.status === 'Cancelled' && data.cancellationReason && (
            <div className="sm:col-span-2 lg:col-span-4">
              <Alert variant="destructive">
                <XCircle className="h-4 w-4" />
                <AlertDescription>Cancelled — {data.cancellationReason}</AlertDescription>
              </Alert>
            </div>
          )}

          {data.instructions && (
            <div className="sm:col-span-2 lg:col-span-4">
              <p className="text-sm text-muted-foreground">Instructions given to candidates</p>
              <p className="mt-1 whitespace-pre-wrap text-sm">{data.instructions}</p>
            </div>
          )}
        </CardContent>
      </Card>

      <Tabs defaultValue="candidates">
        <TabsList>
          <TabsTrigger value="candidates">Candidates ({data.interviewees.length})</TabsTrigger>
          <TabsTrigger value="panel">Panel ({data.panelistCount})</TabsTrigger>
          <TabsTrigger value="questions">Questions ({data.questions.length})</TabsTrigger>
          <TabsTrigger value="scores">Scorecards</TabsTrigger>
        </TabsList>

        <TabsContent value="candidates" className="mt-4 space-y-4">
          <InterviewCandidatesPanel
            interview={data}
            canManage={canManage}
            canScore={canScore}
            myPanelistId={myPanelist?.id}
          />
          {/* Round 4, lane C. Under the candidate list rather than beside it: you book people in
              first, then decide how the day is divided between them. Hidden once the session is
              over or cancelled — there is no day left to lay out. */}
          {!isTerminal && <InterviewSlotApportionPanel interview={data} canManage={canManage} />}
        </TabsContent>

        <TabsContent value="panel" className="mt-4">
          <InterviewPanelPanel interview={data} canManage={canManage} />
        </TabsContent>

        <TabsContent value="questions" className="mt-4">
          <InterviewQuestionsPanel interview={data} canManage={canManage} />
        </TabsContent>

        <TabsContent value="scores" className="mt-4">
          {/* `canManage` decides whether the panel is blinded, not what it may fetch — the server
              narrows the read either way (round 4, lane F5). It is passed so the screen can say the
              view is narrowed instead of reporting an empty list. */}
          <InterviewScoresPanel interview={data} canManage={canManage} />
        </TabsContent>
      </Tabs>

      <Dialog open={rescheduleOpen} onOpenChange={setRescheduleOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reschedule</DialogTitle>
            <DialogDescription>
              Every candidate is re-invited and issued a new confirmation link — the old one stops
              working, and any attendance they had already confirmed is cleared.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="newDate">New date</Label>
              <Input
                id="newDate"
                type="date"
                value={rescheduleForm.date}
                onChange={(e) => setRescheduleForm((f) => ({ ...f, date: e.target.value }))}
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label htmlFor="newStart">Starts</Label>
                <Input
                  id="newStart"
                  type="time"
                  value={rescheduleForm.start}
                  onChange={(e) => setRescheduleForm((f) => ({ ...f, start: e.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="newEnd">Ends</Label>
                <Input
                  id="newEnd"
                  type="time"
                  value={rescheduleForm.end}
                  onChange={(e) => setRescheduleForm((f) => ({ ...f, end: e.target.value }))}
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="newLocation">Location or link</Label>
              <Input
                id="newLocation"
                value={rescheduleForm.location}
                onChange={(e) => setRescheduleForm((f) => ({ ...f, location: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="reason">
                Reason<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Textarea
                id="reason"
                rows={2}
                placeholder="Shown to the candidates in their new invitation."
                value={rescheduleForm.reason}
                onChange={(e) => setRescheduleForm((f) => ({ ...f, reason: e.target.value }))}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setRescheduleOpen(false)}>
              Keep as it is
            </Button>
            <Button
              disabled={reschedule.isPending || !rescheduleForm.reason.trim() || !rescheduleForm.date}
              onClick={() => reschedule.mutate()}
            >
              {reschedule.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Reschedule and re-invite
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Cancel this interview</DialogTitle>
            <DialogDescription>
              A cancelled interview cannot be reopened, rescheduled or closed — you would schedule a new
              one instead.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="cancelReason">
              Reason<span className="ml-0.5 text-red-500">*</span>
            </Label>
            <Textarea
              id="cancelReason"
              rows={3}
              value={cancelReason}
              onChange={(e) => setCancelReason(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelOpen(false)}>
              Keep it
            </Button>
            <Button variant="destructive" disabled={cancel.isPending || !cancelReason.trim()} onClick={() => cancel.mutate()}>
              {cancel.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Cancel interview
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
