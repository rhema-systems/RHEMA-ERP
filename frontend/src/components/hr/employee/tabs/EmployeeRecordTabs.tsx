'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  ArrowLeftRight,
  Award,
  CalendarCheck,
  CalendarDays,
  Coins,
  Compass,
  DoorOpen,
  Gavel,
  Gift,
  GraduationCap,
  HeartPulse,
  Hourglass,
  Laptop,
  Plane,
  Target,
  Users,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { formatDate } from '@/lib/hr/attendance-format';
import { movementService } from '@/services/hr/movement.service';
import { probationService } from '@/services/hr/probation.service';
import { separationService } from '@/services/hr/separation.service';
import { leaveService } from '@/services/hr/leave.service';
import { monthlySummaryService } from '@/services/hr/attendance.service';
import { employeeBenefitEnrollmentService } from '@/services/hr/benefits.service';
import { salaryChangeRequestService } from '@/services/hr/salary-change-request.service';
import { trainingCompletionService } from '@/services/hr/training-completion.service';
import { trainingNominationService } from '@/services/hr/training-nomination.service';
import { performanceAppraisalService } from '@/services/hr/appraisal-run.service';
import { employeeGoalService } from '@/services/hr/goals.service';
import { disciplineService } from '@/services/hr/discipline.service';
import { awardsService } from '@/services/hr/awards.service';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { medicalHealthService } from '@/services/hr/medical-health.service';
import { medicalClaimService } from '@/services/hr/medical-claims.service';
import { travelService } from '@/services/hr/travel.service';
import { employeeOrientationService } from '@/services/hr/employee-orientation.service';
import { successionCandidateService } from '@/services/hr/succession.service';
import { SALARY_CHANGE_STATUS_LABELS, type SalaryChangeRequest } from '@/types/hr/salary-change-request';
import type { StaffMovementSummary } from '@/types/hr/movements';
import type { ProbationPeriodSummary } from '@/types/hr/probation';
import type { SeparationListItem } from '@/types/hr/separation';
import type { LeaveRequest } from '@/types/hr/leave-request';
import type { StaffMonthlyAttendanceSummary } from '@/types/hr/attendance';
import type { EmployeeBenefitEnrollmentListItem } from '@/types/hr/benefits';
import type { TrainingCompletion, TrainingNominationSummary } from '@/types/hr/training-delivery';
import type { PerformanceAppraisal } from '@/types/hr/appraisal-run';
import type { EmployeeGoal } from '@/types/hr/goals';
import type { DisciplinaryCaseSummary } from '@/types/hr/discipline';
import type { EmployeeAwardSummary } from '@/types/hr/awards';
import type { AssetAssignmentSummary } from '@/types/hr/assets';
import type { MedicalExpenseClaimSummary } from '@/types/hr/medical';
import type { StaffTravelRequestSummary } from '@/types/hr/travel';
import type { EmployeeOrientationSummary } from '@/types/hr/orientation';
import type { SuccessionCandidate } from '@/types/hr/succession';
import { RecordSummaryTab, dash } from './RecordSummaryTab';

/**
 * The lane T2 record tabs (round 3; decision D-4): Movements, Probation, Separation, Leave,
 * Attendance, Benefits, Salary changes. Each is one `RecordSummaryTab` over the module's existing
 * by-employee read; each links to the module for the record itself and for every write.
 */

const money = (v?: number | null, currency = 'GHS') =>
  v == null ? '—' : new Intl.NumberFormat('en-GH', { style: 'currency', currency }).format(v);

// ── Movements ────────────────────────────────────────────────────────────────

export function MovementsRecordTab({ employeeId }: { employeeId: string }) {
  return (
    <RecordSummaryTab<StaffMovementSummary>
      title="Movements"
      description="Promotions, transfers, acting appointments and secondments, newest first. Raise or implement one on the movements screen."
      queryKey={['hr', 'employees', employeeId, 'record', 'movements']}
      queryFn={() => movementService.getByEmployee(employeeId)}
      rowKey={(m) => m.id}
      rowHref={(m) => `/hr/movements/${m.id}`}
      emptyIcon={ArrowLeftRight}
      emptyTitle="No movements"
      emptyDescription="No promotion, transfer or acting appointment has been recorded for this employee."
      openHref={`/hr/movements?employeeId=${employeeId}`}
      openLabel="Open in Movements"
      columns={[
        { key: 'number', header: 'Movement', render: (m) => m.movementNumber },
        { key: 'type', header: 'Type', render: (m) => `${m.movementTypeName}${m.isTemporary ? ' · temporary' : ''}` },
        {
          key: 'change',
          header: 'From → to',
          render: (m) => (
            <span className="text-sm">
              {dash(m.currentPositionTitle)} <span className="text-muted-foreground">→</span> {dash(m.newPositionTitle)}
              {m.newOrganizationUnitName && m.newOrganizationUnitName !== m.currentOrganizationUnitName && (
                <span className="block text-xs text-muted-foreground">{m.newOrganizationUnitName}</span>
              )}
            </span>
          ),
        },
        { key: 'effective', header: 'Effective', render: (m) => formatDate(m.effectiveDate), className: 'w-[120px]' },
        { key: 'status', header: 'Status', render: (m) => <StatusBadge status={String(m.status)} />, className: 'w-[130px]' },
      ]}
    />
  );
}

