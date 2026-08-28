import { compatibleApiService as apiService } from './compatibleApiService';

interface ApiResponse<T> {
  success?: boolean;
  data?: T;
  message?: string;
}

export interface GroundRentAssetOption {
  id: string;
  assetCode: string;
  name: string;
  location?: string;
  areaAcres?: number;
  customerBusinessPartnerId?: string;
  customerName?: string;
  approvedAnnualGroundRent?: number;
  approvedRatePerAcre?: number;
  currencyCode: string;
  hasGroundRentAccount: boolean;
}

export interface GroundRentIncomeAccountOption {
  id: string;
  accountNumber: string;
  accountName: string;
  currencyCode: string;
}

export interface GroundRentOptions {
  assets: GroundRentAssetOption[];
  incomeAccounts: GroundRentIncomeAccountOption[];
}

export interface GroundRentCharge {
  id: string;
  periodStart: string;
  periodEnd: string;
  dueDate: string;
  baseAmount: number;
  penaltyAmount: number;
  paidAmount: number;
  outstandingAmount: number;
  status: string;
  financeInvoiceId?: string;
  financeInvoiceNumber?: string;
  financeJournalEntryId?: string;
  penaltyInvoiceId?: string;
  penaltyInvoiceNumber?: string;
  penaltyJournalEntryId?: string;
}

export interface GroundRentReview {
  id: string;
  effectiveDate: string;
  previousAnnualAmount: number;
  newAnnualAmount: number;
  previousRatePerAcre?: number;
  newRatePerAcre?: number;
  escalationMethod: string;
  escalationValue: number;
  notes?: string;
  createdAt: string;
}

export interface GroundRentAccount {
  id: string;
  estateManagedAssetId: string;
  assetCode: string;
  assetName: string;
  location?: string;
  customerBusinessPartnerId: string;
  customerName: string;
  paymentFrequency: string;
  calculationMethod: string;
  annualAmount: number;
  amountPerPeriod: number;
  ratePerAcre?: number;
  currencyCode: string;
  billingStartDate?: string;
  billingStartSource: string;
  canGenerateInvoice: boolean;
  invoiceHoldReason?: string;
  nextDueDate: string;
  paymentTermsDays: number;
  reviewFrequencyMonths: number;
  nextReviewDate?: string;
  escalationMethod: string;
  escalationValue: number;
  gracePeriodDays: number;
  penaltyMethod: string;
  penaltyValue: number;
  penaltyCapAmount?: number;
  groundRentIncomeAccountId: string;
  groundRentIncomeAccount: string;
  autoPostInvoices: boolean;
  status: string;
  notes?: string;
  outstandingAmount: number;
  arrearsAmount: number;
  overdueChargeCount: number;
  charges: GroundRentCharge[];
  reviews: GroundRentReview[];
}

export interface UpsertGroundRentAccount {
  estateManagedAssetId: string;
  customerBusinessPartnerId: string;
  paymentFrequency: string;
  calculationMethod: string;
  annualAmount?: number | null;
  ratePerAcre?: number | null;
  currencyCode: string;
  nextDueDate: string;
  paymentTermsDays: number;
  reviewFrequencyMonths: number;
  nextReviewDate?: string | null;
  escalationMethod: string;
  escalationValue: number;
  gracePeriodDays: number;
  penaltyMethod: string;
  penaltyValue: number;
  penaltyCapAmount?: number | null;
  groundRentIncomeAccountId: string;
  autoPostInvoices: boolean;
  status: string;
  notes?: string | null;
}

export interface RecordGroundRentReceipt {
  target: 'Base' | 'Penalty';
  paymentDate: string;
  amount: number;
  paymentMethod: string;
  paymentMethodId?: string | null;
  bankAccountId?: string | null;
  checkNumber?: string | null;
  transactionReference?: string | null;
  currencyCode: string;
  exchangeRate: number;
  notes?: string | null;
}

interface GroundRentActionResult {
  message: string;
  account: GroundRentAccount;
}

function unwrap<T>(response: ApiResponse<T> | T, fallback: string): T {
  if (
    response !== null &&
    typeof response === 'object' &&
    'data' in response
  ) {
    if (response.data === undefined) {
      throw new Error(response.message || fallback);
    }

    return response.data;
  }

  if (response === undefined || response === null) {
    throw new Error(fallback);
  }

  return response as T;
}

class EstateGroundRentService {
  async getOptions(): Promise<GroundRentOptions> {
    const response = await apiService.get<ApiResponse<GroundRentOptions>>(
      '/estate/ground-rent/options'
    );
    return unwrap(response, 'Unable to load ground-rent setup options.');
  }

  async getAccounts(): Promise<GroundRentAccount[]> {
    const response = await apiService.get<ApiResponse<GroundRentAccount[]>>(
      '/estate/ground-rent/accounts'
    );
    return unwrap(response, 'Unable to load ground-rent accounts.');
  }

  async saveAccount(
    payload: UpsertGroundRentAccount
  ): Promise<GroundRentAccount> {
    const response = await apiService.post<ApiResponse<GroundRentAccount>>(
      '/estate/ground-rent/accounts',
      payload
    );
    return unwrap(response, 'Unable to save the ground-rent account.');
  }

  async generateInvoice(
    accountId: string,
    allowFutureDueDate = false
  ): Promise<GroundRentActionResult> {
    const response = await apiService.post<ApiResponse<GroundRentActionResult>>(
      `/estate/ground-rent/accounts/${accountId}/invoices`,
      { allowFutureDueDate }
    );
    return unwrap(response, 'Unable to generate the ground-rent invoice.');
  }

  async applyReview(
    accountId: string,
    payload: {
      effectiveDate: string;
      escalationMethod?: string;
      escalationValue?: number;
      notes?: string;
    }
  ): Promise<GroundRentActionResult> {
    const response = await apiService.post<ApiResponse<GroundRentActionResult>>(
      `/estate/ground-rent/accounts/${accountId}/reviews`,
      payload
    );
    return unwrap(response, 'Unable to apply the ground-rent review.');
  }

  async assessPenalty(chargeId: string): Promise<GroundRentActionResult> {
    const response = await apiService.post<ApiResponse<GroundRentActionResult>>(
      `/estate/ground-rent/charges/${chargeId}/penalties`
    );
    return unwrap(response, 'Unable to assess the arrears penalty.');
  }

  async postInvoice(
    chargeId: string,
    target: 'Base' | 'Penalty'
  ): Promise<GroundRentActionResult> {
    const response = await apiService.post<ApiResponse<GroundRentActionResult>>(
      `/estate/ground-rent/charges/${chargeId}/post?target=${target}`
    );
    return unwrap(response, 'Unable to post the Finance AR invoice.');
  }

  async recordReceipt(
    chargeId: string,
    payload: RecordGroundRentReceipt
  ): Promise<GroundRentActionResult> {
    const response = await apiService.post<ApiResponse<GroundRentActionResult>>(
      `/estate/ground-rent/charges/${chargeId}/receipts`,
      payload
    );
    return unwrap(response, 'Unable to record the Finance AR receipt.');
  }
}

export const estateGroundRentService = new EstateGroundRentService();
