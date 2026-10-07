import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DeleteFixedAssetDraftButton } from './DeleteFixedAssetDraftButton';

const mocks = vi.hoisted(() => ({
  deleteAsset: vi.fn(),
  hasPermission: vi.fn(),
  toast: vi.fn(),
}));

vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({ hasPermission: mocks.hasPermission }),
}));

vi.mock('@/components/ui/use-toast', () => ({
  useToast: () => ({ toast: mocks.toast }),
}));

vi.mock('@/services/finance/fixed-assets-data.service', () => ({
  fixedAssetsDataService: { deleteAsset: mocks.deleteAsset },
}));

describe('DeleteFixedAssetDraftButton', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.hasPermission.mockReturnValue(true);
    mocks.deleteAsset.mockResolvedValue(undefined);
  });

  it('deletes an authorized Draft asset after explicit confirmation', async () => {
    const onDeleted = vi.fn();
    render(
      <DeleteFixedAssetDraftButton
        asset={{ id: 'asset-1', assetCode: 'FA-2024-001', name: 'Dell Laptop', status: 'Draft' }}
        onDeleted={onDeleted}
      />
    );

    fireEvent.click(screen.getByRole('button', { name: 'Delete draft asset: Dell Laptop' }));
    expect(screen.getByText('Delete draft asset FA-2024-001?')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Delete draft' }));

    await waitFor(() => expect(mocks.deleteAsset).toHaveBeenCalledWith('asset-1'));
    expect(onDeleted).toHaveBeenCalledOnce();
    expect(mocks.toast).toHaveBeenCalledWith(expect.objectContaining({ title: 'Draft asset deleted' }));
  });

  it('does not offer destructive deletion for a posted lifecycle status', () => {
    render(
      <DeleteFixedAssetDraftButton
        asset={{ id: 'asset-2', assetCode: 'FA-2024-002', name: 'Posted Asset', status: 'Capitalized' }}
        onDeleted={vi.fn()}
      />
    );

    expect(screen.queryByRole('button', { name: /Delete draft asset/ })).not.toBeInTheDocument();
  });

  it('does not offer deletion without fixed-asset management permission', () => {
    mocks.hasPermission.mockReturnValue(false);
    render(
      <DeleteFixedAssetDraftButton
        asset={{ id: 'asset-3', assetCode: 'FA-2024-003', name: 'Protected Draft', status: 'Draft' }}
        onDeleted={vi.fn()}
      />
    );

    expect(screen.queryByRole('button', { name: /Delete draft asset/ })).not.toBeInTheDocument();
  });
});
