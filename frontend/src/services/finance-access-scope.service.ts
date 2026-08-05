import { apiService } from './api.service';
import type {
  FinanceAccessBankAccountOption,
  FinanceAccessScopeGrant,
  FinanceAccessUserOption,
  SaveFinanceAccessScopeGrant,
} from '@/types/finance-access-scope';

const root = '/finance/access-scopes';

/** Thin API client; all validation and enforcement remains authoritative on the Finance API. */
export const financeAccessScopeService = {
  grants: (userId?: string) => apiService.get<FinanceAccessScopeGrant[]>(root, userId ? { userId } : undefined),
  users: () => apiService.get<FinanceAccessUserOption[]>(`${root}/users`),
  bankAccounts: () => apiService.get<FinanceAccessBankAccountOption[]>(`${root}/bank-accounts`),
  save: (id: string | undefined, request: SaveFinanceAccessScopeGrant) => id
    ? apiService.put<FinanceAccessScopeGrant>(`${root}/${id}`, request)
    : apiService.post<FinanceAccessScopeGrant>(root, request),
  deactivate: (grant: FinanceAccessScopeGrant, reason: string) => apiService.post<void>(
    `${root}/${grant.id}/deactivate`,
    { reason, rowVersion: grant.rowVersion },
  ),
};
