// Demo feedback round 2, lane B2 — the chart-of-accounts codes HR may name (plan § 6.2).
// Mirrors HrFinanceAccountOption on HrFinanceAccountsController.
//
// ⚠ This is HR's own narrow projection of a Finance account, not Finance's AccountDto. It carries
// what a picker needs — code, number, name, type, active — and deliberately no balance, no posting
// rule and no segment structure. Reach for Finance's own API if you need more; do not widen this.

export interface HrFinanceAccount {
  /** What HR stores on the unit or team. The code is only ever a snapshot for display. */
  id: string;
  accountCode: string;
  accountNumber: string;
  accountName: string;
  accountType: string;
  /**
   * ⚠ HR refuses to newly assign an inactive account, but shows one that is already assigned —
   * so a unit charged to a since-deactivated account still says what it is charged to.
   */
  isActive: boolean;
}
