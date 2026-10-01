import { apiService as api } from './api.service';

export interface SalesCurrencyReferenceDto {
  code: string;
  name: string;
  symbol?: string;
  decimalPlaces: number;
  isBaseCurrency: boolean;
}

export const salesReferenceService = {
  getActiveCurrencies: async (): Promise<SalesCurrencyReferenceDto[]> =>
    (await api.get<SalesCurrencyReferenceDto[]>('/sales/reference/currencies')) ?? [],
};
