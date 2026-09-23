'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { BellRing, Eye, Loader2, MailWarning, Megaphone, Play, Send } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { orientationReminderService } from '@/services/hr/orientation-reminder.service';
import type {
  OrientationReminderPreviewItem,
  OrientationReminderRunResult,
} from '@/types/hr/orientation';

const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

type BadgeVariant = 'default' | 'secondary' | 'outline' | 'destructive';

/** What each rule means, in the words someone reading the log would use. */
const KIND_LABELS: Record<string, string> = {
  OnboardingTaskDueSoon: 'Onboarding task due soon',
  OnboardingTaskOverdue: 'Onboarding task overdue',
  OnboardingTaskAwaitingSignOff: 'Onboarding task awaiting sign-off',
  OrientationDueSoon: 'Orientation due soon',
  OrientationOverdue: 'Orientation overdue',
  OrientationAssessmentNotAttempted: 'Assessment not attempted',
  OrientationAcknowledgementOutstanding: 'Acknowledgement not signed',
  OrientationCertificateExpiring: 'Certificate expiring',
};

/** What happened to an item's email — said plainly, because "false" from a mail call says nothing. */
const EMAIL_OUTCOMES: Record<string, { label: string; variant: BadgeVariant }> = {
  Sent: { label: 'Emailed', variant: 'default' },
  NoMailServer: { label: 'No mail server', variant: 'secondary' },
  NoAddress: { label: 'No email address', variant: 'secondary' },
  Failed: { label: 'Email failed', variant: 'destructive' },
  TimedOut: { label: 'Email timed out', variant: 'destructive' },
  NotRouted: { label: 'Nobody to send to', variant: 'destructive' },
  Pending: { label: 'Sending…', variant: 'outline' },
};

/** The lifecycle notices (round 4, lane K-b), plus the sweep's digest, by catalogue event. */
const NOTICE_KINDS: Record<string, string> = {
  OrientationEnrolled: 'Enrolled',
  OrientationSessionScheduled: 'Session scheduled',
  OrientationSessionRescheduled: 'Session moved',
  OrientationSessionPostponed: 'Session postponed',
  OrientationSessionCancelled: 'Session cancelled',
  OrientationCompleted: 'Completed',
  OrientationCertificateIssued: 'Certificate issued',
  OnboardingWelcome: 'Onboarding — welcome',
  OnboardingCoordinatorAssigned: 'Onboarding — coordinator',
  OnboardingBuddyAssigned: 'Onboarding — buddy',
  OnboardingTaskAssigned: 'Onboarding — task given',
  OnboardingTaskDone: 'Onboarding — awaiting sign-off',
  OrientationReminderDigest: 'Reminder (daily sweep)',
};

/** A notice's email. Null is a notice that was never meant to be emailed. */
const NOTICE_EMAIL: Record<string, { label: string; variant: BadgeVariant }> = {
  Queued: { label: 'Queued', variant: 'outline' },
  Sent: { label: 'Emailed', variant: 'default' },
  Failed: { label: 'Email failed', variant: 'destructive' },
  TimedOut: { label: 'Email timed out', variant: 'destructive' },
  NoAddress: { label: 'No email address', variant: 'secondary' },
  NoMailServer: { label: 'No mail server', variant: 'secondary' },
  Stale: { label: 'Too old to email', variant: 'secondary' },
};

const when = (days: number) =>
  days === 0 ? 'today' : days > 0 ? `${days} day${days === 1 ? '' : 's'} left` : `${-days} day${days === -1 ? '' : 's'} past`;

/**
 * Operates the orientation & onboarding reminder sweep (round 4, lane K) and shows what reached
 * people — the sweep's reminders, and (lane K-b) every lifecycle notice.
 *
 * ⚠ The first HR sweep that delivers. Each person gets ONE in-app notification per run listing their
 * items — onboarding tasks go to the task's assignee, else the plan's coordinator; orientations,
 * assessments, acknowledgements and certificates to the participant — and the same list by email.
 * The email outcome is recorded per item, so "no mail server is configured" reads as that, not as
 * success. Repeating a run is safe: every item is sent once per due date and escalation rung.
 */