// ── Probation ────────────────────────────────────────────────────────────────

export function ProbationRecordTab({ employeeId }: { employeeId: string }) {
  return (
    <RecordSummaryTab<ProbationPeriodSummary>
      title="Probation"
      description="Every probation period on record, with its extensions and reviews. Reviews, extensions and confirmation happen on the probation screen — confirming is what issues the letter."
      queryKey={['hr', 'employees', employeeId, 'record', 'probation']}
      queryFn={() => probationService.getForEmployee(employeeId)}
      rowKey={(p) => p.id}
      rowHref={(p) => `/hr/probation/${p.id}`}
      emptyIcon={Hourglass}
      emptyTitle="No probation period"
      emptyDescription="No probation period has been opened for this employee."
      openHref={`/hr/probation?employeeId=${employeeId}`}
      openLabel="Open in Probation"
      columns={[
        { key: 'period', header: 'Period', render: (p) => `${formatDate(p.startDate)} — ${formatDate(p.currentEndDate)}` },
        { key: 'months', header: 'Months', render: (p) => p.durationMonths, className: 'w-[90px]' },
        { key: 'ext', header: 'Extensions', render: (p) => p.extensionCount, className: 'w-[110px]' },
        { key: 'reviews', header: 'Reviews', render: (p) => p.reviewCount, className: 'w-[90px]' },
        { key: 'status', header: 'Status', render: (p) => <StatusBadge status={p.statusName} />, className: 'w-[130px]' },
      ]}
    />
  );
}

// ── Separation ───────────────────────────────────────────────────────────────

export function SeparationRecordTab({ employeeId }: { employeeId: string }) {
  return (
    <RecordSummaryTab<SeparationListItem>
      title="Separation"
      description="Resignations, retirements, terminations and their clearance. Initiated and progressed on the separations screen."
      queryKey={['hr', 'employees', employeeId, 'record', 'separations']}
      queryFn={() => separationService.getForEmployee(employeeId)}
      rowKey={(s) => s.id}
      rowHref={(s) => `/hr/separations/${s.id}`}
      emptyIcon={DoorOpen}
      emptyTitle="No separation"
      emptyDescription="No separation has been initiated for this employee."
      openHref={`/hr/separations?employeeId=${employeeId}`}
      openLabel="Open in Separations"
      columns={[
        { key: 'number', header: 'Separation', render: (s) => s.separationNumber },
        {
          key: 'type',
          header: 'Type',
          render: (s) => (
            <span>
              {s.separationTypeName}
              {s.isDisciplinary && <Badge variant="destructive" className="ml-2">Disciplinary</Badge>}
              {s.isSystemInitiated && <Badge variant="outline" className="ml-2">System</Badge>}
            </span>
          ),
        },
        { key: 'initiated', header: 'Initiated', render: (s) => formatDate(s.initiatedOn), className: 'w-[120px]' },
        { key: 'lwd', header: 'Last working day', render: (s) => (s.lastWorkingDay ? formatDate(s.lastWorkingDay) : '—'), className: 'w-[140px]' },
        { key: 'status', header: 'Status', render: (s) => <StatusBadge status={s.statusName} />, className: 'w-[130px]' },
      ]}
    />
  );
}

// ── Leave ────────────────────────────────────────────────────────────────────

