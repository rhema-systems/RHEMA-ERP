import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import procurementSettingsService, { type ProcurementSettingsDto } from '@/services/procurementSettingsService';
import Page from './page';

vi.mock('@/services/procurementSettingsService', () => ({ default: { getSettings: vi.fn(), updateSettings: vi.fn() } }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: vi.fn() }) }));
Object.assign(globalThis, { React });

describe('automatic tender closing setting', () => {
  it('loads off by default and saves the chosen value alongside existing approval settings', async () => {
    const settings: ProcurementSettingsDto = {
      id: 'settings', tenantId: 'tenant', autoCloseTenders: false, enforceSegregationOfDuties: true,
      autoCreateInventoryItems: false, autoCreateSupplierItems: false, allowNonInventoryItems: true,
      requireApprovalForPO: true, allowBackorders: true, requireDeliveryDate: true,
      enforceSupplierCatalog: false, allowMultipleSuppliersPerItem: true,
      validateBudgetBeforePO: true, requireContractForPO: false, createdAt: '2026-09-24T00:00:00Z',
    };
    vi.mocked(procurementSettingsService.getSettings).mockResolvedValue(settings);
    vi.mocked(procurementSettingsService.updateSettings).mockResolvedValue({ ...settings, autoCloseTenders: true });
    render(<Page />);
    const toggle = await screen.findByRole('switch', { name: 'Automatically close tenders when the closing date is reached' });
    expect(toggle).not.toBeChecked();
    const sodToggle = screen.getByRole('switch', { name: 'Enforce segregation of duties for procurement transactions' });
    expect(sodToggle).toBeChecked();
    fireEvent.click(sodToggle);
    fireEvent.click(toggle);
    fireEvent.click(screen.getAllByRole('button', { name: 'Save Settings' })[0]);
    await waitFor(() => expect(procurementSettingsService.updateSettings).toHaveBeenCalledWith(expect.objectContaining({
      autoCloseTenders: true, enforceSegregationOfDuties: false, requireApprovalForPO: true, validateBudgetBeforePO: true,
    })));
  });
});
