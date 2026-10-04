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
  numericCode?: string;
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
  numericCode?: string;
  name: string;
  symbol: string;
  decimalPlaces?: number;
  exchangeRate?: number;
  exchangeRateDate?: string | Date;
  createInitialExchangeRate?: boolean;
  initialExchangeRate?: number;
  initialExchangeRateDate?: string | Date;
  initialExchangeRateType?: string;
  initialExchangeRateSource?: string;
  initialExchangeRateSourceReference?: string;
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
  numericCode: string;
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

const ISO_4217_NUMERIC_CODES: Record<string, string> = {
  AED: '784',
  ARS: '032',
  AUD: '036',
  BDT: '050',
  BHD: '048',
  BRL: '986',
  CAD: '124',
  CHF: '756',
  CLP: '152',
  CNY: '156',
  COP: '170',
  CZK: '203',
  DKK: '208',
  EGP: '818',
  ETB: '230',
  EUR: '978',
  GBP: '826',
  GHS: '936',
  HKD: '344',
  HUF: '348',
  IDR: '360',
  ILS: '376',
  INR: '356',
  JPY: '392',
  KES: '404',
  KRW: '410',
  KWD: '414',
  LKR: '144',
  MAD: '504',
  MXN: '484',
  MYR: '458',
  NGN: '566',
  NOK: '578',
  NZD: '554',
  OMR: '512',
  PEN: '604',
  PHP: '608',
  PKR: '586',
  PLN: '985',
  QAR: '634',
  RON: '946',
  RUB: '643',
  RWF: '646',
  SAR: '682',
  SEK: '752',
  SGD: '702',
  THB: '764',
  TRY: '949',
  TZS: '834',
  UGX: '800',
  USD: '840',
  VND: '704',
  XAF: '950',
  XOF: '952',
  ZAR: '710',
};

