import { describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import { salesOrderInvoiceService as service } from './salesOrderInvoiceService';
vi.mock('./api.service', () => ({ apiService: { get: vi.fn(), post: vi.fn() } }));
describe('Sales invoice routes', () => {
  it('keeps invoice operations and read-only distribution under the Sales order owner', async () => {
    await service.get('source'); await service.distribution('source'); await service.submit('source'); await service.post('source');
    expect(apiService.get).toHaveBeenCalledWith('/sales/orders/source/invoice');
    expect(apiService.get).toHaveBeenCalledWith('/sales/orders/source/invoice/distribution');
    expect(apiService.post).toHaveBeenCalledWith('/sales/orders/source/invoice/submit', {});
    expect(apiService.post).toHaveBeenCalledWith('/sales/orders/source/invoice/post', {});
  });
});
