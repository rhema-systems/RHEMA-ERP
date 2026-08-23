'use client';

import { useQuery } from '@tanstack/react-query';
import {
  Medal,
  Users,
  UserCheck,
  UserMinus,
  UserPlus,
  CreditCard,
  Settings,
  CalendarDays,
  Clock,
  Briefcase,
  Coins,
  ShieldPlus,
  Target,
  HandCoins,
  ArrowRightLeft,
  Gavel,
  MessagesSquare,
  Plane,
  GraduationCap,
  HeartPulse,
  HardHat,
  Network,
  DoorOpen,
  ClipboardList,
  FileText,
  Sparkles,
  UserSearch,
  Layers,
  Calculator,
} from 'lucide-react';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';
import { MetricTiles, type MetricTile } from '@/components/hr/common/MetricTiles';
import { HR_ROLES } from '@/components/hr/common/PermissionGate';
import { useAuth } from '@/hooks/use-auth';
import { employeeService } from '@/services/hr/employee.service';
import { disciplineService } from '@/services/hr/discipline.service';
import { movementService } from '@/services/hr/movement.service';
import { travelService } from '@/services/hr/travel.service';
import { staffRequisitionService } from '@/services/hr/recruitment.service';

/**
 * The HR landing page.
 *
 * Two bands of figures and a directory. The workforce band is a genuine partition of the
 * headcount and every authenticated user sees it; the queue band is what the module is waiting
 * on somebody to do, is HR-only, and is not fetched at all for anyone else — a plain employee
 * gets 403 from all four of those endpoints, so firing them would buy four failed requests and
 * an empty row.
 *
 * ⚠ Every queue figure below measured NON-ZERO on live TDC data before it was put here. The two
 * that did not — `position-vacancies/stats` (0 open vacancies against 303 positions) and
 * `hr/teams/summary` (no teams) — are deliberately absent rather than shipped as permanent
 * blank boxes.
 *
 * ⚠ No figure on this page is computed here. Each comes from the service that owns the rule
 * behind it, so the home cannot drift away from the screen it links to.
 */
