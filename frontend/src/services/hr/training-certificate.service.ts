import { apiService } from '../api.service';
import type {
  TrainingCertificate,
  TrainingCertificateSummary,
  IssueCertificateRequest,
  RevokeCertificateRequest,
  CertificateVerificationResult,
  EmployeeCertificate,
  EmployeeCertificateSummary,
  EmployeeCertificateCreate,
  EmployeeCertificateUpdate,
} from '@/types/hr/training-certificates';

/**
 * Certificates we issue off a completed nomination.
 *
 * ⚠ These live on the *completions* controller (`api/training-completions/certificates/...`) because
 * a certificate is an outcome of a completion. `api/training-certificates` holds only the public
 * verification endpoint.
 */
class TrainingCertificateService {
  private readonly baseUrl = '/training-completions/certificates';

  getForEmployee(employeeId: string): Promise<TrainingCertificateSummary[]> {
    return apiService.get<TrainingCertificateSummary[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  /** The caller's own certificates — token-derived. */
  getMine(): Promise<TrainingCertificateSummary[]> {
    return apiService.get<TrainingCertificateSummary[]>(`${this.baseUrl}/mine`);
  }

  getExpiring(daysAhead = 30): Promise<TrainingCertificateSummary[]> {
    return apiService.get<TrainingCertificateSummary[]>(`${this.baseUrl}/expiring`, { daysAhead });
  }

  issue(data: IssueCertificateRequest): Promise<TrainingCertificate> {
    return apiService.post<TrainingCertificate>(this.baseUrl, data);
  }

  revoke(id: string, data: Omit<RevokeCertificateRequest, 'certificateId'>): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/revoke`, { certificateId: id, ...data });
  }

  /**
   * Public verification. Anonymous and rate-limited server-side — the code is the only credential,
   * so this is deliberately the one training endpoint that needs no session.
   */
  verifyByCode(code: string): Promise<CertificateVerificationResult> {
    return apiService.get<CertificateVerificationResult>(
      `/training-certificates/verify/${encodeURIComponent(code)}`,
    );
  }
}

/**
 * Certificates the employee already holds from an outside body — recorded here and verified by HR.
 * We did not issue these, so there is no verification code and no revoke: only verify or correct.
 * Backend route: api/employee-certificates.
 */
class EmployeeCertificateService {
  private readonly baseUrl = '/employee-certificates';

  getById(id: string): Promise<EmployeeCertificate> {
    return apiService.get<EmployeeCertificate>(`${this.baseUrl}/${id}`);
  }

  getForEmployee(employeeId: string): Promise<EmployeeCertificate[]> {
    return apiService.get<EmployeeCertificate[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  /** The caller's own certificates — token-derived. */
  getMine(): Promise<EmployeeCertificate[]> {
    return apiService.get<EmployeeCertificate[]>(`${this.baseUrl}/mine`);
  }

  getUnverified(): Promise<EmployeeCertificateSummary[]> {
    return apiService.get<EmployeeCertificateSummary[]>(`${this.baseUrl}/unverified`);
  }

  getExpiring(daysAhead = 30): Promise<EmployeeCertificateSummary[]> {
    return apiService.get<EmployeeCertificateSummary[]>(`${this.baseUrl}/expiring`, { daysAhead });
  }

  create(data: EmployeeCertificateCreate): Promise<EmployeeCertificate> {
    return apiService.post<EmployeeCertificate>(this.baseUrl, data);
  }

  update(id: string, data: EmployeeCertificateUpdate): Promise<EmployeeCertificate> {
    return apiService.put<EmployeeCertificate>(`${this.baseUrl}/${id}`, { id, ...data });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Returns the verified record with verifiedByName resolved. */
  verify(id: string): Promise<EmployeeCertificate> {
    return apiService.post<EmployeeCertificate>(`${this.baseUrl}/${id}/verify`, { certificateId: id });
  }
}

export const trainingCertificateService = new TrainingCertificateService();
export const employeeCertificateService = new EmployeeCertificateService();
