import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  load: vi.fn(), verify: vi.fn(), complete: vi.fn(), start: vi.fn(), defaultTemplate: vi.fn(),
}));
vi.mock('@/services/awardVerificationService', () => ({ awardVerificationService: {
  getVerificationByTender: mocks.load, verifyBidder: mocks.verify,
  completeVerification: mocks.complete, startVerification: mocks.start,
  getDefaultTemplate: mocks.defaultTemplate,
} }));
vi.mock('./VerificationItemDocuments', () => ({
  VerificationItemDocuments: ({ disabled }: { disabled: boolean }) => <button disabled={disabled}>Attach evidence</button>,
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() } }));

import { AwardVerificationDialog } from './AwardVerificationDialog';
import { AwardVerificationResults } from './AwardVerificationResults';

const bidders = [{ bidId: 'bid-1', businessPartnerId: 'supplier-1', businessPartnerName: 'Goods Supplier', bidNumber: 'BID-1', totalBidAmount: 52000, currency: 'GHS' }];
const props = { open: true, onOpenChange: vi.fn(), tenderId: 'tender-1', bidders };
const record = (status = 'Completed', bidderStatus = 'Passed') => ({
  id: 'verification-1', tenderId: 'tender-1', status, completedDate: '2026-09-06T04:07:54Z',
  bidders: [{
    id: 'bidder-1', tenderBidId: 'bid-1', businessPartnerId: 'supplier-1',
    businessPartnerName: 'Goods Supplier', status: bidderStatus, overallComments: 'Saved review notes',
    itemResults: [{ id: 'item-result-1', checklistItemId: 'item-1', itemText: 'Review existing evidence',
      isRequired: true, isVerified: bidderStatus !== 'Pending', status: bidderStatus === 'Pending' ? 'Pending' : 'Passed',
      comments: 'Saved evidence reference', documents: [],
    }],
  }],
});

beforeEach(() => {
  vi.stubGlobal('React', React);
  vi.clearAllMocks();
  mocks.load.mockResolvedValue(record());
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.restoreAllMocks(); });

