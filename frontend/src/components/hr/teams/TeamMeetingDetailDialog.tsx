'use client';

/**
 * One meeting, opened: the register, the decisions, and the signed minutes.
 *
 * ⚠ **The register is marked in PASSES, not replaced.** The chair ticks the room, then somebody's
 * apology arrives late and gets added. The hold endpoint merges — anyone left out of a pass keeps
 * what was already recorded — so re-holding an already-held meeting is how a correction is made
 * rather than an error.
 *
 * ⚠ **Attendance is tri-state.** Null is "not yet known", which is what every invitee is before the
 * meeting happens, and is a different fact from "did not come". A two-state checkbox would silently
 * record the whole room as absent the moment the dialog opened.
 *
 * ⚠ **"Create task" on a decision row is the point of the minute book.** The assignee and due date
 * come from the DECISION — the server ignores anything this screen might send for them — so the
 * minute and the board cannot end up saying different things about who owns the action.
 *
 * Round 2, lane F2 (plan § 6.6).
 */

import { useEffect, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Paperclip, Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { AttachFileDialog } from '@/components/hr/common/AttachFileDialog';
import { hrDocumentService } from '@/services/hr/hr-document.service';
import { teamMeetingService } from '@/services/hr/team-meeting.service';
import { TEAM_MEETING_STATUS_LABELS } from '@/types/hr/team-meeting';

/** Local, editable copy of one line of the register. */
interface Mark {
  attended: boolean | null;
  apology: boolean;
  notes: string;
}

/** `datetime-local` wants wall-clock, not an instant. */
const localNow = () => {
  const d = new Date();
  d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
  return d.toISOString().slice(0, 16);
};

const TRI_STATE: [boolean | null, string][] = [
  [true, 'Came'],
  [false, 'Absent'],
  [null, 'Not known'],
];

export function TeamMeetingDetailDialog({
  meetingId,
  onOpenChange,
  onChanged,
}: {
  meetingId: string | null;
  onOpenChange: (open: boolean) => void;
  onChanged: () => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [marks, setMarks] = useState<Record<string, Mark>>({});
  const [heldAt, setHeldAt] = useState('');
  const [minutes, setMinutes] = useState('');
  const [newDecision, setNewDecision] = useState('');
  const [attachOpen, setAttachOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  const { data: meeting, isLoading } = useQuery({
    queryKey: ['hr', 'team-meeting', meetingId],
    queryFn: () => teamMeetingService.getMeeting(meetingId!),
    enabled: !!meetingId,
  });

  /**
   * ⚠ Seeded from the server, never from a blank. The merge on the server protects rows this
   * screen omits — not rows it sends back wrong — so a dialog opened on a half-marked register
   * has to start from what is already recorded.
   */
  useEffect(() => {
    if (!meeting) return;
    setMarks(
      Object.fromEntries(
        meeting.attendees.map((a) => [
          a.memberId,
          { attended: a.attended ?? null, apology: a.apology, notes: a.notes ?? '' },
        ]),
      ),
    );
    setHeldAt(meeting.heldAt ? meeting.heldAt.slice(0, 16) : localNow());
    setMinutes(meeting.minutes ?? '');
  }, [meeting]);

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['hr', 'team-meeting', meetingId] });
    onChanged();
  };

  /** Every write here fails the same way, so it is reported the same way — in the rule's own words. */
  const run = async (what: string, fn: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await fn();
      refresh();
    } catch (e: unknown) {
      const err = e as { body?: { message?: string }; message?: string };
      toast({
        variant: 'destructive',
        title: `Could not ${what}`,
        description: err?.body?.message ?? err?.message ?? 'The server refused that.',
      });
    } finally {
      setBusy(false);
    }
  };

  const setMark = (memberId: string, patch: Partial<Mark>) =>
    setMarks((m) => {
      const base: Mark = m[memberId] ?? { attended: null, apology: false, notes: '' };
      return { ...m, [memberId]: { ...base, ...patch } };
    });

  const saveRegister = () =>
    run(meeting?.status === 'Held' ? 'save the register' : 'record this meeting as held', () =>
      teamMeetingService.holdMeeting(meetingId!, {
        heldAt: new Date(heldAt).toISOString(),
        minutes: minutes || null,
        attendance: Object.entries(marks).map(([memberId, m]) => ({
          memberId,
          attended: m.attended,
          apology: m.apology,
          notes: m.notes || null,
        })),
      }),
    );

  const held = meeting?.status === 'Held';
  const cancelled = meeting?.status === 'Cancelled';

  return (
    <>
      <Dialog open={!!meetingId} onOpenChange={onOpenChange}>
        <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-[720px]">
          <DialogHeader>
            <DialogTitle>{meeting?.title ?? 'Meeting'}</DialogTitle>
            <DialogDescription>
              {meeting
                ? `${meeting.kind === 'SiteVisit' ? 'Site visit' : meeting.kind} · ${new Date(
                    meeting.heldAt ?? meeting.scheduledAt,
                  ).toLocaleString()}${meeting.venue ? ` · ${meeting.venue}` : ''}`
                : ''}
            </DialogDescription>
          </DialogHeader>

          {isLoading || !meeting ? (
            <div className="flex justify-center py-8">
              <Loader2 className="h-5 w-5 animate-spin" />
            </div>
          ) : (
            <div className="space-y-5">
              <div className="flex items-center gap-2">
                <Badge variant={held ? 'default' : cancelled ? 'outline' : 'secondary'}>
                  {TEAM_MEETING_STATUS_LABELS[meeting.status]}
                </Badge>
                {meeting.chairName && (
                  <span className="text-muted-foreground text-xs">Chair: {meeting.chairName}</span>
                )}
              </div>

              {cancelled && meeting.cancelledReason && (
                <p className="text-muted-foreground text-sm">
                  Cancelled — {meeting.cancelledReason}
                </p>
              )}

              {meeting.agenda && (
                <div className="space-y-1">
                  <Label className="text-xs uppercase tracking-wide">Agenda</Label>
                  <p className="whitespace-pre-wrap text-sm">{meeting.agenda}</p>
                </div>
              )}

              {/* ── the register ──────────────────────────────────────────── */}
              {!cancelled && (
                <div className="space-y-2">
                  <Label className="text-xs uppercase tracking-wide">
                    Register ({meeting.attendedCount}/{meeting.attendeeCount} came)
                  </Label>
                  {meeting.attendees.length === 0 && (
                    <p className="text-muted-foreground text-sm">
                      Nobody was invited. Edit the meeting to set the invitee list.
                    </p>
                  )}
                  {meeting.attendees.map((a) => {
                    const m = marks[a.memberId] ?? { attended: null, apology: false, notes: '' };
                    return (
                      <div key={a.id} className="flex flex-wrap items-center gap-2 text-sm">
                        <span className="min-w-[9rem] flex-1">{a.memberName || a.memberId}</span>
                        {/*
                          Three states, three buttons. "Not known" stays selectable so a wrong tick
                          can be taken back to the truth rather than only to its opposite.
                        */}
                        <div className="flex gap-1">
                          {TRI_STATE.map(([value, label]) => (
                            <Button
                              key={label}
                              type="button"
                              size="sm"
                              className="h-7 px-2 text-xs"
                              variant={m.attended === value ? 'default' : 'outline'}
                              onClick={() => setMark(a.memberId, { attended: value })}
                            >
                              {label}
                            </Button>
                          ))}
                        </div>
                        <label className="flex items-center gap-1.5 text-xs">
                          <Checkbox
                            checked={m.apology}
                            onCheckedChange={(c) => setMark(a.memberId, { apology: c === true })}
                          />
                          Apology
                        </label>
                        <Input
                          className="h-7 w-36 text-xs"
                          value={m.notes}
                          placeholder="Note"
                          onChange={(e) => setMark(a.memberId, { notes: e.target.value })}
                        />
                      </div>
                    );
                  })}

                  <div className="space-y-1 pt-2">
                    <Label htmlFor="held-at" className="text-xs">
                      Held at
                    </Label>
                    <Input
                      id="held-at"
                      type="datetime-local"
                      className="sm:w-64"
                      value={heldAt}
                      onChange={(e) => setHeldAt(e.target.value)}
                    />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="minutes" className="text-xs">
                      Minutes
                    </Label>
                    <Textarea
                      id="minutes"
                      rows={4}
                      value={minutes}
                      onChange={(e) => setMinutes(e.target.value)}
                      placeholder="What was discussed. The signed copy can be attached below."
                    />
                  </div>
                  <Button size="sm" disabled={busy} onClick={saveRegister}>
                    {busy && <Loader2 className="mr-2 h-3.5 w-3.5 animate-spin" />}
                    {held ? 'Save the register' : 'Record as held'}
                  </Button>
                </div>
              )}

              {/* ── decisions ─────────────────────────────────────────────── */}
              <div className="space-y-2 border-t pt-4">
                <Label className="text-xs uppercase tracking-wide">
                  Decisions ({meeting.decisions.length})
                </Label>
                {meeting.decisions.map((d) => (
                  <div key={d.id} className="space-y-1 rounded-md border p-2">
                    <p className="text-sm">{d.text}</p>
                    <div className="flex flex-wrap items-center gap-2 text-xs">
                      <span className="text-muted-foreground">
                        {d.responsibleName
                          ? `Responsible: ${d.responsibleName}`
                          : 'Nobody responsible'}
                        {d.dueDate ? ` · due ${d.dueDate.slice(0, 10)}` : ''}
                      </span>
                      {d.raisedTaskId ? (
                        <Badge variant="outline">Task raised: {d.raisedTaskTitle}</Badge>
                      ) : (
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          className="h-7 px-2 text-xs"
                          disabled={busy}
                          onClick={() =>
                            run('raise a task from that decision', () =>
                              teamMeetingService.raiseTaskFromDecision(d.id, {
                                objectiveId: null,
                                priority: 'Normal',
                              }),
                            )
                          }
                        >
                          Create task
                        </Button>
                      )}
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        className="ml-auto h-7 px-2"
                        disabled={busy}
                        onClick={() =>
                          run('remove that decision', () => teamMeetingService.deleteDecision(d.id))
                        }
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </Button>
                    </div>
                  </div>
                ))}

                <div className="flex gap-2">
                  <Input
                    value={newDecision}
                    placeholder="Minute a decision…"
                    onChange={(e) => setNewDecision(e.target.value)}
                  />
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={busy || !newDecision.trim()}
                    onClick={() =>
                      run('minute that decision', async () => {
                        await teamMeetingService.addDecision(meeting.id, {
                          text: newDecision.trim(),
                          displayOrder: meeting.decisions.length + 1,
                        });
                        setNewDecision('');
                      })
                    }
                  >
                    <Plus className="h-3.5 w-3.5" />
                  </Button>
                </div>
                <p className="text-muted-foreground text-xs">
                  A raised task takes its owner and due date from the decision, and lands on the board
                  unattached to an objective — move it under one from the Tasks tab if it belongs to a
                  goal.
                </p>
              </div>

              {/* ── the signed minutes ────────────────────────────────────── */}
              <div className="space-y-2 border-t pt-4">
                <Label className="text-xs uppercase tracking-wide">Signed minutes</Label>
                {meeting.hasMinutesDocument ? (
                  <div className="flex items-center gap-2 text-sm">
                    <Paperclip className="h-3.5 w-3.5" />
                    {/*
                      ⚠ Never a plain <a href>: the file sits behind a bearer-token endpoint, so a
                      link would 401. It is fetched as a blob and handed to the browser.
                    */}
                    <button
                      type="button"
                      className="text-primary underline"
                      onClick={() =>
                        run('download the minutes', () =>
                          hrDocumentService.download(
                            teamMeetingService.minutesUrl(meeting.id),
                            meeting.minutesFileName || 'minutes',
                          ),
                        )
                      }
                    >
                      {meeting.minutesFileName || 'Minutes'}
                    </button>
                  </div>
                ) : (
                  <p className="text-muted-foreground text-sm">Nothing on file.</p>
                )}
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setAttachOpen(true)}
                >
                  {meeting.hasMinutesDocument ? 'Replace the minutes…' : 'Attach the minutes…'}
                </Button>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>

      <AttachFileDialog
        open={attachOpen}
        onOpenChange={setAttachOpen}
        title={`Signed minutes — ${meeting?.title ?? ''}`}
        description="It goes through the document store, scanned and registered, before it reaches the meeting."
        currentFileName={meeting?.minutesFileName}
        currentFileSize={meeting?.minutesFileSizeBytes}
        downloadUrl={meetingId ? teamMeetingService.minutesUrl(meetingId) : undefined}
        upload={(file) => teamMeetingService.uploadMinutes(meetingId!, file)}
        onUploaded={refresh}
      />
    </>
  );
}
