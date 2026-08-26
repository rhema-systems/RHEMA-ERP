import { apiService } from '../api.service';
import type { StaffMovement } from '@/types/hr/movements';
import type { StaffActingAppointment } from '@/types/hr/movement-subtypes';

/**
 * Area 25 — the self-service portal's own aggregate reads. Backend: `api/employee-portal`.
 *
 * Same law as the asset portal service: no method here takes an employee id, ever — the
 * server derives the employee from the JWT and the client has nothing to get wrong.
 */

export interface PortalLeaveBalance {
  leaveTypeId: string;
  leaveTypeName: string;
  availableDays: number;
  usedDays: number;
  pendingDays: number;
  entitledDays: number;
}

export interface PortalHoliday {
  name: string;
  date: string;
}

export interface PortalExpiringDocument {
  id: string;
  name: string;
  kind?: string | null;
  expiryDate: string;
  daysUntilExpiry: number;
}

/** Slice-10 stub shape — null until the payslip adapter lands. */
export interface PortalPayslipStub {
  payslipNumber: string;
  generatedAt: string;
  netPay: number;
}

/** Slice-12 stub shape — empty until announcements land. */
export interface PortalAnnouncementStub {
  id: string;
  title: string;
  publishedAt: string;
}

export interface PortalHome {
  employeeId: string;
  employeeName: string;
  movementsAwaitingMyResponse: number;
  surchargesAwaitingMyResponse: number;
  assetsAwaitingAcknowledgement: number;
  leaveBalances: PortalLeaveBalance[];
  nextHoliday?: PortalHoliday | null;
  assetsHeldCount: number;
  openAssetRequisitionCount: number;
  activeCertificatesCount: number;
  expiringCertificatesCount: number;
  trainingComplianceRate: number;
  learningPathsEnrolledCount: number;
  expiringDocuments: PortalExpiringDocument[];
  latestPayslip?: PortalPayslipStub | null;
  announcements: PortalAnnouncementStub[];
}

/** The movements dashboard the career hub renders — counts plus the working lists. */
export interface PortalMovementsDashboard {
  employeeId: string;
  employeeName: string;
  currentPositionTitle?: string | null;
  currentOrganizationUnitName?: string | null;
  totalMovements: number;
  totalPromotions: number;
  totalTransfers: number;
  totalActingAppointments: number;
  totalSecondments: number;
  pendingResponseCount: number;
  activeActingAppointmentCount: number;
  activeSecondmentCount: number;
  currentRoleStartDate?: string | null;
  recentMovements: StaffMovement[];
  pendingResponseMovements: StaffMovement[];
  activeTemporaryAssignments: StaffMovement[];
}

/** Computed at request time from the movement data — not persisted notifications. */
export interface PortalMovementNotification {
  id: string;
  title: string;
  message: string;
  category: string;
  isActionRequired: boolean;
  createdAt: string;
  movementId: string;
  movementNumber: string;
  actionUrl?: string | null;
}

// ── Payslips (slice 10) — the read-only adapter over payroll's frozen snapshots ────────────────
// Shapes measured live (probe-slice10): the server deserializes the stored SnapshotJson and
// returns it typed, so everything here is ordinary camelCase. `payslip` is null only when a
// stored snapshot no longer parses.

export interface MyPayslipSummary {
  id: string;
  payslipNumber: string;
  runNumber: string;
  payPeriod: number;
  payPeriodFrom: string;
  payPeriodTo: string;
  currencyCode: string;
  isSeparateBonusRun: boolean;
  grossIncome: number;
  netIncome: number;
  taxAmount: number;
  employeeContribution: number;
  generatedAt: string;
}

export interface PayslipTransaction {
  id: string;
  transactionType: string;
  componentCode: string | null;
  description: string;
  amount: number;
  taxable: boolean;
  currencyCode: string;
}

export interface PayslipContribution {
  item: string;
  employeeContribution: number;
  employerContribution: number;
  totalContribution: number;
  openingBalance: number;
  totalWithdrawal: number;
  grandTotal: number;
  firstTier: number;
  secondTier: number;
  isProvidentFund: boolean;
}

