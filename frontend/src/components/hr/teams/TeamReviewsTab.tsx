'use client';

/**
 * Periodic reviews of a team — how it did over a quarter, or by the end of a project.
 *
 * ⚠ **A submitted review is immutable, and the screen says so rather than offering an Edit the
 * server would refuse.** A review is a record of what somebody found, not a request for permission;
 * the lead ACKNOWLEDGES it, which says it was read and nothing about whether they agreed. That is
 * why this is not on the workflow engine — there is nothing to approve.
 *
 * ⚠ **A review line snapshots the objective's progress when it is written.** The figure shown here
 * is what was true at the time, not what the objective says now, which is what makes an old review
 * still mean something.
 *
 * Round 2, lane F2 (plan § 6.6).
 */

import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  DateField,
  FieldRow,
  NumberField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { teamMeetingService } from '@/services/hr/team-meeting.service';
import { TEAM_REVIEW_STATUS_LABELS, type TeamReview } from '@/types/hr/team-meeting';
import { TeamReviewDetailDialog } from './TeamReviewDetailDialog';

const schema = z
  .object({
    periodStart: z.string().min(1, 'A start date is required'),
    periodEnd: z.string().min(1, 'An end date is required'),
    overallRating: z.string().optional().or(z.literal('')),
    summary: z.string().max(4000).optional().or(z.literal('')),
    recommendations: z.string().max(4000).optional().or(z.literal('')),
  })
  .refine((v) => v.periodEnd >= v.periodStart, {
    message: 'A review period cannot end before it starts',
    path: ['periodEnd'],
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  periodStart: '',
  periodEnd: new Date().toISOString().slice(0, 10),
  overallRating: '',
  summary: '',
  recommendations: '',
};

const toPayload = (v: FormValues) => ({
  periodStart: v.periodStart,
  periodEnd: v.periodEnd,
  overallRating: v.overallRating ? Number(v.overallRating) : null,
  summary: v.summary || null,
  recommendations: v.recommendations || null,
});

const STATUS_VARIANT: Record<string, 'default' | 'secondary' | 'outline'> = {
  Draft: 'outline',
  Submitted: 'secondary',
  Acknowledged: 'default',
};

export function TeamReviewsTab({ teamId }: { teamId: string }) {
  const queryClient = useQueryClient();
  const [openReview, setOpenReview] = useState<TeamReview | null>(null);

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['hr', 'teams', teamId, 'reviews'] });
    void queryClient.invalidateQueries({ queryKey: ['hr', 'teams', teamId, 'dashboard'] });
  };

  return (
    <div className="space-y-4">
      <ResourceCollectionTab<TeamReview, FormValues>
        parentId={teamId}
        title="reviews"
        singular="review"
        queryKey={['hr', 'teams', teamId, 'reviews']}
        invalidateKeys={[['hr', 'teams', teamId, 'dashboard']]}
        dialogHint="How the team did over a period. Open it after saving to rate each objective, then submit — a submitted review cannot be changed."
        dialogClassName="sm:max-w-[640px]"
        getId={(r) => r.id}
        list={() => teamMeetingService.getReviews(teamId)}
        create={(_id, values) => teamMeetingService.createReview(teamId, toPayload(values))}
        update={(_id, id, values) => teamMeetingService.updateReview(id, toPayload(values))}
        remove={(_id, id) => teamMeetingService.deleteReview(id)}
        loadForEdit={async (r) => {
          const full = await teamMeetingService.getReview(r.id);
          return {
            periodStart: full.periodStart?.slice(0, 10) ?? '',
            periodEnd: full.periodEnd?.slice(0, 10) ?? '',
            overallRating: full.overallRating != null ? String(full.overallRating) : '',
            summary: full.summary ?? '',
            recommendations: full.recommendations ?? '',
          };
        }}
        actions={[
          {
            label: 'Open…',
            run: async (r) => {
              setOpenReview(r);
            },
          },
          {
            label: 'Submit',
            visible: (r) => r.status === 'Draft',
            run: (r) => teamMeetingService.submitReview(r.id),
            confirm: {
              title: 'Submit this review?',
              description:
                'A submitted review becomes the record and cannot be edited afterwards. Rate the objectives first if you mean to.',
            },
          },
          {
            label: 'Acknowledge',
            visible: (r) => r.status === 'Submitted',
            run: (r) => teamMeetingService.acknowledgeReview(r.id),
            confirm: {
              title: 'Acknowledge this review?',
              description:
                'Acknowledging records that you have read it. It does not say you agree with it.',
            },
          },
        ]}
        columns={[
          {
            header: 'Period',
            cell: (r) => (
              <span className="font-medium">
                {r.periodStart?.slice(0, 10)} → {r.periodEnd?.slice(0, 10)}
              </span>
            ),
          },
          { header: 'Reviewed by', cell: (r) => r.reviewedByName || '—' },
          {
            header: 'Rating',
            cell: (r) =>
              r.overallRating != null ? (
                <span>{r.overallRating}/5</span>
              ) : (
                <span className="text-muted-foreground">—</span>
              ),
            className: 'text-right',
          },
          {
            header: 'Objectives rated',
            cell: (r) =>
              r.lineCount > 0 ? r.lineCount : <span className="text-muted-foreground">none</span>,
            className: 'text-right',
          },
          {
            header: 'Acknowledged',
            cell: (r) =>
              r.acknowledgedByName ? (
                <span className="text-xs">
                  {r.acknowledgedByName}
                  {r.acknowledgedOn ? ` · ${r.acknowledgedOn.slice(0, 10)}` : ''}
                </span>
              ) : (
                <span className="text-muted-foreground text-xs">—</span>
              ),
          },
          {
            header: 'Status',
            cell: (r) => (
              <Badge variant={STATUS_VARIANT[r.status] ?? 'outline'}>
                {TEAM_REVIEW_STATUS_LABELS[r.status]}
              </Badge>
            ),
          },
        ]}
        schema={schema}
        emptyForm={empty}
        toForm={() => empty}
        renderFields={(form) => (
          <>
            <FieldRow>
              <DateField form={form} name="periodStart" label="Period from" required />
              <DateField form={form} name="periodEnd" label="Period to" required />
            </FieldRow>
            <NumberField form={form} name="overallRating" label="Overall rating (1–5)" />
            <TextareaField
              form={form}
              name="summary"
              label="Summary"
              rows={4}
              placeholder="How the team did over the period."
            />
            <TextareaField
              form={form}
              name="recommendations"
              label="Recommendations"
              rows={3}
            />
            <p className="text-muted-foreground text-xs">
              A review needs a rating or a summary before it can be submitted, and cannot be changed
              afterwards.
            </p>
          </>
        )}
      />

      <TeamReviewDetailDialog
        reviewId={openReview?.id ?? null}
        teamId={teamId}
        onOpenChange={(open) => !open && setOpenReview(null)}
        onChanged={refresh}
      />
    </div>
  );
}
