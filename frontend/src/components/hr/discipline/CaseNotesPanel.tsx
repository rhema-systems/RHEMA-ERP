'use client';

import { z } from 'zod';
import { EyeOff } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { SwitchField, TextareaField } from '@/components/hr/employee/tabs/fields';
import { disciplineNoteService } from '@/services/hr/discipline.service';
import type { DisciplineNote } from '@/types/hr/discipline';

const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');

const schema = z.object({
  note: z.string().trim().min(1, 'Write the note').max(4000),
  isConfidential: z.boolean(),
});

type Form = z.infer<typeof schema>;

const empty: Form = { note: '', isConfidential: false };

/**
 * HR's running notes on the case.
 *
 * ⚠ **The author is stamped from the token and cannot be set or changed.** It used to be accepted
 * from the request body and copied verbatim, so a note could be attributed to a colleague — on a
 * record that is evidence of what HR knew and when. Fixed as ledger D-08; the create payload no
 * longer carries the field, and the update payload never did.
 *
 * ⚠ **`isConfidential` is a disclosure decision, not a label.** A confidential note is withheld
 * from the case's subject when they read their own case, so the switch decides who can see it
 * rather than how it is styled. The form says so, because "confidential" on its own reads as a
 * badge.
 *
 * ⚠ **The date cannot be edited either** — `UpdateStaffDisciplineNoteDto` carries only the text
 * and the flag, so a note is dated when it is written.
 */
export function CaseNotesPanel({
  caseId,
  canWrite,
  canDelete,
  onChanged,
}: {
  caseId: string;
  canWrite: boolean;
  canDelete: boolean;
  onChanged?: () => void;
}) {
  const queryKey = ['hr', 'discipline', caseId, 'notes'];

  return (
    <Card>
      <CardHeader><CardTitle>Case notes</CardTitle></CardHeader>
      <CardContent>
        <ResourceCollectionTab<DisciplineNote, Form>
          parentId={caseId}
          title="notes"
          singular="note"
          queryKey={queryKey}
          readOnly={!canWrite}
          dialogHint="A note on the case. You are recorded as its author."
          emptyDescription="Nothing has been recorded against this case."
          list={(id) => disciplineNoteService.getForCase(id)}
          create={(id, v) =>
            disciplineNoteService
              .add(id, { note: v.note.trim(), isConfidential: v.isConfidential })
              .then((r) => { onChanged?.(); return r; })
          }
          update={(_id, noteId, v) =>
            disciplineNoteService
              .update(noteId, { note: v.note.trim(), isConfidential: v.isConfidential })
              .then((r) => { onChanged?.(); return r; })
          }
          remove={
            canDelete
              ? (_id, noteId) =>
                  disciplineNoteService.remove(noteId).then((r) => { onChanged?.(); return r; })
              : undefined
          }
          getId={(n) => n.id}
          columns={[
            {
              header: 'Note',
              cell: (n) => <span className="whitespace-pre-wrap">{n.note}</span>,
            },
            { header: 'By', cell: (n) => n.createdByEmployeeName || '—' },
            { header: 'When', cell: (n) => fmtDateTime(n.noteDate) },
            {
              header: '',
              cell: (n) =>
                n.isConfidential ? (
                  <Badge variant="outline" className="gap-1">
                    <EyeOff className="h-3 w-3" />
                    Confidential
                  </Badge>
                ) : null,
            },
          ]}
          schema={schema}
          emptyForm={empty}
          toForm={(n) => ({ note: n.note, isConfidential: n.isConfidential })}
          renderFields={(form, editing) => (
            <>
              <TextareaField
                form={form}
                name="note"
                label="Note"
                rows={6}
                placeholder="e.g. Spoke to the supervisor; confirmed the till was reconciled on the day."
              />
              <SwitchField
                form={form}
                name="isConfidential"
                label="Withhold from the employee"
                description="A confidential note is hidden when the case's subject reads their own case."
              />
              <p className="text-xs text-muted-foreground">
                {editing
                  ? 'The author and the date were set when the note was written and cannot be changed.'
                  : 'You will be recorded as the author, with today’s date.'}
              </p>
            </>
          )}
        />
      </CardContent>
    </Card>
  );
}
