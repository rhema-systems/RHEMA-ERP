'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeftRight, CalendarCheck, CalendarDays, Coins, DoorOpen, Gift, Hourglass } from 'lucide-react';
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
import { SALARY_CHANGE_STATUS_LABELS, type SalaryChangeRequest } from '@/types/hr/salary-change-request';
import type { StaffMovementSummary } from '@/types/hr/movements';
import type { ProbationPeriodSummary } from '@/types/hr/probation';
import type { SeparationListItem } from '@/types/hr/separation';
import type { LeaveRequest } from '@/types/hr/leave-request';
import type { StaffMonthlyAttendanceSummary } from '@/types/hr/attendance';
import type { EmployeeBenefitEnrollmentListItem } from '@/types/hr/benefits';
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
