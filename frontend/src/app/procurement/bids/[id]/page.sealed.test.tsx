import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import BidDetailPage from './page';

const api = vi.hoisted(() => ({ bid: vi.fn(), tender: vi.fn(), push: vi.fn() }));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'bid-1' }), useRouter: () => ({ push: api.push, replace: vi.fn() }) }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: () => true }) }));
vi.mock('@/services/tenderBidService', () => ({ getBidById: api.bid, getBidPayments: vi.fn().mockResolvedValue([]) }));
vi.mock('@/services/tenderService', () => ({ tenderService: { getTenderById: api.tender } }));
vi.mock('@/components/quantity-survey/QuantitySurveyTenderBoqVettingPanel', () => ({ QuantitySurveyTenderBoqVettingPanel: () => null }));

beforeEach(() => {
  vi.stubGlobal('React', React);
  api.tender.mockResolvedValue({ id: 'tender-1', status: 'Published', usesControlledTenderLifecycle: false });
  api.bid.mockResolvedValue({ id: 'bid-1', tenderId: 'tender-1', bidNumber: 'BID-1', status: 'Submitted', isSealed: true,
    totalBidAmount: 52000, technicalProposal: 'do not display', commercialProposal: 'do not display', documents: [] });
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.clearAllMocks(); });

describe('sealed back-office bid', () => {
  it('renders receipt-only guidance with no amount, proposal tabs or download/open actions', async () => {
    render(<BidDetailPage />);
    expect(await screen.findByText('Bid sealed')).toBeInTheDocument();
    expect(screen.getByText('BID-1')).toBeInTheDocument();
    expect(screen.queryByText(/52,000|52000|do not display/)).not.toBeInTheDocument();
    expect(screen.queryByRole('tab')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /download|open bid/i })).not.toBeInTheDocument();
  });
  it('returns to the exact source tender for governed opening prerequisites', async () => {
    render(<BidDetailPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Return to tender' }));
    expect(api.push).toHaveBeenCalledWith('/procurement/tenders/tender-1');
  });
  it('retains the governed opening action after closure and opening time without exposing contents first', async () => {
    api.tender.mockResolvedValue({ id: 'tender-1', status: 'Closed', usesControlledTenderLifecycle: false,
      submissionDeadline: '2020-01-01T10:00:00Z', openingDate: '2020-01-01T10:05:00Z' });
    render(<BidDetailPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Open bid' }));
    expect(await screen.findByRole('dialog', { name: 'Mark Bid as Opened' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Mark as Opened' })).toBeInTheDocument();
    expect(screen.queryByText(/52,000|52000|do not display/)).not.toBeInTheDocument();
  });
});
