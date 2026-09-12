import React from 'react';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as XLSX from 'xlsx';
import { PhysicalCountSheetUploadDialog } from './PhysicalCountSheetUploadDialog';
import { inventoryManagementService as service, type PhysicalCountDetailDto } from '@/services/inventoryManagementService';
import { COUNT_SHEET_HEADERS, COUNT_SHEET_HEADERS_WITHOUT_LOCATION } from '@/lib/physical-count-sheet';

vi.mock('sonner', () => ({ toast: { success: vi.fn(), warning: vi.fn() } }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: { importPhysicalCountSheet: vi.fn() } }));
const onSaved = vi.fn(); const onClose = vi.fn();
const count = { id: 'c', countNumber: 'PC-TEST', status: 'InProgress', rowVersion: 'count-rv', items: [
  { id: 'a', itemCode: 'A', itemName: 'Item A', unitOfMeasure: 'EA', locationName: 'L1', rowVersion: 'rv1' },
  { id: 'b', itemCode: 'B', itemName: 'Item B', unitOfMeasure: 'EA', locationName: 'L2', rowVersion: 'rv2' },
] } as PhysicalCountDetailDto;

async function upload(qty: unknown = 0, source = count, omitSecond = false) {
  render(<PhysicalCountSheetUploadDialog count={source} onSaved={onSaved} onClose={onClose} errorMessage={error => (error as Error).message} />);
  const located = source.items.some(item => item.locationName);
  const workbook = XLSX.utils.book_new();
  const rows = source.items.slice(0, omitSecond ? 1 : 2).map((item, index) => [item.itemCode, item.itemName, item.unitOfMeasure,
    ...(located ? [item.locationName] : []), index === 0 ? qty : 2]);
  XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet([located ? COUNT_SHEET_HEADERS : COUNT_SHEET_HEADERS_WITHOUT_LOCATION, ...rows]), 'Count Sheet');
  const bytes = XLSX.write(workbook, { type: 'array', bookType: 'xlsx' });
  const file = new File([bytes], 'counts.xlsx');
  Object.defineProperty(file, 'arrayBuffer', { value: async () => bytes });
  await act(async () => { fireEvent.change(screen.getByLabelText('Completed Excel count sheet'), { target: { files: [file] } }); });
  return file;
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(service.importPhysicalCountSheet).mockResolvedValue({ savedItems: 2, blankItems: 0 });
  onSaved.mockResolvedValue(undefined);
});

describe('current count sheet upload', () => {
  it('previews without a blank Location column and leaves saving to the user', async () => {
    await upload(0, { ...count, items: count.items.map(item => ({ ...item, locationName: '' })) });
    await screen.findByText(/2 quantities to save/);
    expect(screen.getAllByRole('columnheader')).toHaveLength(4);
    expect(screen.queryByRole('columnheader', { name: 'Location' })).not.toBeInTheDocument();
    fireEvent.click(screen.getAllByRole('button', { name: 'Close', exact: true })[0]);
    expect(service.importPhysicalCountSheet).not.toHaveBeenCalled();
    expect(onSaved).not.toHaveBeenCalled();
  });
  it('saves quantities and the current source through one atomic request', async () => {
    const file = await upload();
    await screen.findByText(/2 quantities to save/);
    expect(service.importPhysicalCountSheet).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Save count sheet' }));
    await waitFor(() => expect(onClose).toHaveBeenCalledOnce());
    expect(service.importPhysicalCountSheet).toHaveBeenCalledExactlyOnceWith('c', file, 'count-rv', [
      { id: 'a', rowVersion: 'rv1' }, { id: 'b', rowVersion: 'rv2' },
    ], expect.any(String));
    expect(onSaved).toHaveBeenCalledOnce();
  });
  it('retains the file on failure and retries the identical operation', async () => {
    vi.mocked(service.importPhysicalCountSheet).mockRejectedValueOnce(new Error('Connection lost'));
    await upload(); await screen.findByText(/2 quantities to save/);
    fireEvent.click(screen.getByRole('button', { name: 'Save count sheet' }));
    await screen.findByRole('alert');
    expect(onClose).not.toHaveBeenCalled();
    expect(onSaved).not.toHaveBeenCalled();
    expect(screen.getByRole('alert')).toHaveTextContent('same upload reference');
    const original = vi.mocked(service.importPhysicalCountSheet).mock.calls[0];
    fireEvent.click(screen.getByRole('button', { name: 'Retry save' }));
    await waitFor(() => expect(onClose).toHaveBeenCalledOnce());
    expect(service.importPhysicalCountSheet).toHaveBeenCalledTimes(2);
    expect(vi.mocked(service.importPhysicalCountSheet).mock.calls[1]).toEqual(original);
  });
  it('rejects negative quantities before any writes', async () => {
    await upload(-1); await screen.findByRole('alert');
    expect(screen.getByRole('button', { name: 'Save count sheet' })).toBeDisabled();
    expect(service.importPhysicalCountSheet).not.toHaveBeenCalled();
  });
  it('rejects replacements that blank previously saved quantities', async () => {
    await upload('', { ...count, items: count.items.map(item => ({ ...item, isCounted: true })) });
    expect(await screen.findByRole('alert')).toHaveTextContent('cannot omit a saved count');
    expect(service.importPhysicalCountSheet).not.toHaveBeenCalled();
  });
  it('rejects a missing item row before any writes', async () => {
    await upload(1, count, true);
    expect(await screen.findByRole('alert')).toHaveTextContent('Keep every item row');
    expect(service.importPhysicalCountSheet).not.toHaveBeenCalled();
  });
});
