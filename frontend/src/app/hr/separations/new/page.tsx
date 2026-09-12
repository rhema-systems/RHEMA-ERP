'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation } from '@tanstack/react-query';
import { AlertTriangle, Info, Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { separationService } from '@/services/hr/separation.service';
import type {
  CreateSeparation,
  SeparationType,
  TerminationReason,
} from '@/types/hr/separation';

/**
 * Raise a separation.
 *
 * ⚠ **Three rules here are the server's, and the form's job is to stop somebody meeting them as a
 * refusal.** They are not duplicated validation — the server enforces them regardless — they are
 * the difference between a form that explains itself and one that argues with you.
 *
 * 1. **A compulsory retirement takes effect on the birthday (FR-HR-093).** The date field is hidden
 *    for that type, because the service computes it and REFUSES a date that disagrees. Same for a
 *    contract expiry where the employee has a contract with an end date.
 * 2. **Absence days decide who signs (FR-HR-092).** At or above the tenant's threshold the
 *    separation is procedural and HR may approve it; below it, the Managing Director must. The
 *    field only appears for the routes where absence is the reason.
 * 3. **A resignation cannot be SUBMITTED without a notice date.** It can be saved as a draft
 *    without one, so the field is not required here — but the form says why it will be wanted.
 */

const TYPE_OPTIONS: { value: SeparationType; label: string; hint?: string }[] = [
  { value: 'VoluntaryResignation', label: 'Resignation', hint: 'The employee is leaving of their own accord.' },
  { value: 'CompulsoryRetirement', label: 'Retirement — compulsory', hint: 'Takes effect on their birthday.' },
  { value: 'VoluntaryRetirement', label: 'Retirement — voluntary', hint: 'Elected early retirement.' },
  { value: 'MedicalRetirement', label: 'Retirement — medical', hint: 'Needs the medical report attached before it can be submitted.' },
  { value: 'ContractExpiry', label: 'Contract expiry', hint: 'Ends on the contract’s own end date.' },
  { value: 'InvoluntaryRedundancy', label: 'Redundancy' },
  { value: 'InvoluntaryForCause', label: 'Termination — conduct' },
  { value: 'InvoluntaryPerformance', label: 'Termination — performance' },
  { value: 'SummaryDismissal', label: 'Summary dismissal', hint: 'Without notice. The natural-justice process still applies.' },
  { value: 'MutualAgreement', label: 'Mutual agreement' },
  { value: 'Death', label: 'Death', hint: 'Needs the death certificate attached before it can be submitted.' },
  { value: 'Other', label: 'Other' },
];

const REASONS: TerminationReason[] = [
  'Resignation', 'Redundancy', 'Dismissal', 'ContractExpiry',
  'Retirement', 'Death', 'MutualAgreement', 'EndOfInternship', 'Other',
];

/** The routes where the effective date is the system's to work out, not the user's to type. */
const DATE_IS_COMPUTED: SeparationType[] = ['CompulsoryRetirement', 'ContractExpiry'];

/** The routes where days of unauthorised absence are the reason, and so decide who signs. */
const ABSENCE_RELEVANT: SeparationType[] = ['InvoluntaryForCause', 'SummaryDismissal'];

/** The routes that carry no notice at all. */
const NO_NOTICE: SeparationType[] = ['Death', 'SummaryDismissal', 'ContractExpiry'];

export default function NewSeparationPage() {
  const router = useRouter();

  const [employeeId, setEmployeeId] = useState('');
  const [separationType, setSeparationType] = useState<SeparationType>('VoluntaryResignation');
  const [reasonCategory, setReasonCategory] = useState<TerminationReason>('Resignation');
  const [reasonNotes, setReasonNotes] = useState('');
  const [noticeGivenOn, setNoticeGivenOn] = useState('');
  const [noticeDays, setNoticeDays] = useState('');
  const [lastWorkingDay, setLastWorkingDay] = useState('');
  const [effectiveDate, setEffectiveDate] = useState('');
  const [absenceDays, setAbsenceDays] = useState('');
  const [error, setError] = useState<string | null>(null);

  const selectedType = TYPE_OPTIONS.find((t) => t.value === separationType);
  const dateIsComputed = DATE_IS_COMPUTED.includes(separationType);
  const absenceRelevant = ABSENCE_RELEVANT.includes(separationType);
  const carriesNotice = !NO_NOTICE.includes(separationType);

  const create = useMutation({
    mutationFn: (payload: CreateSeparation) => separationService.create(payload),
    onSuccess: (created) => router.push(`/hr/separations/${created.id}`),
    onError: (e: Error) => setError(e.message),
  });

  const submit = () => {
    setError(null);

    if (!employeeId) {
      setError('Name the employee who is leaving.');
      return;
    }

    create.mutate({
      employeeId,
      separationType,
      reasonCategory,
      reasonNotes: reasonNotes.trim() || null,
      // ⚠ Empty string, not undefined, is what breaks a DateOnly binder — send null. And for the
      // computed routes send null deliberately: a date here that disagrees with the birthday or the
      // contract is refused, so the form must not send one at all.
      noticeGivenOn: carriesNotice && noticeGivenOn ? noticeGivenOn : null,
      noticeDays: noticeDays === '' ? null : Number(noticeDays),
      lastWorkingDay: lastWorkingDay || null,
      effectiveDate: dateIsComputed ? null : effectiveDate || null,
      absenceDays: absenceRelevant && absenceDays !== '' ? Number(absenceDays) : null,
    });
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Raise a separation"
        description="Opens the record. Nothing is decided by creating it — approval, clearance and the settlement each follow."
        backHref="/hr/separations"
      />

      {error && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Who is leaving</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label>Employee</Label>
            <EmployeePicker value={employeeId} onChange={(id) => setEmployeeId(id ?? '')} />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">The route out</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label>Type</Label>
            <Select value={separationType} onValueChange={(v) => setSeparationType(v as SeparationType)}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {TYPE_OPTIONS.map((t) => (
                  <SelectItem key={t.value} value={t.value}>{t.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            {selectedType?.hint && (
              <p className="text-xs text-muted-foreground">{selectedType.hint}</p>
            )}
          </div>

          <div className="space-y-2">
            <Label>Reason (for reporting)</Label>
            <Select value={reasonCategory} onValueChange={(v) => setReasonCategory(v as TerminationReason)}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {REASONS.map((r) => <SelectItem key={r} value={r}>{r}</SelectItem>)}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              The type drives what happens; the reason is for reporting. That is what lets
              “resignation” and “resignation to avoid dismissal” be the same process with different
              analytics.
            </p>
          </div>

          <div className="space-y-2">
            <Label>Notes</Label>
            <Textarea
              rows={3}
              value={reasonNotes}
              onChange={(e) => setReasonNotes(e.target.value)}
              placeholder="Anything the file should carry about why."
            />
          </div>

          {absenceRelevant && (
            <div className="space-y-2">
              <Label>Days of unauthorised absence</Label>
              <Input
                type="number"
                min={0}
                value={absenceDays}
                onChange={(e) => setAbsenceDays(e.target.value)}
                placeholder="Leave blank if absence is not the reason"
              />
              <Alert>
                <Info className="h-4 w-4" />
                <AlertDescription>
                  This decides who signs. At or above the organisation’s threshold the separation is
                  <strong> procedural</strong> and HR may approve it; below it — or left blank — the
                  Managing Director must sign (FR-HR-092).
                </AlertDescription>
              </Alert>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Dates</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {carriesNotice ? (
            <>
              <div className="space-y-2">
                <Label>Notice given on</Label>
                <Input type="date" value={noticeGivenOn} onChange={(e) => setNoticeGivenOn(e.target.value)} />
                {separationType === 'VoluntaryResignation' && (
                  <p className="text-xs text-muted-foreground">
                    A resignation cannot be submitted without this — the notice period, and any
                    shortfall to be paid, are both counted from it. It can be left blank for now.
                  </p>
                )}
              </div>

              <div className="space-y-2">
                <Label>Notice period (days)</Label>
                <Input
                  type="number"
                  min={0}
                  value={noticeDays}
                  onChange={(e) => setNoticeDays(e.target.value)}
                  placeholder="Leave blank to use the organisation’s default"
                />
              </div>
            </>
          ) : (
            <Alert>
              <Info className="h-4 w-4" />
              <AlertDescription>
                This route carries no notice — a summary dismissal is without notice, a contract that
                expires was always going to, and nobody serves notice on a bereavement.
              </AlertDescription>
            </Alert>
          )}

          <div className="space-y-2">
            <Label>Last working day</Label>
            <Input type="date" value={lastWorkingDay} onChange={(e) => setLastWorkingDay(e.target.value)} />
          </div>

          {dateIsComputed ? (
            <Alert>
              <Info className="h-4 w-4" />
              <AlertDescription>
                {separationType === 'CompulsoryRetirement'
                  ? 'The date employment ends is the employee’s birthday, worked out from their date of birth and the retirement age (FR-HR-093). It is not entered here.'
                  : 'The date employment ends is the contract’s own end date. It is not entered here.'}
              </AlertDescription>
            </Alert>
          ) : (
            <div className="space-y-2">
              <Label>Employment ends on</Label>
              <Input type="date" value={effectiveDate} onChange={(e) => setEffectiveDate(e.target.value)} />
              <p className="text-xs text-muted-foreground">
                Can be left blank and confirmed at approval — the date somebody stops being an
                employee is an outcome of the process, not an input to it.
              </p>
            </div>
          )}
        </CardContent>
      </Card>

      <div className="flex justify-end gap-2">
        <Button variant="outline" onClick={() => router.push('/hr/separations')}>
          Cancel
        </Button>
        <Button onClick={submit} disabled={create.isPending}>
          {create.isPending ? (
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          ) : (
            <Save className="mr-2 h-4 w-4" />
          )}
          Save as draft
        </Button>
      </div>
    </div>
  );
}
