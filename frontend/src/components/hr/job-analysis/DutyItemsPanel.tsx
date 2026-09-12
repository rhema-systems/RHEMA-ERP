'use client';

import { z } from 'zod';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { NumberField, TextareaField, TextField } from '@/components/hr/employee/tabs/fields';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import type { JobDutyItem } from '@/types/hr/job-architecture';
import { childKey, nullIfBlank, type ChildPanelProps } from './shared';

const schema = z.object({
  sequenceNumber: z.coerce.number().int().min(0, 'Use 0 to place it at the end'),
  dutyStatement: z.string().trim().min(1, 'The duty statement is required').max(1000),
  notes: z.string().max(1000).optional(),
});

type Form = z.infer<typeof schema>;

const empty: Form = { sequenceNumber: 0, dutyStatement: '', notes: '' };

/**
 * The numbered duty statements — the itemised list that reads as the job description's body.
 *
 * ⚠ **Zero is not a position, it is a request.** `AddDutyItemAsync` treats any sequence number at
 * or below zero as "put it last" and calls `GetNextSequenceNumberAsync`; a positive number is
 * honoured as typed. So the field defaults to 0 and says so, rather than defaulting to 1 and
 * quietly colliding with the duty that is already first.
 *
 * ⚠ **Nothing renumbers.** There is no resequence endpoint on this collection, so inserting a duty
 * between two others means editing the numbers of the ones below it by hand. Duplicates are
 * accepted by the API; the list falls back to insertion order when they tie.
 */
export function DutyItemsPanel({ jobDescriptionId, canAuthor, canDelete, invalidateKeys }: ChildPanelProps) {
  return (
    <ResourceCollectionTab<JobDutyItem, Form>
      parentId={jobDescriptionId}
      title="duties"
      singular="duty"
      queryKey={childKey(jobDescriptionId, 'duty-items')}
      invalidateKeys={invalidateKeys}
      readOnly={!canAuthor}
      dialogHint="One numbered statement of something the holder does."
      emptyDescription="The duties are the numbered statements that make up the body of the job description."
      list={(id) => jobArchitectureService.getDutyItems(id)}
      create={(id, v) =>
        jobArchitectureService.addDutyItem(id, {
          sequenceNumber: v.sequenceNumber,
          dutyStatement: v.dutyStatement.trim(),
          notes: nullIfBlank(v.notes),
        })
      }
      update={(_id, dutyId, v) =>
        jobArchitectureService.updateDutyItem(dutyId, {
          sequenceNumber: v.sequenceNumber,
          dutyStatement: v.dutyStatement.trim(),
          notes: nullIfBlank(v.notes),
        })
      }
      remove={canDelete ? (_id, dutyId) => jobArchitectureService.deleteDutyItem(dutyId) : undefined}
      getId={(d) => d.id}
      columns={[
        { header: '#', cell: (d) => d.sequenceNumber, className: 'w-[60px]' },
        { header: 'Duty', cell: (d) => <span className="whitespace-pre-wrap">{d.dutyStatement}</span> },
        {
          header: 'Notes',
          cell: (d) => <span className="text-muted-foreground">{d.notes || '—'}</span>,
        },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={(d) => ({
        sequenceNumber: d.sequenceNumber,
        dutyStatement: d.dutyStatement,
        notes: d.notes ?? '',
      })}
      renderFields={(form) => (
        <>
          <NumberField
            form={form}
            name="sequenceNumber"
            label="Number"
            required
            placeholder="0 places it at the end"
          />
          <TextareaField
            form={form}
            name="dutyStatement"
            label="Duty statement"
            rows={3}
            placeholder="e.g. Reconciles the general ledger at each month end."
          />
          <TextField form={form} name="notes" label="Notes" placeholder="Optional" />
        </>
      )}
    />
  );
}
