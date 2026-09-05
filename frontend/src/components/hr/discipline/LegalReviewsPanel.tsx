'use client';

import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  DateField, FieldRow, NumberField, SelectField, SwitchField, TextareaField, TextField,
} from '@/components/hr/employee/tabs/fields';
import { disciplineLegalReviewService } from '@/services/hr/discipline.service';
import {
  LEGAL_RISK_OPTIONS,
  type DisciplineLegalReview,
  type DisciplineLegalRiskLevel,
} from '@/types/hr/discipline';

const toInput = (v?: string | null) => (v ? v.slice(0, 10) : '');
const orNull = (v?: string | null) => {
  const t = (v ?? '').trim();
  return t.length > 0 ? t : null;
};

/** Blank must stay blank: an untouched number input submits '' and Number('') is 0. */
const optionalMoney = z.preprocess(
  (v) => (v === '' || v === null || v === undefined ? null : Number(v)),
  z.number({ error: 'Enter an amount' }).min(0, 'Cannot be negative').nullable(),
);

const schema = z.object({
  referredToLegalDate: z.string().min(1, 'When was it referred?'),
  legalRiskLevel: z.string().min(1, 'Assess the risk'),
  requiresExternalCounsel: z.boolean(),
  externalCounselName: z.string().max(200).optional(),
  externalCounselFirm: z.string().max(700).optional(),
  externalCounselReviewDate: z.string().optional(),
  externalCounselOpinion: z.string().max(4000).optional(),
  legalAdvice: z.string().max(4000).optional(),
  legalReviewCompleteDate: z.string().optional(),
  legalCostsIncurred: optionalMoney,
  isConfidential: z.boolean(),
});

type Form = z.infer<typeof schema>;

const empty: Form = {
  referredToLegalDate: new Date().toISOString().slice(0, 10),
  legalRiskLevel: 'Medium',
  requiresExternalCounsel: false,
  externalCounselName: '',
  externalCounselFirm: '',
  externalCounselReviewDate: '',
  externalCounselOpinion: '',
  legalAdvice: '',
  legalReviewCompleteDate: '',
  legalCostsIncurred: null,
  isConfidential: true,
};

const RISK_TONE: Record<string, string> = {
  None: 'bg-slate-100 text-slate-600',
  Low: 'bg-emerald-100 text-emerald-800',
  Medium: 'bg-amber-100 text-amber-800',
  High: 'bg-orange-100 text-orange-800',
  Critical: 'bg-red-100 text-red-800',
};

/**
 * Referrals of this case to legal, and the advice that came back.
 *
 * This collection was **never rendered at all** — `legalReviews` was typed and carried on the case
 * detail, and no screen showed it. Seven of the eleven fields on the update DTO were top of the
 * closure ledger's "no form can set" table for the same reason.
 *
 * ⚠ **The list read here is the full record, not the case detail's copy.** The detail carries a
 * `...SummaryDto` with seven fields and no advice, counsel or costs; this panel calls
 * `cases/{id}/legal-reviews`, which returns the whole thing. Rendering the detail's copy would
 * produce a table of blanks.
 *
 * ⚠ **The referral and the advice are different moments.** `legalAdvice` and the counsel opinion
 * are not on the create DTO at all — you cannot record the answer while asking the question — so
 * the dialog hides them until the row exists.
 *
 * ⚠ **Confidential by default**, matching the DTO's own `= true`.
 */