export default function OrientationRemindersPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Orientation & onboarding reminders and notices"
        description="The daily reminder sweep, and every notice the system sends — enrolments, session changes, completions, certificates — with what each email did."
        backHref="/administration/hr/orientation"
      />

      <Tabs defaultValue="reminders">
        <TabsList>
          <TabsTrigger value="reminders">
            <BellRing className="mr-2 h-4 w-4" />
            Reminder sweep
          </TabsTrigger>
          <TabsTrigger value="notices">
            <Megaphone className="mr-2 h-4 w-4" />
            Notices
          </TabsTrigger>
        </TabsList>
        <TabsContent value="reminders" className="space-y-6 pt-4">
          <ReminderSweepPanel />
        </TabsContent>
        <TabsContent value="notices" className="space-y-6 pt-4">
          <NoticesPanel />
        </TabsContent>
      </Tabs>
    </div>
  );
}

function ReminderSweepPanel() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [lastRun, setLastRun] = useState<OrientationReminderRunResult | null>(null);
  const [asOf, setAsOf] = useState('');
  const [preview, setPreview] = useState<OrientationReminderPreviewItem[] | null>(null);

  const { data: runs = [], isLoading } = useQuery({
    queryKey: ['hr', 'orientation-reminders', 'runs'],
    queryFn: () => orientationReminderService.getRuns(20),
  });

  const { data: log = [] } = useQuery({
    queryKey: ['hr', 'orientation-reminders', 'log'],
    queryFn: () => orientationReminderService.getLog(14),
  });

  const run = useMutation({
    mutationFn: () => orientationReminderService.run(),
    onSuccess: (result) => {
      setLastRun(result);
      toast({
        title: 'Sweep complete',
        description:
          result.remindersQueued === 0
            ? 'Nothing new to send — everything due has already been reminded.'
            : `${result.remindersQueued} item(s) sent to ${result.notificationsDelivered} person(s).`,
      });
      queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-reminders'] });
      queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-notices'] });
    },
    onError: (error: any) =>
      toast({ title: 'Sweep failed', description: error?.message, variant: 'destructive' }),
  });

  const previewRun = useMutation({
    mutationFn: () =>
      orientationReminderService.preview(asOf ? new Date(`${asOf}T12:00:00`).toISOString() : undefined),
    onSuccess: setPreview,
    onError: (error: any) =>
      toast({ title: 'Preview failed', description: error?.message, variant: 'destructive' }),
  });

  const latest = runs[0];

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-muted-foreground max-w-3xl text-sm">
          The daily sweep: onboarding tasks due soon, overdue or awaiting sign-off; orientations due,
          overdue or waiting on the participant; certificates expiring. Each person gets one
          notification listing theirs, and the same by email. A programme whose “Send reminders” is
          off is left out.
        </p>
        <Button onClick={() => run.mutate()} disabled={run.isPending}>
          {run.isPending ? (
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          ) : (
            <Play className="mr-2 h-4 w-4" />
          )}
          Run now
        </Button>
      </div>

      {latest && !latest.mailServerConfigured && (
        <Alert>
          <MailWarning className="h-4 w-4" />
          <AlertTitle>Reminders are going out in-app only</AlertTitle>
          <AlertDescription>
            No mail server is configured, so the last sweep could not email anyone. People still see
            their reminders under My Notifications. Set up email under{' '}
            <Link href="/administration/settings/email" className="underline">
              email settings
            </Link>{' '}
            and the next sweep emails as well.
          </AlertDescription>
        </Alert>
      )}

      {lastRun && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Last run from this screen</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {lastRun.remindersQueued === 0 ? (
              <p className="text-muted-foreground text-sm">
                Nothing new. Everything currently due has already been reminded — each item goes
                once per due date and escalation rung.
              </p>
            ) : (
              <>
                <div className="flex flex-wrap gap-2 text-sm">
                  <Badge variant="outline">{lastRun.remindersQueued} item(s)</Badge>
                  <Badge variant="default">{lastRun.notificationsDelivered} person(s) notified in-app</Badge>
                  <Badge variant={lastRun.emailsSent > 0 ? 'default' : 'secondary'}>
                    {lastRun.emailsSent} emailed
                  </Badge>
                  {lastRun.emailsNotSent > 0 && (
                    <Badge variant="secondary">{lastRun.emailsNotSent} not emailed</Badge>
                  )}
                  {lastRun.unrouted > 0 && (
                    <Badge variant="destructive">{lastRun.unrouted} with nobody to send to</Badge>
                  )}
                </div>
                <ul className="space-y-1 text-sm">
                  {Object.entries(lastRun.byKind).map(([kind, count]) => (
                    <li key={kind} className="flex items-center justify-between">
                      <span>{KIND_LABELS[kind] ?? kind}</span>
                      <Badge variant="outline">{count}</Badge>
                    </li>
                  ))}
                </ul>
              </>
            )}
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            <Eye className="mr-2 inline h-4 w-4" />
            Preview
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="flex flex-wrap items-end gap-3">
            <div className="space-y-1">
              <Label htmlFor="reminder-as-of">As at</Label>
              <Input
                id="reminder-as-of"
                type="date"
                value={asOf}
                onChange={(e) => setAsOf(e.target.value)}
                className="w-[180px]"
              />
            </div>
            <Button variant="outline" onClick={() => previewRun.mutate()} disabled={previewRun.isPending}>
              {previewRun.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Preview
            </Button>
            <p className="text-muted-foreground text-xs">
              What a sweep would remind on that date (today if blank). Sends nothing and claims
              nothing, so it never takes a reminder away from the real sweep.
            </p>
          </div>
          {preview &&
            (preview.length === 0 ? (
              <p className="text-muted-foreground text-sm">Nothing would be reminded on that date.</p>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>What</TableHead>
                    <TableHead>Reference</TableHead>
                    <TableHead>Due</TableHead>
                    <TableHead>Goes to</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {preview.slice(0, 200).map((p) => (
                    <TableRow key={p.dedupeKey}>
                      <TableCell>{KIND_LABELS[p.kind] ?? p.kind}</TableCell>
                      <TableCell>{p.reference}</TableCell>
                      <TableCell>
                        {fmtDate(p.dueDate)}{' '}
                        <span className="text-muted-foreground text-xs">({when(p.daysRemaining)})</span>
                      </TableCell>
                      <TableCell>
                        {p.routedToName ?? (
                          <span className="text-destructive text-sm">Nobody — no coordinator</span>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            ))}
          {preview && preview.length > 200 && (
            <p className="text-muted-foreground text-xs">Showing the first 200 of {preview.length}.</p>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Recent sweeps</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
            </div>
          ) : runs.length === 0 ? (
            <EmptyState
              title="No sweeps yet"
              description="The daily sweep has not run in this tenant yet. Use Run now to start one."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Started</TableHead>
                  <TableHead>Trigger</TableHead>
                  <TableHead className="text-right">Items</TableHead>
                  <TableHead className="text-right">People notified</TableHead>
                  <TableHead className="text-right">Emailed</TableHead>
                  <TableHead className="text-right">Not emailed</TableHead>
                  <TableHead className="text-right">Nobody to send to</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {runs.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell>{fmtDateTime(r.startedAt)}</TableCell>
                    <TableCell>
                      <Badge variant={r.trigger === 'Manual' ? 'default' : 'outline'}>{r.trigger}</Badge>
                    </TableCell>
                    <TableCell className="text-right">{r.remindersQueued}</TableCell>
                    <TableCell className="text-right">{r.notificationsDelivered}</TableCell>
                    <TableCell className="text-right">{r.emailsSent}</TableCell>
                    <TableCell className="text-right">
                      {r.emailsNotSent}
                      {!r.mailServerConfigured && r.emailsNotSent > 0 && (
                        <span className="text-muted-foreground ml-1 text-xs">(no mail server)</span>
                      )}
                    </TableCell>
                    <TableCell className="text-right">{r.unrouted}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            <BellRing className="mr-2 inline h-4 w-4" />
            Sent in the last 14 days
          </CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {log.length === 0 ? (
            <EmptyState title="Nothing sent" description="No orientation or onboarding reminders have gone out recently." />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Sent</TableHead>
                  <TableHead>What</TableHead>
                  <TableHead>Reference</TableHead>
                  <TableHead>Due</TableHead>
                  <TableHead>Tier</TableHead>
                  <TableHead>To</TableHead>
                  <TableHead>Email</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {log.map((entry) => {
                  const outcome = EMAIL_OUTCOMES[entry.emailOutcome] ?? { label: entry.emailOutcome, variant: 'outline' as const };
                  return (
                    <TableRow key={entry.id}>
                      <TableCell>{fmtDateTime(entry.dispatchedAt)}</TableCell>
                      <TableCell>
                        <div>{KIND_LABELS[entry.kind] ?? entry.kind}</div>
                        <div className="text-muted-foreground text-xs">{entry.itemType}</div>
                      </TableCell>
                      <TableCell>{entry.reference}</TableCell>
                      <TableCell>
                        {fmtDate(entry.dueDate)}{' '}
                        <span className="text-muted-foreground text-xs">({when(entry.daysRemaining)})</span>
                      </TableCell>
                      <TableCell>
                        {entry.escalationTier > 0 ? (
                          <Badge variant={entry.escalationTier >= 2 ? 'destructive' : 'outline'}>
                            Tier {entry.escalationTier}
                          </Badge>
                        ) : (
                          <span className="text-muted-foreground">—</span>
                        )}
                      </TableCell>
                      <TableCell>
                        {entry.routedToName ?? <span className="text-destructive">Nobody</span>}
                        {entry.notificationId && (
                          <div className="text-muted-foreground text-xs">notified in-app</div>
                        )}
                      </TableCell>
                      <TableCell>
                        <Badge variant={outcome.variant}>{outcome.label}</Badge>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <p className="text-muted-foreground text-xs">
        Escalation: an overdue item is reminded when it falls due (tier 1), again after a week (tier
        2) and after a fortnight (tier 3). Items more than 90 days overdue are treated as history and
        not reminded. The windows — how far ahead to remind, and how long to wait before chasing — are
        set under{' '}
        <Link href="/administration/hr/settings/policy" className="underline">
          HR policy settings
        </Link>
        .
      </p>
    </>
  );
}

/**
 * Every notice the system sent (round 4, lane K-b): the lifecycle notices — enrolled, a session
 * placed, moved, postponed or cancelled, completed, certificate issued — and the sweep's digests.
 * Each is in the person's My Notifications from the moment the event saved; its email follows within
 * a minute, and what the email did is shown here.
 */
function NoticesPanel() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [days, setDays] = useState('14');
  const [kind, setKind] = useState('all');
  const [emailStatus, setEmailStatus] = useState('all');

  const { data: notices = [], isLoading } = useQuery({
    queryKey: ['hr', 'orientation-notices', days, kind, emailStatus],
    queryFn: () =>
      orientationReminderService.getRecentNotices(
        Number(days),
        kind === 'all' ? undefined : kind,
        emailStatus === 'all' ? undefined : emailStatus,
      ),
  });

  const sendQueued = useMutation({
    mutationFn: () => orientationReminderService.sendQueuedNotices(),
    onSuccess: (result) => {
      toast({
        title: result.busy ? 'Already sending' : 'Queued emails sent',
        description: result.busy
          ? 'The dispatcher is sending right now; try again in a minute.'
          : !result.mailServerConfigured && result.picked > 0
            ? `No mail server is configured: ${result.noMailServer} notice(s) stay in-app only.`
            : `${result.sent} emailed, ${result.failed} failed, ${result.retrying} to retry, ${result.noAddress} without an address. ${result.stillQueued} still queued.`,
      });
      queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-notices'] });
    },
    onError: (error: any) =>
      toast({ title: 'Could not send', description: error?.message, variant: 'destructive' }),
  });

  const counts = notices.reduce<Record<string, number>>((acc, n) => {
    const key = n.emailStatus ?? 'None';
    acc[key] = (acc[key] ?? 0) + 1;
    return acc;
  }, {});

  return (
    <>
      <div className="flex flex-wrap items-end gap-3">
        <div className="space-y-1">
          <Label>Sent in the last</Label>
          <Select value={days} onValueChange={setDays}>
            <SelectTrigger className="w-[140px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="7">7 days</SelectItem>
              <SelectItem value="14">14 days</SelectItem>
              <SelectItem value="30">30 days</SelectItem>
              <SelectItem value="90">90 days</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <Label>What</Label>
          <Select value={kind} onValueChange={setKind}>
            <SelectTrigger className="w-[220px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Everything</SelectItem>
              {Object.entries(NOTICE_KINDS).map(([value, label]) => (
                <SelectItem key={value} value={value}>
                  {label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <Label>Email</Label>
          <Select value={emailStatus} onValueChange={setEmailStatus}>
            <SelectTrigger className="w-[180px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Any</SelectItem>
              {Object.entries(NOTICE_EMAIL).map(([value, { label }]) => (
                <SelectItem key={value} value={value}>
                  {label}
                </SelectItem>
              ))}
              <SelectItem value="None">In-app only</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <Button
          variant="outline"
          className="ml-auto"
          onClick={() => sendQueued.mutate()}
          disabled={sendQueued.isPending}
        >
          {sendQueued.isPending ? (
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          ) : (
            <Send className="mr-2 h-4 w-4" />
          )}
          Send queued emails now
        </Button>
      </div>

      {notices.length > 0 && (
        <div className="flex flex-wrap gap-2 text-sm">
          <Badge variant="outline">{notices.length} notice(s)</Badge>
          {Object.entries(counts).map(([status, count]) => (
            <Badge key={status} variant={NOTICE_EMAIL[status]?.variant ?? 'secondary'}>
              {count} {status === 'None' ? 'in-app only' : (NOTICE_EMAIL[status]?.label ?? status).toLowerCase()}
            </Badge>
          ))}
        </div>
      )}

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
            </div>
          ) : notices.length === 0 ? (
            <EmptyState
              title="No notices"
              description="Nothing matching was sent in that period. Notices go out when somebody is enrolled, a session is placed, moved or called off, an orientation is completed, or a certificate is issued."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Sent</TableHead>
                  <TableHead>What</TableHead>
                  <TableHead>To</TableHead>
                  <TableHead>Notice</TableHead>
                  <TableHead>Email</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {notices.map((n) => {
                  const email = n.emailStatus ? NOTICE_EMAIL[n.emailStatus] : null;
                  return (
                    <TableRow key={n.id}>
                      <TableCell className="whitespace-nowrap">{fmtDateTime(n.sentAt)}</TableCell>
                      <TableCell>
                        <div>{(n.kind && NOTICE_KINDS[n.kind]) ?? 'Manual notice'}</div>
                        {n.programTitle && (
                          <div className="text-muted-foreground text-xs">{n.programTitle}</div>
                        )}
                      </TableCell>
                      <TableCell>
                        {n.recipientName ?? '—'}
                        <div className="text-muted-foreground text-xs">{n.isRead ? 'read' : 'unread'}</div>
                      </TableCell>
                      <TableCell className="max-w-[420px]">
                        <div className="font-medium">{n.subject}</div>
                        {n.message && (
                          <div className="text-muted-foreground line-clamp-2 whitespace-pre-line text-xs">
                            {n.message}
                          </div>
                        )}
                      </TableCell>
                      <TableCell>
                        {email ? (
                          <Badge variant={email.variant}>{email.label}</Badge>
                        ) : n.emailStatus ? (
                          <Badge variant="outline">{n.emailStatus}</Badge>
                        ) : (
                          <span className="text-muted-foreground text-sm">In-app only</span>
                        )}
                        {n.emailStatus === 'Queued' && n.emailAttempts > 0 && (
                          <div className="text-muted-foreground text-xs">attempt {n.emailAttempts} of 3 failed</div>
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

      <p className="text-muted-foreground text-xs">
        A notice is in the person’s My Notifications from the moment the event is saved. Its email is
        sent within a minute by the dispatcher — a failed one is retried twice, five minutes apart.
        With no mail server configured, or no address on the employee’s record, the in-app notice is
        the delivery. Enrolment notices follow a programme’s “Send reminders” switch; session changes,
        completions and certificates are always sent.
      </p>
    </>
  );
}
