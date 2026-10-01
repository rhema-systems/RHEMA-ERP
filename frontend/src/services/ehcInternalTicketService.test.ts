import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import { ehcInternalTicketService } from './ehcInternalTicketService';

vi.mock('./api.service', () => ({
  apiService: { request: vi.fn() },
}));

describe('ehcInternalTicketService ticket routing', () => {
  beforeEach(() => vi.mocked(apiService.request).mockReset());

  it('sends only the destination department when routing from ticket details', async () => {
    vi.mocked(apiService.request).mockResolvedValue({ success: true, data: null });

    await ehcInternalTicketService.routeTicket('ticket-1', 'department-1');

    expect(apiService.request).toHaveBeenCalledWith(
      '/ehc/internal/tickets/ticket-1/route?assignedOrganizationUnitId=department-1',
      { method: 'POST' }
    );
  });
});