export function LegalReviewsPanel({
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
  const queryKey = ['hr', 'discipline', caseId, 'legal-reviews'];

  // A plain number, not an object — and worth showing: it is the case's legal spend to date.
  const { data: totalCosts } = useQuery({
    queryKey: [...queryKey, 'total-costs'],
    queryFn: () => disciplineLegalReviewService.getTotalCostsForCase(caseId),
    enabled: !!caseId,
  });

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0">
        <CardTitle>Legal review</CardTitle>
        {typeof totalCosts === 'number' && totalCosts > 0 && (
          <span className="text-sm text-muted-foreground">
            Legal costs to date:{' '}
            <strong>{totalCosts.toLocaleString(undefined, { minimumFractionDigits: 2 })}</strong>
          </span>
        )}
      </CardHeader>
      <CardContent>
        <ResourceCollectionTab<DisciplineLegalReview, Form>
          parentId={caseId}
          title="legal referrals"
          singular="legal referral"
          queryKey={queryKey}
          invalidateKeys={[[...queryKey, 'total-costs']]}
          readOnly={!canWrite}
          dialogClassName="sm:max-w-[680px]"
          dialogHint="Refer this case to legal and record how risky it is."
          emptyDescription="Nothing on this case has been referred to legal."
          list={(id) => disciplineLegalReviewService.getForCase(id)}
          create={(id, v) =>
            disciplineLegalReviewService
              .refer(id, {
                referredToLegalDate: v.referredToLegalDate,
                legalRiskLevel: v.legalRiskLevel as DisciplineLegalRiskLevel,
                requiresExternalCounsel: v.requiresExternalCounsel,
                externalCounselName: v.requiresExternalCounsel ? orNull(v.externalCounselName) : null,
                externalCounselFirm: v.requiresExternalCounsel ? orNull(v.externalCounselFirm) : null,
                isConfidential: v.isConfidential,
              })
              .then((r) => { onChanged?.(); return r; })
          }
          update={(_id, reviewId, v) =>
            disciplineLegalReviewService
              .update(reviewId, {
                legalRiskLevel: v.legalRiskLevel as DisciplineLegalRiskLevel,
                legalReviewCompleteDate: orNull(v.legalReviewCompleteDate),
                legalAdvice: orNull(v.legalAdvice),
                requiresExternalCounsel: v.requiresExternalCounsel,
                externalCounselName: v.requiresExternalCounsel ? orNull(v.externalCounselName) : null,
                externalCounselFirm: v.requiresExternalCounsel ? orNull(v.externalCounselFirm) : null,
                externalCounselReviewDate: v.requiresExternalCounsel ? orNull(v.externalCounselReviewDate) : null,
                externalCounselOpinion: v.requiresExternalCounsel ? orNull(v.externalCounselOpinion) : null,
                legalCostsIncurred: v.legalCostsIncurred,
                isConfidential: v.isConfidential,
              })
              .then((r) => { onChanged?.(); return r; })
          }
          remove={
            canDelete
              ? (_id, reviewId) =>
                  disciplineLegalReviewService.remove(reviewId).then((r) => { onChanged?.(); return r; })
              : undefined
          }
          actions={[
            {
              label: 'Mark review complete',
              visible: (r) => !r.legalReviewCompleteDate,
              confirm: {
                title: 'Mark this legal review complete?',
                description: 'This records you and today’s date against it. Record the advice first if you have it.',
              },
              run: async (r) => {
                await disciplineLegalReviewService.complete(r.id);
                onChanged?.();
              },
            },
          ]}
          getId={(r) => r.id}
          columns={[
            { header: 'Referred', cell: (r) => toInput(r.referredToLegalDate) || '—' },
            { header: 'By', cell: (r) => r.referredByName ?? '—' },
            {
              header: 'Risk',
              cell: (r) => (
                <Badge className={RISK_TONE[r.legalRiskLevel] ?? 'bg-slate-100 text-slate-700'}>
                  {r.legalRiskLevelName ?? r.legalRiskLevel}
                </Badge>
              ),
            },
            {
              header: 'Counsel',
              cell: (r) =>
                r.requiresExternalCounsel
                  ? [r.externalCounselName, r.externalCounselFirm].filter(Boolean).join(' — ') || 'External, unnamed'
                  : 'In-house',
            },
            {
              header: 'Costs',
              cell: (r) =>
                r.legalCostsIncurred == null
                  ? '—'
                  : r.legalCostsIncurred.toLocaleString(undefined, { minimumFractionDigits: 2 }),
              className: 'text-right',
            },
            {
              header: 'Status',
              cell: (r) => (
                <div className="flex flex-wrap gap-1">
                  {r.legalReviewCompleteDate ? (
                    <Badge variant="outline">Complete</Badge>
                  ) : (
                    <Badge variant="secondary">Open</Badge>
                  )}
                  {r.isConfidential && <Badge variant="outline">Confidential</Badge>}
                </div>
              ),
            },
          ]}
          schema={schema}
          emptyForm={empty}
          toForm={(r) => ({
            referredToLegalDate: toInput(r.referredToLegalDate),
            legalRiskLevel: r.legalRiskLevel,
            requiresExternalCounsel: r.requiresExternalCounsel,
            externalCounselName: r.externalCounselName ?? '',
            externalCounselFirm: r.externalCounselFirm ?? '',
            externalCounselReviewDate: toInput(r.externalCounselReviewDate),
            externalCounselOpinion: r.externalCounselOpinion ?? '',
            legalAdvice: r.legalAdvice ?? '',
            legalReviewCompleteDate: toInput(r.legalReviewCompleteDate),
            legalCostsIncurred: r.legalCostsIncurred ?? null,
            isConfidential: r.isConfidential,
          })}
          renderFields={(form, editing) => {
            const external = !!form.watch('requiresExternalCounsel');
            return (
              <>
                <FieldRow>
                  {/* Create-only in effect: the update DTO cannot move the referral date. */}
                  {!editing ? (
                    <DateField form={form} name="referredToLegalDate" label="Referred on" required />
                  ) : (
                    <div />
                  )}
                  <SelectField
                    form={form}
                    name="legalRiskLevel"
                    label="Legal risk"
                    required
                    options={LEGAL_RISK_OPTIONS}
                  />
                </FieldRow>

                {/* Advice is what comes BACK — the create DTO has no field for it. */}
                {editing && (
                  <>
                    <TextareaField
                      form={form}
                      name="legalAdvice"
                      label="Legal advice received"
                      rows={4}
                      placeholder="What legal advised, and any conditions attached to it."
                    />
                    <FieldRow>
                      <DateField form={form} name="legalReviewCompleteDate" label="Review completed" />
                      <NumberField
                        form={form}
                        name="legalCostsIncurred"
                        label="Legal costs incurred"
                        step="0.01"
                        placeholder="Leave blank if none yet"
                      />
                    </FieldRow>
                  </>
                )}

                <SwitchField
                  form={form}
                  name="requiresExternalCounsel"
                  label="External counsel required"
                  description="Off means the in-house view is enough."
                />

                {external && (
                  <>
                    <FieldRow>
                      <TextField form={form} name="externalCounselName" label="Counsel" placeholder="Name" />
                      <TextField form={form} name="externalCounselFirm" label="Firm" />
                    </FieldRow>
                    {editing && (
                      <>
                        <DateField form={form} name="externalCounselReviewDate" label="Counsel reviewed on" />
                        <TextareaField
                          form={form}
                          name="externalCounselOpinion"
                          label="Counsel's opinion"
                          rows={4}
                        />
                      </>
                    )}
                  </>
                )}

                <SwitchField
                  form={form}
                  name="isConfidential"
                  label="Confidential"
                  description="On by default. Legal advice on a live case is privileged unless someone decides otherwise."
                />

                {!editing && (
                  <p className="text-xs text-muted-foreground">
                    You are recorded as the person referring this case. The advice, costs and
                    completion date are added once legal has responded.
                  </p>
                )}
              </>
            );
          }}
        />
      </CardContent>
    </Card>
  );
}
