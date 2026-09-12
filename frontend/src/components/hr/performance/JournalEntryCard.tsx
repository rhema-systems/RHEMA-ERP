'use client';

import { useState } from 'react';
import { BookLock, ChevronDown, ChevronRight, Share2, Target, Trash2, Pencil, Undo2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { formatDateTime } from '@/lib/hr/attendance-format';
import type { PerformanceJournalEntry } from '@/types/hr/journal';

interface JournalEntryCardProps {
  entry: PerformanceJournalEntry;
  onEdit: () => void;
  onDelete: () => void;
  onTogglePrivacy: () => void;
}

/**
 * One journal entry, collapsed to its title until opened.
 *
 * The private/shared state is the most important thing on the card, so it is a badge rather than
 * an icon: someone scanning their journal needs to see at a glance which notes their manager can
 * read. Sharing is offered as an explicit, reversible action with the consequence spelled out —
 * "your manager and HR can read it" — because the entry is otherwise invisible to them and people
 * misjudge that either way.
 */
export function JournalEntryCard({
  entry,
  onEdit,
  onDelete,
  onTogglePrivacy,
}: JournalEntryCardProps) {
  const [open, setOpen] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [confirmShare, setConfirmShare] = useState(false);

  return (
    <>
      <Card>
        <CardContent className="space-y-3 p-4">
          <div className="flex items-start justify-between gap-3">
            <button
              type="button"
              className="flex flex-1 items-start gap-2 text-left"
              onClick={() => setOpen((o) => !o)}
              aria-expanded={open}
            >
              {open ? (
                <ChevronDown className="mt-1 h-4 w-4 shrink-0 text-muted-foreground" />
              ) : (
                <ChevronRight className="mt-1 h-4 w-4 shrink-0 text-muted-foreground" />
              )}
              <div className="space-y-1">
                <p className="font-medium leading-snug">{entry.title}</p>
                <p className="text-xs text-muted-foreground">
                  {formatDateTime(entry.entryDate)}
                  {entry.cycleCode ? ` · ${entry.cycleCode}` : ''}
                  {entry.subjectEmployeeName ? ` · about ${entry.subjectEmployeeName}` : ''}
                </p>
              </div>
            </button>

            <div className="flex shrink-0 items-center gap-2">
              {entry.isPrivate ? (
                <Badge variant="secondary" className="gap-1">
                  <BookLock className="h-3 w-3" />
                  Private
                </Badge>
              ) : (
                <Badge variant="default" className="gap-1">
                  <Share2 className="h-3 w-3" />
                  Shared
                </Badge>
              )}
            </div>
          </div>

          {entry.relatedGoalTitle && (
            <p className="flex items-center gap-2 text-xs text-muted-foreground">
              <Target className="h-3 w-3" />
              {entry.relatedGoalTitle}
            </p>
          )}

          {open && (
            <>
              <p className="whitespace-pre-wrap text-sm leading-relaxed">{entry.body}</p>
              <div className="flex flex-wrap items-center gap-2 pt-1">
                <Button variant="outline" size="sm" onClick={onEdit}>
                  <Pencil className="mr-2 h-3.5 w-3.5" />
                  Edit
                </Button>
                {entry.isPrivate ? (
                  <Button variant="outline" size="sm" onClick={() => setConfirmShare(true)}>
                    <Share2 className="mr-2 h-3.5 w-3.5" />
                    Share with my manager
                  </Button>
                ) : (
                  <Button variant="outline" size="sm" onClick={onTogglePrivacy}>
                    <Undo2 className="mr-2 h-3.5 w-3.5" />
                    Make private again
                  </Button>
                )}
                <Button
                  variant="ghost"
                  size="sm"
                  className="text-destructive"
                  onClick={() => setConfirmDelete(true)}
                >
                  <Trash2 className="mr-2 h-3.5 w-3.5" />
                  Delete
                </Button>
              </div>
            </>
          )}
        </CardContent>
      </Card>

      {/* Sharing is one-way in effect — the manager may have read it by the time you undo. */}
      <AlertDialog open={confirmShare} onOpenChange={setConfirmShare}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Share this entry?</AlertDialogTitle>
            <AlertDialogDescription>
              Your line manager and HR will be able to read “{entry.title}”. You can make it
              private again later, but anyone who has already read it will have seen it.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={onTogglePrivacy}>Share</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog open={confirmDelete} onOpenChange={setConfirmDelete}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete this entry?</AlertDialogTitle>
            <AlertDialogDescription>
              “{entry.title}” will be removed from your journal. This cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={onDelete}>Delete</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