export function LeaveRecordTab({ employeeId }: { employeeId: string }) {
  const [year, setYear] = useState(new Date().getFullYear());
  const { data: balances } = useQuery({
    queryKey: ['hr', 'employees', employeeId, 'record', 'leave-balances', year],
    queryFn: () => leaveService.getEmployeeBalances(employeeId, year),
  });

  return (
    <RecordSummaryTab<LeaveRequest>
      title="Leave"
      description="This year's balances and the request history. Requests, approvals and adjustments live on the leave screens."
      queryKey={['hr', 'employees', employeeId, 'record', 'leave-history', year]}
      queryFn={async () => (await leaveService.getEmployeeHistory(employeeId, year, 1, 50)).items ?? []}
      rowKey={(r) => r.id}
      rowHref={(r) => `/hr/leave/requests/${r.id}`}
      emptyIcon={CalendarDays}
      emptyTitle={`No leave requests in ${year}`}
      emptyDescription="Nothing was requested in this year. Change the year to look further back."
      openHref={`/hr/leave/requests?employeeId=${employeeId}`}
      openLabel="Open in Leave"
      toolbar={
        <div className="space-y-3">
          <YearPicker year={year} onChange={setYear} />
          {balances && balances.length > 0 && (
            <div className="flex flex-wrap gap-2" data-testid="leave-balances">
              {balances.map((b) => (
                <div key={b.id} className="rounded-md border px-3 py-1.5 text-sm">
                  <span className="font-medium">{b.leaveTypeName}</span>
                  {b.leaveSubTypeName && <span className="text-muted-foreground"> · {b.leaveSubTypeName}</span>}
                  <span className="ml-2 text-muted-foreground">
                    {b.availableDays} of {b.entitledDays} left
                    {b.pendingDays > 0 ? ` · ${b.pendingDays} pending` : ''}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      }
      columns={[
        { key: 'number', header: 'Request', render: (r) => r.requestNumber },
        { key: 'type', header: 'Type', render: (r) => `${r.leaveTypeName}${r.leaveSubTypeName ? ` · ${r.leaveSubTypeName}` : ''}` },
        { key: 'period', header: 'Period', render: (r) => `${formatDate(r.startDate)} — ${formatDate(r.endDate)}`, className: 'w-[220px]' },
        { key: 'days', header: 'Days', render: (r) => r.totalDays, className: 'w-[70px]' },
        { key: 'status', header: 'Status', render: (r) => <StatusBadge status={r.status} />, className: 'w-[130px]' },
      ]}
    />
  );
}

// ── Attendance ───────────────────────────────────────────────────────────────

export function AttendanceRecordTab({ employeeId }: { employeeId: string }) {
  const [year, setYear] = useState(new Date().getFullYear());
  return (
    <RecordSummaryTab<StaffMonthlyAttendanceSummary>
      title="Attendance"
      description="The monthly roll-up payroll reads — days present, absent, on leave, late. Daily records, regularisations and overtime live on the attendance screens."
      queryKey={['hr', 'employees', employeeId, 'record', 'attendance', year]}
      queryFn={() => monthlySummaryService.getByEmployeeAndYear(employeeId, year)}
      rowKey={(m) => `${m.year}-${m.month}`}
      emptyIcon={CalendarCheck}
      emptyTitle={`No monthly summaries for ${year}`}
      emptyDescription="Summaries are generated from daily attendance at month end. Change the year to look further back."
      openHref={`/hr/attendance/daily?employeeId=${employeeId}`}
      openLabel="Open in Attendance"
      toolbar={<YearPicker year={year} onChange={setYear} />}
      columns={[
        { key: 'month', header: 'Month', render: (m) => `${m.monthName} ${m.year}` },
        { key: 'present', header: 'Present', render: (m) => `${m.daysPresent} / ${m.totalWorkingDays}`, className: 'w-[110px]' },
        { key: 'absent', header: 'Absent', render: (m) => m.daysAbsent, className: 'w-[90px]' },
        { key: 'leave', header: 'On leave', render: (m) => m.daysOnLeave, className: 'w-[90px]' },
        { key: 'late', header: 'Late', render: (m) => m.daysLate, className: 'w-[80px]' },
        { key: 'pct', header: 'Attendance', render: (m) => `${Math.round(m.attendancePercentage)}%`, className: 'w-[110px]' },
        {
          key: 'final',
          header: 'Finalised',
          render: (m) => (m.isFinalized ? <Badge variant="outline">Yes</Badge> : <span className="text-muted-foreground">No</span>),
          className: 'w-[100px]',
        },
      ]}
    />
  );
}

// ── Benefits ─────────────────────────────────────────────────────────────────

export function BenefitsRecordTab({ employeeId }: { employeeId: string }) {
  return (
    <RecordSummaryTab<EmployeeBenefitEnrollmentListItem>
      title="Benefits"
      description="Benefit policies this employee is enrolled in, with the value assessed and what is left of each limit. Enrolments are managed on the benefits screen."
      queryKey={['hr', 'employees', employeeId, 'record', 'benefits']}
      queryFn={() => employeeBenefitEnrollmentService.getByEmployee(employeeId)}
      rowKey={(b) => b.id}
      emptyIcon={Gift}
      emptyTitle="No benefit enrolments"
      emptyDescription="This employee is not enrolled in any benefit policy."
      openHref={`/hr/benefits?employeeId=${employeeId}`}
      openLabel="Open in Benefits"
      columns={[
        { key: 'policy', header: 'Policy', render: (b) => b.benefitPolicyName },
        { key: 'source', header: 'Source', render: (b) => String(b.source), className: 'w-[120px]' },
        { key: 'value', header: 'Assessed value', render: (b) => money(b.assessedValue, b.currency), className: 'w-[140px]' },
        {
          key: 'limit',
          header: 'Limit used',
          render: (b) => (b.coverageLimit > 0 ? `${money(b.utilizedAmount, b.currency)} of ${money(b.coverageLimit, b.currency)}` : '—'),
          className: 'w-[220px]',
        },
        { key: 'from', header: 'From', render: (b) => formatDate(b.effectiveFrom), className: 'w-[110px]' },
        { key: 'status', header: 'Status', render: (b) => <StatusBadge status={String(b.status)} />, className: 'w-[130px]' },
      ]}
    />
  );
}

// ── Salary changes ───────────────────────────────────────────────────────────

const KIND_LABEL: Record<SalaryChangeRequest['kind'], string> = {
  Placement: 'Placement on the scale',
  NegotiatedAmount: 'Negotiated amount',
  PayBasisSwitch: 'Pay basis switch',
};

export function SalaryChangesRecordTab({ employeeId }: { employeeId: string }) {
  return (
    <RecordSummaryTab<SalaryChangeRequest>
      title="Salary changes"
      description="Every change of pay requested for this employee and where it stands. A new request, approval and retry are on the Salary tab."
      queryKey={['hr', 'employees', employeeId, 'record', 'salary-changes']}
      queryFn={() => salaryChangeRequestService.list(employeeId)}
      rowKey={(r) => r.id}
      emptyIcon={Coins}
      emptyTitle="No salary change requests"
      emptyDescription="No change of pay has been requested for this employee."
      openHref={`/hr/employees/${employeeId}?tab=salary`}
      openLabel="Open the Salary tab"
      columns={[
        { key: 'kind', header: 'Change', render: (r) => KIND_LABEL[r.kind] ?? r.kind },
        {
          key: 'to',
          header: 'Proposed',
          render: (r) =>
            r.kind === 'Placement'
              ? `${dash(r.proposedGradeCode)}${r.proposedLevelCode ? ` / ${r.proposedLevelCode}` : ''}${r.proposedNotchNumber ? ` / notch ${r.proposedNotchNumber}` : ''}`
              : r.kind === 'NegotiatedAmount'
                ? money(r.proposedAmount, r.proposedCurrencyCode ?? 'GHS')
                : dash(r.proposedPayBasis),
        },
        { key: 'result', header: 'Monthly basic', render: (r) => money(r.resultingMonthlyBasicPay), className: 'w-[140px]' },
        { key: 'effective', header: 'Effective', render: (r) => formatDate(r.effectiveDate), className: 'w-[120px]' },
        { key: 'status', header: 'Status', render: (r) => <StatusBadge status={SALARY_CHANGE_STATUS_LABELS[r.status] ?? r.status} />, className: 'w-[170px]' },
      ]}
    />
  );
}

// ═════════════════════════════════════════════════════════════════════════════
// Lane T3: Training, Appraisals & goals, Discipline, Awards, Assets, Medical, Travel, Orientation,
// Succession — the same shape, over each module's own by-employee read.
// ═════════════════════════════════════════════════════════════════════════════

// ── Training ─────────────────────────────────────────────────────────────────

export function TrainingRecordTab({ employeeId }: { employeeId: string }) {
  const { data: nominations } = useQuery({
    queryKey: ['hr', 'employees', employeeId, 'record', 'training-nominations'],
    queryFn: () => trainingNominationService.getByEmployee(employeeId),
  });
  const pending = (nominations ?? []).filter((n) => String(n.status) !== 'Completed' && String(n.status) !== 'Cancelled');
  return (
    <RecordSummaryTab<TrainingCompletion>
      title="Training"
      description="Programmes completed, with scores and the manager's verification; nominations still in flight above. Nominate, schedule and record completions on the training screens."
      queryKey={['hr', 'employees', employeeId, 'record', 'training-completions']}
      queryFn={() => trainingCompletionService.getByEmployee(employeeId)}
      rowKey={(c) => c.id}
      emptyIcon={GraduationCap}
      emptyTitle="No training completed"
      emptyDescription="No completion has been recorded for this employee."
      openHref={`/hr/training/completions?employeeId=${employeeId}`}
      openLabel="Open in Training"
      toolbar={
        pending.length > 0 ? (
          <div className="flex flex-wrap gap-2" data-testid="training-nominations">
            {pending.map((n) => (
              <div key={n.id} className="rounded-md border px-3 py-1.5 text-sm">
                <span className="font-medium">{n.programName}</span>
                <span className="ml-2 text-muted-foreground">
                  {formatDate(n.trainingStartDate)} · {String(n.status)}
                </span>
              </div>
            ))}
          </div>
        ) : null
      }
      columns={[
        { key: 'program', header: 'Programme', render: (c) => c.programName },
        { key: 'date', header: 'Completed', render: (c) => formatDate(c.completionDate), className: 'w-[120px]' },
        { key: 'score', header: 'Score', render: (c) => (c.finalScore == null ? '—' : c.finalScore), className: 'w-[80px]' },
        { key: 'passed', header: 'Passed', render: (c) => (c.isPassed ? 'Yes' : 'No'), className: 'w-[80px]' },
        {
          key: 'verified',
          header: 'Verified',
          render: (c) => (c.isVerifiedByManager ? <Badge variant="outline">By {c.verifiedByName ?? 'manager'}</Badge> : <span className="text-muted-foreground">Not yet</span>),
          className: 'w-[170px]',
        },
        { key: 'status', header: 'Status', render: (c) => <StatusBadge status={String(c.status)} />, className: 'w-[130px]' },
      ]}
    />
  );
}

// ── Appraisals & goals ───────────────────────────────────────────────────────

export function AppraisalsRecordTab({ employeeId }: { employeeId: string }) {
  const { data: goals } = useQuery({
    queryKey: ['hr', 'employees', employeeId, 'record', 'goals'],
    queryFn: () => employeeGoalService.getByEmployee(employeeId),
  });
  const openGoals = (goals ?? []).filter((g) => !['Completed', 'Cancelled', 'Closed'].includes(String(g.status)));
  return (
    <RecordSummaryTab<PerformanceAppraisal>
      title="Appraisals & goals"
      description="Every appraisal on record with its score — a withdrawn one with its reason and no score; the goals still open are listed above. Scoring, calibration and goal-setting happen on the performance screens."
      queryKey={['hr', 'employees', employeeId, 'record', 'appraisals']}
      queryFn={() => performanceAppraisalService.getByEmployee(employeeId)}
      rowKey={(a) => a.id}
      rowHref={(a) => `/hr/performance/hr-review/${a.id}`}
      emptyIcon={Target}
      emptyTitle="No appraisals"
      emptyDescription="No appraisal has been opened for this employee in any cycle."
      openHref={`/hr/performance/employee-goals?employeeId=${employeeId}`}
      openLabel="Open in Performance"
      toolbar={
        openGoals.length > 0 ? (
          <div className="space-y-1" data-testid="open-goals">
            <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Open goals</p>
            <div className="flex flex-wrap gap-2">
              {openGoals.map((g) => (
                <a key={g.id} href={`/hr/performance/employee-goals/${g.id}`} className="rounded-md border px-3 py-1.5 text-sm hover:bg-muted">
                  <span className="font-medium">{g.title}</span>
                  <span className="ml-2 text-muted-foreground">{g.weight}% · {String(g.status)}</span>
                </a>
              ))}
            </div>
          </div>
        ) : null
      }
      columns={[
        { key: 'number', header: 'Appraisal', render: (a) => `${a.appraisalNumber}${a.appraisalCycleCode ? ` · ${a.appraisalCycleCode}` : ''}` },
        { key: 'period', header: 'Period', render: (a) => `${formatDate(a.startDate)} — ${formatDate(a.endDate)}`, className: 'w-[220px]' },
        { key: 'score', header: 'Score', render: (a) => (a.adjustedScore ?? a.overallScore ?? '—'), className: 'w-[90px]' },
        { key: 'rank', header: 'Rank in unit', render: (a) => dash(a.rankInUnit), className: 'w-[110px]' },
        {
          key: 'status',
          header: 'Status',
          // A withdrawn appraisal says why (performance closure E-d1); the server sends it no score.
          render: (a) => (
            <div className="space-y-1">
              <StatusBadge status={String(a.status)} />
              {a.withdrawnReason && (
                <p className="line-clamp-2 text-xs text-muted-foreground" title={a.withdrawnReason}>
                  {a.withdrawnReason}
                </p>
              )}
            </div>
          ),
          className: 'w-[200px]',
        },
      ]}
    />
  );
}

// ── Discipline ───────────────────────────────────────────────────────────────

export function DisciplineRecordTab({ employeeId }: { employeeId: string }) {
  return (
    <RecordSummaryTab<DisciplinaryCaseSummary>
      title="Discipline"
      description="Disciplinary cases against this employee and their outcome. Cases are opened, heard and closed on the discipline screen."
      queryKey={['hr', 'employees', employeeId, 'record', 'discipline']}
      queryFn={() => disciplineService.getByEmployee(employeeId)}
      rowKey={(c) => c.id}
      rowHref={(c) => `/hr/discipline/${c.id}`}
      emptyIcon={Gavel}
      emptyTitle="No disciplinary cases"
      emptyDescription="No case has been opened against this employee."
      openHref={`/hr/discipline?employeeId=${employeeId}`}
      openLabel="Open in Discipline"
      columns={[
        { key: 'number', header: 'Case', render: (c) => c.caseNumber },
        { key: 'offense', header: 'Offence', render: (c) => `${c.offenseName} · ${c.severityName}` },
        { key: 'incident', header: 'Incident', render: (c) => formatDate(c.incidentDate), className: 'w-[120px]' },
        {
          key: 'outcome',
          header: 'Outcome',
          render: (c) => {
            const parts = [c.hasWarning && 'Warning', c.hasSuspension && 'Suspension', c.hasFine && 'Fine', c.hasTermination && 'Termination'].filter(Boolean);
            return parts.length ? parts.join(', ') : '—';
          },
          className: 'w-[200px]',
        },
        { key: 'status', header: 'Status', render: (c) => <StatusBadge status={c.statusName} />, className: 'w-[140px]' },
      ]}
    />
  );
}

// ── Awards ───────────────────────────────────────────────────────────────────

export function AwardsRecordTab({ employeeId }: { employeeId: string }) {
  return (
    <RecordSummaryTab<EmployeeAwardSummary>
      title="Awards"
      description="Awards conferred on this employee. Nominations, committee decisions and presentations are on the awards screen."
      queryKey={['hr', 'employees', employeeId, 'record', 'awards']}
      queryFn={() => awardsService.getForEmployee(employeeId)}
      rowKey={(a) => a.id}
      rowHref={(a) => `/hr/awards/${a.id}`}
      emptyIcon={Award}
      emptyTitle="No awards"
      emptyDescription="No award has been conferred on this employee."
      openHref={`/hr/awards?employeeId=${employeeId}`}
      openLabel="Open in Awards"
      columns={[
        { key: 'number', header: 'Award', render: (a) => a.awardNumber },
        { key: 'type', header: 'Type', render: (a) => `${a.awardTypeName}${a.awardLevelName ? ` · ${a.awardLevelName}` : ''}` },
        { key: 'date', header: 'Awarded', render: (a) => formatDate(a.awardDate), className: 'w-[120px]' },
        { key: 'amount', header: 'Amount', render: (a) => money(a.monetaryAmount), className: 'w-[130px]' },
        { key: 'presented', header: 'Presented', render: (a) => (a.presentationDate ? formatDate(a.presentationDate) : 'Not yet'), className: 'w-[120px]' },
      ]}
    />
  );
}

// ── Assets ───────────────────────────────────────────────────────────────────

export function AssetsRecordTab({ employeeId }: { employeeId: string }) {
  return (
    <RecordSummaryTab<AssetAssignmentSummary>
      title="Assets"
      description="Company assets currently in this employee's hands. Issue, transfer and return them on the assets screens."
      queryKey={['hr', 'employees', employeeId, 'record', 'assets']}
      queryFn={() => assetRegisterService.getActiveAssignmentsForEmployee(employeeId)}
      rowKey={(a) => a.id}
      rowHref={(a) => `/hr/assets/assignments/${a.id}`}
      emptyIcon={Laptop}
      emptyTitle="No assets assigned"
      emptyDescription="No company asset is assigned to this employee at the moment."
      openHref={`/hr/assets/assignments?employeeId=${employeeId}`}
      openLabel="Open in Assets"
      columns={[
        { key: 'asset', header: 'Asset', render: (a) => `${a.assetName} · ${a.assetNumber}` },
        { key: 'type', header: 'Type', render: (a) => `${a.assetTypeName}${a.isBenefitInKind ? ' · benefit in kind' : ''}` },
        { key: 'since', header: 'Since', render: (a) => formatDate(a.assignmentDate), className: 'w-[120px]' },
        { key: 'due', header: 'Return due', render: (a) => (a.expectedReturnDate ? formatDate(a.expectedReturnDate) : '—'), className: 'w-[120px]' },
        { key: 'ack', header: 'Acknowledged', render: (a) => (a.employeeAcknowledged ? 'Yes' : 'No'), className: 'w-[120px]' },
        { key: 'status', header: 'Status', render: (a) => <StatusBadge status={a.statusName} />, className: 'w-[130px]' },
      ]}
    />
  );
}

// ── Medical ──────────────────────────────────────────────────────────────────

export function MedicalRecordTab({ employeeId }: { employeeId: string }) {
  const { data: profile } = useQuery({
    queryKey: ['hr', 'employees', employeeId, 'record', 'health-profile'],
    queryFn: () => medicalHealthService.getProfileByEmployee(employeeId),
  });
  return (
    <RecordSummaryTab<MedicalExpenseClaimSummary>
      title="Medical"
      description="The health profile in brief and the expense claims filed. Clinical records stay on the medical screens, where access is narrower than this page's."
      queryKey={['hr', 'employees', employeeId, 'record', 'medical-claims']}
      queryFn={() => medicalClaimService.getByEmployee(employeeId)}
      rowKey={(c) => c.id}
      rowHref={(c) => `/hr/medical/claims/${c.id}`}
      emptyIcon={HeartPulse}
      emptyTitle="No medical claims"
      emptyDescription="No expense claim has been filed by or for this employee."
      openHref={profile ? `/hr/medical/health/${profile.id}` : `/hr/medical/claims?employeeId=${employeeId}`}
      openLabel="Open in Medical"
      toolbar={
        profile ? (
          <div className="flex flex-wrap gap-2 text-sm" data-testid="health-profile">
            <span className="rounded-md border px-3 py-1.5">Blood group <span className="font-medium">{String(profile.bloodGroup)}</span></span>
            {profile.preferredFacilityName && <span className="rounded-md border px-3 py-1.5">Facility <span className="font-medium">{profile.preferredFacilityName}</span></span>}
            {profile.emergencyContactName && <span className="rounded-md border px-3 py-1.5">Emergency <span className="font-medium">{profile.emergencyContactName}</span>{profile.emergencyContactPhone ? ` · ${profile.emergencyContactPhone}` : ''}</span>}
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">No health profile on file.</p>
        )
      }
      columns={[
        { key: 'number', header: 'Claim', render: (c) => `${c.claimNumber}${c.isForDependent && c.dependentName ? ` · for ${c.dependentName}` : ''}` },
        { key: 'type', header: 'Expense', render: (c) => `${c.expenseTypeName ?? String(c.expenseType)} · ${c.facilityName}` },
        { key: 'date', header: 'Service', render: (c) => formatDate(c.serviceDate), className: 'w-[120px]' },
        { key: 'amount', header: 'Requested / approved', render: (c) => `${money(c.amountRequested)}${c.amountApproved != null ? ` / ${money(c.amountApproved)}` : ''}`, className: 'w-[220px]' },
        { key: 'status', header: 'Status', render: (c) => <StatusBadge status={c.statusName ?? String(c.status)} />, className: 'w-[140px]' },
      ]}
    />
  );
}

// ── Travel ───────────────────────────────────────────────────────────────────

export function TravelRecordTab({ employeeId }: { employeeId: string }) {
  return (
    <RecordSummaryTab<StaffTravelRequestSummary>
      title="Travel"
      description="Travel requested by or for this employee, with its budget and risk level. Requests, bookings and claims are on the travel screens."
      queryKey={['hr', 'employees', employeeId, 'record', 'travel']}
      queryFn={() => travelService.getByEmployee(employeeId)}
      rowKey={(t) => t.id}
      rowHref={(t) => `/hr/travel/${t.id}`}
      emptyIcon={Plane}
      emptyTitle="No travel"
      emptyDescription="No travel request has been raised for this employee."
      openHref={`/hr/travel?employeeId=${employeeId}`}
      openLabel="Open in Travel"
      columns={[
        { key: 'number', header: 'Request', render: (t) => t.requestNumber },
        { key: 'where', header: 'Destination', render: (t) => `${t.destinationCity}, ${t.destinationCountryName}${t.isInternational ? ' · international' : ''}` },
        { key: 'when', header: 'Dates', render: (t) => `${formatDate(t.travelStartDate)} — ${formatDate(t.travelEndDate)}`, className: 'w-[220px]' },
        { key: 'cost', header: 'Estimate', render: (t) => money(t.estimatedTotalCost, t.currencyCode), className: 'w-[130px]' },
        { key: 'status', header: 'Status', render: (t) => <StatusBadge status={String(t.status)} />, className: 'w-[140px]' },
      ]}
    />
  );
}

// ── Orientation ──────────────────────────────────────────────────────────────

export function OrientationRecordTab({ employeeId }: { employeeId: string }) {
  return (
    <RecordSummaryTab<EmployeeOrientationSummary>
      title="Orientation"
      description="Orientation programmes this employee was enrolled in, with progress and result. Enrolment and sessions are on the orientation screens."
      queryKey={['hr', 'employees', employeeId, 'record', 'orientation']}
      queryFn={() => employeeOrientationService.getByEmployee(employeeId)}
      rowKey={(o) => o.id}
      emptyIcon={Compass}
      emptyTitle="No orientation"
      emptyDescription="This employee has not been enrolled in an orientation programme."
      openHref={`/hr/orientation/enrollments?employeeId=${employeeId}`}
      openLabel="Open in Orientation"
      columns={[
        { key: 'program', header: 'Programme', render: (o) => `${o.programTitle ?? o.programCode ?? '—'}${o.sessionTitle ? ` · ${o.sessionTitle}` : ''}` },
        { key: 'enrolled', header: 'Enrolled', render: (o) => formatDate(o.enrolledAt), className: 'w-[120px]' },
        { key: 'progress', header: 'Progress', render: (o) => `${Math.round(o.progressPercentage)}%`, className: 'w-[100px]' },
        { key: 'result', header: 'Result', render: (o) => (o.completedAt ? `${o.isPassed ? 'Passed' : 'Not passed'}${o.finalScore != null ? ` · ${o.finalScore}` : ''}` : '—'), className: 'w-[150px]' },
        { key: 'status', header: 'Status', render: (o) => <StatusBadge status={String(o.completionStatus)} />, className: 'w-[140px]' },
      ]}
    />
  );
}

// ── Succession ───────────────────────────────────────────────────────────────

export function SuccessionRecordTab({ employeeId }: { employeeId: string }) {
  return (
    <RecordSummaryTab<SuccessionCandidate>
      title="Succession"
      description="Succession plans this employee is a candidate on, and how ready they are judged to be. Plans and readiness reviews are on the succession screens."
      queryKey={['hr', 'employees', employeeId, 'record', 'succession']}
      queryFn={() => successionCandidateService.getByEmployee(employeeId)}
      rowKey={(c) => c.id}
      rowHref={(c) => `/hr/succession/${c.successionPlanId}`}
      emptyIcon={Users}
      emptyTitle="Not on a succession plan"
      emptyDescription="This employee is not a candidate on any succession plan."
      openHref={`/hr/succession?employeeId=${employeeId}`}
      openLabel="Open in Succession"
      columns={[
        { key: 'plan', header: 'Plan', render: (c) => c.planNumber },
        { key: 'type', header: 'As', render: (c) => `${c.typeName}${c.isEmergencyOnly ? ' · emergency only' : ''}`, className: 'w-[180px]' },
        { key: 'rank', header: 'Rank', render: (c) => c.rank, className: 'w-[70px]' },
        { key: 'ready', header: 'Readiness', render: (c) => `${c.currentReadinessName}${c.readyByDate ? ` · by ${formatDate(c.readyByDate)}` : ''}` },
        { key: 'pool', header: 'Talent pool', render: (c) => dash(c.talentPoolName), className: 'w-[160px]' },
      ]}
    />
  );
}

// ── shared ───────────────────────────────────────────────────────────────────

function YearPicker({ year, onChange }: { year: number; onChange: (y: number) => void }) {
  const now = new Date().getFullYear();
  return (
    <div className="flex items-center gap-2" data-testid="record-year-picker">
      <Button variant="outline" size="sm" onClick={() => onChange(year - 1)} aria-label="Previous year">
        ‹
      </Button>
      <span className="min-w-[4ch] text-center text-sm font-medium">{year}</span>
      <Button variant="outline" size="sm" onClick={() => onChange(year + 1)} disabled={year >= now} aria-label="Next year">
        ›
      </Button>
    </div>
  );
}
