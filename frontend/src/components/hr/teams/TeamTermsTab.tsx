'use client';

/**
 * A team's terms of reference — what it is chartered to do.
 *
 * ⚠ **Approved terms are immutable and the screen says so.** Editing in place would silently change
 * what the committee was chartered to do last year, and every decision minuted under the old terms
 * would start reading against the new ones. "New version" clones to a draft at version + 1;
 * approving it supersedes the old. The row menu offers only the actions the row's own status
 * allows, and the server refuses the rest anyway.
 *
 * ⚠ **Who may write is decided by the server against the team record**, not by this screen. HR, or
 * the team's lead or deputy. A refusal comes back 403 with a sentence worth showing.
 *
 * ⚠ **Approval goes through the workflow ENGINE, not this screen (lane F3).** "Approve" is offered
 * on every version awaiting one and the server decides: the approver must be the head of the unit
 * the committee serves, falling back to HR, and the engine must also have that step assigned to
 * them. Before F3 the only question asked was whether the caller could write to the team — which
 * meant the lead could approve their own charter.
 *
 * Round 2, lanes F1 and F3 (plan § 6.6).
 */

import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  DateField,
  FieldRow,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { WorkflowReasonDialog } from '@/components/workflow/WorkflowReasonDialog';
import { teamActivityService } from '@/services/hr/team-activity.service';
import {
  TEAM_TOR_STATUS_LABELS,
  type TeamTermsOfReference,
} from '@/types/hr/team-activity';

