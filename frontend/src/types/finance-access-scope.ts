export type FinanceAccessScopeType = 'Tenant' | 'BankAccount';
export type FinanceAccessLevel = 'Read' | 'Operate' | 'Approve' | 'Administer';

export interface FinanceAccessScopeGrant {
  id: string;
  userId: string;
  username: string;
  userDisplayName: string;
  scopeType: FinanceAccessScopeType;
  scopeValue?: string;
  scopeDisplayName?: string;
  accessLevel: FinanceAccessLevel;
  effectiveFrom: string;
  effectiveTo?: string;
  isActive: boolean;
  reason: string;
  rowVersion: string;
}

export interface SaveFinanceAccessScopeGrant {
  userId: string;
  scopeType: FinanceAccessScopeType;
  scopeValue?: string;
  accessLevel: FinanceAccessLevel;
  effectiveFrom: string;
  effectiveTo?: string;
  isActive: boolean;
  reason: string;
  rowVersion?: string;
}

export interface FinanceAccessUserOption {
  userId: string;
  username: string;
  displayName: string;
}

export interface FinanceAccessBankAccountOption {
  bankAccountId: string;
  accountNumber: string;
  accountName: string;
  bankName: string;
  currency: string;
  isActive: boolean;
}
