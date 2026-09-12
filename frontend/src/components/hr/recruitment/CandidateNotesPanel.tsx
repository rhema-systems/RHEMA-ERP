'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Lock, MessageSquare, Pencil, Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { formatDateTime } from '@/lib/hr/attendance-format';
import { jobCandidateService } from '@/services/hr/recruitment-pipeline.service';
import type { CandidateNote } from '@/types/hr/recruitment-pipeline';

/**
 * Recruiters' notes on a candidate.
 *
 * ⚠ **"Private" here means private to HR, not to the author.** The whole `job-candidates` controller
 * is HR-gated, so every note on this screen is already invisible outside HR; the flag marks the ones
 * that must not be repeated to the candidate or a hiring manager who is shown the record. That is a
 * different rule from the performance journal, where a private entry is owner-only — worth stating
 * plainly on the toggle, because people misjudge it in both directions.
 */
export function CandidateNotesPanel({
  candidateId,
  canEdit,
}: {
  candidateId: string;
  canEdit: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [editing, setEditing] = useState<CandidateNote | null>(null);
  const [adding, setAdding] = useState(false);
  const [deleting, setDeleting] = useState<CandidateNote | null>(null);
  const [text, setText] = useState('');
  const [isPrivate, setIsPrivate] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'candidate-notes', candidateId],
    queryFn: () => jobCandidateService.getNotes(candidateId, true),
    enabled: !!candidateId,
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'candidate-notes', candidateId] });

  const save = useMutation({
    mutationFn: () => {
      const payload = { noteText: text.trim(), isPrivate };
      return editing
        ? jobCandidateService.updateNote(candidateId, editing.id, payload)
        : jobCandidateService.addNote(candidateId, payload);
    },
    onSuccess: async () => {
      await refresh();
      close();
      toast({ title: 'Note saved' });
    },
    onError: (e: any) => toast({ title: 'Could not save', description: e?.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: (noteId: string) => jobCandidateService.deleteNote(noteId),
    onSuccess: async () => {
      await refresh();
      setDeleting(null);
      toast({ title: 'Note removed' });
    },
    onError: (e: any) => toast({ title: 'Could not remove', description: e?.message, variant: 'destructive' }),
  });

  const close = () => {
    setAdding(false);
    setEditing(null);
    setText('');
    setIsPrivate(false);
  };

  const openAdd = () => {
    setText('');
    setIsPrivate(false);
    setAdding(true);
  };

  const openEdit = (note: CandidateNote) => {
    setText(note.noteText);
    setIsPrivate(note.isPrivate);
    setEditing(note);
  };

  const notes = data ?? [];

  return (
    <div className="space-y-4">
      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
        </div>
      ) : notes.length === 0 ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={MessageSquare}
              title="No notes"
              description="Record impressions, follow-ups and anything worth remembering about this candidate."
            />
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-3">
          {notes.map((n) => (
            <Card key={n.id}>
              <CardContent className="space-y-2 pt-5">
                <div className="flex items-start justify-between gap-4">
                  <p className="whitespace-pre-wrap text-sm">{n.noteText}</p>
                  {canEdit && (
                    <div className="flex shrink-0 gap-0.5">
                      <Button variant="ghost" size="icon" onClick={() => openEdit(n)} aria-label="Edit">
                        <Pencil className="h-3.5 w-3.5" />
                      </Button>
                      <Button variant="ghost" size="icon" onClick={() => setDeleting(n)} aria-label="Remove">
                        <Trash2 className="h-3.5 w-3.5" />
                      </Button>
                    </div>
                  )}
                </div>
                <div className="flex items-center gap-2 text-xs text-muted-foreground">
                  <span>{formatDateTime(n.createdAt)}</span>
                  {n.isPrivate && (
                    <Badge variant="secondary" className="gap-1">
                      <Lock className="h-3 w-3" />
                      Private to HR
                    </Badge>
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {canEdit && (
        <Button variant="outline" onClick={openAdd}>
          <Plus className="mr-2 h-4 w-4" />
          Add note
        </Button>
      )}

      <Dialog open={adding || !!editing} onOpenChange={(o) => !o && close()}>
        <DialogContent className="sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit note' : 'Add note'}</DialogTitle>
            <DialogDescription>
              Notes are visible to HR only. Everything on this record is already outside the
              candidate&rsquo;s and hiring managers&rsquo; reach.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <Textarea value={text} onChange={(e) => setText(e.target.value)} rows={6} />
            <div className="flex items-center justify-between rounded-md border p-3">
              <div className="space-y-0.5">
                <Label>Private</Label>
                <p className="text-xs text-muted-foreground">
                  Marks the note as not to be shared outside HR — including with a hiring manager who
                  is walked through this record.
                </p>
              </div>
              <Switch checked={isPrivate} onCheckedChange={setIsPrivate} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={close}>
              Cancel
            </Button>
            <Button disabled={!text.trim() || save.isPending} onClick={() => save.mutate()}>
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={!!deleting}
        onOpenChange={(o) => !o && setDeleting(null)}
        title="Remove this note?"
        description="It cannot be recovered."
        confirmText="Remove"
        variant="destructive"
        onConfirm={async () => {
          if (deleting) await remove.mutateAsync(deleting.id);
          return true;
        }}
      />
    </div>
  );
}