export interface PayslipBankDetail {
  bankName: string | null;
  accountNumber: string | null;
  currencyCode: string;
  exchangeRate: number | null;
  amount: number;
}

export interface PayslipOvertime {
  workingHours: number;
  overtimeHours: number;
  holidayHours: number;
  saturdayHours: number;
  sundayHours: number;
  daySixAndSevenHours: number;
  totalHours: number;
}

/** The frozen payslip document, as payroll serialized it at generation time. */
export interface MyPayslipDocument {
  runNumber: string;
  payPeriod: number;
  payPeriodFrom: string;
  payPeriodTo: string;
  isSeparateBonusRun: boolean;
  companyName: string | null;
  companyAddress: string | null;
  companyPhone: string | null;
  employeeNumber: string;
  employeeName: string;
  departmentName: string | null;
  sectionName: string | null;
  positionTitle: string | null;
  staffCategory: string | null;
  jobLocation: string | null;
  ssfNumber: string | null;
  staffTin: string | null;
  currencyCode: string;
  basicSalary: number;
  grossIncome: number;
  taxableIncome: number;
  taxRelief: number;
  incomeTax: number;
  employeeContribution: number;
  employerContribution: number;
  netIncome: number;
  overtime: PayslipOvertime | null;
  /** ⚠ Deductions can be EMPTY (measured: a run with zero tax emits no rows), and a synthetic
   * "Income Tax - Total" row carries `id: Guid.Empty` — never key rows on `id` alone. */
  earnings: PayslipTransaction[];
  deductions: PayslipTransaction[];
  contributions: PayslipContribution[];
  bankDetails: PayslipBankDetail[];
}

export interface MyPayslipDetail extends MyPayslipSummary {
  payslip: MyPayslipDocument | null;
}

class MePortalService {
  private readonly baseUrl = '/employee-portal';

  /** The personal aggregate behind the portal landing — one read, every figure deep-linkable. */
  getHome(): Promise<PortalHome> {
    return apiService.get<PortalHome>(`${this.baseUrl}/home`);
  }

  // ── Movements (slice 7) — every read token-derived, respond ownership-checked ──

  getMovementsDashboard(): Promise<PortalMovementsDashboard> {
    return apiService.get<PortalMovementsDashboard>(`${this.baseUrl}/dashboard`);
  }

  getMovements(): Promise<StaffMovement[]> {
    return apiService.get<StaffMovement[]>(`${this.baseUrl}/movements`);
  }

  getMovement(id: string): Promise<StaffMovement> {
    return apiService.get<StaffMovement>(`${this.baseUrl}/movements/${id}`);
  }

  /** Refused (409, message shown as-is) unless the movement is awaiting THIS employee's answer. */
  respondToMovement(id: string, accepted: boolean, comments?: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/movements/${id}/respond`, { accepted, comments });
  }

  /** Implemented/approved movements in date order — the career timeline. */
  getCareerPath(): Promise<StaffMovement[]> {
    return apiService.get<StaffMovement[]>(`${this.baseUrl}/career-path`);
  }

  getActingAppointments(): Promise<StaffActingAppointment[]> {
    return apiService.get<StaffActingAppointment[]>(`${this.baseUrl}/acting-appointments`);
  }

  getSecondments(): Promise<StaffMovement[]> {
    return apiService.get<StaffMovement[]>(`${this.baseUrl}/secondments`);
  }

  getMovementNotifications(): Promise<PortalMovementNotification[]> {
    return apiService.get<PortalMovementNotification[]>(`${this.baseUrl}/notifications`);
  }

  // ── Payslips (slice 10) — newest PAY PERIOD first; empty until payroll publishes ──

  getPayslips(): Promise<MyPayslipSummary[]> {
    return apiService.get<MyPayslipSummary[]>(`${this.baseUrl}/payslips`);
  }

  /** Somebody else's snapshot id is a 404 lookup miss — never a 403. */
  getPayslip(id: string): Promise<MyPayslipDetail> {
    return apiService.get<MyPayslipDetail>(`${this.baseUrl}/payslips/${id}`);
  }
}

export const mePortalService = new MePortalService();
