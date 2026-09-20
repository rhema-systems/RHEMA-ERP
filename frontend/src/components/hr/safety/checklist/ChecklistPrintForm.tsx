'use client';

import { cn } from '@/lib/utils';
import type {
  SafetyInspection,
  SafetyInspectionItem,
  SheInspectionChecklist,
  SheInspectionChecklistSection,
} from '@/types/hr/safety-inspections';
import { bandLabel, fmtPct, standardChoices } from './checklist-scoring';

/**
 * The paper form, rendered from the template — blank as a builder preview, filled when an inspection
 * is passed. One component serves both so the preview is exactly what will print
 * (docs/HR/areas/she/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md §2.10). Black on white regardless of theme.
 */
interface Props {
  checklist: SheInspectionChecklist;
  inspection?: SafetyInspection | null;
  className?: string;
}

const box = (checked: boolean) => (checked ? '☑' : '☐');
const fmtDate = (v?: string | null) =>
  v ? new Date(v).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '';
const fmtDateTime = (v?: string | null) =>
  v ? `${fmtDate(v)} ${new Date(v).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}` : '';

const TABLE =
  'w-full border-collapse [&_td]:border [&_td]:border-black [&_td]:px-1.5 [&_td]:py-0.5 [&_td]:align-top [&_th]:border [&_th]:border-black [&_th]:px-1.5 [&_th]:py-0.5 [&_th]:text-left [&_th]:font-semibold';

function Heading({ children }: { children: React.ReactNode }) {
  return <div className="mt-3 border-b-2 border-black pb-0.5 text-[12px] font-bold uppercase tracking-wide">{children}</div>;
}

