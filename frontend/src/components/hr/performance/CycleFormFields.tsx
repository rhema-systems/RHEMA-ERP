'use client';

import type { FieldValues, Path, UseFormReturn } from 'react-hook-form';
import { DateField, FieldRow } from '@/components/hr/employee/tabs/fields';

/**
 * The phase dates on an appraisal cycle, in pipeline order.
 *
 * All 21 are optional, and leaving one blank is a real choice rather than an omission: a phase
 * with no deadline simply never shows up on the calendar, in the progress dashboard's deadline
 * risks, or in the reminders HR sends. That is how an organisation running no interim reviews
 * or no peer nominations turns those phases off — there is no separate switch.
 *
 * Grouped the way the pipeline actually runs, so the "year-end" block reads top to bottom in
 * the order the steps happen.
 */
type Row = [label: string, openField: string | null, deadlineField: string];

const GROUPS: { title: string; hint?: string; rows: Row[] }[] = [
  {
    title: 'Goal setting',
    hint: 'The window employees write and submit their goals in.',
    rows: [['Goal setting', 'goalSettingOpenDate', 'goalSettingDeadline']],
  },
  {
    title: 'Interim reviews',
    hint: 'Only the ones your settings profile’s review frequency generates are used.',
    rows: [
      ['Q1 review', 'q1ReviewOpenDate', 'q1ReviewDeadline'],
      ['Mid-year review', 'midYearOpenDate', 'midYearDeadline'],
      ['Q3 review', 'q3ReviewOpenDate', 'q3ReviewDeadline'],
    ],
  },
  {
    title: 'Year-end pipeline',
    hint: 'The order below is the order the steps run in.',
    rows: [
      ['Peer nomination', null, 'peerNominationDeadline'],
      ['Self-evaluation', 'selfEvaluationOpenDate', 'selfEvaluationDeadline'],
      ['Peer evaluation', 'peerEvaluationOpenDate', 'peerEvaluationDeadline'],
      ['Manager evaluation', 'managerEvaluationOpenDate', 'managerEvaluationDeadline'],
      ['Calibration', 'calibrationOpenDate', 'calibrationDeadline'],
      ['HR review', 'hrReviewOpenDate', 'hrReviewDeadline'],
      ['Employee acknowledgment', null, 'employeeAcknowledgeDeadline'],
      ['Final conversation', null, 'finalConversationDeadline'],
    ],
  },
];

export function CyclePhaseDateFields<T extends FieldValues>({ form }: { form: UseFormReturn<T> }) {
  return (
    <div className="space-y-6">
      {GROUPS.map((group) => (
        <div key={group.title} className="space-y-3">
          <div>
            <p className="text-sm font-medium">{group.title}</p>
            {group.hint && <p className="text-xs text-muted-foreground">{group.hint}</p>}
          </div>
          {group.rows.map(([label, openField, deadlineField]) => (
            <FieldRow key={deadlineField}>
              {openField ? (
                <DateField form={form} name={openField as Path<T>} label={`${label} opens`} />
              ) : (
                <div />
              )}
              <DateField
                form={form}
                name={deadlineField as Path<T>}
                label={`${label} deadline`}
              />
            </FieldRow>
          ))}
        </div>
      ))}
    </div>
  );
}
