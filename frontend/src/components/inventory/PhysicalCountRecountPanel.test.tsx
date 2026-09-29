import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PhysicalCountRecountPanel } from './PhysicalCountRecountPanel';
import { inventoryManagementService as service, type PhysicalCountDetailDto } from '@/services/inventoryManagementService';

vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: { createPhysicalCountRecount: vi.fn() } }));
const count = { id: 'root', rowVersion: 'rv', status: 'UnderReview', canCreateRecount: true,
  items: [{ id: 'a', itemCode: 'A', itemName: 'Item A', isCounted: true, rowVersion: 'a-rv' },
    { id: 'b', itemCode: 'B', itemName: 'Item B', isCounted: true, rowVersion: 'b-rv', supersededByPhysicalCountId: 'existing-child' }]
} as PhysicalCountDetailDto;
const onOpen = vi.fn().mockResolvedValue(undefined);
const errorMessage = (error: unknown) => (error as Error).message;
beforeEach(() => { vi.clearAllMocks(); });

describe('selective recount sheets', () => {
  it('sends only selected eligible lines with reasons and retained concurrency versions', async () => {
    vi.mocked(service.createPhysicalCountRecount).mockResolvedValue({ id: 'child' } as never);
    render(<PhysicalCountRecountPanel count={count} unsaved={false} onOpen={onOpen} errorMessage={errorMessage} />);
    fireEvent.click(screen.getByRole('button', { name: 'Select items for recount' }));
    expect(screen.queryByRole('checkbox', { name: 'Recount B' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('checkbox', { name: 'Recount A' }));
    fireEvent.click(screen.getByRole('button', { name: 'Create recount sheet' }));
    expect(screen.getByRole('alert')).toHaveTextContent('give a reason');
    expect(service.createPhysicalCountRecount).not.toHaveBeenCalled();
    fireEvent.change(screen.getByRole('textbox', { name: 'Recount reason A' }), { target: { value: 'Check damaged packaging' } });
    fireEvent.click(screen.getByRole('button', { name: 'Create recount sheet' }));
    await waitFor(() => expect(onOpen).toHaveBeenCalledWith({ id: 'child' }));
    expect(service.createPhysicalCountRecount).toHaveBeenCalledWith('root', { rowVersion: 'rv', idempotencyKey: expect.any(String), items: [{ physicalCountItemId: 'a', itemRowVersion: 'a-rv', reason: 'Check damaged packaging' }] });
  });

  it('retains selection and the same retry key when creation fails', async () => {
    vi.mocked(service.createPhysicalCountRecount).mockRejectedValue(new Error('The count changed. Reload.'));
    render(<PhysicalCountRecountPanel count={count} unsaved={false} onOpen={onOpen} errorMessage={errorMessage} />);
    fireEvent.click(screen.getByRole('button', { name: 'Select items for recount' }));
    fireEvent.click(screen.getByRole('checkbox', { name: 'Recount A' }));
    fireEvent.change(screen.getByRole('textbox', { name: 'Recount reason A' }), { target: { value: 'Check again' } });
    fireEvent.click(screen.getByRole('button', { name: 'Create recount sheet' }));
    await screen.findByText('The count changed. Reload.');
    const first = vi.mocked(service.createPhysicalCountRecount).mock.calls[0][1];
    fireEvent.click(screen.getByRole('button', { name: 'Create recount sheet' }));
    await waitFor(() => expect(service.createPhysicalCountRecount).toHaveBeenCalledTimes(2));
    expect(vi.mocked(service.createPhysicalCountRecount).mock.calls[1][1]).toEqual(first);
    expect(onOpen).not.toHaveBeenCalled();
  });

  it('hides mutation for denied actors and prevents navigation with unsaved observations', () => {
    render(<PhysicalCountRecountPanel count={{ ...count, canCreateRecount: false, sheets: [{ id: 'root', countNumber: 'PC1' }, { id: 'child', countNumber: 'PC2', recountAttempt: 1 }] as never }} unsaved onOpen={onOpen} errorMessage={errorMessage} />);
    expect(screen.queryByRole('button', { name: 'Select items for recount' })).not.toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Count sheet' })).toBeDisabled();
  });
});