export function ChecklistPrintForm({ checklist, inspection, className }: Props) {
  const choices = standardChoices(checklist.allowPartialCompliance);
  const standardSections = checklist.sections.filter((s) => s.kind === 'Standard');
  const criticalSections = checklist.sections.filter((s) => s.kind === 'Critical');
  const scored = checklist.scoringMode !== 'None';

  const valueFor = (fieldId: string) => inspection?.fieldValues.find((v) => v.checklistFieldId === fieldId);
  const answerFor = (checklistItemId: string): SafetyInspectionItem | undefined =>
    inspection?.items.find((i) => i.checklistItemId === checklistItemId);
  const signatureFor = (signatoryId: string) => inspection?.signatures.find((s) => s.checklistSignatoryId === signatoryId);
  const ncItems = (inspection?.items ?? []).filter(
    (i) => i.checklistItemId && (i.status === 'NonCompliant' || i.status === 'PartiallyCompliant'),
  );

  const renderSection = (section: SheInspectionChecklistSection) => (
    <tbody key={section.id || section.title}>
      <tr>
        <td colSpan={2 + choices.length + 1} className="bg-neutral-100 font-semibold">
          {section.code ? `${section.code}. ` : ''}
          {section.title}
          {section.description ? <span className="font-normal text-neutral-600"> — {section.description}</span> : null}
        </td>
      </tr>
      {section.items.map((item) => {
        const answer = item.id ? answerFor(item.id) : undefined;
        return (
          <tr key={item.id || `${section.title}-${item.itemOrder}`}>
            <td className="w-8 text-center tabular-nums">{item.itemNumber || ''}</td>
            <td>
              {item.itemDescription}
              {item.regulatoryReference ? <span className="text-neutral-600"> ({item.regulatoryReference})</span> : null}
            </td>
            {choices.map((c) => (
              <td key={c.value} className="w-8 text-center text-[13px]">
                {box(answer?.status === c.value)}
              </td>
            ))}
            <td className="w-40">{answer?.deficiencyNoted ?? ''}</td>
          </tr>
        );
      })}
    </tbody>
  );

  return (
    <div className={cn('she-print-form bg-white p-6 text-[11px] leading-snug text-black', className)}>
      {/* ── Title block ── */}
      <div className="text-center">
        <div className="text-[14px] font-bold uppercase">{checklist.printTitle || checklist.name}</div>
        {checklist.printSubtitle ? <div className="text-[12px]">{checklist.printSubtitle}</div> : null}
        <div className="mt-0.5 text-[10px] text-neutral-600">
          {checklist.checklistNumber} v{checklist.version}
          {inspection ? ` · ${inspection.inspectionNumber}` : ''}
        </div>
      </div>

      {/* ── Document information ── */}
      <Heading>Document information</Heading>
      <table className={TABLE}>
        <thead>
          <tr>
            <th className="w-1/3">Item</th>
            <th>Details</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td>Inspection date</td>
            <td>{inspection ? fmtDate(inspection.inspectionDate) : ''}</td>
          </tr>
          <tr>
            <td>Inspector</td>
            <td>{inspection?.inspectorName ?? ''}</td>
          </tr>
          <tr>
            <td>Location</td>
            <td>
              {inspection?.locationName ?? ''}
              {inspection?.specificArea ? ` — ${inspection.specificArea}` : ''}
            </td>
          </tr>
          <tr>
            <td>Organization unit</td>
            <td>{inspection?.organizationUnitName ?? ''}</td>
          </tr>
          {checklist.fields.map((f) => {
            const v = valueFor(f.id);
            let display = v?.valueDisplay ?? v?.valueText ?? '';
            if (f.fieldType === 'YesNo' && display) display = display === 'true' ? 'Yes' : 'No';
            if (f.fieldType === 'Date' && display) display = fmtDate(display);
            return (
              <tr key={f.id}>
                <td>
                  {f.label}
                  {f.isRequired ? ' *' : ''}
                </td>
                <td>
                  {f.fieldType === 'Choice' && !inspection
                    ? f.choices.map((c) => `${box(false)} ${c}`).join('   ')
                    : f.fieldType === 'Choice' && inspection
                      ? f.choices.map((c) => `${box(display === c)} ${c}`).join('   ')
                      : display}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>

      {/* ── Scoring guide ── */}
      {scored && standardSections.length > 0 ? (
        <>
          <Heading>Scoring guide</Heading>
          <div className="flex flex-wrap gap-x-6">
            {choices.map((c) => (
              <span key={c.value}>
                <b>{c.short}</b> = {c.label}
              </span>
            ))}
          </div>
        </>
      ) : null}

      {/* ── Sections ── */}
      {standardSections.length > 0 ? (
        <>
          <Heading>Inspection checklist</Heading>
          <table className={TABLE}>
            <thead>
              <tr>
                <th className="w-8">No.</th>
                <th>Inspection item</th>
                {choices.map((c) => (
                  <th key={c.value} className="w-8 text-center">
                    {c.short}
                  </th>
                ))}
                <th className="w-40">Remarks</th>
              </tr>
            </thead>
            {standardSections.map(renderSection)}
          </table>
        </>
      ) : null}

      {/* ── Critical non-conformities ── */}
      {criticalSections.map((section) => (
        <div key={section.id || section.title}>
          <Heading>{section.title}</Heading>
          {checklist.criticalSectionNote ? <div className="mb-1 italic">{checklist.criticalSectionNote}</div> : null}
          <table className={TABLE}>
            <thead>
              <tr>
                <th>Item</th>
                <th className="w-10 text-center">Yes</th>
                <th className="w-10 text-center">No</th>
                <th className="w-40">Remarks</th>
              </tr>
            </thead>
            <tbody>
              {section.items.map((item) => {
                const answer = answerFor(item.id);
                return (
                  <tr key={item.id}>
                    <td>{item.itemDescription}</td>
                    <td className="text-center text-[13px]">{box(answer?.status === 'NonCompliant')}</td>
                    <td className="text-center text-[13px]">{box(answer?.status === 'Compliant')}</td>
                    <td>{answer?.deficiencyNoted ?? ''}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      ))}

      {/* ── Summary ── */}
      {checklist.scoringMode === 'CompliancePercentage' ? (
        <>
          <Heading>Inspection summary</Heading>
          <table className={cn(TABLE, 'w-auto min-w-[18rem]')}>
            <tbody>
              <tr>
                <td>Total applicable items</td>
                <td className="w-20 text-right tabular-nums">{inspection?.totalApplicableItems ?? ''}</td>
              </tr>
              <tr>
                <td>Total compliant</td>
                <td className="text-right tabular-nums">{inspection?.totalCompliantItems ?? ''}</td>
              </tr>
              <tr>
                <td>Total non-compliant</td>
                <td className="text-right tabular-nums">
                  {inspection?.totalNonCompliantItems == null
                    ? ''
                    : inspection.totalNonCompliantItems + (inspection.totalPartiallyCompliantItems ?? 0)}
                </td>
              </tr>
              {criticalSections.length > 0 ? (
                <tr>
                  <td>Critical non-conformities</td>
                  <td className="text-right tabular-nums">{inspection?.criticalNonConformityCount ?? ''}</td>
                </tr>
              ) : null}
              <tr>
                <td className="font-semibold">Compliance percentage</td>
                <td className="text-right font-semibold tabular-nums">
                  {inspection?.compliancePercentage == null ? '' : fmtPct(inspection.compliancePercentage)}
                </td>
              </tr>
            </tbody>
          </table>
        </>
      ) : null}

      {/* ── Outcome ── */}
      {scored && checklist.outcomes.length > 0 ? (
        <>
          <Heading>{checklist.scoringMode === 'QualitativeRating' ? 'Overall inspection rating' : 'Status'}</Heading>
          <div className="space-y-0.5">
            {checklist.outcomes.map((o) => (
              <div key={o.id}>
                <span className="text-[13px]">{box(inspection?.outcomeId === o.id)}</span> <b>{o.label}</b>
                {o.description && o.description !== o.label ? ` — ${o.description}` : ''}
                {o.reinspectionWithinDays ? ` (re-inspection within ${o.reinspectionWithinDays} days)` : ''}
              </div>
            ))}
          </div>
          {inspection?.outcomeOverrideReason ? (
            <div className="mt-1">
              <b>Recommended by score:</b> {inspection.recommendedOutcomeLabel} · <b>Reason for decision:</b>{' '}
              {inspection.outcomeOverrideReason}
            </div>
          ) : null}
          {checklist.scoringMode === 'CompliancePercentage' && checklist.outcomes.some((o) => o.minPercent != null) ? (
            <>
              <Heading>Recommended approval criteria</Heading>
              <table className={cn(TABLE, 'w-auto min-w-[24rem]')}>
                <thead>
                  <tr>
                    <th className="w-28">Compliance score</th>
                    <th>Decision</th>
                  </tr>
                </thead>
                <tbody>
                  {[...checklist.outcomes]
                    .filter((o) => o.minPercent != null)
                    .sort((a, b) => (b.minPercent ?? 0) - (a.minPercent ?? 0))
                    .map((o) => (
                      <tr key={o.id}>
                        <td className="tabular-nums">{bandLabel(o)}</td>
                        <td>{o.description || o.label}</td>
                      </tr>
                    ))}
                </tbody>
              </table>
            </>
          ) : null}
        </>
      ) : null}

      {/* ── Corrective actions ── */}
      <Heading>Corrective actions required</Heading>
      <table className={TABLE}>
        <thead>
          <tr>
            <th className="w-8">Item</th>
            <th>Non-conformance</th>
            <th>Corrective action</th>
            <th className="w-28">Responsible person</th>
            <th className="w-20">Due date</th>
            <th className="w-12 text-center">Closed</th>
          </tr>
        </thead>
        <tbody>
          {ncItems.length === 0
            ? [0, 1, 2].map((n) => (
                <tr key={n}>
                  <td>&nbsp;</td>
                  <td />
                  <td />
                  <td />
                  <td />
                  <td className="text-center text-[13px]">{box(false)}</td>
                </tr>
              ))
            : ncItems.map((i) => (
                <tr key={i.id}>
                  <td className="text-center tabular-nums">{i.itemNumber || ''}</td>
                  <td>
                    {i.itemDescription}
                    {i.deficiencyNoted ? <div className="text-neutral-700">{i.deficiencyNoted}</div> : null}
                  </td>
                  <td>{i.actionRequired ?? ''}</td>
                  <td>{i.responsiblePersonName ?? ''}</td>
                  <td>{fmtDate(i.targetDate)}</td>
                  <td className="text-center text-[13px]">{box(i.isResolved)}</td>
                </tr>
              ))}
        </tbody>
      </table>

      {/* ── Comments ── */}
      <Heading>Inspector&apos;s comments</Heading>
      <div className="min-h-[2.5rem] whitespace-pre-wrap border-b border-black">{inspection?.findingsAndObservations ?? ''}</div>
      {inspection?.recommendedActions ? (
        <div className="mt-1 whitespace-pre-wrap">
          <b>Recommended actions:</b> {inspection.recommendedActions}
        </div>
      ) : null}
      <Heading>Inspected party&apos;s comments</Heading>
      <div className="min-h-[2.5rem] whitespace-pre-wrap border-b border-black">{inspection?.subjectComments ?? ''}</div>

      {/* ── Signatures ── */}
      {checklist.signatories.length > 0 ? (
        <>
          <Heading>Signatures</Heading>
          <table className={TABLE}>
            <thead>
              <tr>
                <th className="w-40">Position</th>
                <th>Name</th>
                <th className="w-32">Signature</th>
                <th className="w-32">Date</th>
              </tr>
            </thead>
            <tbody>
              {checklist.signatories.map((s) => {
                const sig = signatureFor(s.id);
                return (
                  <tr key={s.id}>
                    <td>{s.roleLabel}</td>
                    <td>{sig?.signedName ?? ''}</td>
                    <td className="italic text-neutral-600">
                      {sig ? (sig.signedByEmployeeId ? 'Signed in system' : 'Recorded by inspector') : ''}
                    </td>
                    <td>{sig ? fmtDateTime(sig.signedAt) : ''}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </>
      ) : null}

      {/* ── Instructions ── */}
      {checklist.instructions ? (
        <div className="mt-3 whitespace-pre-wrap text-[10px] leading-snug">{checklist.instructions}</div>
      ) : null}
    </div>
  );
}
