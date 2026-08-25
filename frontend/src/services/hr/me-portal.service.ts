import { apiService } from '../api.service';

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

class MePortalService {
  private readonly baseUrl = '/employee-portal';

  /** The personal aggregate behind the portal landing — one read, every figure deep-linkable. */
  getHome(): Promise<PortalHome> {
    return apiService.get<PortalHome>(`${this.baseUrl}/home`);
  }
}

export const mePortalService = new MePortalService();
