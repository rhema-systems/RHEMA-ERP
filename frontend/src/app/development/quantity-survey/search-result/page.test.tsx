import React from 'react';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { CivilQsSearchResult } from './page';

const mocks = vi.hoisted(() => ({ query: '', get: vi.fn(), post: vi.fn(), put: vi.fn() }));
vi.mock('next/navigation', () => ({ useSearchParams: () => new URLSearchParams(mocks.query) }));
vi.mock('@/services/api.service', () => ({ apiService: { get: mocks.get, post: mocks.post, put: mocks.put } }));
vi.mock('@/lib/procurement-tender-header-actions', () => ({ getProcurementProblemMessage: (error: Error) => error.message }));

const recordId = '11111111-1111-1111-1111-111111111111';
describe('Civil and QS exact search result', () => {
  beforeEach(() => { vi.clearAllMocks(); vi.stubGlobal('React', React); });
  afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

  it('loads a valuation by its interim valuation identity and displays saved lines without writing', async () => {
    mocks.query = `kind=valuation-worksheets&recordId=${recordId}`;
    mocks.get.mockResolvedValue({ id: 'different-worksheet-id', projectInterimValuationId: recordId,
      interimValuationLabel: 'VAL-2026-184', status: 'Vetted', projectId: 'project-one',
      lines: [{ id: 'line', description: 'Excavation', currentCertifiedQuantity: 42 }] });
    render(<CivilQsSearchResult />);
    expect(await screen.findByText('VAL-2026-184')).toBeInTheDocument();
    expect(screen.getByText('Excavation')).toBeInTheDocument();
    expect(mocks.get).toHaveBeenCalledWith(`/quantity-survey/valuation-worksheets/${recordId}`);
    expect(mocks.get).toHaveBeenCalledTimes(1);
    expect(mocks.post).not.toHaveBeenCalled(); expect(mocks.put).not.toHaveBeenCalled();
  });

  it('opens the exact Civil task through its authorized owner', async () => {
    mocks.query = `kind=civil-task&recordId=${recordId}`;
    mocks.get.mockResolvedValue({ id: recordId, title: 'Inspect culvert', instructions: 'Check\nfoundation', assignedToName: 'Site engineer' });
    render(<CivilQsSearchResult />);
    expect(await screen.findByText('Inspect culvert')).toBeInTheDocument();
    expect(screen.getByText('Site engineer')).toBeInTheDocument();
    expect(mocks.get).toHaveBeenCalledWith(`/civil-engineering/direct-tasks/${recordId}`);
    expect(mocks.post).not.toHaveBeenCalled();
  });

  it.each<[string, Record<string, unknown>, string, string]>([
    ['rate-library', { code: 'RATE-21', name: 'Excavation', rates: [{ id: 'rate', unitRate: 125, currencyCode: 'GHS' }] }, 'RATE-21 · Excavation', '125'],
    ['price-index-imports', { indexFamilyCode: 'CPI', originalFileName: 'prices.csv', values: [{ id: 'value', indexPeriod: '2026-09-01', indexValue: 117.2 }] }, 'CPI · prices.csv', '117.2'],
    ['escalation-formulas', { code: 'ESC-9', name: 'Works escalation', components: [{ component: 'Labour', coefficient: 0.35 }] }, 'ESC-9 · Works escalation', '0.35'],
  ])('opens exact %s owner data and its child rows without writing', async (kind, record, title, value) => {
    mocks.query = `kind=${kind}&recordId=${recordId}`;
    mocks.get.mockResolvedValue({ id: recordId, ...record });
    render(<CivilQsSearchResult />);
    expect(await screen.findByRole('heading', { name: title })).toBeInTheDocument();
    expect(screen.getByText(value)).toBeInTheDocument();
    expect(mocks.get).toHaveBeenCalledWith(`/quantity-survey/${kind}/${recordId}`);
    expect(mocks.post).not.toHaveBeenCalled(); expect(mocks.put).not.toHaveBeenCalled();
  });

  it('shows a denied read without stale record details', async () => {
    mocks.query = `kind=measurements&recordId=${recordId}`;
    mocks.get.mockRejectedValue(new Error('Access denied.'));
    render(<CivilQsSearchResult />);
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Access denied.'));
    expect(screen.queryByText('Details')).not.toBeInTheDocument();
  });

  it('does not issue arbitrary owner requests from an invalid deep link', async () => {
    mocks.query = `kind=unrecognized&recordId=${recordId}`;
    render(<CivilQsSearchResult />);
    expect(await screen.findByRole('alert')).toHaveTextContent('invalid');
    expect(mocks.get).not.toHaveBeenCalled();
  });
});
