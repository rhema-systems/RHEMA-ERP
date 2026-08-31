/**
 * Banks and their branches — the reference data an employee's bank account points at.
 *
 * ⚠ Ten write endpoints and, until now, **no screen anywhere**: a bank could not be added, renamed,
 * retired or deleted from the product at all. `hr/banks` is in the ledger's confirmed-unreachable
 * list (both instruments agreed), and the branch half was never even flagged because nothing
 * referenced it.
 */

export interface Bank {
  id: string;
  name: string;
  code: string;
  swiftCode?: string | null;
  countryId?: string | null;
  countryName?: string | null;
  isActive: boolean;
  /**
   * ⚠ Hardcoded to zero on four of the five reads until 2026-08-31 — the list, the active list,
   * by-id and by-code all answered "0 branches" however many a bank had.
   */
  branchCount: number;
}

export interface BankBranch {
  id: string;
  bankId: string;
  bankName: string;
  bankCode: string;
  name: string;
  code?: string | null;
  address?: string | null;
  city?: string | null;
  countryId?: string | null;
  countryName?: string | null;
  phoneNumber?: string | null;
  email?: string | null;
  isActive: boolean;
}

export interface CreateBank {
  name: string;
  code: string;
  swiftCode?: string | null;
  countryId?: string | null;
  isActive: boolean;
}

/**
 * ⚠ A partial update: every field but `id` is nullable server-side, and omitting one leaves it
 * alone rather than nulling it. Proven by `probe-lane2-groupA.mjs` before the form was written,
 * because it decides whether the form may send only what changed.
 */
export interface UpdateBank {
  id: string;
  name?: string | null;
  code?: string | null;
  swiftCode?: string | null;
  countryId?: string | null;
  isActive?: boolean | null;
}

export interface CreateBankBranch {
  bankId: string;
  name: string;
  code?: string | null;
  address?: string | null;
  city?: string | null;
  countryId?: string | null;
  phoneNumber?: string | null;
  email?: string | null;
  isActive: boolean;
}

export interface UpdateBankBranch {
  id: string;
  name?: string | null;
  code?: string | null;
  address?: string | null;
  city?: string | null;
  countryId?: string | null;
  phoneNumber?: string | null;
  email?: string | null;
  isActive?: boolean | null;
}
