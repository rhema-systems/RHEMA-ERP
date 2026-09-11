'use client';

import { useEffect } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
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
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { BudgetLinePicker } from '@/components/hr/recruitment/BudgetLinePicker';
import { BudgetCheckPanel } from '@/components/hr/recruitment/BudgetCheckPanel';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import { locationService } from '@/services/hr/location.service';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import {
  STAFF_REPLACEMENT_REASONS,
  STAFF_REQUISITION_PRIORITIES,
  STAFF_REQUISITION_TYPES,
  type StaffRequisitionPriority,
  type StaffRequisitionType,
} from '@/types/hr/recruitment';

export interface RequisitionFormState {
  positionId: string;
  /**
   * The approved job description for the position, or '' (round 3, lane Q). Filled from the
   * position's current description when the position is chosen; the detail screen had shown
   * "Job description: —" on every requisition because no form ever sent this.
   */
  jobDescriptionId: string;
  locationId: string;
  type: StaffRequisitionType;
  priority: StaffRequisitionPriority;
  requisitionTitle: string;
  description: string;
  numberOfPositions: number;
  replacementForEmployeeId: string | null;
  replacementForEmployeeName: string | null;
  replacementReason: string;
  employeeDepartureDate: string;
  desiredStartDate: string;
  latestAcceptableStartDate: string;
  targetFillDate: string;
  businessJustification: string;
  impactIfNotFilled: string;
  /** The approved budget line to draw down from, or '' (R5). Replaces the self-declared flag + free-text code. */
  manpowerBudgetLineId: string;
  exceptionJustification: string;
  allowInternalCandidates: boolean;
  allowExternalCandidates: boolean;
  notes: string;
}

export const emptyRequisitionForm = (): RequisitionFormState => ({
  positionId: '',
  jobDescriptionId: '',
  locationId: '',
  type: 'NewPosition',
  priority: 'Medium',
  requisitionTitle: '',
  description: '',
  numberOfPositions: 1,
  replacementForEmployeeId: null,
  replacementForEmployeeName: null,
  replacementReason: '',
  employeeDepartureDate: '',
  desiredStartDate: '',
  latestAcceptableStartDate: '',
  targetFillDate: '',
  businessJustification: '',
  impactIfNotFilled: '',
  manpowerBudgetLineId: '',
  exceptionJustification: '',
  allowInternalCandidates: true,
  allowExternalCandidates: true,
  notes: '',
});

interface Props {
  value: RequisitionFormState;
  onChange: (next: RequisitionFormState) => void;
  /** The position cannot move once a vacancy hangs off the requisition. */
  positionLocked?: boolean;
  /** Editing: the requisition whose own posts must not count as drawdown in the preview (R5). */
  requisitionId?: string;
}

/**
 * The requisition form, shared by the create and edit screens.
 *
 * **Org placement comes from the position**, not from separate pickers: the position already
 * carries its organisation unit and level, and letting someone choose a mismatched pair is how the
 * employee endpoints ended up refusing valid-looking payloads. The parent reads them off the
 * selected position when it builds the payload.
 */
