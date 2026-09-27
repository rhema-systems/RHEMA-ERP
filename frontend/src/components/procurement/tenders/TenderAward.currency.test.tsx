import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getAward: vi.fn(),
  recommendation: vi.fn(),
  getVerification: vi.fn(),
  startVerification: vi.fn(),
  createAward: vi.fn(),
  getTender: vi.fn(),
  hasPermission: vi.fn(),
}));

vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: mocks.hasPermission }) }));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'tender-1' }), useRouter: () => ({ push: vi.fn() }) }));
vi.mock('@/services/tenderAwardService', () => ({
  getAwardByTenderId: mocks.getAward,
  generateAwardRecommendation: mocks.recommendation,
  createAward: mocks.createAward,
}));
vi.mock('@/services/tenderService', () => ({ getTenderById: mocks.getTender }));
vi.mock('@/services/awardVerificationService', () => ({ awardVerificationService: {
  getVerificationByTender: mocks.getVerification,
  startVerification: mocks.startVerification,
} }));
vi.mock('@/services/procurement-award-readiness.service', () => ({ procurementAwardReadinessService: {
  latest: vi.fn().mockResolvedValue({
    status: 'Ready', isReady: true, isCurrent: true, decisionSequence: 3,
    allowedActions: ['RecordAward'], blockedReasons: [], recommendation: { subjectIds: ['bid-1'] },
  }),
} }));
vi.mock('./VerificationItemDocuments', () => ({ VerificationItemDocuments: () => null }));

import { TenderAward } from './TenderAward';
import CreateAwardPage from '@/app/procurement/tenders/[id]/create-award/page';

const bid = {
  bidId: 'bid-1', bidNumber: 'BID-1', businessPartnerId: 'supplier-1',
  businessPartnerName: 'Test Goods Supplier', totalBidAmount: 52000,
  averageScore: 90, evaluationCount: 3, recommendationCount: 1, totalEvaluators: 3,
};
const tenderProps = {
  tenderId: 'tender-1', tenderNumber: 'TND-1', tenderTitle: 'Goods test', tenderCurrency: 'GHS',
};

beforeEach(() => {
  // The focused Vitest configuration uses the classic JSX transform, whereas
  // Next supplies the automatic React runtime in the application.
  vi.stubGlobal('React', React);
  vi.clearAllMocks();
  mocks.hasPermission.mockReturnValue(true);
  mocks.getAward.mockResolvedValue(null);
  mocks.recommendation.mockResolvedValue({
    totalBids: 1, evaluatedBids: 1, recommendedBidId: 'bid-1', recommendedAmount: 52000,
    recommendedScore: 90, recommendedBusinessPartner: 'Test Goods Supplier',
    recommendedBidNumber: 'BID-1', bidRecommendations: [bid],
  });
  mocks.getVerification.mockResolvedValue({
    id: 'verification-1', status: 'InProgress', bidders: [{
      id: 'bidder-1', tenderBidId: 'bid-1', businessPartnerId: 'supplier-1',
      businessPartnerName: 'Test Goods Supplier', status: 'Pending', itemResults: [{
        id: 'result-1', checklistItemId: 'item-1', checklistItemText: 'Existing evidence review',
        isRequired: true, isVerified: false, status: 'Pending', documents: [],
      }],
    }],
  });
  mocks.getTender.mockResolvedValue({
    id: 'tender-1', tenderNumber: 'TND-1', title: 'Goods test', currency: 'GHS',
    bids: [{ id: 'bid-1', currency: 'EUR' }],
  });
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe('tender award currency presentation', () => {
  it.each(['GHS', 'USD', 'EUR'])('shows saved %s in comparison, recommendation and reopened verification', async (currency) => {
    render(<TenderAward {...tenderProps} bids={[{ id: 'bid-1', currency }]} />);
    expect(await screen.findAllByText(`${currency} 52,000.00`)).toHaveLength(2);
    fireEvent.click(screen.getByRole('button', { name: 'View Verification (1)' }));
    const dialog = await screen.findByRole('dialog', { name: 'Award Verification Checklist' });
    expect(await within(dialog).findByText(`${currency} 52,000.00`)).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Complete Verification' })).toBeDisabled();
    expect(mocks.startVerification).not.toHaveBeenCalled();
    expect(mocks.createAward).not.toHaveBeenCalled();
    expect(document.body.textContent).not.toContain('$52,000');
  });

  it('passes currency for newly selected bidders and falls back to tender currency', async () => {
    render(<TenderAward {...tenderProps} tenderCurrency="GBP" bids={[{ id: 'bid-1' }]} />);
    await screen.findAllByText('GBP 52,000.00');
    fireEvent.click(screen.getAllByRole('checkbox')[1]);
    fireEvent.click(screen.getByRole('button', { name: 'Verify Selected (1)' }));
    expect(await within(screen.getByRole('dialog')).findByText('GBP 52,000.00')).toBeVisible();
  });

  it('labels the recommendation input with its tender currency without changing the amount or submitting', async () => {
    render(<TenderAward {...tenderProps} bids={[{ id: 'bid-1', currency: 'GHS' }]} />);
    fireEvent.click(await screen.findByRole('button', { name: 'Recommend' }));
    expect(screen.getByRole('spinbutton', { name: 'Award Amount (GHS) *' })).toHaveValue(52000);
    expect(mocks.createAward).not.toHaveBeenCalled();
  });

  it.each(['PendingApproval', 'Awarded'])('preserves the saved award currency for %s', async (status) => {
    mocks.getAward.mockResolvedValue({
      id: 'award-1', status, awardedAmount: 51000, currency: 'EUR',
      businessPartnerName: 'Test Goods Supplier', awardDate: '2026-09-06T00:00:00Z',
    });
    render(<TenderAward {...tenderProps} />);
    expect(await screen.findByText('EUR 51,000.00')).toBeVisible();
  });

  it.each(['GHS', 'USD', 'EUR'])('submits the displayed %s bid currency without changing the amount', async (currency) => {
    render(<TenderAward {...tenderProps} bids={[{ id: 'bid-1', currency }]} />);
    fireEvent.click(await screen.findByRole('button', { name: 'Recommend' }));
    expect(screen.getByRole('spinbutton', { name: `Award Amount (${currency}) *` })).toHaveValue(52000);
    fireEvent.click(screen.getByRole('button', { name: 'Submit Recommendation' }));
    await waitFor(() => expect(mocks.createAward).toHaveBeenCalledWith(expect.objectContaining({
      tenderId: 'tender-1', tenderBidId: 'bid-1', awardedAmount: 52000, currency,
    })));
  });

  it('submits the tender currency when the saved bid has no currency', async () => {
    render(<TenderAward {...tenderProps} bids={[{ id: 'bid-1' }]} />);
    fireEvent.click(await screen.findByRole('button', { name: 'Recommend' }));
    fireEvent.click(screen.getByRole('button', { name: 'Submit Recommendation' }));
    await waitFor(() => expect(mocks.createAward).toHaveBeenCalledWith(expect.objectContaining({
      awardedAmount: 52000, currency: 'GHS',
    })));
  });

  it('keeps the separate create-award entry point and its verification in the saved bid currency', async () => {
    render(<CreateAwardPage />);
    expect(await screen.findAllByText('EUR 52,000.00')).toHaveLength(2);
    fireEvent.click(screen.getByRole('button', { name: 'Verify Bidder' }));
    expect(await within(screen.getByRole('dialog')).findByText('EUR 52,000.00')).toBeVisible();
    expect(mocks.createAward).not.toHaveBeenCalled();
  });
});
