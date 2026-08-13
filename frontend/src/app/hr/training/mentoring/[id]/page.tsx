'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import {
  Loader2,
  Lock,
  Plus,
  Star,
  CalendarClock,
  CheckCircle2,
  XCircle,
  Trash2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import {
  DateField,
  NumberField,
  TextareaField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { mentoringService } from '@/services/hr/mentoring.service';
import { MENTORING_FORMAT_OPTIONS } from '@/types/hr/mentoring';
import type { MentoringSession, MentoringSessionFormat } from '@/types/hr/mentoring';

const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const formatLabel = (v?: string | null) =>
  v ? MENTORING_FORMAT_OPTIONS.find((o) => o.value === v)?.label ?? v : '—';

interface SessionFormValues {
  sessionDate: string;
  durationMinutes: string;
  format: MentoringSessionFormat | '';
  topicsDiscussed: string;
  actionItems: string;
  ownNotes: string;
}

/**
 * One mentoring relationship and its session log.
 *
 * The private fields drive most of the decisions here. `mentorNotes` and `menteeNotes` come back null
 * unless you wrote them, so the page uses the server's `viewerIsMentor`/`viewerIsMentee` rather than
 * null-ness to decide what to show — a null note for the mentor means "the mentee's, not yours", and
 * rendering that as an empty textarea would invite someone to overwrite words they never saw.
 *
 * There is exactly one editable note box, labelled as yours, and the other side is shown as withheld.
 */
export default function MentoringPairPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [logOpen, setLogOpen] = useState(false);
  const [editing, setEditing] = useState<MentoringSession | null>(null);
  const [closeOpen, setCloseOpen] = useState(false);
  const [closureNotes, setClosureNotes] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<MentoringSession | null>(null);
  const [busy, setBusy] = useState(false);

  const pairKey = ['hr', 'mentoring', 'pairs', id];
  const sessionsKey = ['hr', 'mentoring', 'pairs', id, 'sessions'];

  const { data: pair, isLoading, isError } = useQuery({
    queryKey: pairKey,
    queryFn: () => mentoringService.getPairById(id),
    enabled: !!id,
  });
  const { data: sessions } = useQuery({
    queryKey: sessionsKey,
    queryFn: () => mentoringService.getSessionsForPair(id),
    enabled: !!id,
  });

  const form = useForm<SessionFormValues>({
    defaultValues: {
      sessionDate: new Date().toISOString().slice(0, 10),
      durationMinutes: '60',
      format: 'InPerson',
      topicsDiscussed: '',
      actionItems: '',
      ownNotes: '',
    },
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !pair) {
    return (
      <div className="p-6">
        <EmptyState
          title="Not available"
          description="Either this pair does not exist, or mentoring records are limited to the mentor, the mentee, the programme coordinator and HR."
        />
      </div>
    );
  }

  const inPair = pair.viewerIsMentor || pair.viewerIsMentee;
  const isClosed = pair.status === 'Completed' || pair.status === 'Cancelled';
  const myNotesOf = (s: MentoringSession) => (pair.viewerIsMentor ? s.mentorNotes : s.menteeNotes);
  const theirLabel = pair.viewerIsMentor ? pair.menteeName : pair.mentorName;

  const openLog = () => {
    form.reset({
      sessionDate: new Date().toISOString().slice(0, 10),
      durationMinutes: '60',
      format: 'InPerson',
      topicsDiscussed: '',
      actionItems: '',
      ownNotes: '',
    });
    setEditing(null);
    setLogOpen(true);
  };

  const openEdit = (s: MentoringSession) => {
    form.reset({
      sessionDate: s.sessionDate.slice(0, 10),
      durationMinutes: s.durationMinutes.toString(),
      format: (s.format ?? '') as MentoringSessionFormat | '',
      topicsDiscussed: s.topicsDiscussed ?? '',
      actionItems: s.actionItems ?? '',
      ownNotes: myNotesOf(s) ?? '',
    });
    setEditing(s);
    setLogOpen(true);
  };

  const saveSession = async () => {
    const v = form.getValues();
    setBusy(true);
    try {
      // Only ever send the note field that is ours. The other side's is null in what we were given,
      // and the server preserves it — but sending it back at all would be claiming authorship.
      const notes = pair.viewerIsMentor
        ? { mentorNotes: v.ownNotes || null }
        : pair.viewerIsMentee
          ? { menteeNotes: v.ownNotes || null }
          : {};

      const payload = {
        sessionDate: new Date(v.sessionDate).toISOString(),
        durationMinutes: Number(v.durationMinutes),
        format: (v.format || null) as MentoringSessionFormat | null,
        topicsDiscussed: v.topicsDiscussed || null,
        actionItems: v.actionItems || null,
        attendedByMentor: true,
        attendedByMentee: true,
        ...notes,
      };

      if (editing) {
        await mentoringService.updateSession(editing.id, payload);
      } else {
        await mentoringService.logSession({ pairId: id, ...payload });
      }

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: sessionsKey }),
        queryClient.invalidateQueries({ queryKey: pairKey }),
      ]);
      toast({ title: editing ? 'Session updated' : 'Session logged' });
      setLogOpen(false);
      setEditing(null);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to save the session.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  const removeSession = async (): Promise<boolean> => {
    if (!deleteTarget) return false;
    try {
      await mentoringService.deleteSession(deleteTarget.id);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: sessionsKey }),
        queryClient.invalidateQueries({ queryKey: pairKey }),
      ]);
      toast({ title: 'Session deleted' });
      setDeleteTarget(null);
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to delete the session.',
        variant: 'destructive',
      });
      return false;
    }
  };

  const closePair = async (): Promise<boolean> => {
    if (!closureNotes.trim()) {
      toast({
        title: 'Closure notes are required',
        description: 'Say how the relationship ended.',
        variant: 'destructive',
      });
      return false;
    }
    try {
      await mentoringService.closePair(id, closureNotes.trim());
      await queryClient.invalidateQueries({ queryKey: ['hr', 'mentoring'] });
      toast({ title: 'Pair closed' });
      setCloseOpen(false);
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to close the pair.',
        variant: 'destructive',
      });
      return false;
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${pair.mentorName} → ${pair.menteeName}`}
        description={`${pair.programName} · started ${fmt(pair.startDate)}`}
        backHref="/hr/training/mentoring"
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge status={pair.statusName} />
            {!isClosed && (
              <>
                <Button size="sm" onClick={openLog}>
                  <Plus className="mr-2 h-4 w-4" /> Log a session
                </Button>
                <Button variant="outline" size="sm" onClick={() => setCloseOpen(true)}>
                  Close pair
                </Button>
              </>
            )}
          </div>
        }
      />

      <MetricTiles
        tiles={[
          { label: 'Sessions', value: pair.totalSessionsCount, icon: CalendarClock },
          {
            label: 'Time together',
            value: pair.totalMinutes ? `${Math.round((pair.totalMinutes / 60) * 10) / 10}h` : '—',
          },
          { label: 'Mentor', value: pair.mentorPosition ?? pair.mentorName },
          { label: 'Mentee', value: pair.menteePosition ?? pair.menteeName },
        ]}
      />

      {!inPair && (
        <Alert>
          <Lock className="h-4 w-4" />
          <AlertTitle>You are looking at someone else&apos;s mentoring</AlertTitle>
          <AlertDescription>
            You can see that sessions took place and what was agreed, but not what either person wrote
            privately, and not the ratings they gave each other.
          </AlertDescription>
        </Alert>
      )}

      {(pair.goals || pair.focusAreas) && (
        <Card>
          <CardHeader>
            <CardTitle>What this is for</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4">
            {pair.goals && (
              <div>
                <p className="text-sm text-muted-foreground">Goals</p>
                <p className="font-medium">{pair.goals}</p>
              </div>
            )}
            {pair.focusAreas && (
              <div>
                <p className="text-sm text-muted-foreground">Focus areas</p>
                <p className="font-medium">{pair.focusAreas}</p>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {inPair && (
        <Card>
          <CardHeader>
            <CardTitle>Ratings</CardTitle>
            <CardDescription>
              Each rating belongs to whoever gave it. Yours is shown; theirs is not, and they cannot
              see yours either.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-6">
            <div>
              <p className="text-sm text-muted-foreground">
                {pair.viewerIsMentee ? 'Your rating of the mentor' : `${pair.mentorName}'s rating`}
              </p>
              {pair.viewerIsMentee ? (
                <p className="flex items-center gap-1 text-lg font-medium">
                  <Star className="h-4 w-4" /> {pair.mentorRating ?? 'Not yet given'}
                </p>
              ) : (
                <p className="flex items-center gap-1 text-sm text-muted-foreground">
                  <Lock className="h-3.5 w-3.5" /> Private to {pair.menteeName}
                </p>
              )}
            </div>
            <div>
              <p className="text-sm text-muted-foreground">
                {pair.viewerIsMentor ? 'Your rating of the mentee' : `${pair.menteeName}'s rating`}
              </p>
              {pair.viewerIsMentor ? (
                <p className="flex items-center gap-1 text-lg font-medium">
                  <Star className="h-4 w-4" /> {pair.menteeRating ?? 'Not yet given'}
                </p>
              ) : (
                <p className="flex items-center gap-1 text-sm text-muted-foreground">
                  <Lock className="h-3.5 w-3.5" /> Private to {pair.mentorName}
                </p>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Session log</CardTitle>
          <CardDescription>
            Topics and action items are shared. Personal notes are not.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {(sessions ?? []).length === 0 ? (
            <EmptyState
              icon={CalendarClock}
              title="No sessions logged"
              description={
                isClosed
                  ? 'This relationship closed without a logged session.'
                  : 'Log the first one after you meet.'
              }
            />
          ) : (
            (sessions ?? []).map((s) => (
              <div key={s.id} className="rounded-md border p-4">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <span className="font-medium">{fmt(s.sessionDate)}</span>
                    <Badge variant="outline" className="text-[10px]">
                      {formatLabel(s.format)}
                    </Badge>
                    <span className="text-sm text-muted-foreground">{s.durationMinutes} min</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="flex items-center gap-1 text-xs text-muted-foreground">
                      {s.attendedByMentor ? (
                        <CheckCircle2 className="h-3.5 w-3.5 text-green-600" />
                      ) : (
                        <XCircle className="h-3.5 w-3.5 text-red-500" />
                      )}
                      mentor
                    </span>
                    <span className="flex items-center gap-1 text-xs text-muted-foreground">
                      {s.attendedByMentee ? (
                        <CheckCircle2 className="h-3.5 w-3.5 text-green-600" />
                      ) : (
                        <XCircle className="h-3.5 w-3.5 text-red-500" />
                      )}
                      mentee
                    </span>
                    {inPair && !isClosed && (
                      <>
                        <Button variant="ghost" size="sm" onClick={() => openEdit(s)}>
                          Edit
                        </Button>
                        <Button variant="ghost" size="sm" onClick={() => setDeleteTarget(s)}>
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </>
                    )}
                  </div>
                </div>

                {s.topicsDiscussed && (
                  <div className="mt-3">
                    <p className="text-xs text-muted-foreground">Topics discussed</p>
                    <p className="text-sm">{s.topicsDiscussed}</p>
                  </div>
                )}
                {s.actionItems && (
                  <div className="mt-2">
                    <p className="text-xs text-muted-foreground">Action items</p>
                    <p className="text-sm">{s.actionItems}</p>
                  </div>
                )}

                {inPair && (
                  <div className="mt-3 rounded-md bg-muted/40 p-3">
                    <p className="text-xs text-muted-foreground">Your private note</p>
                    <p className="text-sm">
                      {myNotesOf(s) || <span className="text-muted-foreground">Nothing written.</span>}
                    </p>
                    <p className="mt-2 flex items-center gap-1 text-xs text-muted-foreground">
                      <Lock className="h-3 w-3" /> {theirLabel}&apos;s own note is not shown to you,
                      and yours is not shown to them.
                    </p>
                  </div>
                )}
              </div>
            ))
          )}
        </CardContent>
      </Card>

      {pair.closureNotes && (
        <Card>
          <CardHeader>
            <CardTitle>Closure</CardTitle>
            <CardDescription>Ended {fmt(pair.endDate)}</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm">{pair.closureNotes}</p>
          </CardContent>
        </Card>
      )}

      <Dialog open={logOpen} onOpenChange={setLogOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit session' : 'Log a session'}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <FieldRow>
              <DateField form={form} name="sessionDate" label="Date" required />
              <NumberField form={form} name="durationMinutes" label="Duration (minutes)" required />
            </FieldRow>
            <SelectField form={form} name="format" label="Format" options={MENTORING_FORMAT_OPTIONS} />
            <TextareaField form={form} name="topicsDiscussed" label="Topics discussed" rows={3} />
            <TextareaField form={form} name="actionItems" label="Action items" rows={2} />

            <div className="space-y-1">
              <TextareaField form={form} name="ownNotes" label="Your private note" rows={3} />
              <p className="text-xs text-muted-foreground">
                Only you can read this. {theirLabel} writes their own, which you will not see.
              </p>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setLogOpen(false)} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={saveSession} disabled={busy}>
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {editing ? 'Save changes' : 'Log session'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={closeOpen}
        onOpenChange={setCloseOpen}
        title="Close this mentoring pair?"
        description="The relationship is marked completed and no further sessions can be logged."
        confirmText="Close pair"
        confirmDisabled={!closureNotes.trim()}
        onConfirm={closePair}
      >
        <div className="space-y-2">
          <TextareaLite
            value={closureNotes}
            onChange={setClosureNotes}
            placeholder="How did it end? For example: programme completed, mentee confirmed in role."
          />
        </div>
      </ConfirmationDialog>

      <ConfirmationDialog
        open={!!deleteTarget}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Delete this session?"
        description="The log entry and both private notes on it are removed. This cannot be undone."
        confirmText="Delete"
        variant="destructive"
        onConfirm={removeSession}
      />
    </div>
  );
}

/** Small uncontrolled-free textarea for dialogs that are not react-hook-form driven. */
function TextareaLite({
  value,
  onChange,
  placeholder,
}: {
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
}) {
  return (
    <textarea
      className="min-h-[90px] w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder={placeholder}
      maxLength={2000}
    />
  );
}