function normalizeCurrencyDto(currency: CurrencyApiDto): CurrencyDetailDto {
  return {
    id: currency.id ?? '',
    code: currency.code ?? currency.currencyCode ?? '',
    numericCode: currency.numericCode,
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

function toDateOnly(value?: string | Date): string | undefined {
  if (!value) return undefined;

  if (value instanceof Date) {
    return value.toISOString().split('T')[0];
  }

  return value.split('T')[0];
}

function getNumericCode(currencyCode: string, provided?: string): string {
  const normalized = currencyCode.trim().toUpperCase();
  const numericCode = provided?.trim() || ISO_4217_NUMERIC_CODES[normalized];

  if (!numericCode) {
    throw new Error(`Missing ISO 4217 numeric code for ${normalized}.`);
  }

  return numericCode;
}

function buildCreateCurrencyPayload(data: CreateCurrencyDto) {
  const currencyCode = data.code.trim().toUpperCase();
  const createInitialExchangeRate = data.createInitialExchangeRate === true && data.isBaseCurrency !== true;
  const initialExchangeRate = data.initialExchangeRate ?? data.exchangeRate;
  const initialExchangeRateDate = data.initialExchangeRateDate ?? data.exchangeRateDate ?? new Date();

  return {
    currencyCode,
    numericCode: getNumericCode(currencyCode, data.numericCode),
    currencyName: data.name.trim(),
    currencySymbol: data.symbol?.trim() || undefined,
    decimalPlaces: data.decimalPlaces ?? 2,
    roundingMethod: 'Standard',
    roundingPrecision: 0.01,
    symbolPosition: 'Before',
    decimalSeparator: '.',
    thousandsSeparator: ',',
    digitGrouping: 3,
    isBaseCurrency: data.isBaseCurrency ?? false,
    isActive: data.isActive ?? true,
    countryName: data.country?.trim() || undefined,
    createInitialExchangeRate,
    // Keep initial FX on the backend ExchangeRate model, not the Currency master record.
    initialExchangeRate: createInitialExchangeRate ? initialExchangeRate : undefined,
    initialExchangeRateDate: createInitialExchangeRate ? toDateOnly(initialExchangeRateDate) : undefined,
    initialExchangeRateType: createInitialExchangeRate ? (data.initialExchangeRateType || 'Daily') : undefined,
    initialExchangeRateSource: createInitialExchangeRate
      ? data.initialExchangeRateSource?.trim() || 'Manual Entry'
      : undefined,
    initialExchangeRateSourceReference: createInitialExchangeRate
      ? data.initialExchangeRateSourceReference?.trim() || undefined
      : undefined,
  };
}

function buildUpdateCurrencyPayload(data: UpdateCurrencyDto) {
  return {
    currencyName: data.name.trim(),
    currencySymbol: data.symbol?.trim() || undefined,
    roundingMethod: 'Standard',
    roundingPrecision: 0.01,
    symbolPosition: 'Before',
    thousandsSeparator: ',',
    digitGrouping: 3,
    currencyClassification: 'Regional',
    geographicRegion: data.country?.trim() || undefined,
    countryName: data.country?.trim() || undefined,
    autoRetrieveExchangeRate: false,
    exchangeRateUpdateFrequency: 'Daily',
    isActive: data.isActive ?? true,
  };
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
    return api.get<PaymentTermListDto[]>(`/finance/PaymentTerms/by-type/${applicableTo}`);
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
    const res = await api.get<CurrencyApiDto[]>('/finance/Currencies');
    return normalizeCurrencyList(res ?? []);
  },

  // Get active currencies
  getActive: async (): Promise<CurrencyListDto[]> => {
    const res = await api.get<CurrencyApiDto[]>('/finance/Currencies/active');
    return normalizeCurrencyList(res ?? []);
  },

  // Get currency by ID
  getById: async (id: string): Promise<CurrencyDetailDto> => {
    const response = await api.get<CurrencyApiDto>(`/finance/Currencies/${id}`);
    return normalizeCurrencyDto(response ?? {});
  },

  // Get currency by code
  getByCode: async (code: string): Promise<CurrencyDetailDto> => {
    const response = await api.get<CurrencyApiDto>(`/finance/Currencies/code/${code}`);
    return normalizeCurrencyDto(response ?? {});
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
    const response = await api.post<CurrencyApiDto>('/finance/Currencies', buildCreateCurrencyPayload(data));
    return normalizeCurrencyDto(response ?? {});
  },

  // Update currency
  update: async (id: string, data: UpdateCurrencyDto): Promise<CurrencyDetailDto> => {
    const response = await api.put<CurrencyApiDto>(`/finance/Currencies/${id}`, buildUpdateCurrencyPayload(data));
    return normalizeCurrencyDto(response ?? {});
  },

  // Delete currency
  delete: async (id: string): Promise<void> => {
    await api.delete(`/finance/Currencies/${id}`);
  },

  // Update exchange rate
  updateExchangeRate: async (id: string, exchangeRate: number): Promise<void> => {
    await api.put(`/finance/Currencies/${id}/exchange-rate`, { rate: exchangeRate });
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

// Procurement consumes the Finance-owned currency master through a narrowly
// scoped Procurement projection. This avoids requiring operational Procurement
// users to hold the unrelated finance.view permission.
export const procurementCurrencyService = {
  getActive: async (): Promise<CurrencyListDto[]> => {
    const response = await api.get<CurrencyApiDto[]>('/procurement/reference-data/currencies');
    return normalizeCurrencyList(response ?? []);
  },
};

export const estateCurrencyService = {
  getActive: async (): Promise<CurrencyListDto[]> => {
    const response = await api.get<CurrencyApiDto[]>('/estate/reference-data/currencies');
    return normalizeCurrencyList(response ?? []);
  },
};

const financeCommonService = {
  paymentTerms: paymentTermService,
  currencies: currencyService,
  estateCurrencies: estateCurrencyService,
};

export default financeCommonService;
