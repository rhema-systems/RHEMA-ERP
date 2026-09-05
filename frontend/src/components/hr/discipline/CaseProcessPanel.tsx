'use client';

import { useEffect, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { CheckCircle2, CalendarPlus, Pencil, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { useToast } from '@/hooks/use-toast';
import { disciplineProcessService } from '@/services/hr/discipline.service';
import type {
  DisciplinaryRepresentativeType,
  DisciplinaryCase,
} from '@/types/hr/discipline';

/**
 * The investigation and the hearing, made recordable.
 *
 * Both were read-only on the case screen — and unlike the sanctions beside them, for no reason
 * beyond nobody having built the editors. Neither imposes a penalty, so FR-HR-080's issuing
 * authority does not govern either. `probe-authority-gate.mjs` establishes that the sanctions'
 * block does still stand, and why.
 *
 * ⚠ **Recording findings is not completing the investigation.** The update writes the findings and
 * leaves the case where it is; `complete` is a separate call and is what advances the case. The two
 * are offered as separate actions here for exactly that reason.
 *
 * ⚠ **`complete` takes a bare JSON string**, not an object — the action is
 * `[FromBody] string findings`, and `{ findings }` 400s naming no field. The client sends the
 * string; this panel never builds that body itself.
 */

const REPRESENTATIVE_OPTIONS: { value: DisciplinaryRepresentativeType; label: string }[] = [
  { value: 'LegalCounsel', label: 'Legal counsel' },
  { value: 'FamilyMember', label: 'Family member' },
  { value: 'WorkplaceColleague', label: 'A colleague' },
  { value: 'UnionRepresentative', label: 'Union representative' },
  { value: 'Other', label: 'Other' },
];

const toLocalInput = (v?: string | null) => {
  if (!v) return '';
  const d = new Date(v);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};
const toDateInput = (v?: string | null) => (v ? toLocalInput(v).slice(0, 10) : '');

interface Props {
  caseId: string;
  detail: DisciplinaryCase;
  canWrite: boolean;
  onChanged: () => void;
}

export function CaseProcessPanel({ caseId, detail, canWrite, onChanged }: Props) {
  const { toast } = useToast();
  const investigation = detail.investigation;
  const hearing = detail.hearing;

  const [dialog, setDialog] = useState<null | 'investigation' | 'complete' | 'hearing' | 'outcome'>(null);

  const [inv, setInv] = useState({
    investigatorId: '', investigationStartDate: '', investigationEndDate: '',
    investigationFindings: '', evidenceCollected: '',
  });
  const [findings, setFindings] = useState('');
  const [hear, setHear] = useState({ hearingDate: '', hearingVenue: '', hearingOfficerId: '' });
  const [outcome, setOutcome] = useState({
    employeeAttendedHearing: true,
    employeeStatement: '',
    employeeHadRepresentation: false,
    representativeType: '' as '' | DisciplinaryRepresentativeType,
    representativeEmployeeId: '',
    representativeName: '',
    representativePosition: '',
    representativeContactInfo: '',
    hearingNotes: '',
  });

  useEffect(() => {
    if (dialog !== 'investigation') return;
    setInv({
      investigatorId: investigation?.investigatorId ?? '',
      investigationStartDate: toDateInput(investigation?.investigationStartDate),
      investigationEndDate: toDateInput(investigation?.investigationEndDate),
      investigationFindings: investigation?.investigationFindings ?? '',
      evidenceCollected: investigation?.evidenceCollected ?? '',
    });
  }, [dialog, investigation]);

  useEffect(() => {
    if (dialog !== 'complete') return;
    setFindings(investigation?.investigationFindings ?? '');
  }, [dialog, investigation]);

  useEffect(() => {
    if (dialog !== 'hearing') return;
    setHear({
      hearingDate: toLocalInput(hearing?.hearingDate),
      hearingVenue: hearing?.hearingVenue ?? '',
      hearingOfficerId: hearing?.hearingOfficerId ?? '',
    });
  }, [dialog, hearing]);

  useEffect(() => {
    if (dialog !== 'outcome') return;
    setOutcome({
      employeeAttendedHearing: hearing?.employeeAttendedHearing ?? true,
      employeeStatement: hearing?.employeeStatement ?? '',
      employeeHadRepresentation: hearing?.employeeHadRepresentation ?? false,
      representativeType: (hearing?.representativeType ?? '') as '' | DisciplinaryRepresentativeType,
      representativeEmployeeId: hearing?.representativeEmployeeId ?? '',
      representativeName: hearing?.representativeName ?? '',
      representativePosition: hearing?.representativePosition ?? '',
      representativeContactInfo: hearing?.representativeContactInfo ?? '',
      hearingNotes: hearing?.hearingNotes ?? '',
    });
  }, [dialog, hearing]);

  const orNull = (v: string) => (v.trim() === '' ? null : v);

  const run = useMutation<unknown, Error>({
    mutationFn: () => {
      switch (dialog) {
        case 'investigation':
          return investigation
            ? disciplineProcessService.updateInvestigation(caseId, {
                investigatorId: orNull(inv.investigatorId),
                investigationStartDate: orNull(inv.investigationStartDate),
                investigationEndDate: orNull(inv.investigationEndDate),
                investigationFindings: orNull(inv.investigationFindings),
                evidenceCollected: orNull(inv.evidenceCollected),
              })
            : disciplineProcessService.openInvestigation(caseId, {
                investigatorId: orNull(inv.investigatorId),
                investigationStartDate: orNull(inv.investigationStartDate),
              });
        case 'complete':
          return disciplineProcessService.completeInvestigation(caseId, findings);
        case 'hearing':
          return disciplineProcessService.scheduleHearing(caseId, {
            hearingDate: hear.hearingDate,
            hearingVenue: orNull(hear.hearingVenue),
            hearingOfficerId: orNull(hear.hearingOfficerId),
          });
        case 'outcome':
          return disciplineProcessService.recordHearingOutcome(caseId, {
            employeeAttendedHearing: outcome.employeeAttendedHearing,
            employeeStatement: orNull(outcome.employeeStatement),
            employeeHadRepresentation: outcome.employeeHadRepresentation,
            // Everything about the representative is dropped when nobody accompanied them, rather
            // than left behind from a previous save.
            representativeType: outcome.employeeHadRepresentation
              ? (outcome.representativeType || null) : null,
            representativeEmployeeId: outcome.employeeHadRepresentation
              ? orNull(outcome.representativeEmployeeId) : null,
            representativeName: outcome.employeeHadRepresentation
              ? orNull(outcome.representativeName) : null,
            representativePosition: outcome.employeeHadRepresentation
              ? orNull(outcome.representativePosition) : null,
            representativeContactInfo: outcome.employeeHadRepresentation
              ? orNull(outcome.representativeContactInfo) : null,
            hearingNotes: orNull(outcome.hearingNotes),
          });
        default:
          throw new Error('No action chosen.');
      }
    },
    onSuccess: () => {
      setDialog(null);
      onChanged();
      toast({ title: 'Saved' });
    },
    onError: (e: Error) =>
      toast({ title: 'The change was refused', description: e.message, variant: 'destructive' }),
  });

  const Field = ({ label, value }: { label: string; value?: string | null }) => (
    <div>
      <dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm">{value?.trim() ? value : '—'}</dd>
    </div>
  );
  const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

  return (
    <>
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>Investigation</CardTitle>
          {canWrite && (
            <div className="flex gap-2">
              <Button variant="outline" size="sm" onClick={() => setDialog('investigation')}>
                {investigation
                  ? <><Pencil className="mr-2 h-4 w-4" /> Edit</>
                  : <><Plus className="mr-2 h-4 w-4" /> Open an investigation</>}
              </Button>
              {investigation && !investigation.investigationEndDate && (
                <Button size="sm" onClick={() => setDialog('complete')}>
                  <CheckCircle2 className="mr-2 h-4 w-4" /> Complete it
                </Button>
              )}
            </div>
          )}
        </CardHeader>
        <CardContent>
          {investigation ? (
            <div className="grid gap-5 md:grid-cols-3">
              <Field label="Investigator" value={investigation.investigatorName} />
              <Field label="Started" value={fmtDate(investigation.investigationStartDate)} />
              <Field label="Completed" value={fmtDate(investigation.investigationEndDate)} />
              <div className="md:col-span-3">
                <Field label="Findings" value={investigation.investigationFindings} />
              </div>
              <div className="md:col-span-3">
                <Field label="Evidence collected" value={investigation.evidenceCollected} />
              </div>
            </div>
          ) : (
            <EmptyState title="No investigation" description="None has been opened for this case." />
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>Hearing</CardTitle>
          {canWrite && (
            <div className="flex gap-2">
              <Button variant="outline" size="sm" onClick={() => setDialog('hearing')}>
                {hearing
                  ? <><Pencil className="mr-2 h-4 w-4" /> Reschedule</>
                  : <><CalendarPlus className="mr-2 h-4 w-4" /> Schedule a hearing</>}
              </Button>
              {hearing && (
                <Button size="sm" onClick={() => setDialog('outcome')}>
                  Record what happened
                </Button>
              )}
            </div>
          )}
        </CardHeader>
        <CardContent>
          {hearing ? (
            <div className="grid gap-5 md:grid-cols-3">
              <Field label="Date" value={fmtDate(hearing.hearingDate)} />
              <Field label="Venue" value={hearing.hearingVenue} />
              <Field label="Hearing officer" value={hearing.hearingOfficerName} />
              <Field
                label="Employee attended"
                value={hearing.employeeAttendedHearing === undefined
                  ? null : hearing.employeeAttendedHearing ? 'Yes' : 'No'}
              />
              <Field
                label="Accompanied"
                value={hearing.employeeHadRepresentation
                  ? (hearing.representativeEmployeeName ?? hearing.representativeName ?? 'Yes')
                  : 'No'}
              />
              <Field label="Representative type" value={hearing.representativeType} />
              <div className="md:col-span-3">
                <Field label="The employee's statement" value={hearing.employeeStatement} />
              </div>
              <div className="md:col-span-3">
                <Field label="Notes" value={hearing.hearingNotes} />
              </div>
            </div>
          ) : (
            <EmptyState title="No hearing" description="None has been scheduled for this case." />
          )}
        </CardContent>
      </Card>

      {/* ── investigation ─────────────────────────────────────────────────── */}
      <Dialog open={dialog === 'investigation'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{investigation ? 'Edit the investigation' : 'Open an investigation'}</DialogTitle>
            <DialogDescription>
              {investigation
                ? 'Recording findings here does not complete the investigation — "Complete it" is what moves the case on.'
                : 'A case carries one investigation. The investigator and start date can be filled in later.'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Investigator</Label>
              <EmployeePicker
                value={inv.investigatorId}
                onChange={(v) => setInv((f) => ({ ...f, investigatorId: v ?? '' }))}
                initialLabel={investigation?.investigatorName ?? null}
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label>Started</Label>
                <Input
                  type="date"
                  value={inv.investigationStartDate}
                  onChange={(e) => setInv((f) => ({ ...f, investigationStartDate: e.target.value }))}
                />
              </div>
              {investigation && (
                <div className="space-y-2">
                  <Label>Completed</Label>
                  <Input
                    type="date"
                    value={inv.investigationEndDate}
                    onChange={(e) => setInv((f) => ({ ...f, investigationEndDate: e.target.value }))}
                  />
                </div>
              )}
            </div>
            {investigation && (
              <>
                <div className="space-y-2">
                  <Label>Findings</Label>
                  <Textarea
                    value={inv.investigationFindings}
                    onChange={(e) => setInv((f) => ({ ...f, investigationFindings: e.target.value }))}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Evidence collected</Label>
                  <Textarea
                    value={inv.evidenceCollected}
                    onChange={(e) => setInv((f) => ({ ...f, evidenceCollected: e.target.value }))}
                  />
                </div>
              </>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)}>Cancel</Button>
            <Button onClick={() => run.mutate()} disabled={run.isPending}>
              {run.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── complete ──────────────────────────────────────────────────────── */}
      <Dialog open={dialog === 'complete'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Complete the investigation</DialogTitle>
            <DialogDescription>
              This advances the case. The findings recorded here are what the decision is taken on.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Findings *</Label>
            <Textarea
              rows={6}
              value={findings}
              onChange={(e) => setFindings(e.target.value)}
              placeholder="What the investigation established."
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)}>Cancel</Button>
            <Button onClick={() => run.mutate()} disabled={run.isPending || !findings.trim()}>
              {run.isPending ? 'Saving…' : 'Complete'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── hearing ───────────────────────────────────────────────────────── */}
      <Dialog open={dialog === 'hearing'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{hearing ? 'Reschedule the hearing' : 'Schedule a hearing'}</DialogTitle>
            <DialogDescription>
              The employee must be given notice of it — the show-cause notice and this date are what
              the natural-justice gate reads.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Date and time *</Label>
              <Input
                type="datetime-local"
                value={hear.hearingDate}
                onChange={(e) => setHear((f) => ({ ...f, hearingDate: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Venue</Label>
              <Input
                value={hear.hearingVenue}
                onChange={(e) => setHear((f) => ({ ...f, hearingVenue: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Hearing officer</Label>
              <EmployeePicker
                value={hear.hearingOfficerId}
                onChange={(v) => setHear((f) => ({ ...f, hearingOfficerId: v ?? '' }))}
                initialLabel={hearing?.hearingOfficerName ?? null}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)}>Cancel</Button>
            <Button onClick={() => run.mutate()} disabled={run.isPending || !hear.hearingDate}>
              {run.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── the outcome ───────────────────────────────────────────────────── */}
      <Dialog open={dialog === 'outcome'} onOpenChange={(o) => !o && setDialog(null)}>
        <DialogContent className="max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>What happened at the hearing</DialogTitle>
            <DialogDescription>
              Whether the employee was heard, and by whom they were accompanied, is the record the
              case turns on later (FR-HR-179).
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="flex items-center gap-2">
              <Checkbox
                id="attended"
                checked={outcome.employeeAttendedHearing}
                onCheckedChange={(v) =>
                  setOutcome((f) => ({ ...f, employeeAttendedHearing: v === true }))}
              />
              <Label htmlFor="attended">The employee attended</Label>
            </div>
            <div className="space-y-2">
              <Label>The employee&apos;s statement</Label>
              <Textarea
                rows={4}
                value={outcome.employeeStatement}
                onChange={(e) => setOutcome((f) => ({ ...f, employeeStatement: e.target.value }))}
              />
            </div>
            <div className="flex items-center gap-2">
              <Checkbox
                id="represented"
                checked={outcome.employeeHadRepresentation}
                onCheckedChange={(v) =>
                  setOutcome((f) => ({ ...f, employeeHadRepresentation: v === true }))}
              />
              <Label htmlFor="represented">They were accompanied</Label>
            </div>

            {outcome.employeeHadRepresentation && (
              <div className="space-y-4 rounded-md border p-3">
                <div className="space-y-2">
                  <Label>Who accompanied them</Label>
                  <Select
                    value={outcome.representativeType}
                    onValueChange={(v) =>
                      setOutcome((f) => ({
                        ...f, representativeType: v as DisciplinaryRepresentativeType,
                      }))}
                  >
                    <SelectTrigger><SelectValue placeholder="Choose" /></SelectTrigger>
                    <SelectContent>
                      {REPRESENTATIVE_OPTIONS.map((o) => (
                        <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                {outcome.representativeType === 'WorkplaceColleague' ? (
                  <div className="space-y-2">
                    <Label>The colleague</Label>
                    <EmployeePicker
                      value={outcome.representativeEmployeeId}
                      onChange={(v) =>
                        setOutcome((f) => ({ ...f, representativeEmployeeId: v ?? '' }))}
                      initialLabel={hearing?.representativeEmployeeName ?? null}
                    />
                  </div>
                ) : (
                  <>
                    <div className="space-y-2">
                      <Label>Name</Label>
                      <Input
                        value={outcome.representativeName}
                        onChange={(e) =>
                          setOutcome((f) => ({ ...f, representativeName: e.target.value }))}
                      />
                    </div>
                    <div className="grid grid-cols-2 gap-3">
                      <div className="space-y-2">
                        <Label>Position</Label>
                        <Input
                          value={outcome.representativePosition}
                          onChange={(e) =>
                            setOutcome((f) => ({ ...f, representativePosition: e.target.value }))}
                        />
                      </div>
                      <div className="space-y-2">
                        <Label>Contact</Label>
                        <Input
                          value={outcome.representativeContactInfo}
                          onChange={(e) =>
                            setOutcome((f) => ({ ...f, representativeContactInfo: e.target.value }))}
                        />
                      </div>
                    </div>
                  </>
                )}
              </div>
            )}

            <div className="space-y-2">
              <Label>Notes</Label>
              <Textarea
                rows={3}
                value={outcome.hearingNotes}
                onChange={(e) => setOutcome((f) => ({ ...f, hearingNotes: e.target.value }))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)}>Cancel</Button>
            <Button onClick={() => run.mutate()} disabled={run.isPending}>
              {run.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
