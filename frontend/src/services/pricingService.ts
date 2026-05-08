import { apiService } from './api.service';

export interface PriceListLookupResult {
  priceListLineId: string;
  priceListId: string;
  priceListCode: string;
  priceListName: string;
  basePrice: number;
  netPrice: number;
  unitOfMeasure: string;
  minQuantity: number;
  maxQuantity?: number;
  effectiveFrom: string;
  effectiveTo?: string;
  currency: string;
  discountPercent?: number;
  lastPriceUpdate?: string;
  finalPrice: number;
}

export interface PriceHistoryResult {
  date: string;
  unitPrice: number;
  unitOfMeasure: string;
  priceListName: string;
  currency: string;
}

class PricingService {
  /**
   * Get the best price for an item from a supplier's price lists
   */
  async getSupplierItemPrice(
    supplierId: string,
    itemId: string,
    quantity: number = 1
  ): Promise<PriceListLookupResult | null> {
    try {
      const response = await apiService.get<PriceListLookupResult>(
        `/pricing/supplier/${supplierId}/item/${itemId}/price?quantity=${quantity}`
      );
      return response;
    } catch (error: any) {
      if (error.response?.status === 404) {
        return null; // No price found is not an error
      }
      console.error('Error getting supplier item price:', error);
      throw error;
    }
  }

  /**
   * Get all available prices for an item from a supplier (all quantity tiers)
   */
  async getAllSupplierItemPrices(
    supplierId: string,
    itemId: string
  ): Promise<PriceListLookupResult[]> {
    try {
      const response = await apiService.get<PriceListLookupResult[]>(
        `/pricing/supplier/${supplierId}/item/${itemId}/prices`
      );
      return response;
    } catch (error) {
      console.error('Error getting all supplier item prices:', error);
      throw error;
    }
  }

  /**
   * Get price history for an item from a supplier
   */
  async getPriceHistory(
    supplierId: string,
    itemId: string,
    months: number = 12
  ): Promise<PriceHistoryResult[]> {
    try {
      const response = await apiService.get<PriceHistoryResult[]>(
        `/pricing/supplier/${supplierId}/item/${itemId}/history?months=${months}`
      );
      return response;
    } catch (error) {
      console.error('Error getting price history:', error);
      throw error;
    }
  }
}

const pricingService = new PricingService();

export default pricingService;
