'use client';

/**
 * A case's injury, its incapacity and compensation (round 5, lane K-II-b; PNDCL 187).
 *
 * ⚠ **The figure is indicative, and the screen says so every time it shows it.** Under the Act the
 * chief labour officer notifies the amount due (s.35), it is paid to the Court (s.11(3)), and nothing
 * may be set off against it (s.27) — so it is never money HR pays out, and never a settlement line.
 *
 * ⚠ **The assessment is the whole set.** Its injuries are replaced on every save; the percentage each
 * schedule row had is copied onto the injury, so an administrator editing the schedule later moves no
 * finding. Once the labour officer's amount is recorded, the assessment it answered is fixed.
 */

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, ClipboardCheck, Landmark, Link2, Loader2, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select, SelectContent, SelectGroup, SelectItem, SelectLabel, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { useToast } from '@/components/ui/use-toast';
import { medicalBoardService } from '@/services/hr/medical-board.service';
import { safetyIncidentService } from '@/services/hr/safety-incident.service';
import {
  INCAPACITY_KIND_LABEL,
  LOSS_OF_USE_LABEL,
  NOT_PAYABLE_LABEL,
  type AssessedInjury,
  type CompensationNotPayableReason,
  type IncapacityKind,
  type IncapacityScheduleItem,
  type LossOfUse,
  type MedicalBoardCase,
} from '@/types/hr/medical-board';

