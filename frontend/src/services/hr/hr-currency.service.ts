import { apiService } from '../api.service';

/**
 * The currencies an HR screen may offer.
 *
 * ⚠ **Not `api/finance/currencies`.** That endpoint is mapped to `FinancePermissions.ViewFinance`
 * by a convention map — nothing on the controller shows it — so an HR user gets a **403** and every
 * picker fed from it renders EMPTY for exactly the people who use it. Measured 2026-09-01 on the
 * guarantor surety picker, the position's guarantor requirement, and area 13's development-cost
 * panel, which had been broken since it shipped.
 *
 * Finance still owns the master: this is read-only and there is no way to add a currency from HR.
 */
export interface HrCurrencyOption {
  /** ⚠ `code`/`name`, NOT Finance's `currencyCode`/`currencyName`. */
  code: string;
  name: string;
  symbol?: string | null;
}

class HrCurrencyService {
  getActive() {
    return apiService.get<HrCurrencyOption[]>('/hr/currencies');
  }
}

export const hrCurrencyService = new HrCurrencyService();
