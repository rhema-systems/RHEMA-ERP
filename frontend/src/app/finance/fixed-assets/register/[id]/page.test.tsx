import React from 'react';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import FixedAssetDetailPage from './page';

const mocks = vi.hoisted(() => ({ getAssetById: vi.fn(), id: 'asset-1' }));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: mocks.id }) }));
vi.mock('@/services/finance/fixed-assets-data.service', () => ({ fixedAssetsDataService: mocks }));

describe('fixed asset record view', () => {
  beforeEach(() => { mocks.getAssetById.mockReset(); mocks.id = 'asset-1'; });
  afterEach(cleanup);

  it('loads the exact asset without requesting the entire register or opening an edit form', async () => {
    mocks.getAssetById.mockResolvedValue({ id: 'asset-1', assetCode: 'FA-001', name: 'Office building',
      status: 'Active', fixedAssetCategoryName: 'Buildings', purchaseDate: '2026-09-01' });
    render(<FixedAssetDetailPage />);
    expect(await screen.findByText('FA-001 · Office building')).toBeInTheDocument();
    expect(mocks.getAssetById).toHaveBeenCalledWith('asset-1');
    expect(screen.getByText('Buildings')).toBeInTheDocument();
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /save|post|approve/i })).not.toBeInTheDocument();
  });

  it('shows server rejection details without rendering a previous record', async () => {
    mocks.getAssetById.mockRejectedValue({ detail: 'Access denied to asset.', code: 'FINANCE_ACCESS_DENIED' });
    render(<FixedAssetDetailPage />);
    expect(await screen.findByRole('alert')).toHaveTextContent('Access denied to asset. FINANCE_ACCESS_DENIED');
    expect(screen.queryByText('FA-001')).not.toBeInTheDocument();
  });

  it('ignores a late response when navigation selects a different record', async () => {
    let resolveFirst!: (value: unknown) => void;
    mocks.getAssetById.mockImplementationOnce(() => new Promise(resolve => { resolveFirst = resolve; }))
      .mockResolvedValueOnce({ id: 'asset-2', assetCode: 'FA-002', name: 'Generator', status: 'Active' });
    const view = render(<FixedAssetDetailPage />);
    mocks.id = 'asset-2';
    view.rerender(<FixedAssetDetailPage />);
    expect(await screen.findByText('FA-002 · Generator')).toBeInTheDocument();
    resolveFirst({ id: 'asset-1', assetCode: 'FA-001', name: 'Stale', status: 'Active' });
    await waitFor(() => expect(screen.queryByText('FA-001 · Stale')).not.toBeInTheDocument());
  });
});
