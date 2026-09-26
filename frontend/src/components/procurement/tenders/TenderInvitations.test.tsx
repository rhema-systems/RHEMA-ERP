import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { expect, it, vi } from 'vitest';
import TenderInvitations from './TenderInvitations';
import { businessPartnerService } from '@/services/businessPartnerService';
import { tenderService } from '@/services/tenderService';

vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: { getPartners: vi.fn() } }));
vi.mock('@/services/tenderService', () => ({ tenderService: { inviteTenderers: vi.fn() } }));
vi.mock('@/services/purchasingService', () => ({ purchasingService: { getSuggestedSuppliersForRequisition: vi.fn() } }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

it('retains supplier selection after adding the customer role to the same partner', async () => {
  vi.mocked(businessPartnerService.getPartners).mockResolvedValue({ items: [
    { id: 'dual', partnerCode: 'BP-100', partnerName: 'Dual role partner', partnerType: 'CustomerAndSupplier' },
    { id: 'customer', partnerCode: 'BP-101', partnerName: 'Customer only', partnerType: 'Customer' },
  ] } as any);
  vi.mocked(tenderService.inviteTenderers).mockResolvedValue(undefined as any);
  const update = vi.fn();
  render(<TenderInvitations tenderId="tender" formData={{ invitations: [] } as any} updateFormData={update} />);
  await screen.findByText('Dual role partner');
  expect(screen.queryByText('Customer only')).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole('checkbox'));
  fireEvent.click(screen.getByRole('button', { name: 'Invite 1 Business Partner' }));
  await waitFor(() => expect(tenderService.inviteTenderers).toHaveBeenCalledWith('tender', {
    businessPartnerIds: ['dual'], sendNotifications: false,
  }));
  await waitFor(() => expect(update).toHaveBeenCalledWith({ invitations: [expect.objectContaining({ businessPartnerId: 'dual' })] }));
});
