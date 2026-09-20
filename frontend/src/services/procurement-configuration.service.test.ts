import { describe, expect, it, vi } from 'vitest';
const api = vi.hoisted(() => ({ post: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: api }));
import { procurementConfigurationService } from './procurement-configuration.service';

describe('governed decision withdrawal API', () => {
  it('uses the decision-specific action, not profile retirement', async () => {
    await procurementConfigurationService.withdrawDecision(
      'profile-3',
      'DEC-011',
      { rowVersion: 'row-11', reason: 'Approved architecture alignment' }
    );
    expect(api.post).toHaveBeenCalledExactlyOnceWith(
      '/procurement/configuration-profiles/profile-3/decisions/DEC-011/withdraw',
      { rowVersion: 'row-11', reason: 'Approved architecture alignment' }
    );
  });
});