export function RequisitionFormFields({ value, onChange, positionLocked, requisitionId }: Props) {
  const set = <K extends keyof RequisitionFormState>(key: K, v: RequisitionFormState[K]) =>
    onChange({ ...value, [key]: v });

  const positions = useQuery({
    queryKey: ['hr', 'positions', 'all'],
    queryFn: () => employeePositionService.getAll(),
  });

  const locations = useQuery({
    queryKey: ['hr', 'locations', 'all'],
    queryFn: () => locationService.getAll(),
  });

  // The position's current APPROVED description (the same read the offer letter uses). A draft
  // or superseded one is not offered: recruiting against a description nobody signed off is
  // the thing the approval exists to prevent.
  const currentJd = useQuery({
    queryKey: ['hr', 'job-descriptions', 'current-for-position', value.positionId],
    queryFn: () => jobArchitectureService.getCurrentForPosition(value.positionId),
    enabled: !!value.positionId,
  });

  // Fill the description in when the position changes and nothing is chosen yet. Deliberately
  // one-way: a description already on the record (edit) is kept even if a newer version has
  // since been approved — the user sees both and decides.
  useEffect(() => {
    if (!value.positionId || value.jobDescriptionId || !currentJd.data?.id) return;
    onChange({ ...value, jobDescriptionId: currentJd.data.id });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value.positionId, value.jobDescriptionId, currentJd.data?.id]);

  const jdIsCurrent = !!value.jobDescriptionId && currentJd.data?.id === value.jobDescriptionId;

  const isReplacement = value.type === 'Replacement';

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">The role</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="requisitionTitle">Title *</Label>
            <Input
              id="requisitionTitle"
              value={value.requisitionTitle}
              onChange={(e) => set('requisitionTitle', e.target.value)}
              placeholder="e.g. Two additional Accounts Officers"
            />
          </div>

          <div className="space-y-1.5">
            <Label>Position *</Label>
            <Select
              value={value.positionId}
              onValueChange={(v) => set('positionId', v)}
              disabled={positionLocked}
            >
              <SelectTrigger>
                <SelectValue placeholder="Select a position" />
              </SelectTrigger>
              <SelectContent>
                {(positions.data ?? []).map((p) => (
                  <SelectItem key={p.id} value={p.id}>
                    {p.title}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {positionLocked && (
              <p className="text-xs text-muted-foreground">
                A vacancy has been opened from this requisition, so the position is fixed.
              </p>
            )}
          </div>

          <div className="space-y-1.5">
            <Label>Job description</Label>
            {!value.positionId ? (
              <p className="text-sm text-muted-foreground">Choose the position first.</p>
            ) : currentJd.isLoading ? (
              <p className="text-sm text-muted-foreground">Looking up the position's description…</p>
            ) : value.jobDescriptionId ? (
              <div className="rounded-md border px-3 py-2 text-sm">
                {jdIsCurrent && currentJd.data ? (
                  <Link
                    href={`/hr/job-descriptions/${currentJd.data.id}`}
                    className="font-medium text-primary hover:underline"
                    target="_blank"
                  >
                    {currentJd.data.jobTitle} · v{currentJd.data.versionNumber}
                  </Link>
                ) : (
                  <span className="font-medium">The description already on this requisition</span>
                )}
                <p className="text-xs text-muted-foreground">
                  {jdIsCurrent
                    ? 'The current approved description for this position.'
                    : currentJd.data
                      ? `A newer approved version exists (${currentJd.data.jobTitle} · v${currentJd.data.versionNumber}).`
                      : 'This position has no current approved description; the one on record is kept.'}
                </p>
                {!jdIsCurrent && (
                  <div className="mt-1 flex gap-3">
                    {currentJd.data && (
                      <button
                        type="button"
                        className="text-xs text-primary hover:underline"
                        onClick={() => set('jobDescriptionId', currentJd.data!.id)}
                      >
                        Use the current version
                      </button>
                    )}
                    {/* Needed when the position is changed on edit: the old description now
                        belongs to another post and the server refuses it. */}
                    <button
                      type="button"
                      className="text-xs text-muted-foreground hover:underline"
                      onClick={() => set('jobDescriptionId', '')}
                    >
                      Remove it
                    </button>
                  </div>
                )}
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">
                No approved job description for this position yet. The requisition can be raised
                without one; write and approve the description before the offer stage.
              </p>
            )}
          </div>

          <div className="space-y-1.5">
            <Label>Type *</Label>
            <Select value={value.type} onValueChange={(v) => set('type', v as StaffRequisitionType)}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {STAFF_REQUISITION_TYPES.map((t) => (
                  <SelectItem key={t} value={t}>
                    {humanizeEnum(t)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-1.5">
            <Label>Priority *</Label>
            <Select
              value={value.priority}
              onValueChange={(v) => set('priority', v as StaffRequisitionPriority)}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {STAFF_REQUISITION_PRIORITIES.map((p) => (
                  <SelectItem key={p} value={p}>
                    {p}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="numberOfPositions">How many heads *</Label>
            <Input
              id="numberOfPositions"
              type="number"
              min={1}
              max={100}
              value={value.numberOfPositions}
              onChange={(e) => set('numberOfPositions', Number(e.target.value) || 1)}
            />
          </div>

          <div className="space-y-1.5">
            <Label>Location</Label>
            <Select value={value.locationId} onValueChange={(v) => set('locationId', v)}>
              <SelectTrigger>
                <SelectValue placeholder="Select a location" />
              </SelectTrigger>
              <SelectContent>
                {(locations.data ?? []).map((l) => (
                  <SelectItem key={l.id} value={l.id}>
                    {l.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-1.5 md:col-span-2">
            <Label htmlFor="description">Description</Label>
            <Textarea
              id="description"
              rows={3}
              value={value.description}
              onChange={(e) => set('description', e.target.value)}
            />
          </div>
        </CardContent>
      </Card>

      {isReplacement && (
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Who is being replaced</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2">
            <div className="space-y-1.5">
              <Label>Outgoing employee</Label>
              <EmployeePicker
                value={value.replacementForEmployeeId}
                initialLabel={value.replacementForEmployeeName}
                onChange={(id, label) =>
                  onChange({
                    ...value,
                    replacementForEmployeeId: id,
                    replacementForEmployeeName: label,
                  })
                }
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="employeeDepartureDate">Departure date</Label>
              <Input
                id="employeeDepartureDate"
                type="date"
                value={value.employeeDepartureDate}
                onChange={(e) => set('employeeDepartureDate', e.target.value)}
              />
            </div>
            <div className="space-y-1.5 md:col-span-2">
              <Label>Why they left</Label>
              <Select
                value={value.replacementReason}
                onValueChange={(v) => set('replacementReason', v)}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select a reason" />
                </SelectTrigger>
                <SelectContent>
                  {STAFF_REPLACEMENT_REASONS.map((reason) => (
                    <SelectItem key={reason} value={reason}>
                      {humanizeEnum(reason)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Timing</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-3">
          <div className="space-y-1.5">
            <Label htmlFor="desiredStartDate">Desired start *</Label>
            <Input
              id="desiredStartDate"
              type="date"
              value={value.desiredStartDate}
              onChange={(e) => set('desiredStartDate', e.target.value)}
            />
            <p className="text-xs text-muted-foreground">
              The fiscal year for the budget check comes from this date.
            </p>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="latestAcceptableStartDate">Latest acceptable start</Label>
            <Input
              id="latestAcceptableStartDate"
              type="date"
              value={value.latestAcceptableStartDate}
              onChange={(e) => set('latestAcceptableStartDate', e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="targetFillDate">Target fill date</Label>
            <Input
              id="targetFillDate"
              type="date"
              value={value.targetFillDate}
              onChange={(e) => set('targetFillDate', e.target.value)}
            />
            <p className="text-xs text-muted-foreground">
              What the overdue list is measured against.
            </p>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">The case for it</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="businessJustification">Business justification *</Label>
            <Textarea
              id="businessJustification"
              rows={4}
              value={value.businessJustification}
              onChange={(e) => set('businessJustification', e.target.value)}
              placeholder="What the work is, and why it needs another person rather than a redistribution."
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="impactIfNotFilled">Impact if not filled</Label>
            <Textarea
              id="impactIfNotFilled"
              rows={3}
              value={value.impactIfNotFilled}
              onChange={(e) => set('impactIfNotFilled', e.target.value)}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Budget and audience</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Round 2b, R5: budgeted is a fact about a link. The picker offers the approved lines
              for the post; the live check below says what the server will say at submit; the
              exception box appears when the check says one will be required (D-4). */}
          <BudgetLinePicker
            positionId={value.positionId}
            value={value.manpowerBudgetLineId}
            onChange={(id) => set('manpowerBudgetLineId', id)}
          />
          {value.positionId && (
            <BudgetCheckPanel
              preview={{
                positionId: value.positionId,
                numberOfPositions: Math.max(1, value.numberOfPositions || 1),
                desiredStartDate: value.desiredStartDate || null,
                manpowerBudgetLineId: value.manpowerBudgetLineId || null,
                excludeRequisitionId: requisitionId ?? null,
              }}
            />
          )}
          <div className="space-y-1.5">
            <Label htmlFor="exceptionJustification">Exception justification</Label>
            <Textarea
              id="exceptionJustification"
              rows={3}
              value={value.exceptionJustification}
              onChange={(e) => set('exceptionJustification', e.target.value)}
              placeholder="Why this post should be recruited for without an approved budget line, or with no establishment gap."
            />
            <p className="text-xs text-muted-foreground">
              Required to submit when the check above says so. The approver sees it beside the check.
            </p>
          </div>

          <div className="space-y-2">
            {/* The server refuses a requisition with neither audience selected, so say why here
                rather than letting the save fail. */}
            <p className="text-sm font-medium">Who may apply *</p>
            <div className="flex items-center gap-2">
              <Checkbox
                id="allowInternalCandidates"
                checked={value.allowInternalCandidates}
                onCheckedChange={(c) => set('allowInternalCandidates', c === true)}
              />
              <Label htmlFor="allowInternalCandidates" className="font-normal">
                Internal candidates
              </Label>
            </div>
            <div className="flex items-center gap-2">
              <Checkbox
                id="allowExternalCandidates"
                checked={value.allowExternalCandidates}
                onCheckedChange={(c) => set('allowExternalCandidates', c === true)}
              />
              <Label htmlFor="allowExternalCandidates" className="font-normal">
                External candidates
              </Label>
            </div>
            {!value.allowInternalCandidates && !value.allowExternalCandidates && (
              <p className="text-xs text-destructive">
                Pick at least one — a vacancy nobody may apply for cannot be saved. The vacancy
                inherits these, and its adverts are created from them on publication.
              </p>
            )}
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="notes">Notes</Label>
            <Textarea
              id="notes"
              rows={2}
              value={value.notes}
              onChange={(e) => set('notes', e.target.value)}
            />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
