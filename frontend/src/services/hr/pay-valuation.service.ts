import { apiService } from '../api.service';
import type {
  PayToValueItem,
  SeparationSettlement,
  SettlementLine,
  ValueSettlementLine,
} from '@/types/hr/separation';

/**
 * Finance's step: the pay HR records in days, valued (leave settings audit 2, decision P2).
 * Backend route: `api/hr/pay-valuation`, gated on `HR.Pay.Value`.
 *
 * HR records the days on a leaver's settlement (notice paid in lieu, annual leave owed) and on leave
 * cashed in while employed; Finance puts the money on them. Leave cashed in is paid through
 * `leaveEncashmentService.markAsProcessed`, which takes the amount and is gated the same way.
 */
class PayValuationService {
  private readonly baseUrl = '/hr/pay-valuation';

  /** What awaits Finance: statements with pay lines to value, then leave cashed in to pay. */
  getQueue(): Promise<PayToValueItem[]> {
    return apiService.get<PayToValueItem[]>(this.baseUrl);
  }

  /** A leaver's whole statement — the pay lines to value and everything they sit beside. */
  getSettlement(separationId: string): Promise<SeparationSettlement> {
    return apiService.get<SeparationSettlement>(`${this.baseUrl}/settlements/${separationId}`);
  }

  /** Value one pay line: the amount, and where it came from. */
  valueLine(lineId: string, data: ValueSettlementLine): Promise<SettlementLine> {
    return apiService.put<SettlementLine>(`${this.baseUrl}/settlement-lines/${lineId}`, data);
  }
}

export const payValuationService = new PayValuationService();
