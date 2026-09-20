// Client-side mirror of the server's scoring rules
// (docs/HR/areas/she/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md §2.5–2.6), so the run screen can show the
// score as the inspector ticks. The server recomputes on Complete; this never writes anything.

import type {
  SafetyInspection,
  SafetyInspectionItem,
  SafetyInspectionScore,
  SheComplianceStatus,
  SheInspectionChecklist,
  SheInspectionChecklistOutcome,
} from '@/types/hr/safety-inspections';
import type { SheRiskLevel } from '@/types/hr/safety-hazards';

/** Synthetic sections the server builds for legacy section-less items carry the empty GUID. */
export const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

export interface LocalAnswer {
  status: SheComplianceStatus;
  deficiencyNoted: string;
  actionRequired: string;
  riskLevel: SheRiskLevel | '';
}

export const answerFromItem = (item: SafetyInspectionItem): LocalAnswer => ({
  status: item.status,
  deficiencyNoted: item.deficiencyNoted ?? '',
  actionRequired: item.actionRequired ?? '',
  riskLevel: item.riskLevel ?? '',
});

/** The band whose lower bound is the greatest one at or below the score — gapless by construction. */
export function bandFor(
  outcomes: SheInspectionChecklistOutcome[],
  pct: number | null | undefined,
): SheInspectionChecklistOutcome | null {
  if (pct == null) return null;
  const bands = outcomes
    .filter((o) => o.minPercent != null && o.minPercent <= pct)
    .sort((a, b) => (b.minPercent ?? 0) - (a.minPercent ?? 0));
  return bands[0] ?? null;
}

export function computeLocalScore(
  inspection: SafetyInspection,
  answers: Record<string, LocalAnswer>,
  fieldValues: Record<string, { text: string; ref: string }>,
): SafetyInspectionScore | null {
  const checklist = inspection.checklist;
  if (!checklist) return null;

  const items = inspection.items.filter((i) => i.checklistItemId);
  const statusOf = (i: SafetyInspectionItem) => answers[i.id]?.status ?? i.status;
  const standard = items.filter((i) => (i.sectionKind ?? 'Standard') === 'Standard');
  const critical = items.filter((i) => i.sectionKind === 'Critical');

  const compliant = standard.filter((i) => statusOf(i) === 'Compliant').length;
  const nonCompliant = standard.filter((i) => statusOf(i) === 'NonCompliant').length;
  const partial = standard.filter((i) => statusOf(i) === 'PartiallyCompliant').length;
  const na = standard.filter((i) => statusOf(i) === 'NotApplicable').length;
  const notAssessed = items.filter((i) => statusOf(i) === 'NotAssessed').length;
  const criticalHits = critical.filter((i) => statusOf(i) === 'NonCompliant').length;
  const applicable = compliant + nonCompliant + partial;
  const pct = applicable > 0 ? Math.round((compliant * 10000) / applicable) / 100 : null;
  const isDisqualified = checklist.scoringMode !== 'None' && criticalHits > 0;

  let recommended: SheInspectionChecklistOutcome | null = null;
  if (isDisqualified) recommended = checklist.outcomes.find((o) => o.isDisqualifying) ?? null;
  else if (checklist.scoringMode === 'CompliancePercentage') recommended = bandFor(checklist.outcomes, pct);

  const filled = new Set(
    Object.entries(fieldValues)
      .filter(([, v]) => v.text.trim() !== '' || v.ref !== '')
      .map(([k]) => k),
  );
  const missing = checklist.fields.filter((f) => f.isRequired && !filled.has(f.id)).map((f) => f.label);

  return {
    scoringMode: checklist.scoringMode,
    scoringModeName: checklist.scoringModeName,
    totalItems: standard.length,
    totalApplicableItems: applicable,
    totalCompliantItems: compliant,
    totalNonCompliantItems: nonCompliant,
    totalPartiallyCompliantItems: partial,
    totalNotApplicableItems: na,
    totalNotAssessedItems: notAssessed,
    criticalNonConformityCount: criticalHits,
    compliancePercentage: pct,
    isDisqualified,
    recommendedOutcomeId: recommended?.id ?? null,
    recommendedOutcomeLabel: recommended?.label ?? null,
    isReadyToComplete: items.length > 0 && notAssessed === 0 && missing.length === 0,
    missingRequiredFields: missing,
  };
}

/** The status choices a standard-section item offers on this template. */
export function standardChoices(allowPartial: boolean): { value: SheComplianceStatus; label: string; short: string }[] {
  const base: { value: SheComplianceStatus; label: string; short: string }[] = [
    { value: 'Compliant', label: 'Compliant', short: 'C' },
    { value: 'NonCompliant', label: 'Non-compliant', short: 'NC' },
  ];
  if (allowPartial) base.push({ value: 'PartiallyCompliant', label: 'Partially compliant', short: 'P' });
  base.push({ value: 'NotApplicable', label: 'Not applicable', short: 'NA' });
  return base;
}

/** Critical items answer Yes (the non-conformity is present → NonCompliant) or No (Compliant). */
export const CRITICAL_CHOICES: { value: SheComplianceStatus; label: string; short: string }[] = [
  { value: 'NonCompliant', label: 'Yes', short: 'Yes' },
  { value: 'Compliant', label: 'No', short: 'No' },
];

export const fmtPct = (v: number | null | undefined) => (v == null ? '—' : `${v.toFixed(v % 1 === 0 ? 0 : 2)}%`);

/** "95–100%", "0–69.99%", or "—" for a band-less outcome. */
export function bandLabel(o: SheInspectionChecklistOutcome): string {
  if (o.minPercent == null) return '—';
  const max = o.maxPercent == null ? '' : `–${o.maxPercent}`;
  return `${o.minPercent}${max}%`;
}
