import { apiService as api } from './api.service';

// Payment Term Types
export interface PaymentTermListDto {
  id: string;
  code: string;
  name: string;
  description?: string;
  dueDays: number;
  discountPercent?: number;
  discountDays?: number;
  isActive: boolean;
  isDefault: boolean;
  displayOrder: number;
  applicableTo?: string;
}

export interface PaymentTermDetailDto extends PaymentTermListDto {
  description?: string;
  createdAt: string;
  createdBy?: string;
  modifiedAt?: string;
  modifiedBy?: string;
}

export interface CreatePaymentTermDto {
  code: string;
  name: string;
  description?: string;
  dueDays: number;
  discountPercent?: number;
  discountDays?: number;
  isActive?: boolean;
  isDefault?: boolean;
  displayOrder?: number;
  applicableTo?: string;
}

export interface UpdatePaymentTermDto extends CreatePaymentTermDto {
  id: string;
}

// Currency Types
export interface CurrencyListDto {
  id: string;
  code: string;
  name: string;
  symbol: string;
  decimalPlaces: number;
  exchangeRate: number;
  exchangeRateDate?: string;
  isBaseCurrency: boolean;
  isActive: boolean;
  displayOrder: number;
  formatString?: string;
  country?: string;
}

export interface CurrencyDetailDto extends CurrencyListDto {
  exchangeRateDate?: string;
  formatString?: string;
  createdAt: string;
  createdBy?: string;
  modifiedAt?: string;
  modifiedBy?: string;
}

export interface CreateCurrencyDto {
  code: string;
  name: string;
  symbol: string;
  decimalPlaces?: number;
  exchangeRate?: number;
  exchangeRateDate?: string | Date;
  isBaseCurrency?: boolean;
  isActive?: boolean;
  displayOrder?: number;
  formatString?: string;
  country?: string;
}

export interface UpdateCurrencyDto extends CreateCurrencyDto {
  id: string;
}

// Payment Term Service
export const paymentTermService = {
  // Get all payment terms
  getAll: async (): Promise<PaymentTermListDto[]> => {
    return api.get<PaymentTermListDto[]>('/finance/PaymentTerms');
  },

  // Get active payment terms
  getActive: async (): Promise<PaymentTermListDto[]> => {
    return api.get<PaymentTermListDto[]>('/finance/PaymentTerms/active');
  },

  // Get payment term by ID
  getById: async (id: string): Promise<PaymentTermDetailDto> => {
    return api.get<PaymentTermDetailDto>(`/finance/PaymentTerms/${id}`);
  },

  // Get payment term by code
  getByCode: async (code: string): Promise<PaymentTermDetailDto> => {
    return api.get<PaymentTermDetailDto>(`/finance/PaymentTerms/code/${code}`);
  },

  // Get default payment term
  getDefault: async (): Promise<PaymentTermDetailDto | null> => {
    try {
      return await api.get<PaymentTermDetailDto>('/finance/PaymentTerms/default');
    } catch {
      return null;
    }
  },

  // Get payment terms by applicable type
  getByApplicableTo: async (applicableTo: string): Promise<PaymentTermListDto[]> => {
    return api.get<PaymentTermListDto[]>(`/finance/PaymentTerms/applicable/${applicableTo}`);
  },

  // Create payment term
  create: async (data: CreatePaymentTermDto): Promise<PaymentTermDetailDto> => {
    return api.post<PaymentTermDetailDto>('/finance/PaymentTerms', data);
  },

  // Update payment term
  update: async (id: string, data: UpdatePaymentTermDto): Promise<PaymentTermDetailDto> => {
    return api.put<PaymentTermDetailDto>(`/finance/PaymentTerms/${id}`, data);
  },

  // Delete payment term
  delete: async (id: string): Promise<void> => {
    await api.delete(`/finance/PaymentTerms/${id}`);
  },

  // Set as default
  setAsDefault: async (id: string): Promise<void> => {
    await api.post(`/finance/PaymentTerms/${id}/set-default`);
  },

  // Toggle active status
  toggleActive: async (id: string): Promise<void> => {
    await api.post(`/finance/PaymentTerms/${id}/toggle-active`);
  },
};

// Currency Service
export const currencyService = {
  // Get all currencies
  getAll: async (): Promise<CurrencyListDto[]> => {
    return api.get<CurrencyListDto[]>('/finance/Currencies');
  },

  // Get active currencies
  getActive: async (): Promise<CurrencyListDto[]> => {
    return api.get<CurrencyListDto[]>('/finance/Currencies/active');
  },

  // Get currency by ID
  getById: async (id: string): Promise<CurrencyDetailDto> => {
    return api.get<CurrencyDetailDto>(`/finance/Currencies/${id}`);
  },

  // Get currency by code
  getByCode: async (code: string): Promise<CurrencyDetailDto> => {
    return api.get<CurrencyDetailDto>(`/finance/Currencies/code/${code}`);
  },

  // Get base currency
  getBaseCurrency: async (): Promise<CurrencyDetailDto | null> => {
    try {
      return await api.get<CurrencyDetailDto>('/finance/Currencies/base');
    } catch {
      return null;
    }
  },

  // Create currency
  create: async (data: CreateCurrencyDto): Promise<CurrencyDetailDto> => {
    // Convert Date object to ISO string for API
    const apiData = {
      ...data,
      exchangeRateDate: data.exchangeRateDate instanceof Date
        ? data.exchangeRateDate.toISOString()
        : data.exchangeRateDate
    };
    return api.post<CurrencyDetailDto>('/finance/Currencies', apiData);
  },

  // Update currency
  update: async (id: string, data: UpdateCurrencyDto): Promise<CurrencyDetailDto> => {
    // Convert Date object to ISO string for API
    const apiData = {
      ...data,
      exchangeRateDate: data.exchangeRateDate instanceof Date
        ? data.exchangeRateDate.toISOString()
        : data.exchangeRateDate
    };
    return api.put<CurrencyDetailDto>(`/finance/Currencies/${id}`, apiData);
  },

  // Delete currency
  delete: async (id: string): Promise<void> => {
    await api.delete(`/finance/Currencies/${id}`);
  },

  // Update exchange rate
  updateExchangeRate: async (id: string, exchangeRate: number): Promise<void> => {
    await api.post(`/finance/Currencies/${id}/exchange-rate`, { exchangeRate });
  },

  // Set as base currency
  setAsBaseCurrency: async (id: string): Promise<void> => {
    await api.post(`/finance/Currencies/${id}/set-base`);
  },

  // Toggle active status
  toggleActive: async (id: string): Promise<void> => {
    await api.post(`/finance/Currencies/${id}/toggle-active`);
  },
};

const financeCommonService = {
  paymentTerms: paymentTermService,
  currencies: currencyService,
};

export default financeCommonService;
