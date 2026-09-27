import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { businessPartnerFinanceProfileService, type SaveApProfile } from './businessPartnerFinanceProfileService';

const request: SaveApProfile = {
  businessPartnerRoleId: 'supplier-role', effectiveFrom: '2026-09-01', paymentTermId: 'net30',
  defaultExpenseAccountId: 'expense-1', defaultTaxGroupId: 'tax-group-1', subjectToWithholding: false,
  withholdingDefaults: [{ categoryCode: 'LEGACY', withholdingTaxId: 'tax-1', isDefaultForAp: false, isActive: false }],
};

beforeEach(() => { localStorage.clear(); vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 'profile-1' }) })); });
afterEach(() => vi.unstubAllGlobals());

describe('canonical Finance profile transport', () => {
  it('sends expense/tax defaults and inactive WHT rows to the existing draft endpoint', async () => {
    await businessPartnerFinanceProfileService.saveAp('partner-1', 'profile-1', request);
    expect(fetch).toHaveBeenCalledWith(expect.stringContaining('/partner-1/ap/profile-1'), expect.objectContaining({ method: 'PUT', body: JSON.stringify(request) }));
  });

  it('creates a new version without updating the approved profile', async () => {
    await businessPartnerFinanceProfileService.saveAp('partner-1', null, request);
    expect(fetch).toHaveBeenCalledWith(expect.stringMatching(/\/partner-1\/ap$/), expect.objectContaining({ method: 'POST' }));
  });

  it('preserves server rejection detail and code', async () => {
    vi.mocked(fetch).mockResolvedValue({ ok: false, json: async () => ({ title: 'Conflict', detail: 'An independent approver is required.', code: 'MAKER_CHECKER_REQUIRED' }) } as Response);
    await expect(businessPartnerFinanceProfileService.decide('partner-1', 'ap', 'profile-1', 'approve')).rejects.toThrow('An independent approver is required. (MAKER_CHECKER_REQUIRED)');
  });

  it('handles a non-JSON denial without hiding it as success', async () => {
    vi.mocked(fetch).mockResolvedValue({ ok: false, json: async () => { throw new SyntaxError('not JSON'); } } as unknown as Response);
    await expect(businessPartnerFinanceProfileService.get('partner-1')).rejects.toThrow('Finance profile request failed.');
  });
});