describe('saved award verification status', () => {
  it.each(['Passed', 'Verified'])('reopens completed %s reviews as 1/1 verified and read-only', async (status) => {
    mocks.load.mockResolvedValue(record('Completed', status));
    render(<AwardVerificationDialog {...props} />);
    expect(await screen.findByText('Verified', { exact: true })).toBeVisible();
    expect(screen.getByText(/Progress: 1 of 1 bidders verified/)).toBeVisible();
    expect(screen.getByText('Verification completed · read-only')).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Verify Bidder' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Complete Verification' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Pass' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Attach evidence' })).toBeDisabled();
    expect(screen.getByRole('textbox', { name: 'Review comments (optional)' })).toBeDisabled();
    expect(screen.getByRole('textbox', { name: 'Review comments (optional)' })).toHaveValue('Saved evidence reference');
    expect(screen.getByPlaceholderText('Overall verification comments for this bidder...')).toBeDisabled();
    expect(mocks.verify).not.toHaveBeenCalled();
    expect(mocks.complete).not.toHaveBeenCalled();
    expect(mocks.start).not.toHaveBeenCalled();
  });

  it.each(['Pending', 'UnexpectedStatus', 'Failed'])('does not count %s as a successful review', async (status) => {
    mocks.load.mockResolvedValue(record('InProgress', status));
    render(<AwardVerificationDialog {...props} onAwardBidder={vi.fn()} />);
    await screen.findByText('1 bidder(s) remaining');
    expect(screen.queryByText('All bidders verified!')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Complete Verification' })).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Award' })).not.toBeInTheDocument();
  });

  it('locks a cancelled review even when its bidder is pending', async () => {
    mocks.load.mockResolvedValue(record('Cancelled', 'Pending'));
    render(<AwardVerificationDialog {...props} />);
    await screen.findByText('Verification cancelled · read-only');
    expect(screen.getByRole('button', { name: 'Pass' })).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Verify Bidder' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Complete Verification' })).not.toBeInTheDocument();
  });

  it('allows an in-progress passed review to complete once', async () => {
    mocks.load.mockResolvedValue(record('InProgress', 'Passed'));
    mocks.complete.mockResolvedValue(record());
    render(<AwardVerificationDialog {...props} />);
    await screen.findByText('All bidders verified!');
    fireEvent.click(screen.getByRole('button', { name: 'Complete Verification' }));
    await screen.findByText('Verification completed · read-only');
    expect(mocks.complete).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('button', { name: 'Complete Verification' })).not.toBeInTheDocument();
  });

  it('uses a failed save response instead of optimistically reporting verified', async () => {
    mocks.load.mockResolvedValue(record('InProgress', 'Pending'));
    mocks.verify.mockResolvedValue(record('InProgress', 'Failed').bidders[0]);
    render(<AwardVerificationDialog {...props} />);
    await screen.findByText('1 bidder(s) remaining');
    fireEvent.click(screen.getByRole('button', { name: 'Fail' }));
    fireEvent.click(screen.getByRole('button', { name: 'Verify Bidder' }));
    await waitFor(() => expect(mocks.verify).toHaveBeenCalledTimes(1));
    expect(await screen.findAllByText('Failed', { exact: true })).toHaveLength(2);
    expect(screen.queryByText('Verified', { exact: true })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Complete Verification' })).toBeDisabled();
  });

  it('shows a load error, never empty success or a completion action', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    mocks.load.mockRejectedValue(new Error(JSON.stringify({ detail: 'Evaluator permission required', code: 'PERMISSION_DENIED' })));
    render(<AwardVerificationDialog {...props} />);
    expect(await screen.findByRole('alert')).toHaveTextContent('Evaluator permission required (PERMISSION_DENIED)');
    expect(screen.queryByText('All bidders verified!')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Complete Verification' })).not.toBeInTheDocument();
    expect(mocks.complete).not.toHaveBeenCalled();
  });

  it.each(['', '   '])('saves and completes decisions with optional blank comments %j', async (comments) => {
    const data = record('InProgress', 'Pending');
    data.bidders[0].overallComments = comments;
    data.bidders[0].itemResults[0].comments = comments;
    mocks.load.mockResolvedValue(data);
    mocks.verify.mockResolvedValue({ ...data.bidders[0], status: 'Passed' });
    mocks.complete.mockResolvedValue({ ...data, status: 'Completed' });
    render(<AwardVerificationDialog {...props} />);
    await screen.findByText('1 bidder(s) remaining');
    expect(screen.getByRole('textbox', { name: 'Review comments (optional)' })).toHaveValue(comments);
    expect(screen.getByText('Comments and attachments are optional. Click Verify Bidder to save your decisions.')).toBeVisible();
    fireEvent.click(screen.getByRole('button', { name: 'Pass' }));
    fireEvent.click(screen.getByRole('button', { name: 'Verify Bidder' }));
    await screen.findByText('All bidders verified!');
    expect(mocks.verify).toHaveBeenCalledWith('verification-1', expect.objectContaining({
      bidderId: 'bidder-1',
      itemResults: [expect.objectContaining({ checklistItemId: 'item-1', status: 'Passed', comments: comments || undefined })],
    }));
    fireEvent.click(screen.getByRole('button', { name: 'Complete Verification' }));
    await screen.findByText('Verification completed · read-only');
    expect(mocks.complete).toHaveBeenCalledTimes(1);
  });

  it('still requires a decision when comments are optional', async () => {
    const data = record('InProgress', 'Pending');
    data.bidders[0].itemResults[0].comments = '';
    mocks.load.mockResolvedValue(data);
    render(<AwardVerificationDialog {...props} />);
    await screen.findByText('1 bidder(s) remaining');
    fireEvent.click(screen.getByRole('button', { name: 'Verify Bidder' }));
    expect(mocks.verify).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Complete Verification' })).toBeDisabled();
  });

  it('blocks verification when an explicitly required document is missing, even with notes', async () => {
    const data = record('InProgress', 'Pending');
    Object.assign(data.bidders[0].itemResults[0], { requiresDocument: true });
    mocks.load.mockResolvedValue(data);
    render(<AwardVerificationDialog {...props} />);
    await screen.findByText('1 bidder(s) remaining');
    fireEvent.click(screen.getByRole('button', { name: 'Pass' }));
    fireEvent.click(screen.getByRole('button', { name: 'Verify Bidder' }));
    expect(mocks.verify).not.toHaveBeenCalled();
    expect(screen.getByText('Document required: retain the supporting file for this check.')).toBeVisible();
  });

  it('does not complete a persisted passed review whose explicit document evidence is missing', async () => {
    const data = record('InProgress', 'Passed');
    Object.assign(data.bidders[0].itemResults[0], { requiresDocument: true });
    mocks.load.mockResolvedValue(data);
    render(<AwardVerificationDialog {...props} />);
    await screen.findByText('Verified', { exact: true });
    expect(screen.getByRole('button', { name: 'Complete Verification' })).toBeDisabled();
    expect(mocks.complete).not.toHaveBeenCalled();
  });

  it('counts only Passed/Verified bidders on the Results tab', async () => {
    const data = record();
    data.bidders.push({ ...data.bidders[0], id: 'pending-2', tenderBidId: 'bid-2', businessPartnerName: 'Pending Supplier', status: 'Pending' });
    mocks.load.mockResolvedValue(data);
    render(<AwardVerificationResults tenderId="tender-1" />);
    expect(await screen.findByText('1 / 2')).toBeVisible();
    expect(screen.getAllByText('Passed', { exact: true })[0]).toHaveClass('text-green-700');
  });
});
