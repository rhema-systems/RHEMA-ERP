import { apiService } from '../api.service';
import type {
  Bank,
  BankBranch,
  CreateBank,
  CreateBankBranch,
  UpdateBank,
  UpdateBankBranch,
} from '@/types/hr/bank';

/**
 * Banks and bank branches.
 *
 * ⚠ The routes are absolute rather than hung off one base: banks live under `hr/banks` and a branch
 * is addressed at `hr/bank-branches/{id}` once it exists, while it is CREATED under its bank. That
 * asymmetry is the controller's, and hiding it behind a single base constant would only mislead.
 *
 * ⚠ **Retiring is not deleting.** `deactivate` keeps the bank on file for the accounts that already
 * point at it; `remove` is for one entered in error. The screen offers both and says which is which.
 */
class BankService {
  getBanks() {
    return apiService.get<Bank[]>('/hr/banks');
  }

  getActiveBanks() {
    return apiService.get<Bank[]>('/hr/banks/active');
  }

  getBank(id: string) {
    return apiService.get<Bank>(`/hr/banks/${id}`);
  }

  createBank(payload: CreateBank) {
    return apiService.post<Bank>('/hr/banks', payload);
  }

  updateBank(id: string, payload: UpdateBank) {
    return apiService.put<Bank>(`/hr/banks/${id}`, payload);
  }

  activateBank(id: string) {
    return apiService.patch<Bank>(`/hr/banks/${id}/activate`, {});
  }

  deactivateBank(id: string) {
    return apiService.patch<Bank>(`/hr/banks/${id}/deactivate`, {});
  }

  deleteBank(id: string) {
    return apiService.delete<void>(`/hr/banks/${id}`);
  }

  getBranches(bankId: string) {
    return apiService.get<BankBranch[]>(`/hr/banks/${bankId}/branches`);
  }

  /** Only branches an account may still be opened at — what the employee bank tab offers. */
  getActiveBranches(bankId: string) {
    return apiService.get<BankBranch[]>(`/hr/banks/${bankId}/branches/active`);
  }

  createBranch(bankId: string, payload: CreateBankBranch) {
    return apiService.post<BankBranch>(`/hr/banks/${bankId}/branches`, payload);
  }

  updateBranch(id: string, payload: UpdateBankBranch) {
    return apiService.put<BankBranch>(`/hr/bank-branches/${id}`, payload);
  }

  activateBranch(id: string) {
    return apiService.patch<BankBranch>(`/hr/bank-branches/${id}/activate`, {});
  }

  deactivateBranch(id: string) {
    return apiService.patch<BankBranch>(`/hr/bank-branches/${id}/deactivate`, {});
  }

  deleteBranch(id: string) {
    return apiService.delete<void>(`/hr/bank-branches/${id}`);
  }
}

export const bankService = new BankService();
