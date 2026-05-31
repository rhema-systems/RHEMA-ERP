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

type CurrencyApiDto = Partial<{
  id: string;
  code: string;
  currencyCode: string;
  name: string;
  currencyName: string;
  symbol: string;
  currencySymbol: string;
  decimalPlaces: number;
  exchangeRate: number;
  exchangeRateDate: string;
  isBaseCurrency: boolean;
  isActive: boolean;
  displayOrder: number;
  formatString: string;
  country: string;
  countryName: string;
  createdAt: string;
  createdBy: string;
  modifiedAt: string;
  modifiedBy: string;
  updatedAt: string;
  updatedBy: string;
}>;

function normalizeCurrencyDto(currency: CurrencyApiDto): CurrencyDetailDto {
  return {
    id: currency.id ?? '',
    code: currency.code ?? currency.currencyCode ?? '',
    name: currency.name ?? currency.currencyName ?? '',
    symbol: currency.symbol ?? currency.currencySymbol ?? '',
    decimalPlaces: typeof currency.decimalPlaces === 'number' ? currency.decimalPlaces : 2,
    exchangeRate: typeof currency.exchangeRate === 'number' ? currency.exchangeRate : 1,
    exchangeRateDate: currency.exchangeRateDate,
    isBaseCurrency: currency.isBaseCurrency ?? false,
    isActive: currency.isActive ?? true,
    displayOrder: typeof currency.displayOrder === 'number' ? currency.displayOrder : 0,
    formatString: currency.formatString,
    country: currency.country ?? currency.countryName,
    createdAt: currency.createdAt ?? '',
    createdBy: currency.createdBy,
    modifiedAt: currency.modifiedAt ?? currency.updatedAt,
    modifiedBy: currency.modifiedBy ?? currency.updatedBy,
  };
}

function normalizeCurrencyList(currencies: CurrencyApiDto[]): CurrencyListDto[] {
  return currencies.map(normalizeCurrencyDto);
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
    const res = await api.get<any[]>('/finance/Currencies');
    return res.map(c => ({
      ...c,
      code: c.code || c.currencyCode,
      name: c.name || c.currencyName,
      symbol: c.symbol || c.currencySymbol
    })) as CurrencyListDto[];
  },

  // Get active currencies
  getActive: async (): Promise<CurrencyListDto[]> => {
    const res = await api.get<any[]>('/finance/Currencies/active');
    return res.map(c => ({
      ...c,
      code: c.code || c.currencyCode,
      name: c.name || c.currencyName,
      symbol: c.symbol || c.currencySymbol
    })) as CurrencyListDto[];
  },

  // Get currency by ID
  getById: async (id: string): Promise<CurrencyDetailDto> => {
    const c = await api.get<any>(`/finance/Currencies/${id}`);
    return {
      ...c,
      code: c.code || c.currencyCode,
      name: c.name || c.currencyName,
      symbol: c.symbol || c.currencySymbol
    } as CurrencyDetailDto;
  },

  // Get currency by code
  getByCode: async (code: string): Promise<CurrencyDetailDto> => {
    const c = await api.get<any>(`/finance/Currencies/code/${code}`);
    return {
      ...c,
      code: c.code || c.currencyCode,
      name: c.name || c.currencyName,
      symbol: c.symbol || c.currencySymbol
    } as CurrencyDetailDto;
  },

  // Get base currency
  getBaseCurrency: async (): Promise<CurrencyDetailDto | null> => {
    try {
      const response = await api.get<CurrencyApiDto>('/finance/Currencies/base');
      return normalizeCurrencyDto(response ?? {});
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
    const response = await api.post<CurrencyApiDto>('/finance/Currencies', apiData);
    return normalizeCurrencyDto(response ?? {});
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
    const response = await api.put<CurrencyApiDto>(`/finance/Currencies/${id}`, apiData);
    return normalizeCurrencyDto(response ?? {});
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