export default function HrHomePage() {
  const { hasAnyRole } = useAuth();
  const isHr = hasAnyRole(HR_ROLES);

  // ── The workforce band ──────────────────────────────────────────────────────
  // One grouping, four tiles. These used to come from two endpoints that answered two different
  // questions: `stats/active` counted the record-enabled flag and read 6,474 while the status
  // grouping beside it put 6,421 in Active and 53 in Probation — so the "Active" tile silently
  // contained the "On probation" tile. Everything below is now derived from the single grouping,
  // which is why the four numbers cannot contradict each other.
  const { data: byStatus, isLoading: statusLoading } = useQuery({
    queryKey: ['hr', 'employees', 'stats', 'by-status'],
    queryFn: () => employeeService.getCountByStatus(),
  });

  const { data: total, isLoading: totalLoading } = useQuery({
    queryKey: ['hr', 'employees', 'stats', 'total'],
    queryFn: () => employeeService.getTotalCount(),
  });

  const workforceLoading = statusLoading || totalLoading;
  const headcount = total ?? 0;
  const share = (n: number) => (headcount > 0 ? `${Math.round((n / headcount) * 100)}% of headcount` : undefined);

  // ⚠ The keys are the StaffStatus names, because the API registers JsonStringEnumConverter and
  // it applies to dictionary keys too. Measured, not assumed: ["Active","Terminated","Probation"].
  const active = byStatus?.Active ?? 0;
  const probation = byStatus?.Probation ?? 0;
  const terminated = byStatus?.Terminated ?? 0;
  // Suspended, Retired, OnLeave and Inactive are real statuses with no rows on TDC today. They
  // are folded into one tile rather than given four permanently-zero ones, and the tile hides
  // itself when it is empty instead of claiming the four above are the whole population.
  const other = headcount - active - probation - terminated;

  const workforceTiles: MetricTile[] = [
    { label: 'Total employees', value: headcount, icon: Users, href: '/hr/employees' },
    { label: 'Active', value: active, hint: share(active), icon: UserCheck, tone: 'success' },
    { label: 'On probation', value: probation, hint: share(probation), icon: UserPlus, href: '/hr/probation' },
    {
      label: 'Terminated',
      value: terminated,
      hint: other > 0 ? `${other} on another status` : share(terminated),
      icon: UserMinus,
    },
  ];

  // ── The queue band ──────────────────────────────────────────────────────────
  // `enabled: isHr` is doing real work: without it every employee who opened the HR home would
  // fire four requests that answer 403.
  const { data: discipline, isLoading: disciplineLoading } = useQuery({
    queryKey: ['hr', 'home', 'discipline'],
    queryFn: () => disciplineService.getDashboard(),
    enabled: isHr,
  });

  const { data: movements, isLoading: movementsLoading } = useQuery({
    queryKey: ['hr', 'home', 'movements'],
    queryFn: () => movementService.getDashboard(),
    enabled: isHr,
  });

  const { data: travel, isLoading: travelLoading } = useQuery({
    queryKey: ['hr', 'home', 'travel'],
    queryFn: () => travelService.getDashboard(),
    enabled: isHr,
  });

  const { data: requisitions, isLoading: requisitionsLoading } = useQuery({
    queryKey: ['hr', 'home', 'requisitions'],
    queryFn: () => staffRequisitionService.getStatusSummary(),
    enabled: isHr,
  });

  const queueLoading = disciplineLoading || movementsLoading || travelLoading || requisitionsLoading;

  const waiting = (n: number | undefined): MetricTile['tone'] => ((n ?? 0) > 0 ? 'warning' : 'default');

  const queueTiles: MetricTile[] = [
    {
      label: 'Movement approvals pending',
      value: movements?.pendingApproval ?? 0,
      hint:
        (movements?.overdueApprovalActions ?? 0) > 0
          ? `${movements?.overdueApprovalActions} already overdue`
          : undefined,
      icon: ArrowRightLeft,
      tone: (movements?.overdueApprovalActions ?? 0) > 0 ? 'danger' : waiting(movements?.pendingApproval),
      href: '/hr/movements',
    },
    {
      label: 'Travel approvals pending',
      value: travel?.pendingApprovalCount ?? 0,
      hint: `${travel?.totalRequests ?? 0} requests in total`,
      icon: Plane,
      tone: waiting(travel?.pendingApprovalCount),
      href: '/hr/travel',
    },
    {
      label: 'Cases awaiting decision',
      value: discipline?.casesAwaitingDecision ?? 0,
      hint: `${discipline?.totalOpenCases ?? 0} cases open`,
      icon: Gavel,
      tone: waiting(discipline?.casesAwaitingDecision),
      href: '/hr/discipline/queues',
    },
    {
      label: 'Requisitions submitted',
      value: requisitions?.submitted ?? 0,
      hint: `${requisitions?.approved ?? 0} approved so far`,
      icon: ClipboardList,
      tone: waiting(requisitions?.submitted),
      href: '/hr/recruitment/requisitions',
    },
  ];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Human Resources"
        description="Employee records, the HR pipelines, and the setup behind them."
      />

      <section>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Workforce</h2>
        {workforceLoading ? <TileSkeletons /> : <MetricTiles tiles={workforceTiles} />}
      </section>

      {isHr && (
        <section>
          <h2 className="mb-3 text-sm font-medium text-muted-foreground">Waiting on HR</h2>
          {queueLoading ? <TileSkeletons /> : <MetricTiles tiles={queueTiles} />}
        </section>
      )}

      <section>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Areas</h2>
        <NavCardGrid items={AREAS} />
      </section>
    </div>
  );
}

function TileSkeletons() {
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      {[0, 1, 2, 3].map((i) => (
        <Skeleton key={i} className="h-[104px] w-full" />
      ))}
    </div>
  );
}

/**
 * Every HR area that has a screen, in one list.
 *
 * ⚠ This grid carried 13 of the 26 areas under `/hr`. The thirteen missing ones — recruitment,
 * training, safety, medical, travel, succession, probation, orientation, separations,
 * competencies, job descriptions, manpower budgets and the organogram — were reachable only from
 * the sidebar, so the page the module opens on was a directory of half the module. Each entry
 * below points at a route that exists and renders a real landing page; `run-slice10.mjs` and
 * `sweep-routes.mjs` between them keep that true.
 */
