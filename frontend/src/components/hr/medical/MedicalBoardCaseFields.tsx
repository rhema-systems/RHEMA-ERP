'use client';

/**
 * The fields of one case before a medical board — who, the question, why, and the examination it
 * rests on (round 5, lane K-II-a). Shared by "Request a board" (its first case) and "Add a case".
 *
 * ⚠ The examinations offered are the chosen employee's own: the server refuses anybody else's
 * (lane K3), so offering one would be offering a refusal. Reached through the employee's profile.
 */

import { useQuery } from '@tanstack/react-query';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { medicalHealthService } from '@/services/hr/medical-health.service';
import {
  MEDICAL_BOARD_OUTCOME_LABEL,
  MEDICAL_BOARD_PURPOSE_LABEL,
  type MedicalBoardPurpose,
} from '@/types/hr/medical-board';

/** Radix Select cannot carry an empty value, so "none chosen" is a sentinel. */
export const NO_EXAM = '__none';

const PURPOSES = Object.keys(MEDICAL_BOARD_PURPOSE_LABEL) as MedicalBoardPurpose[];

export interface MedicalBoardCaseDraft {
  employeeId: string;
  purpose: MedicalBoardPurpose | '';
  reason: string;
  examId: string;
}

export const EMPTY_CASE: MedicalBoardCaseDraft = { employeeId: '', purpose: '', reason: '', examId: NO_EXAM };

export const caseDraftComplete = (d: MedicalBoardCaseDraft) => !!d.employeeId && !!d.purpose && !!d.reason.trim();

export function MedicalBoardCaseFields({
  value,
  onChange,
  enabled,
  idPrefix,
}: {
  value: MedicalBoardCaseDraft;
  onChange: (next: MedicalBoardCaseDraft) => void;
  /** Load the pickers only while the dialog is open. */
  enabled: boolean;
  idPrefix: string;
}) {
  const { data: profile } = useQuery({
    queryKey: ['hr', 'medical-profiles', 'by-employee', value.employeeId],
    queryFn: () => medicalHealthService.getProfileByEmployee(value.employeeId),
    enabled: enabled && !!value.employeeId,
  });
  const { data: exams = [] } = useQuery({
    queryKey: ['hr', 'medical-profiles', profile?.id, 'exams'],
    queryFn: () => medicalHealthService.getExamsByProfile(profile?.id ?? ''),
    enabled: enabled && !!profile?.id,
  });

  const set = (patch: Partial<MedicalBoardCaseDraft>) => onChange({ ...value, ...patch });

  return (
    <>
      <div className="space-y-2">
        <Label>Employee <span className="text-red-500">*</span></Label>
        <EmployeePicker
          value={value.employeeId || null}
          onChange={(id) => set({ employeeId: id ?? '', examId: NO_EXAM })}
        />
      </div>

      <div className="space-y-2">
        <Label htmlFor={`${idPrefix}-purpose`}>
          What the board is asked about them <span className="text-red-500">*</span>
        </Label>
        <Select value={value.purpose} onValueChange={(v) => set({ purpose: v as MedicalBoardPurpose })}>
          <SelectTrigger id={`${idPrefix}-purpose`}>
            <SelectValue placeholder="Choose the question the board is asked" />
          </SelectTrigger>
          <SelectContent>
            {PURPOSES.map((p) => (
              <SelectItem key={p} value={p}>{MEDICAL_BOARD_PURPOSE_LABEL[p]}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        {/* ⚠ Said here, because it decides whether leave can ever rest on this case. */}
        <p className="text-xs text-muted-foreground">
          Only a case about an absence — extended sick leave, an injury on duty, or other — can
          satisfy a leave type&apos;s medical-board rule, and only once it has been decided in the
          leave year being counted.
        </p>
      </div>

      <div className="space-y-2">
        <Label htmlFor={`${idPrefix}-reason`}>
          Why a board is needed <span className="text-red-500">*</span>
        </Label>
        <Textarea
          id={`${idPrefix}-reason`}
          rows={3}
          value={value.reason}
          onChange={(e) => set({ reason: e.target.value })}
          placeholder="e.g. sick leave this year has passed the point at which a board must sit."
        />
        <p className="text-xs text-muted-foreground">
          Required. A case put to a board without a stated question is one nobody can tell whether
          it answered.
        </p>
      </div>

      <div className="space-y-2">
        <Label htmlFor={`${idPrefix}-exam`}>Based on an examination</Label>
        <Select value={value.examId} onValueChange={(v) => set({ examId: v })} disabled={!value.employeeId}>
          <SelectTrigger id={`${idPrefix}-exam`}><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value={NO_EXAM}>None</SelectItem>
            {exams.map((x) => (
              <SelectItem key={x.id} value={x.id}>
                {x.examDate?.slice(0, 10)}
                {x.result ? ` · ${MEDICAL_BOARD_OUTCOME_LABEL[x.result as keyof typeof MEDICAL_BOARD_OUTCOME_LABEL] ?? x.result}` : ''}
                {x.facilityName ? ` · ${x.facilityName}` : ''}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <p className="text-xs text-muted-foreground">
          {!value.employeeId
            ? 'Choose the employee first — only their own examinations are offered.'
            : exams.length === 0
              ? 'No examinations are recorded for this employee.'
              : 'Only this employee’s own examinations are offered.'}
        </p>
      </div>
    </>
  );
}