const schema = z
  .object({
    purpose: z.string().min(1, 'Say why the team exists').max(4000),
    scope: z.string().max(4000).optional().or(z.literal('')),
    authority: z.string().max(4000).optional().or(z.literal('')),
    membershipRules: z.string().max(4000).optional().or(z.literal('')),
    meetingCadence: z.string().max(500).optional().or(z.literal('')),
    reportingLine: z.string().max(500).optional().or(z.literal('')),
    deliverables: z.string().max(4000).optional().or(z.literal('')),
    effectiveFrom: z.string().min(1, 'A start date is required'),
    effectiveTo: z.string().optional().or(z.literal('')),
    notes: z.string().max(2000).optional().or(z.literal('')),
  })
  .refine((v) => !v.effectiveTo || v.effectiveTo >= v.effectiveFrom, {
    message: 'The terms cannot lapse before they take effect',
    path: ['effectiveTo'],
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  purpose: '',
  scope: '',
  authority: '',
  membershipRules: '',
  meetingCadence: '',
  reportingLine: '',
  deliverables: '',
  effectiveFrom: new Date().toISOString().slice(0, 10),
  effectiveTo: '',
  notes: '',
};

const toPayload = (v: FormValues) => ({
  purpose: v.purpose,
  scope: v.scope || null,
  authority: v.authority || null,
  membershipRules: v.membershipRules || null,
  meetingCadence: v.meetingCadence || null,
  reportingLine: v.reportingLine || null,
  deliverables: v.deliverables || null,
  effectiveFrom: v.effectiveFrom,
  effectiveTo: v.effectiveTo || null,
  notes: v.notes || null,
});

const STATUS_VARIANT: Record<string, 'default' | 'secondary' | 'outline'> = {
  Draft: 'outline',
  PendingApproval: 'secondary',
  Approved: 'default',
  Superseded: 'outline',
};

export function TeamTermsTab({ teamId }: { teamId: string }) {
  const queryClient = useQueryClient();
  // Which version a reason is being typed for, and which of the two doors it is going through.
  const [reasonFor, setReasonFor] = useState<{ id: string; mode: 'reject' | 'recall' } | null>(null);
  const [busy, setBusy] = useState(false);

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['hr', 'teams', teamId, 'terms'] });
    void queryClient.invalidateQueries({ queryKey: ['hr', 'teams', teamId, 'dashboard'] });
  };

  return (
    <>
    <ResourceCollectionTab<TeamTermsOfReference, FormValues>
      parentId={teamId}
      title="terms of reference"
      singular="version"
      queryKey={['hr', 'teams', teamId, 'terms']}
      dialogHint="What the team is chartered to do. Once approved this text becomes the record and cannot be edited — take a new version instead."
      dialogClassName="sm:max-w-[720px]"
      getId={(t) => t.id}
      list={() => teamActivityService.getTerms(teamId)}
      create={(_id, values) => teamActivityService.createTerms(teamId, toPayload(values))}
      update={(_id, id, values) => teamActivityService.updateTerms(id, toPayload(values))}
      remove={(_id, id) => teamActivityService.deleteTerms(id)}
      // The list is a summary; the full text arrives on the detail read.
      loadForEdit={async (t) => {
        const full = await teamActivityService.getTermsById(t.id);
        return {
          purpose: full.purpose,
          scope: full.scope ?? '',
          authority: full.authority ?? '',
          membershipRules: full.membershipRules ?? '',
          meetingCadence: full.meetingCadence ?? '',
          reportingLine: full.reportingLine ?? '',
          deliverables: full.deliverables ?? '',
          effectiveFrom: full.effectiveFrom?.slice(0, 10) ?? '',
          effectiveTo: full.effectiveTo?.slice(0, 10) ?? '',
          notes: full.notes ?? '',
        };
      }}
      actions={[
        {
          label: 'Submit for approval',
          visible: (t) => t.status === 'Draft',
          run: (t) => teamActivityService.submitTerms(t.id),
        },
        {
          // ⚠ Only from PendingApproval since F3. Approving straight from Draft would mean nobody
          // was ever asked — the engine never saw it, so there is no approval to record.
          label: 'Approve',
          visible: (t) => t.status === 'PendingApproval',
          run: (t) => teamActivityService.approveTerms(t.id),
          confirm: {
            title: 'Approve these terms of reference?',
            description:
              'Approved terms cannot be edited afterwards, and any version currently approved will be superseded. '
              + 'Only the head of the unit this committee serves, or HR, can sign this off.',
          },
        },
        {
          label: 'Send back…',
          visible: (t) => t.status === 'PendingApproval',
          run: async (t) => {
            setReasonFor({ id: t.id, mode: 'reject' });
          },
        },
        {
          label: 'Withdraw…',
          visible: (t) => t.status === 'PendingApproval',
          run: async (t) => {
            setReasonFor({ id: t.id, mode: 'recall' });
          },
        },
        {
          label: 'New version',
          visible: (t) => t.status === 'Approved',
          run: (t) => teamActivityService.newTermsVersion(t.id),
        },
      ]}
      columns={[
        { header: 'Version', cell: (t) => `v${t.version}` },
        {
          header: 'Status',
          cell: (t) => (
            <Badge variant={STATUS_VARIANT[t.status] ?? 'outline'}>
              {TEAM_TOR_STATUS_LABELS[t.status]}
            </Badge>
          ),
        },
        { header: 'In force from', cell: (t) => t.effectiveFrom?.slice(0, 10) ?? '—' },
        {
          header: 'Until',
          cell: (t) => {
            if (!t.effectiveTo) return <span className="text-muted-foreground">Open-ended</span>;
            const days = t.daysUntilExpiry;
            // ⚠ The countdown is the SERVER's, so this badge and the nightly reminder cannot
            // disagree about the same charter.
            const expiring = t.status === 'Approved' && days !== null && days !== undefined && days <= 30;
            return (
              <span className={expiring ? 'text-amber-600' : undefined}>
                {t.effectiveTo.slice(0, 10)}
                {expiring && (days! < 0 ? ' · lapsed' : ` · ${days} days left`)}
              </span>
            );
          },
        },
        { header: 'Approved by', cell: (t) => t.approvedByName || '—' },
        {
          header: 'Charter',
          cell: (t) =>
            t.hasDocument ? (
              <a
                className="text-primary text-xs underline"
                href={teamActivityService.termsDocumentUrl(t.id)}
              >
                {t.documentFileName ?? 'On file'}
              </a>
            ) : (
              <span className="text-muted-foreground text-xs">—</span>
            ),
        },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={() => empty}
      renderFields={(form) => (
        <>
          <TextareaField form={form} name="purpose" label="Purpose" rows={3} required />
          <TextareaField
            form={form}
            name="scope"
            label="Scope"
            rows={2}
            placeholder="What is in scope — and, as often matters more, what is not."
          />
          <TextareaField
            form={form}
            name="authority"
            label="Authority"
            rows={2}
            placeholder="What the team may decide on its own, and what it may only recommend."
          />
          <TextareaField
            form={form}
            name="membershipRules"
            label="Membership rules"
            rows={2}
            placeholder="How members are appointed, for how long, and what quorum means here."
          />
          <FieldRow>
            <TextField
              form={form}
              name="meetingCadence"
              label="Meeting cadence"
              placeholder="Monthly, first Tuesday"
            />
            <TextField form={form} name="reportingLine" label="Reports to" />
          </FieldRow>
          <TextareaField form={form} name="deliverables" label="Deliverables" rows={2} />
          <FieldRow>
            <DateField form={form} name="effectiveFrom" label="In force from" required />
            <DateField form={form} name="effectiveTo" label="Until" />
          </FieldRow>
          <TextareaField form={form} name="notes" label="Notes" rows={2} />
          <p className="text-muted-foreground text-xs">
            The signed charter is attached from the row&apos;s menu after saving — it goes through the
            document store, not this form.
          </p>
        </>
      )}
    />

    {/*
      ⚠ Two doors, one dialog, and they are NOT the same act. "Send back" is an approver refusing,
      and the reason is required because a refusal nobody explained leaves the committee with
      nothing to act on. "Withdraw" is the committee taking back its own submission before anyone
      ruled — nothing was refused, so no reason is stored.
    */}
    <WorkflowReasonDialog
      open={reasonFor !== null}
      onOpenChange={(open) => !open && setReasonFor(null)}
      title={reasonFor?.mode === 'reject' ? 'Send these terms back?' : 'Withdraw this submission?'}
      description={
        reasonFor?.mode === 'reject'
          ? 'The version returns to draft for the committee to rework. Say what needs to change.'
          : 'The version returns to draft and nobody is asked to approve it. Nothing is recorded against it.'
      }
      reasonLabel={reasonFor?.mode === 'reject' ? 'What needs to change' : 'Note (optional)'}
      reasonPlaceholder={
        reasonFor?.mode === 'reject'
          ? 'The authority section does not cover calling for records.'
          : 'Withdrawn to add the reporting line.'
      }
      confirmText={reasonFor?.mode === 'reject' ? 'Send back' : 'Withdraw'}
      requireReason={reasonFor?.mode === 'reject'}
      variant={reasonFor?.mode === 'reject' ? 'destructive' : 'default'}
      isLoading={busy}
      onConfirm={async (reason) => {
        if (!reasonFor) return;
        setBusy(true);
        try {
          if (reasonFor.mode === 'reject') await teamActivityService.rejectTerms(reasonFor.id, reason);
          else await teamActivityService.recallTerms(reasonFor.id, reason || undefined);
          setReasonFor(null);
          refresh();
        } finally {
          setBusy(false);
        }
      }}
    />
    </>
  );
}
