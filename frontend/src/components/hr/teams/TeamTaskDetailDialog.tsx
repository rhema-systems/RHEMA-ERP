'use client';

/**
 * One task, opened: its tick list, its files, and the two moves that need words.
 *
 * ⚠ **This is where an ordinary member does their work.** The tab's row menu offers the simple
 * transitions; blocking needs a reason and completing may want a note, so those live here where
 * there is room to type. Adding, ticking and attaching are all writes the server allows to the
 * task's own assignee — the one thing an ordinary member may do.
 *
 * ⚠ **Nothing here decides who the caller is.** Every control is offered and the server refuses
 * what it should, with a sentence. Hiding a button is a courtesy, not a permission model.
 *
 * Round 2, lane F1 (plan § 6.6).
 */

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
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
import { teamActivityService } from '@/services/hr/team-activity.service';
import { TEAM_TASK_STATUS_LABELS, type TeamTaskStatus } from '@/types/hr/team-activity';

export function TeamTaskDetailDialog({
  taskId,
  onOpenChange,
  onChanged,
}: {
  taskId: string | null;
  onOpenChange: (open: boolean) => void;
  onChanged: () => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [newItem, setNewItem] = useState('');
  const [blockedReason, setBlockedReason] = useState('');
  const [completionNotes, setCompletionNotes] = useState('');
  const [attachOpen, setAttachOpen] = useState(false);

  const { data: task, isLoading } = useQuery({
    queryKey: ['hr', 'team-task', taskId],
    queryFn: () => teamActivityService.getTaskById(taskId!),
    enabled: !!taskId,
  });

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['hr', 'team-task', taskId] });
    onChanged();
  };

  /** Every write here fails the same way, so it is reported the same way — with the rule's own words. */
  const run = async (what: string, fn: () => Promise<unknown>) => {
    try {
      await fn();
      refresh();
    } catch (e: any) {
      toast({
        variant: 'destructive',
        title: `Could not ${what}`,
        description: e?.body?.message ?? e?.message ?? 'The server refused that.',
      });
    }
  };

  const move = (status: TeamTaskStatus) =>
    run(`move this task to ${TEAM_TASK_STATUS_LABELS[status].toLowerCase()}`, () =>
      teamActivityService.changeTaskStatus(taskId!, {
        status,
        blockedReason: status === 'Blocked' ? blockedReason : undefined,
        completionNotes: status === 'Completed' ? completionNotes || undefined : undefined,
      }),
    );

  return (
    <>
      <Dialog open={!!taskId} onOpenChange={onOpenChange}>
        <DialogContent className="sm:max-w-[680px]">
          <DialogHeader>
            <DialogTitle>{task?.title ?? 'Task'}</DialogTitle>
            <DialogDescription>
              {task?.objectiveTitle ? `Under: ${task.objectiveTitle}` : 'Not under an objective'}
              {task?.assigneeName ? ` · ${task.assigneeName}` : ''}
            </DialogDescription>
          </DialogHeader>

          {isLoading || !task ? (
            <div className="flex justify-center py-8">
              <Loader2 className="h-5 w-5 animate-spin" />
            </div>
          ) : (
            <div className="space-y-5">
              <div className="flex items-center gap-2">
                <Badge>{TEAM_TASK_STATUS_LABELS[task.status]}</Badge>
                {task.isOverdue && <Badge variant="destructive">Overdue</Badge>}
                {task.dueDate && (
                  <span className="text-muted-foreground text-xs">
                    Due {task.dueDate.slice(0, 10)}
                  </span>
                )}
              </div>

              {task.description && <p className="text-sm">{task.description}</p>}

              {/* ── the tick list ─────────────────────────────────────────── */}
              <div className="space-y-2">
                <Label className="text-xs uppercase tracking-wide">
                  Checklist ({task.checklistDone}/{task.checklistTotal})
                </Label>
                {task.checklistItems.map((item) => (
                  <div key={item.id} className="flex items-center gap-2">
                    <Checkbox
                      checked={item.isDone}
                      onCheckedChange={(checked) =>
                        run('tick that off', () =>
                          teamActivityService.updateChecklistItem(item.id, {
                            text: item.text,
                            displayOrder: item.displayOrder,
                            isDone: checked === true,
                          }),
                        )
                      }
                    />
                    <span className={item.isDone ? 'text-muted-foreground text-sm line-through' : 'text-sm'}>
                      {item.text}
                    </span>
                    {/* Who ticked it and when — a tick nobody owns is worse than no tick. */}
                    {item.isDone && item.doneByName && (
                      <span className="text-muted-foreground text-xs">
                        · {item.doneByName}
                      </span>
                    )}
                    <Button
                      variant="ghost"
                      size="sm"
                      className="ml-auto h-7 px-2"
                      onClick={() =>
                        run('remove that item', () => teamActivityService.deleteChecklistItem(item.id))
                      }
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                  </div>
                ))}
                <div className="flex gap-2">
                  <Input
                    value={newItem}
                    onChange={(e) => setNewItem(e.target.value)}
                    placeholder="Add a step…"
                    onKeyDown={(e) => {
                      if (e.key !== 'Enter' || !newItem.trim()) return;
                      e.preventDefault();
                      void run('add that item', async () => {
                        await teamActivityService.addChecklistItem(task.id, {
                          text: newItem.trim(),
                          displayOrder: task.checklistItems.length + 1,
                        });
                        setNewItem('');
                      });
                    }}
                  />
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={!newItem.trim()}
                    onClick={() =>
                      run('add that item', async () => {
                        await teamActivityService.addChecklistItem(task.id, {
                          text: newItem.trim(),
                          displayOrder: task.checklistItems.length + 1,
                        });
                        setNewItem('');
                      })
                    }
                  >
                    <Plus className="h-3.5 w-3.5" />
                  </Button>
                </div>
              </div>

              {/* ── files ─────────────────────────────────────────────────── */}
              <div className="space-y-2">
                <Label className="text-xs uppercase tracking-wide">
                  Files ({task.attachments.length})
                </Label>
                {task.attachments.map((a) => (
                  <div key={a.id} className="flex items-center gap-2 text-sm">
                    <Paperclip className="h-3.5 w-3.5" />
                    <a className="text-primary underline" href={teamActivityService.taskAttachmentUrl(a.id)}>
                      {a.title || a.fileName || 'Attachment'}
                    </a>
                    {a.uploadedByName && (
                      <span className="text-muted-foreground text-xs">· {a.uploadedByName}</span>
                    )}
                    <Button
                      variant="ghost"
                      size="sm"
                      className="ml-auto h-7 px-2"
                      onClick={() =>
                        run('remove that file', () => teamActivityService.deleteTaskAttachment(a.id))
                      }
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                  </div>
                ))}
                <Button variant="outline" size="sm" onClick={() => setAttachOpen(true)}>
                  Attach a file…
                </Button>
              </div>

              {/* ── the two moves that need words ─────────────────────────── */}
              <div className="space-y-3 border-t pt-4">
                {task.status !== 'Blocked' && (
                  <div className="space-y-2">
                    <Label htmlFor="blocked-reason">What is blocking this?</Label>
                    <Textarea
                      id="blocked-reason"
                      rows={2}
                      value={blockedReason}
                      onChange={(e) => setBlockedReason(e.target.value)}
                      placeholder="Waiting on the site access permit"
                    />
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={!blockedReason.trim()}
                      onClick={() => move('Blocked')}
                    >
                      Mark blocked
                    </Button>
                  </div>
                )}

                {task.status !== 'Completed' && (
                  <div className="space-y-2">
                    <Label htmlFor="completion-notes">Completion notes</Label>
                    <Textarea
                      id="completion-notes"
                      rows={2}
                      value={completionNotes}
                      onChange={(e) => setCompletionNotes(e.target.value)}
                      placeholder="Optional — what was actually done."
                    />
                    <Button size="sm" onClick={() => move('Completed')}>
                      Mark complete
                    </Button>
                  </div>
                )}

                {task.status === 'Completed' && task.completionNotes && (
                  <p className="text-muted-foreground text-sm">
                    Completed {task.completedOn?.slice(0, 10)} · {task.completionNotes}
                  </p>
                )}
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>

      <AttachFileDialog
        open={attachOpen}
        onOpenChange={setAttachOpen}
        title={`Attach a file — ${task?.title ?? ''}`}
        description="It goes through the document store, scanned and registered, before it reaches the task."
        upload={(file) => teamActivityService.uploadTaskAttachment(taskId!, file)}
        onUploaded={refresh}
      />
    </>
  );
}