const day = (d?: string | null) => (d ? d.slice(0, 10) : '—');
const money = (amount?: number | null, currency?: string | null) =>
  amount == null ? '—' : `${currency ?? 'GHS'} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
const pct = (v?: number | null) => (v == null ? '—' : `${Number(v).toLocaleString(undefined, { maximumFractionDigits: 2 })}%`);

/** The kinds the dialog offers: the two permanent kinds are one choice — the server settles which (s.38). */
type KindChoice = 'None' | 'TemporaryTotal' | 'TemporaryPartial' | 'Permanent';
const KIND_CHOICES: { value: KindChoice; label: string }[] = [
  { value: 'None', label: 'No incapacity' },
  { value: 'TemporaryTotal', label: 'Temporary total — absence certified necessary (s.7)' },
  { value: 'TemporaryPartial', label: 'Temporary partial — after it, until final assessment (s.7)' },
  { value: 'Permanent', label: 'Permanent — partial or total follows from the percentage (ss.5, 6, 38)' },
];

const OWN = '__own';
const PAYABLE = '__payable';

interface DraftInjury {
  key: number;
  scheduleItemId: string; // OWN for the panel's own assessment
  description: string;
  percentage: string;
  lossOfUse: LossOfUse;
  nonDominantSide: boolean;
}

/** The screen's mirror of the server's rating — the server's figure is the one stored. */
function rate(d: DraftInjury, row?: IncapacityScheduleItem): number | null {
  if (d.scheduleItemId === OWN || row?.kind === 'Disfigurement') {
    const own = Number(d.percentage || (row ? row.percentage : NaN));
    return Number.isFinite(own) ? own : null;
  }
  if (!row) return null;
  return row.percentage * (d.lossOfUse === 'Partial' ? 0.5 : 1) * (d.nonDominantSide ? 0.9 : 1);
}

export function MedicalBoardCaseIncapacity({
  boardId,
  boardCase: c,
  editable,
  onChanged,
}: {
  boardId: string;
  boardCase: MedicalBoardCase;
  /** Board not stopped and case not withdrawn. */
  editable: boolean;
  onChanged: () => void | Promise<void>;
}) {
  const { toast } = useToast();
  const [dialog, setDialog] = useState<null | 'assess' | 'notice' | 'incident'>(null);
  const [busy, setBusy] = useState(false);

  // assessment draft
  const [kind, setKind] = useState<KindChoice>('Permanent');
  const [assessedBy, setAssessedBy] = useState('');
  const [assessedOn, setAssessedOn] = useState(new Date().toISOString().slice(0, 10));
  const [notes, setNotes] = useState('');
  const [notPayable, setNotPayable] = useState<string>(PAYABLE);
  const [injuries, setInjuries] = useState<DraftInjury[]>([]);
  const [nextKey, setNextKey] = useState(1);

  // notice draft
  const [notified, setNotified] = useState('');
  const [notifiedOn, setNotifiedOn] = useState('');
  const [dueOn, setDueOn] = useState('');
  const [agreed, setAgreed] = useState('');
  const [agreedOn, setAgreedOn] = useState('');

  const injuryCase = c.purpose === 'InjuryOnDuty';
  const assessed = !!c.incapacityKind;
  const temporary = c.incapacityKind === 'TemporaryTotal' || c.incapacityKind === 'TemporaryPartial';
  const permanent = c.incapacityKind === 'PermanentPartial' || c.incapacityKind === 'PermanentTotal';
  const canAssess = editable && !c.assessmentFixed;
  const canNotice = editable && assessed && c.incapacityKind !== 'None' && !c.compensationNotPayableReason;

  const { data: schedule = [], isLoading: scheduleLoading } = useQuery({
    queryKey: ['hr', 'medical-boards', 'incapacity-schedule'],
    queryFn: () => medicalBoardService.getSchedule(false),
    enabled: dialog === 'assess',
  });
  const rows = useMemo(() => new Map(schedule.map((r) => [r.id, r])), [schedule]);

  const { data: incidents = [], isLoading: incidentsLoading, isError: incidentsUnreadable } = useQuery({
    queryKey: ['safety', 'incidents', 'involving', c.employeeId],
    queryFn: () => safetyIncidentService.getByInvolvedEmployee(c.employeeId),
    enabled: dialog === 'incident',
    retry: false,
  });

  const run = async (what: string, fn: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await fn();
      await onChanged();
      toast({ title: what });
      setDialog(null);
    } catch (e: any) {
      toast({ title: 'Error', description: e?.message || 'Action failed.', variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  const openAssess = () => {
    setKind(!c.incapacityKind ? 'Permanent'
      : c.incapacityKind === 'PermanentPartial' || c.incapacityKind === 'PermanentTotal' ? 'Permanent'
        : (c.incapacityKind as KindChoice));
    setAssessedBy(c.incapacityAssessedBy ?? '');
    setAssessedOn(c.incapacityAssessedOn?.slice(0, 10) ?? new Date().toISOString().slice(0, 10));
    setNotes(c.incapacityNotes ?? '');
    setNotPayable(c.compensationNotPayableReason ?? PAYABLE);
    let k = 1;
    setInjuries(c.injuries.map((i) => ({
      key: k++,
      scheduleItemId: i.scheduleItemId ?? OWN,
      description: i.scheduleItemId ? '' : i.description,
      percentage: i.scheduleItemId && i.scheduleKind !== 'Disfigurement' ? '' : String(i.basePercentage),
      lossOfUse: i.lossOfUse,
      nonDominantSide: i.nonDominantSide,
    })));
    setNextKey(k);
    setDialog('assess');
  };

  const openNotice = () => {
    setNotified(c.notifiedCompensation != null ? String(c.notifiedCompensation) : '');
    setNotifiedOn(c.compensationNotifiedOn?.slice(0, 10) ?? '');
    setDueOn(c.compensationDueOn?.slice(0, 10) ?? '');
    setAgreed(c.agreedCompensation != null ? String(c.agreedCompensation) : '');
    setAgreedOn(c.compensationAgreedOn?.slice(0, 10) ?? '');
    setDialog('notice');
  };

  const addInjury = () => {
    setInjuries((list) => [...list, {
      key: nextKey, scheduleItemId: '', description: '', percentage: '', lossOfUse: 'Total', nonDominantSide: false,
    }]);
    setNextKey((n) => n + 1);
  };
  const patchInjury = (key: number, patch: Partial<DraftInjury>) =>
    setInjuries((list) => list.map((d) => (d.key === key ? { ...d, ...patch } : d)));

  const draftTotal = Math.min(100, injuries.reduce((sum, d) => sum + (rate(d, rows.get(d.scheduleItemId)) ?? 0), 0));

  const saveAssessment = () =>
    run('Assessment recorded', () => {
      const payload: AssessedInjury[] = kind === 'None' ? [] : injuries.map((d) => {
        const row = rows.get(d.scheduleItemId);
        if (d.scheduleItemId === OWN)
          return { description: d.description.trim(), percentage: Number(d.percentage), nonDominantSide: false };
        if (row?.kind === 'Disfigurement')
          return { scheduleItemId: d.scheduleItemId, percentage: d.percentage ? Number(d.percentage) : null, nonDominantSide: false };
        return { scheduleItemId: d.scheduleItemId, lossOfUse: d.lossOfUse, nonDominantSide: d.nonDominantSide };
      });
      return medicalBoardService.assessIncapacity(boardId, c.id, {
        kind: (kind === 'Permanent' ? 'PermanentPartial' : kind) as IncapacityKind,
        injuries: payload,
        assessedOn: assessedOn || null,
        assessedBy: assessedBy.trim(),
        notes: notes.trim() || null,
        notPayableReason: notPayable === PAYABLE ? null : (notPayable as CompensationNotPayableReason),
      });
    });

  const saveNotice = () =>
    run('Recorded', () =>
      medicalBoardService.recordCompensation(boardId, c.id, {
        notifiedCompensation: notified ? Number(notified) : null,
        notifiedOn: notified ? notifiedOn || null : null,
        dueOn: notified ? dueOn || null : null,
        agreedCompensation: agreed ? Number(agreed) : null,
        agreedOn: agreed ? agreedOn || null : null,
      }),
    );

  const byKind = (k: IncapacityScheduleItem['kind']) => schedule.filter((r) => r.kind === k);

  return (
    <div className="mt-3 space-y-3 border-t pt-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="text-sm font-medium">Incapacity and compensation</div>
        <div className="flex flex-wrap gap-2">
          {injuryCase && editable && (
            <Button size="sm" variant="outline" onClick={() => setDialog('incident')}>
              <Link2 className="mr-2 h-4 w-4" /> {c.safetyIncidentId ? 'Change the incident' : 'Link the safety incident'}
            </Button>
          )}
          {canAssess && (
            <Button size="sm" variant="outline" onClick={openAssess}>
              <ClipboardCheck className="mr-2 h-4 w-4" /> {assessed ? 'Revise the assessment' : 'Record the assessment'}
            </Button>
          )}
          {canNotice && (
            <Button size="sm" variant="outline" onClick={openNotice}>
              <Landmark className="mr-2 h-4 w-4" /> The labour officer&apos;s notice
            </Button>
          )}
        </div>
      </div>

      {injuryCase && (
        <p className="text-sm">
          {c.safetyIncidentId ? (
            <>
              Safety incident{' '}
              <Link href={`/hr/safety/incidents/${c.safetyIncidentId}`} className="font-medium underline underline-offset-2">
                {c.safetyIncidentNumber ?? 'linked'}
              </Link>
              {c.safetyIncidentDate ? ` of ${day(c.safetyIncidentDate)}` : ''}
              {c.claimNoticeDueBy && (
                <span className="text-muted-foreground">
                  {' '}— notice of the accident and the claim are due within six months, by {day(c.claimNoticeDueBy)} (s.12)
                </span>
              )}
            </>
          ) : (
            <span className="text-muted-foreground">No safety incident is linked to this injury case.</span>
          )}
        </p>
      )}

      {!c.incapacityKind ? (
        <p className="text-sm text-muted-foreground">Not assessed.</p>
      ) : (
        <div className="space-y-2 text-sm">
          <div className="flex flex-wrap gap-x-6 gap-y-1">
            <span className="font-medium">{INCAPACITY_KIND_LABEL[c.incapacityKind]}</span>
            {permanent && <span>{pct(c.incapacityPercentage)}</span>}
            <span className="text-muted-foreground">
              assessed {day(c.incapacityAssessedOn)} by {c.incapacityAssessedBy}
            </span>
            {c.compensationNotPayableReason && (
              <span className="text-amber-700 dark:text-amber-300">
                Not payable — {NOT_PAYABLE_LABEL[c.compensationNotPayableReason]}
              </span>
            )}
          </div>

          {c.injuries.length > 0 && (
            <ul className="space-y-0.5">
              {c.injuries.map((i) => (
                <li key={i.id} className="flex flex-wrap justify-between gap-2">
                  <span>
                    {i.description}
                    <span className="ml-2 text-xs text-muted-foreground">
                      {i.scheduleItemId ? `${pct(i.basePercentage)} on the schedule` : "the panel's own assessment (s.6(1)(b))"}
                      {i.lossOfUse === 'Partial' ? ' · partial loss of use (50 %)' : ''}
                      {i.nonDominantSide ? ' · the side not favoured (90 %)' : ''}
                    </span>
                  </span>
                  <span className="font-medium">{pct(i.percentage)}</span>
                </li>
              ))}
            </ul>
          )}

          {temporary && c.temporaryIncapacityMaxMonths && (
            <p className="text-muted-foreground">
              Paid periodically through payroll for at most {c.temporaryIncapacityMaxMonths} months (s.7)
              {c.temporaryPaymentsEndBy ? ` — until ${day(c.temporaryPaymentsEndBy)} from the incident` : ''}.
            </p>
          )}

          {c.incapacityNotes && <p className="whitespace-pre-wrap text-muted-foreground">{c.incapacityNotes}</p>}

          {(c.indicativeCompensation != null || c.indicativeCompensationBasis) && (
            <div className="rounded-md border border-amber-300/60 bg-amber-50 p-3 dark:border-amber-900/60 dark:bg-amber-950/40">
              <div className="flex flex-wrap items-baseline gap-2">
                <span className="text-xs uppercase tracking-wide text-muted-foreground">Indicative figure</span>
                <span className="font-medium">{money(c.indicativeCompensation, c.compensationCurrency)}</span>
              </div>
              <p className="mt-1 text-xs">{c.indicativeCompensationBasis}</p>
            </div>
          )}

          {(c.notifiedCompensation != null || c.agreedCompensation != null) && (
            <div className="grid gap-x-6 gap-y-1 sm:grid-cols-2">
              {c.notifiedCompensation != null && (
                <span>
                  Notified by the labour officer: <strong>{money(c.notifiedCompensation, c.compensationCurrency)}</strong>
                  {' '}on {day(c.compensationNotifiedOn)} — due by {day(c.compensationDueOn)} (s.35)
                </span>
              )}
              {c.agreedCompensation != null && (
                <span>
                  Agreed in writing: <strong>{money(c.agreedCompensation, c.compensationCurrency)}</strong> on {day(c.compensationAgreedOn)} (s.15)
                </span>
              )}
            </div>
          )}

          {c.assessmentFixed && (
            <p className="text-xs text-muted-foreground">
              The labour officer&apos;s amount is recorded, so this assessment is fixed.
            </p>
          )}
        </div>
      )}

      {/* ── The assessment ──────────────────────────────────────────── */}
      <Dialog open={dialog === 'assess'} onOpenChange={(o) => setDialog(o ? 'assess' : null)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[680px]">
          <DialogHeader>
            <DialogTitle>The incapacity assessment — {c.employeeName}</DialogTitle>
            <DialogDescription>
              The whole assessment: saving replaces the last. The compensation rests on the attending
              medical officer&apos;s assessment (s.2(3)).
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-1.5 sm:col-span-2">
                <Label htmlFor="inc-kind">Incapacity found</Label>
                <Select value={kind} onValueChange={(v) => setKind(v as KindChoice)}>
                  <SelectTrigger id="inc-kind"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {KIND_CHOICES.map((k) => <SelectItem key={k.value} value={k.value}>{k.label}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="inc-by">Assessed by <span className="text-red-500">*</span></Label>
                <Input id="inc-by" value={assessedBy} onChange={(e) => setAssessedBy(e.target.value)}
                  placeholder="The attending medical officer" />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="inc-on">Assessed on</Label>
                <Input id="inc-on" type="date" value={assessedOn} onChange={(e) => setAssessedOn(e.target.value)} />
              </div>
              <div className="space-y-1.5 sm:col-span-2">
                <Label htmlFor="inc-payable">Compensation</Label>
                <Select value={notPayable} onValueChange={setNotPayable}>
                  <SelectTrigger id="inc-payable"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value={PAYABLE}>Payable</SelectItem>
                    {(Object.keys(NOT_PAYABLE_LABEL) as CompensationNotPayableReason[]).map((r) => (
                      <SelectItem key={r} value={r}>Not payable — {NOT_PAYABLE_LABEL[r]}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            {kind !== 'None' && (
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <Label>Injuries{kind === 'Permanent' && <span className="text-red-500"> *</span>}</Label>
                  <Button size="sm" variant="outline" onClick={addInjury}>
                    <Plus className="mr-2 h-4 w-4" /> Add an injury
                  </Button>
                </div>
                {scheduleLoading && (
                  <p className="flex items-center gap-2 text-sm text-muted-foreground">
                    <Loader2 className="h-4 w-4 animate-spin" /> Reading the schedule…
                  </p>
                )}
                {!scheduleLoading && schedule.length === 0 && (
                  <p className="text-sm text-amber-700 dark:text-amber-300">
                    The compensation schedule is empty — an administrator loads the Act&apos;s rows on{' '}
                    <Link href="/hr/medical/boards/incapacity-schedule" className="underline underline-offset-2">the schedule page</Link>.
                    The panel&apos;s own assessment can still be recorded.
                  </p>
                )}

                {injuries.map((d) => {
                  const row = rows.get(d.scheduleItemId);
                  const own = d.scheduleItemId === OWN;
                  const disfigurement = row?.kind === 'Disfigurement';
                  const rated = rate(d, row);
                  return (
                    <div key={d.key} className="space-y-2 rounded-md border p-3">
                      <div className="flex items-start gap-2">
                        <Select value={d.scheduleItemId} onValueChange={(v) => patchInjury(d.key, { scheduleItemId: v, percentage: '', lossOfUse: 'Total', nonDominantSide: false })}>
                          <SelectTrigger className="flex-1"><SelectValue placeholder="Choose the injury" /></SelectTrigger>
                          <SelectContent className="max-h-80">
                            <SelectItem value={OWN}>Not on the schedule — the panel&apos;s own assessment (s.6(1)(b))</SelectItem>
                            {(['Incapacity', 'Disfigurement'] as const).map((k) => byKind(k).length > 0 && (
                              <SelectGroup key={k}>
                                <SelectLabel>{k === 'Incapacity' ? 'Third Schedule — incapacity' : 'First Schedule — disfigurement (up to)'}</SelectLabel>
                                {byKind(k).map((r) => (
                                  <SelectItem key={r.id} value={r.id}>{r.injury} — {pct(r.percentage)}</SelectItem>
                                ))}
                              </SelectGroup>
                            ))}
                          </SelectContent>
                        </Select>
                        <Button size="icon" variant="ghost" onClick={() => setInjuries((l) => l.filter((x) => x.key !== d.key))}>
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>

                      {own && (
                        <div className="grid gap-2 sm:grid-cols-[1fr_8rem]">
                          <Input placeholder="The injury, as the panel assessed it" value={d.description}
                            onChange={(e) => patchInjury(d.key, { description: e.target.value })} />
                          <Input type="number" min={0} max={100} step="0.01" placeholder="%" value={d.percentage}
                            onChange={(e) => patchInjury(d.key, { percentage: e.target.value })} />
                        </div>
                      )}

                      {disfigurement && row && (
                        <div className="flex items-center gap-2 text-sm">
                          <Input className="w-28" type="number" min={0} max={row.percentage} step="0.01"
                            placeholder={String(row.percentage)} value={d.percentage}
                            onChange={(e) => patchInjury(d.key, { percentage: e.target.value })} />
                          <span className="text-muted-foreground">% — up to {pct(row.percentage)}, as the practitioner determines (s.8)</span>
                        </div>
                      )}

                      {row && !disfigurement && (
                        <div className="flex flex-wrap items-center gap-4 text-sm">
                          <Select value={d.lossOfUse} onValueChange={(v) => patchInjury(d.key, { lossOfUse: v as LossOfUse })}>
                            <SelectTrigger className="w-64"><SelectValue /></SelectTrigger>
                            <SelectContent>
                              {(Object.keys(LOSS_OF_USE_LABEL) as LossOfUse[]).map((l) => (
                                <SelectItem key={l} value={l}>{LOSS_OF_USE_LABEL[l]}</SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                          {row.appliesToArmOrHand && (
                            <label className="flex items-center gap-2">
                              <input type="checkbox" checked={d.nonDominantSide}
                                onChange={(e) => patchInjury(d.key, { nonDominantSide: e.target.checked })} />
                              The arm or hand the employee does not favour (90 %)
                            </label>
                          )}
                        </div>
                      )}

                      <div className="text-right text-xs text-muted-foreground">
                        {rated == null ? '—' : `adds ${pct(Math.round(rated * 100) / 100)}`}
                      </div>
                    </div>
                  );
                })}

                {kind === 'Permanent' && injuries.length > 0 && (
                  <p className="text-sm">
                    Together: <strong>{pct(Math.round(draftTotal * 100) / 100)}</strong>
                    {draftTotal >= 100 ? ' — permanent total (s.38)' : ' — permanent partial'}.
                    <span className="text-muted-foreground"> Several injuries are added up, never above 100 % (s.6(2)).</span>
                  </p>
                )}
                {(kind === 'TemporaryTotal' || kind === 'TemporaryPartial') && (
                  <p className="text-xs text-muted-foreground">
                    Temporary incapacity is paid periodically through payroll (s.7); injuries recorded here
                    describe it but work out no lump sum.
                  </p>
                )}
              </div>
            )}

            <div className="space-y-1.5">
              <Label htmlFor="inc-notes">Notes</Label>
              <Textarea id="inc-notes" rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
            </div>

            <p className="flex gap-2 rounded-md border border-amber-300/60 bg-amber-50 p-2 text-xs dark:border-amber-900/60 dark:bg-amber-950/40">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
              The figure worked out from this is indicative: the labour officer notifies the amount due
              (s.35), it is paid to the Court (s.11(3)), and nothing may be set off against it (s.27).
            </p>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button disabled={busy || !assessedBy.trim() || (kind === 'Permanent' && injuries.length === 0)} onClick={saveAssessment}>
              Save the assessment
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── The labour officer's notice, and any agreement ─────────────── */}
      <Dialog open={dialog === 'notice'} onOpenChange={(o) => setDialog(o ? 'notice' : null)}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>The labour officer&apos;s notice</DialogTitle>
            <DialogDescription>
              The chief labour officer notifies the amount due; it is payable within three months (s.35).
              ⚠ Once recorded, the assessment it answers is fixed.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="space-y-1.5">
              <Label htmlFor="n-amount">Amount notified</Label>
              <Input id="n-amount" type="number" min={0} step="0.01" value={notified} onChange={(e) => setNotified(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="n-on">Notified on</Label>
              <Input id="n-on" type="date" value={notifiedOn} onChange={(e) => setNotifiedOn(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="n-due">Due by</Label>
              <Input id="n-due" type="date" value={dueOn} onChange={(e) => setDueOn(e.target.value)} />
              <p className="text-xs text-muted-foreground">Three months on, if left empty.</p>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="a-amount">Agreed in writing</Label>
              <Input id="a-amount" type="number" min={0} step="0.01" value={agreed} onChange={(e) => setAgreed(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="a-on">Agreed on</Label>
              <Input id="a-on" type="date" value={agreedOn} onChange={(e) => setAgreedOn(e.target.value)} />
            </div>
          </div>
          <p className="text-xs text-muted-foreground">
            An agreement is never below the Act&apos;s amount (s.15) — the notified amount, or else the indicative one.
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button disabled={busy || (!!notified && !notifiedOn) || (!!agreed && !agreedOn)} onClick={saveNotice}>Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── The safety incident ─────────────────────────────────────── */}
      <Dialog open={dialog === 'incident'} onOpenChange={(o) => setDialog(o ? 'incident' : null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Which incident was the injury reported in?</DialogTitle>
            <DialogDescription>
              Incidents in Safety that name {c.employeeName} among the people involved — the server refuses
              any other.
            </DialogDescription>
          </DialogHeader>
          <div className="max-h-80 space-y-2 overflow-y-auto">
            {incidentsLoading && (
              <p className="flex items-center gap-2 py-4 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" /> Loading…
              </p>
            )}
            {incidentsUnreadable && (
              <p className="py-4 text-sm text-muted-foreground">
                The incidents could not be read from here — you may not have access to Safety records.
              </p>
            )}
            {!incidentsLoading && !incidentsUnreadable && incidents.length === 0 && (
              <p className="py-4 text-sm text-muted-foreground">No incident in Safety names this employee.</p>
            )}
            {incidents.map((i) => (
              <button
                key={i.id}
                type="button"
                disabled={busy || i.id === c.safetyIncidentId}
                onClick={() => run('Incident linked', () => medicalBoardService.linkSafetyIncident(boardId, c.id, i.id))}
                className="flex w-full items-start justify-between gap-3 rounded-md border p-3 text-left text-sm hover:bg-muted disabled:cursor-not-allowed disabled:opacity-60"
              >
                <span>
                  <span className="font-medium">{i.incidentNumber}</span>
                  <span className="mt-0.5 block text-xs text-muted-foreground">
                    {day(i.incidentDate)} · {i.categoryName} · {i.severityName}
                    {i.locationName ? ` · ${i.locationName}` : ''}
                  </span>
                </span>
                {i.id === c.safetyIncidentId && <span className="text-xs">linked</span>}
              </button>
            ))}
          </div>
          <DialogFooter>
            {c.safetyIncidentId && (
              <Button variant="ghost" disabled={busy}
                onClick={() => run('Incident unlinked', () => medicalBoardService.linkSafetyIncident(boardId, c.id, null))}>
                Unlink
              </Button>
            )}
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
