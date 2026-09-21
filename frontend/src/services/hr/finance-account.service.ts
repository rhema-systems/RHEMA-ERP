import { apiService } from '../api.service';
import type { HrFinanceAccount } from '@/types/hr/finance-account';

/**
 * The chart-of-accounts codes HR may name on a unit or a team (round 2, lane B2, plan § 6.2).
 *
 * ⚠ `api/hr/finance-accounts`, NOT `api/finance/accounts`. The Finance endpoint is gated on a
 * Finance permission by a convention map, so an HR user gets a 403 and any picker fed from it
 * renders empty for exactly the people who use it. HR has its own narrow read for the same reason
 * it has its own currency read.
 *
 * Read-only by design. Creating, amending or deactivating an account is Finance's, and there is
 * nothing here that pretends otherwise.
 */
class FinanceAccountService {
  private readonly base = '/hr/finance-accounts';

  /**
   * Accounts a picker can offer. The search runs server-side and the list is capped, because a
   * chart of accounts is long and a picker is not a report.
   */
  search(params: { search?: string; take?: number; includeInactive?: boolean } = {}): Promise<HrFinanceAccount[]> {
    return apiService.get<HrFinanceAccount[]>(this.base, {
      ...(params.search ? { search: params.search } : {}),
      ...(params.take ? { take: params.take } : {}),
      ...(params.includeInactive ? { includeInactive: true } : {}),
    });
  }

  /**
   * One account by id — what a picker needs to name the account a unit is ALREADY charged to,
   * including one Finance has since deactivated.
   */
  getById(id: string): Promise<HrFinanceAccount> {
    return apiService.get<HrFinanceAccount>(`${this.base}/${id}`);
  }
}

export const financeAccountService = new FinanceAccountService();
