import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { SupplierPortalAccount } from './SupplierPortalAccount';
import { businessPartnerService } from '@/services/businessPartnerService';

vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: { getMyAccount: vi.fn() } }));
Object.assign(globalThis, { React });

describe('supplier portal account', () => {
  it('displays the assigned account number from the current-user endpoint', async () => {
    vi.mocked(businessPartnerService.getMyAccount).mockResolvedValue({ id: 'partner', partnerCode: 'SUP260123', partnerName: 'Supplier One' });
    render(<SupplierPortalAccount />);
    expect(await screen.findByText('SUP260123')).toBeInTheDocument();
    expect(screen.getByText('Business Partner Account Number')).toBeInTheDocument();
    expect(businessPartnerService.getMyAccount).toHaveBeenCalledWith();
  });
});
