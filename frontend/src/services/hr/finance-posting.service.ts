import { apiService } from '../api.service';
import type {
  HrFinanceAccountRole,
  HrFinancePostingRecord,
  HrFinancePostingRecordQuery,
  HrFinancePostingSettings,
  HrFinancePostingSummary,
  PagedHrFinancePostingRecords,
  UpsertHrFinanceAccountMapping,
  UpsertHrFinancePostingRule,
} from '@/types/hr/finance-posting';

/**
 * HR → Finance posting: settings (account roles, event rules) and the posting register
 * (HR finish plan lane 8). Backed by `HrFinancePostingController` at `api/hr/finance-posting`.
 *
 * Reads are HR.Company.Read (the HR role has it); every write — mapping an account, enabling an
 * event, retrying, reversing — is HR.Company.Admin and 403s for the HR desk by design. A reversal
 * creates a Finance journal; it is administration's action, not a desk's.
 */
class FinancePostingService {
  private readonly base = '/hr/finance-posting';

  getSettings(): Promise<HrFinancePostingSettings> {
    return apiService.get<HrFinancePostingSettings>(`${this.base}/settings`);
  }

  upsertMapping(payload: UpsertHrFinanceAccountMapping): Promise<HrFinancePostingSettings> {
    return apiService.put<HrFinancePostingSettings>(`${this.base}/mappings`, payload);
  }

  clearMapping(role: HrFinanceAccountRole): Promise<HrFinancePostingSettings> {
    return apiService.delete<HrFinancePostingSettings>(`${this.base}/mappings/${role}`);
  }

  upsertRule(payload: UpsertHrFinancePostingRule): Promise<HrFinancePostingSettings> {
    return apiService.put<HrFinancePostingSettings>(`${this.base}/rules`, payload);
  }

  getSummary(): Promise<HrFinancePostingSummary> {
    return apiService.get<HrFinancePostingSummary>(`${this.base}/records/summary`);
  }

  getRecords(query: HrFinancePostingRecordQuery = {}): Promise<PagedHrFinancePostingRecords> {
    const params: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(query)) {
      if (v !== undefined && v !== null && v !== '') params[k] = v;
    }
    return apiService.get<PagedHrFinancePostingRecords>(`${this.base}/records`, params);
  }

  getRecord(id: string): Promise<HrFinancePostingRecord> {
    return apiService.get<HrFinancePostingRecord>(`${this.base}/records/${id}`);
  }

  /** Every posting row for one claim or advance — what the detail card shows. */
  getRecordsForSource(sourceDocumentId: string): Promise<HrFinancePostingRecord[]> {
    return apiService.get<HrFinancePostingRecord[]>(`${this.base}/records/source/${sourceDocumentId}`);
  }

  retry(id: string): Promise<HrFinancePostingRecord> {
    return apiService.post<HrFinancePostingRecord>(`${this.base}/records/${id}/retry`, {});
  }

  reverse(id: string, reason: string): Promise<HrFinancePostingRecord> {
    return apiService.post<HrFinancePostingRecord>(`${this.base}/records/${id}/reverse`, { reason });
  }

  /** AP hand-off rows: pull Finance's invoice status, and the payment voucher once paid. HR.Company.Read. */
  refresh(id: string): Promise<HrFinancePostingRecord> {
    return apiService.post<HrFinancePostingRecord>(`${this.base}/records/${id}/refresh`, {});
  }
}

export const financePostingService = new FinancePostingService();