const AREAS = [
  {
    title: 'Employees',
    description: 'Directory, profiles and lifecycle.',
    href: '/hr/employees',
    icon: Users,
  },
  {
    title: 'Organogram',
    description: 'The organisation as a chart — units, positions, locations, teams and people.',
    href: '/hr/organogram',
    icon: Network,
  },
  {
    title: 'Recruitment',
    description:
      'Requisitions and vacancies through advertising, screening and interviews to an offer and a hire.',
    href: '/hr/recruitment',
    icon: UserSearch,
  },
  {
    title: 'Orientation & Onboarding',
    description: 'Induction programmes, sessions and the checklist a new joiner works through.',
    href: '/hr/orientation',
    icon: DoorOpen,
  },
  {
    title: 'Probation',
    description: 'Probation periods, reviews, oaths of secrecy and confirmation.',
    href: '/hr/probation',
    icon: UserPlus,
  },
  {
    title: 'Leave',
    description: 'Requests, approvals, balances, plans and year-end.',
    href: '/hr/leave',
    icon: CalendarDays,
  },
  {
    title: 'Attendance & Time',
    description: 'Daily attendance, overtime, remote work, alerts and payroll exports.',
    href: '/hr/attendance',
    icon: Clock,
  },
  {
    title: 'Emoluments',
    description: 'The resolved pay package: basic, allowances and deductions.',
    href: '/hr/emoluments',
    icon: Coins,
  },
  {
    title: 'Benefits',
    description: 'Benefit enrolments, coverage balances and claims.',
    href: '/hr/benefits',
    icon: ShieldPlus,
  },
  {
    title: 'Medical & Health',
    description: 'Schemes, insurance, claims, facilities and the clinical record.',
    href: '/hr/medical',
    icon: HeartPulse,
  },
  {
    title: 'Performance',
    description:
      'Appraisal cycles and the goal cascade, through calibration, appeals and the outcomes an appraisal leads to.',
    href: '/hr/performance',
    icon: Target,
  },
  {
    title: 'Competencies',
    description: 'The competencies a position expects, and where the gaps are.',
    href: '/hr/competencies',
    icon: Sparkles,
  },
  {
    title: 'Job Descriptions',
    description: 'What each job is for — duties, requirements and the gaps against the establishment.',
    href: '/hr/job-descriptions',
    icon: FileText,
  },
  {
    title: 'Training & Learning',
    description: 'Programmes, nominations, schedules, certificates and learning paths.',
    href: '/hr/training',
    icon: GraduationCap,
  },
  {
    title: 'Service Bonds',
    description:
      'Service obligations from sponsored training — who owes time, who owes money, and what has been settled.',
    href: '/hr/service-bonds',
    icon: HandCoins,
  },
  {
    title: 'Succession',
    description: 'Critical positions, talent pools, readiness and emergency cover.',
    href: '/hr/succession',
    icon: Layers,
  },
  {
    title: 'Staff Movements',
    description:
      'Promotions, transfers, demotions, secondments and acting appointments — from request through approval to the change taking effect.',
    href: '/hr/movements',
    icon: ArrowRightLeft,
  },
  {
    title: 'Discipline',
    description:
      'Misconduct cases from report through investigation and hearing to decision, sanction and appeal.',
    href: '/hr/discipline',
    icon: Gavel,
  },
  {
    title: 'Grievances',
    description:
      'Raise a grievance and follow it up the escalation route, or answer one you have been asked about.',
    href: '/hr/grievances',
    icon: MessagesSquare,
  },
  {
    title: 'Separations',
    description: 'Resignations, retirements and dismissals through clearance to final settlement.',
    href: '/hr/separations',
    icon: DoorOpen,
  },
  {
    title: 'Awards & Recognition',
    description:
      'Nominate a colleague, vote, and see the awards and long-service milestones you have received.',
    href: '/hr/awards',
    icon: Medal,
  },
  {
    title: 'Staff Travel',
    description: 'Trip requests, approvals, per diem and travel claims.',
    href: '/hr/travel',
    icon: Plane,
  },
  {
    title: 'Safety, Health & Environment',
    description:
      'Incidents, hazards, risk assessments, permits, PPE, audits and the environmental register.',
    href: '/hr/safety',
    icon: HardHat,
  },
  {
    title: 'Consulting',
    description: 'Client engagements, consultant timesheets and invoices.',
    href: '/hr/consulting',
    icon: Briefcase,
  },
  {
    title: 'Manpower Budgets',
    description: 'Planned headcount and cost against the establishment.',
    href: '/hr/manpower-budgets',
    icon: Calculator,
  },
  {
    title: 'Payroll',
    description: 'Runs, payslips and transactions.',
    href: '/hr/payroll',
    icon: CreditCard,
  },
  {
    title: 'HR Setup',
    description: 'Organization, positions, policy settings and reference data.',
    href: '/administration/hr',
    icon: Settings,
  },
];
