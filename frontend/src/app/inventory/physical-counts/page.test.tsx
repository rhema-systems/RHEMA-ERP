import React from 'react';
vi.mock('next/navigation', () => ({ useSearchParams: () => new URLSearchParams(),
  usePathname: () => '/inventory/physical-counts', useRouter: () => ({ replace: vi.fn() }) }));
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import PhysicalCountsPage from './page';
import { inventoryManagementService as service, type PhysicalCountDetailDto } from '@/services/inventoryManagementService';
import { toast } from 'sonner';
import { procurementCurrencyService } from '@/services/financeCommonService';
import { workflowApiService } from '@/services/workflow-api.service';

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() } }));
vi.mock('@/services/financeCommonService', () => ({ procurementCurrencyService: { getActive: vi.fn().mockResolvedValue([{ code: 'GHS', isBaseCurrency: true }]) } }));
vi.mock('@/services/workflow-api.service', () => ({ workflowApiService: { getWorkflowEntitySummary: vi.fn() } }));
vi.mock('@/components/inventory/PhysicalCountControlPanel', () => ({ PhysicalCountControlPanel: () => null }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: {
  getPhysicalCounts: vi.fn(), getWarehouses: vi.fn(), getInventoryItems: vi.fn(),
  getPhysicalCountById: vi.fn(), getPhysicalCountEvidence: vi.fn(),
  getWarehouseItems: vi.fn(), getWarehouseLocations: vi.fn(), getBinStock: vi.fn(),
  addCountItem: vi.fn(), removeCountItem: vi.fn(), updatePhysicalCount: vi.fn(), recordCountItem: vi.fn(),
  startPhysicalCount: vi.fn(), completePhysicalCount: vi.fn(), uploadPhysicalCountEvidence: vi.fn(), submitReviewedPhysicalCount: vi.fn(),
  cancelPhysicalCount: vi.fn(), createPhysicalCount: vi.fn(),
} }));

let count: PhysicalCountDetailDto;
beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(workflowApiService.getWorkflowEntitySummary).mockImplementation(async (entityType, entityId) => ({
    entityType, entityId, approvalRequired: true, hasActiveInstance: false,
    canCurrentUserApprove: false, pendingApprovers: [],
  }));
  vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
  Element.prototype.scrollIntoView = vi.fn();
  count = { id: 'count-1', countNumber: 'PC-TEST', warehouseId: 'warehouse-1', warehouseName: 'Demo Warehouse',
    countType: 'FullCount', status: 'Draft', countDate: '2026-09-01', rowVersion: 'AQID', totalItems: 1, countedItems: 0, itemsWithVariance: 0,
    totalVarianceValue: 0, freezeInventory: true, blindCount: true, systemQuantityVisible: false,
    canReview: true, notes: '', evidence: [], actions: [], items: [{ id: 'line-1', inventoryItemId: 'pvc', itemCode: 'PVC',
      itemName: 'Pipe', locationName: 'Main', locationId: 'bin-1', systemQuantity: 0, countedQuantity: 0, varianceQuantity: 0,
      varianceValue: 0, variancePercent: 0, unitOfMeasure: 'EA', isCounted: false, countAttempts: 0, requiresRecount: false, rowVersion: 'BAUG' }],
  };
  vi.mocked(service.getPhysicalCounts).mockImplementation(async () => [count]);
  vi.mocked(service.getPhysicalCountById).mockImplementation(async () => count);
  vi.mocked(service.getPhysicalCountEvidence).mockResolvedValue([]);
  vi.mocked(service.getWarehouses).mockResolvedValue([]);
  vi.mocked(service.getInventoryItems).mockResolvedValue([]);
  vi.mocked(service.getWarehouseItems).mockResolvedValue([
    { warehouseId: 'warehouse-1', inventoryItemId: 'pvc', itemCode: 'PVC', itemName: 'Pipe', currentStock: 999 },
    { warehouseId: 'warehouse-1', inventoryItemId: 'kit', itemCode: 'KIT', itemName: 'Barcode kit', currentStock: 999 },
    { warehouseId: 'other', inventoryItemId: 'foreign', itemCode: 'OTHER', itemName: 'Other warehouse item' },
  ] as never);
  vi.mocked(service.getWarehouseLocations).mockResolvedValue([
    { id: 'bin-1', warehouseId: 'warehouse-1', locationCode: 'LOC-001', name: 'Main', isActive: true },
    { id: 'closed', warehouseId: 'warehouse-1', locationCode: 'CLOSED', isActive: false },
  ] as never);
  vi.mocked(service.getBinStock).mockResolvedValue({ totalCount: 3, items: [
    { warehouseId: 'warehouse-1', locationId: 'bin-1', inventoryItemId: 'pvc', itemCode: 'PVC', itemName: 'Pipe', currentStock: 999 },
    { warehouseId: 'warehouse-1', locationId: 'bin-1', inventoryItemId: 'kit', itemCode: 'KIT', itemName: 'Barcode kit', currentStock: 999 },
    { warehouseId: 'other', locationId: 'bin-other', inventoryItemId: 'foreign', itemCode: 'OTHER', itemName: 'Other warehouse item' },
  ] } as never);
  vi.mocked(service.addCountItem).mockResolvedValue({} as never);
  vi.mocked(service.removeCountItem).mockResolvedValue();
  vi.mocked(service.updatePhysicalCount).mockImplementation(async (_id, data) => { count = { ...count, ...data }; return count; });
  vi.mocked(service.createPhysicalCount).mockResolvedValue({ ...count, id: 'created-count' });
});

async function open() {
  render(<PhysicalCountsPage />);
  fireEvent.click(await screen.findByRole('button', { name: count.status === 'Draft' ? 'Edit draft' : 'View' }));
  return screen.findByRole('dialog', { name: /PC-TEST/ });
}
function tab(name: string | RegExp) { fireEvent.mouseDown(screen.getByRole('tab', { name }), { button: 0, ctrlKey: false }); }

async function choose(label: string, option: string) {
  await act(async () => { fireEvent.keyDown(screen.getByRole('combobox', { name: label }), { key: 'Enter' }); });
  const choice = await screen.findByRole('option', { name: option });
  await act(async () => { fireEvent.click(choice); });
}

async function openCreate() {
  vi.mocked(service.getWarehouses).mockResolvedValue([{ id: 'warehouse-1', name: 'Demo Warehouse' }, { id: 'warehouse-2', name: 'Second Warehouse' }] as never);
  render(<PhysicalCountsPage />);
  await screen.findByRole('button', { name: 'Edit draft' });
  fireEvent.click(screen.getByRole('button', { name: 'New Count' }));
  await choose('Warehouse *', 'Demo Warehouse');
  return screen.getByRole('dialog', { name: 'Create Physical Count' });
}

describe('physical count posting eligibility', () => {
  it.each([false, undefined])('does not offer Post without server eligibility (%s)', async canPost => {
    count = { ...count, status: 'ReadyToPost', canPost };
    await open();
    expect(screen.queryByRole('button', { name: 'Post' })).not.toBeInTheDocument();
  });

  it('offers Post to the eligible Finance actor in both dialog and full-page mode', async () => {
    count = { ...count, status: 'ReadyToPost', canPost: true };
    await open();
    expect(screen.getByRole('button', { name: 'Post' })).toBeEnabled();
    tab('Items (1)');
    fireEvent.click(await screen.findByRole('button', { name: 'View items in full page' }));
    expect(screen.getByRole('button', { name: 'Post' })).toBeEnabled();
  });

  it('uses the operational currency projection without requiring Finance module access', async () => {
    await open();
    expect(procurementCurrencyService.getActive).toHaveBeenCalledOnce();
  });
});

describe('physical count creation scope', () => {
  it('shows newest counts first without a separate item-count summary row', async () => {
    const rows = [
      { ...count, id: 'old', countNumber: 'PC-0001', createdAt: '2026-09-10T09:00:00Z', countDate: '2026-09-10T00:00:00Z' },
      { ...count, id: 'fallback', countNumber: 'PC-0002', countDate: '2026-09-11T00:00:00Z' },
      { ...count, id: 'new', countNumber: 'PC-0003', createdAt: '2026-09-11T10:00:00Z', countDate: '2026-09-09T00:00:00Z' },
    ];
    vi.mocked(service.getPhysicalCounts).mockResolvedValue(rows);
    render(<PhysicalCountsPage />);
    await screen.findByRole('heading', { name: 'PC-0003' });
    expect(screen.getAllByRole('heading', { name: /^PC-/ }).map(heading => heading.textContent)).toEqual(['PC-0003', 'PC-0002', 'PC-0001']);
    expect(rows.map(row => row.countNumber)).toEqual(['PC-0001', 'PC-0002', 'PC-0003']);
    expect(screen.queryByText(/Items:.*With Variance:/)).not.toBeInTheDocument();
  });

  it('defaults to warehouse-wide and omits a location from the request', async () => {
    const dialog = await openCreate();
    expect(screen.getByRole('combobox', { name: 'Count scope' })).toHaveTextContent('Warehouse-wide');
    expect(screen.queryByRole('combobox', { name: 'Location *' })).not.toBeInTheDocument();
    expect(dialog).toHaveClass('h-[640px]', 'max-h-[90dvh]', 'overflow-hidden');
    fireEvent.click(screen.getByRole('button', { name: 'Create Count' }));
    await waitFor(() => expect(service.createPhysicalCount).toHaveBeenCalledOnce());
    const request = vi.mocked(service.createPhysicalCount).mock.calls[0][0];
    expect(request.warehouseId).toBe('warehouse-1');
    expect(request).not.toHaveProperty('locationId');
    expect(service.getWarehouseLocations).not.toHaveBeenCalled();
  });

  it('requires an active location belonging to the warehouse for a location count', async () => {
    vi.mocked(service.getWarehouseLocations).mockResolvedValue([
      { id: 'bin-1', warehouseId: 'warehouse-1', locationCode: 'LOC-001', name: 'Main', isActive: true },
      { id: 'closed', warehouseId: 'warehouse-1', locationCode: 'CLOSED', isActive: false },
      { id: 'foreign', warehouseId: 'warehouse-2', locationCode: 'OTHER', isActive: true },
    ] as never);
    await openCreate();
    await choose('Count scope', 'Selected location');
    expect(screen.getByRole('button', { name: 'Create Count' })).toBeDisabled();
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Location *' })).toBeEnabled());
    fireEvent.keyDown(screen.getByRole('combobox', { name: 'Location *' }), { key: 'Enter' });
    expect(await screen.findByRole('option', { name: 'LOC-001 - Main' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'CLOSED' })).not.toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'OTHER' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('option', { name: 'LOC-001 - Main' }));
    fireEvent.click(screen.getByRole('button', { name: 'Create Count' }));
    await waitFor(() => expect(service.createPhysicalCount).toHaveBeenCalledWith(expect.objectContaining({ warehouseId: 'warehouse-1', locationId: 'bin-1' })));
  });

  it('clears the selected location when changing scope or warehouse', async () => {
    await openCreate();
    await choose('Count scope', 'Selected location');
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Location *' })).toBeEnabled());
    await choose('Location *', 'LOC-001 - Main');
    await choose('Count scope', 'Warehouse-wide');
    await choose('Count scope', 'Selected location');
    expect(screen.getByRole('button', { name: 'Create Count' })).toBeDisabled();
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Location *' })).toBeEnabled());
    await choose('Location *', 'LOC-001 - Main');
    await choose('Warehouse *', 'Second Warehouse');
    expect(screen.getByRole('button', { name: 'Create Count' })).toBeDisabled();
    expect(await screen.findByText('No active locations are available for this warehouse.')).toBeInTheDocument();
    expect(service.createPhysicalCount).not.toHaveBeenCalled();
  });

  it('ignores stale location responses after the warehouse changes', async () => {
    let resolveFirst!: (value: never) => void;
    vi.mocked(service.getWarehouseLocations).mockImplementation(warehouseId => warehouseId === 'warehouse-1'
      ? new Promise(resolve => { resolveFirst = resolve; })
      : Promise.resolve([{ id: 'bin-2', warehouseId: 'warehouse-2', locationCode: 'LOC-002', name: 'Second', isActive: true }] as never));
    await openCreate();
    await choose('Count scope', 'Selected location');
    expect(screen.getByRole('combobox', { name: 'Location *' })).toBeDisabled();
    await choose('Warehouse *', 'Second Warehouse');
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Location *' })).toBeEnabled());
    await act(async () => { resolveFirst([{ id: 'bin-1', warehouseId: 'warehouse-1', locationCode: 'LOC-001', name: 'Main', isActive: true }] as never); });
    await choose('Location *', 'LOC-002 - Second');
    fireEvent.click(screen.getByRole('button', { name: 'Create Count' }));
    await waitFor(() => expect(service.createPhysicalCount).toHaveBeenCalledWith(expect.objectContaining({ warehouseId: 'warehouse-2', locationId: 'bin-2' })));
  });

  it('shows a location load error, prevents creation and allows retry', async () => {
    vi.mocked(service.getWarehouseLocations).mockRejectedValueOnce({ isAxiosError: true, response: { data: { detail: 'Locations could not be loaded.', extensions: { code: 'LOCATION_ACCESS' } } } });
    await openCreate();
    await choose('Count scope', 'Selected location');
    expect(await screen.findByRole('alert')).toHaveTextContent('Locations could not be loaded. (LOCATION_ACCESS)');
    expect(screen.getByRole('button', { name: 'Create Count' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Location *' })).toBeEnabled());
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('preserves the selected scope and location on a server rejection', async () => {
    vi.mocked(service.createPhysicalCount).mockRejectedValueOnce({ isAxiosError: true, response: { data: { detail: 'The selected location is no longer active.', extensions: { code: 'LOCATION_INACTIVE' } } } });
    await openCreate();
    await choose('Count scope', 'Selected location');
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Location *' })).toBeEnabled());
    await choose('Location *', 'LOC-001 - Main');
    fireEvent.click(screen.getByRole('button', { name: 'Create Count' }));
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('The selected location is no longer active. (LOCATION_INACTIVE)'));
    expect(screen.getByRole('dialog', { name: 'Create Physical Count' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Location *' })).toHaveTextContent('LOC-001 - Main');
  });

  it.each([undefined, 'bin-1'])('shows the saved count scope in the register and details (%s)', async locationId => {
    count.locationId = locationId; count.locationName = locationId ? 'Main' : undefined;
    await open();
    expect(screen.getByText(locationId ? 'Selected location — Main' : 'Warehouse-wide (all locations)', { exact: false })).toBeInTheDocument();
    expect(screen.getByText(locationId ? /Scope: Location: Main/ : /Scope: Warehouse-wide/)).toBeInTheDocument();
  });
});

describe('physical count draft editing', () => {
  it('shows control history newest first without changing the original audit sequence', async () => {
    count.actions = [1, 3, 2].map(sequence => ({ id: `action-${sequence}`, sequence, actionType: 'CountRecorded', actorRole: 'Counter', occurredAtUtc: '2026-09-11T10:00:00Z', comment: `Event ${sequence}`, integrityHash: `hash-${sequence}` })) as PhysicalCountDetailDto['actions'];
    await open(); tab('Control history');
    expect(within(screen.getByRole('tabpanel')).getAllByText(/^#\d+ CountRecorded$/).map(element => element.textContent)).toEqual(['#3 CountRecorded', '#2 CountRecorded', '#1 CountRecorded']);
    expect(count.actions.map(action => action.sequence)).toEqual([1, 3, 2]);
  });
  it('requires a reason and explicit confirmation before cancelling the selected draft', async () => {
    vi.mocked(service.cancelPhysicalCount).mockImplementation(async () => { count = { ...count, status: 'Cancelled' }; });
    render(<PhysicalCountsPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Cancel draft count PC-TEST' }));
    const popup = screen.getByRole('dialog', { name: 'Cancel draft count?' });
    const confirm = within(popup).getByRole('button', { name: 'Cancel count' });
    expect(confirm).toBeDisabled();
    expect(service.cancelPhysicalCount).not.toHaveBeenCalled();
    fireEvent.change(within(popup).getByLabelText('Cancellation reason'), { target: { value: '   ' } });
    expect(confirm).toBeDisabled();
    fireEvent.change(within(popup).getByLabelText('Cancellation reason'), { target: { value: '  Duplicate draft  ' } });
    fireEvent.click(confirm);
    await waitFor(() => expect(service.cancelPhysicalCount).toHaveBeenCalledWith('count-1', 'Duplicate draft'));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Cancel draft count?' })).not.toBeInTheDocument());
    expect(screen.queryByRole('button', { name: 'Cancel draft count PC-TEST' })).not.toBeInTheDocument();
    expect(toast.success).toHaveBeenCalledWith('Draft count cancelled.');
    expect(service.startPhysicalCount).not.toHaveBeenCalled();
  });
  it('keeps the draft when the cancellation popup is dismissed', async () => {
    render(<PhysicalCountsPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Cancel draft count PC-TEST' }));
    fireEvent.click(screen.getByRole('button', { name: 'Keep draft' }));
    expect(screen.queryByRole('dialog', { name: 'Cancel draft count?' })).not.toBeInTheDocument();
    expect(service.cancelPhysicalCount).not.toHaveBeenCalled();
  });
  it('preserves the cancellation reason and shows server detail on failure', async () => {
    vi.mocked(service.cancelPhysicalCount).mockRejectedValueOnce({ isAxiosError: true, response: { data: { detail: 'This count has already started approval.', extensions: { code: 'COUNT_CHANGED' } } } });
    render(<PhysicalCountsPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Cancel draft count PC-TEST' }));
    fireEvent.change(screen.getByLabelText('Cancellation reason'), { target: { value: 'Duplicate draft' } });
    fireEvent.click(screen.getByRole('button', { name: 'Cancel count' }));
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('This count has already started approval. (COUNT_CHANGED)'));
    expect(screen.getByLabelText('Cancellation reason')).toHaveValue('Duplicate draft');
    expect(screen.getByRole('button', { name: 'Cancel count' })).toBeEnabled();
  });
  it.each(['InProgress', 'UnderReview', 'PendingStoresApproval', 'ReadyToPost', 'Posted', 'Cancelled'])('hides the draft cancellation icon for %s', async status => {
    count.status = status; render(<PhysicalCountsPage />);
    await screen.findByRole('button', { name: 'View' });
    expect(screen.queryByRole('button', { name: 'Cancel draft count PC-TEST' })).not.toBeInTheDocument();
  });
  it('separates the count-sheet Upload from supporting files and preserves a failed attachment', async () => {
    count.status = 'InProgress';
    vi.mocked(service.uploadPhysicalCountEvidence).mockRejectedValueOnce({ isAxiosError: true, response: { data: {
      detail: 'Upload rejected because a clean virus-scan result was not obtained.', extensions: { code: 'FILE_VIRUS_SCAN_INCOMPLETE' },
    } } });
    await open();
    expect(screen.queryByRole('button', { name: 'Upload evidence' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Upload' })).toBeEnabled();
    fireEvent.click(screen.getByText(/Supporting files and upload history/));
    const file = new File(['count evidence'], 'counts.xlsx');
    fireEvent.change(screen.getByLabelText('Supporting evidence file'), { target: { files: [file] } });
    fireEvent.click(screen.getByRole('button', { name: 'Attach supporting file' }));
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(expect.stringContaining('FILE_VIRUS_SCAN_INCOMPLETE')));
    expect(screen.getByRole('button', { name: 'Upload' })).toBeEnabled();
    expect(service.uploadPhysicalCountEvidence).toHaveBeenCalledWith('count-1', file, '');
    expect(service.completePhysicalCount).not.toHaveBeenCalled();
  });
  it('keeps uncounted inputs blank and distinguishes clearing from entering zero', async () => {
    count.status = 'InProgress';
    await open(); tab('Items (1)');
    const input = screen.getByRole('spinbutton', { name: 'Counted quantity PVC' });
    expect(input).toHaveValue(null);
    expect(screen.getByRole('button', { name: 'Save Counts' })).toBeDisabled();
    fireEvent.change(input, { target: { value: '0' } });
    expect(screen.getByRole('button', { name: 'Save Counts' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Upload count sheet' })).toBeDisabled();
    fireEvent.change(input, { target: { value: '' } });
    expect(screen.getByRole('button', { name: 'Save Counts' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Upload count sheet' })).toBeEnabled();
  });

  it('shows Saved instead of a fake zero for a protected saved count', async () => {
    count.status = 'InProgress'; count.items[0].isCounted = true;
    await open(); tab('Items (1)');
    expect(screen.getByPlaceholderText('Saved')).toHaveValue(null);
  });

  it('saves edited quantities after switching to Details and keeps a disabled Save button after success', async () => {
    count.status = 'UnderReview'; count.systemQuantityVisible = true; count.items[0].isCounted = true;
    await open(); tab('Items (1)');
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Counted quantity PVC' }), { target: { value: '23' } });
    tab('Details');
    fireEvent.click(screen.getByRole('button', { name: 'Save Counts' }));
    await waitFor(() => expect(service.recordCountItem).toHaveBeenCalledWith('count-1', expect.objectContaining({ physicalCountItemId: 'line-1', countedQuantity: 23 })));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Save Counts' })).toBeDisabled());
    expect(toast.success).toHaveBeenCalledWith('Count quantities saved.');
    expect(service.completePhysicalCount).not.toHaveBeenCalled();
  });

  it('keeps Excel upload optional after saved manual corrections', async () => {
    count.status = 'UnderReview'; count.countedItems = 1;
    vi.mocked(service.getPhysicalCountEvidence).mockResolvedValue([{ isImportedCountSheet: true, isCurrentCountSheet: false, centralDocumentVersionId: 'sheet-v1', uploadedAtUtc: '2026-09-11T00:00:00Z' }] as never);
    await open(); tab('Items (1)');
    expect(screen.getByRole('button', { name: 'Submit for approval' })).toBeEnabled();
    expect(screen.queryByText(/Upload a revised count sheet before/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Upload count sheet' }));
    const upload = await screen.findByRole('dialog', { name: 'Upload count sheet' });
    expect(within(upload).getByLabelText('Completed Excel count sheet')).toBeInTheDocument();
    expect(within(upload).getByRole('button', { name: 'Save count sheet' })).toBeDisabled();
    expect(service.recordCountItem).not.toHaveBeenCalled();
  });

  it('searches and saves a quantity in full page without requiring another Excel upload', async () => {
    count.status = 'UnderReview'; count.systemQuantityVisible = true; count.countedItems = 1; count.items[0].isCounted = true;
    vi.mocked(service.getPhysicalCountEvidence).mockResolvedValue([{ isImportedCountSheet: true, isCurrentCountSheet: false, centralDocumentVersionId: 'old-sheet', uploadedAtUtc: '2026-09-11T00:00:00Z' }] as never);
    await open(); tab('Items (1)');
    fireEvent.click(screen.getByRole('button', { name: 'View items in full page' }));
    fireEvent.change(screen.getByRole('textbox', { name: 'Search count items' }), { target: { value: 'pvc' } });
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Counted quantity PVC' }), { target: { value: '23' } });
    const footer = screen.getByTestId('count-action-footer');
    expect(footer).not.toHaveClass('hidden');
    const fullPageSave = within(footer).getByRole('button', { name: 'Save Counts' });
    const fullPageSubmit = within(footer).getByRole('button', { name: 'Submit for approval' });
    expect(fullPageSubmit).toHaveClass('bg-destructive', 'text-white');
    expect(fullPageSubmit).toBeDisabled();
    expect(screen.getAllByRole('button', { name: 'Save Counts' })).toHaveLength(1);
    fireEvent.click(fullPageSave);
    await waitFor(() => expect(service.recordCountItem).toHaveBeenCalledWith('count-1', expect.objectContaining({ countedQuantity: 23 })));
    await waitFor(() => expect(fullPageSave).toBeDisabled());
    expect(fullPageSubmit).toBeEnabled();
    fireEvent.click(screen.getByRole('button', { name: 'Restore dialog' }));
    expect(screen.getByRole('button', { name: 'Submit for approval' })).toBeEnabled();
    expect(service.uploadPhysicalCountEvidence).not.toHaveBeenCalled();
  });

  it('confirms submission from full page and retains the count when the server rejects it', async () => {
    count.status = 'UnderReview'; count.countedItems = 1; count.rowVersion = 'v1';
    vi.mocked(service.getPhysicalCountEvidence).mockResolvedValue([{ centralDocumentVersionId: 'sheet-v1', uploadedAtUtc: '2026-09-11T00:00:00Z' }] as never);
    vi.mocked(service.submitReviewedPhysicalCount).mockRejectedValueOnce(new Error('Adjustment validation failed.'));
    await open(); tab('Items (1)');
    fireEvent.click(screen.getByRole('button', { name: 'View items in full page' }));
    const footer = screen.getByTestId('count-action-footer');
    expect(footer).toHaveClass('shrink-0');
    expect(screen.getByRole('tabpanel')).not.toContainElement(footer);
    fireEvent.click(within(footer).getByRole('button', { name: 'Submit for approval' }));
    const confirmation = await screen.findByRole('dialog', { name: 'Submit reviewed count?' });
    expect(service.submitReviewedPhysicalCount).not.toHaveBeenCalled();
    const confirm = within(confirmation).getByRole('button', { name: 'Submit for approval' });
    expect(confirm).toHaveClass('bg-destructive', 'text-white');
    fireEvent.click(confirm);
    await waitFor(() => expect(service.submitReviewedPhysicalCount).toHaveBeenCalledWith('count-1', expect.objectContaining({ rowVersion: 'v1' })));
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Adjustment validation failed.'));
    expect(confirmation).toBeInTheDocument();
    expect(count.status).toBe('UnderReview');
    expect(service.recordCountItem).not.toHaveBeenCalled();
  });

  it('does not expose submission to a full-page viewer without review permission', async () => {
    count.status = 'UnderReview'; count.canReview = false;
    await open(); tab('Items (1)');
    fireEvent.click(screen.getByRole('button', { name: 'View items in full page' }));
    expect(screen.queryByRole('button', { name: 'Submit for approval' })).not.toBeInTheDocument();
  });

  it('uses an accessible eye-only View action in the register', async () => {
    count.status = 'UnderReview'; render(<PhysicalCountsPage />);
    const view = await screen.findByRole('button', { name: 'View' });
    expect(view).toHaveTextContent('');
    expect(view.querySelector('svg')).toBeInTheDocument();
    expect(view).toHaveAttribute('title', 'View count');
  });

  it('keeps three tabs inside the same fixed, scrolling frame without a separate Variance tab', async () => {
    const dialog = await open();
    expect(screen.queryByRole('tab', { name: 'Variance' })).not.toBeInTheDocument();
    expect(screen.getAllByRole('tab')).toHaveLength(3);
    for (const name of ['Details', 'Items (1)', 'Control history']) {
      tab(name);
      expect(dialog).toHaveClass('flex', 'flex-col', 'h-[680px]', 'max-h-[90dvh]', 'overflow-hidden');
      expect(screen.getByRole('tablist')).toHaveClass('shrink-0');
      expect(screen.getByRole('tablist').parentElement).toHaveClass('min-h-0', 'flex-1', 'flex-col');
      expect(screen.getByRole('tabpanel')).toHaveClass('min-h-0', 'flex-1', name.startsWith('Items') ? 'overflow-hidden' : 'overflow-y-auto');
      const footer = within(dialog).getAllByRole('button', { name: 'Close' })[0].parentElement;
      expect(footer).toHaveClass('shrink-0');
      expect(screen.getByRole('tabpanel')).not.toContainElement(footer!);
    }
  });

  it('expands the Items view and restores the dialog without losing unsaved quantities', async () => {
    count.status = 'UnderReview'; count.systemQuantityVisible = true; count.items[0].isCounted = true;
    const dialog = await open(); tab('Items (1)');
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Counted quantity PVC' }), { target: { value: '23' } });
    fireEvent.click(screen.getByRole('button', { name: 'View items in full page' }));
    expect(dialog).toHaveClass('!h-dvh', '!w-screen', '!max-w-none', '!left-0', '!top-0');
    expect(screen.getByRole('tablist', { hidden: true })).toHaveClass('hidden');
    expect(screen.getByRole('spinbutton', { name: 'Counted quantity PVC' })).toHaveValue(23);
    fireEvent.click(screen.getByRole('button', { name: 'Restore dialog' }));
    expect(dialog).toHaveClass('h-[680px]', 'max-h-[90dvh]');
    expect(dialog).not.toHaveClass('!h-dvh');
    expect(screen.getByRole('tablist')).not.toHaveClass('hidden');
    expect(screen.getByRole('spinbutton', { name: 'Counted quantity PVC' })).toHaveValue(23);
    expect(screen.getByRole('button', { name: 'Save Counts' })).toBeEnabled();
    expect(service.completePhysicalCount).not.toHaveBeenCalled();
  });

  it('saves optional draft notes without starting the count', async () => {
    await open();
    fireEvent.change(screen.getByLabelText('Notes (optional)'), { target: { value: 'Two-item count' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save draft notes' }));
    await waitFor(() => expect(service.updatePhysicalCount).toHaveBeenCalledWith('count-1', { notes: 'Two-item count' }));
    expect(service.startPhysicalCount).not.toHaveBeenCalled();
  });

  it('confirms removal, refreshes the saved list and leaves stock counting untouched', async () => {
    vi.mocked(service.removeCountItem).mockImplementation(async () => { count = { ...count, items: [], totalItems: 0 }; });
    await open(); tab('Items (1)');
    fireEvent.click(screen.getByRole('button', { name: 'Remove PVC' }));
    expect(service.removeCountItem).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Remove item' }));
    await screen.findByText('No items in this count. Add an item before starting.');
    expect(service.removeCountItem).toHaveBeenCalledExactlyOnceWith('count-1', 'line-1');
    expect(service.startPhysicalCount).not.toHaveBeenCalled();
    expect(service.completePhysicalCount).not.toHaveBeenCalled();
  });

  it('retains the removal dialog and the row after an error, and allows retry', async () => {
    vi.mocked(service.removeCountItem).mockRejectedValueOnce(new Error('Count already started. Reload it.'));
    await open(); tab('Items (1)');
    fireEvent.click(screen.getByRole('button', { name: 'Remove PVC' }));
    fireEvent.click(screen.getByRole('button', { name: 'Remove item' }));
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Count already started. Reload it.'));
    expect(screen.getByRole('dialog', { name: 'Remove count item?' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Remove item' })).toBeEnabled());
    fireEvent.click(screen.getByRole('button', { name: 'Remove item' }));
    await waitFor(() => expect(service.removeCountItem).toHaveBeenCalledTimes(2));
  });

  it.each(['InProgress', 'PendingStoresApproval', 'Posted'])('hides draft editing after start (%s)', async status => {
    count.status = status;
    await open(); tab('Items (1)');
    expect(screen.queryByRole('button', { name: 'Add item' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Remove PVC' })).not.toBeInTheDocument();
    tab('Details');
    expect(screen.queryByLabelText('Notes (optional)')).not.toBeInTheDocument();
  });

  it.each([
    ['Count lines can only be added before the count starts.', 'Count lines can only be added before the count starts.'],
    [{ detail: 'Reload the changed count.', extensions: { code: 'COUNT_CHANGED' } }, 'Reload the changed count. (COUNT_CHANGED)'],
  ])('shows the server rejection when saving draft notes (%j)', async (data, expected) => {
    vi.mocked(service.updatePhysicalCount).mockRejectedValueOnce({ isAxiosError: true, response: { data } });
    await open();
    fireEvent.change(screen.getByLabelText('Notes (optional)'), { target: { value: 'Keep this note' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save draft notes' }));
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(expected));
    expect(screen.getByLabelText('Notes (optional)')).toHaveValue('Keep this note');
    expect(screen.getByRole('button', { name: 'Save draft notes' })).toBeEnabled();
  });

  it('explains that draft preparation needs no file, but completing a count still does', async () => {
    await open();
    expect(screen.getByText(/No file needed to prepare this draft/)).toBeInTheDocument();
  });

  it('requires review of all quantities instead of automatic completion', async () => {
    count.status = 'InProgress';
    await open();
    expect(screen.queryByRole('button', { name: 'Complete Count' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Review variance' })).toBeDisabled();
    expect(screen.getByText(/Saved quantities are used for review/)).toBeInTheDocument();
  });

  it('marks just one sheet Current and keeps earlier files in collapsed history', async () => {
    count.status = 'InProgress';
    vi.mocked(service.getPhysicalCountEvidence).mockResolvedValue([
      { centralDocumentVersionId: 'old', fileName: 'old.xlsx', uploadedAtUtc: '2026-09-11', isImportedCountSheet: true },
      { centralDocumentVersionId: 'new', fileName: 'new.xlsx', uploadedAtUtc: '2026-09-11', isImportedCountSheet: true, isCurrentCountSheet: true },
      { centralDocumentVersionId: 'support', fileName: 'notes.pdf', uploadedAtUtc: '2026-09-11' },
    ] as never);
    await open();
    expect(await screen.findByText('new.xlsx')).toBeVisible();
    expect(screen.getAllByText('Current', { exact: true })).toHaveLength(1);
    expect(screen.getByText('old.xlsx')).not.toBeVisible();
    expect(screen.getByRole('button', { name: 'Replace' })).toBeEnabled();
    fireEvent.click(screen.getByText(/Supporting files and upload history/));
    expect(screen.getByText('Previous count sheet')).toBeVisible();
    expect(screen.getByText('Attachment only')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Review variance' })).toBeDisabled();
  });

  it('adds a warehouse item with an active location without sending system quantities', async () => {
    await open(); tab('Items (1)');
    fireEvent.click(screen.getByRole('button', { name: 'Add item' }));
    const addDialog = await screen.findByRole('dialog', { name: 'Add count item' });
    const select = within(addDialog).getByRole('combobox', { name: 'Count item' });
    expect(select).toBeDisabled();
    await waitFor(() => expect(within(addDialog).getByRole('combobox', { name: 'Location' })).toBeEnabled());
    await choose('Location', 'LOC-001 - Main');
    await waitFor(() => expect(select).toBeEnabled());
    fireEvent.click(select);
    const search = await screen.findByPlaceholderText('Search by name or code...');
    expect(search).toBeVisible();
    fireEvent.change(search, { target: { value: 'KIT' } });
    fireEvent.click(await screen.findByRole('option', { name: /Barcode kit/ }));
    expect(screen.queryByText('999')).not.toBeInTheDocument();
    const save = within(addDialog).getByRole('button', { name: 'Add item' });
    expect(save).toBeEnabled();
    fireEvent.click(save);
    await waitFor(() => expect(service.addCountItem).toHaveBeenCalledExactlyOnceWith('count-1', { inventoryItemId: 'kit', locationId: 'bin-1' }));
    expect(service.startPhysicalCount).not.toHaveBeenCalled();
  });

  it('excludes existing items and other warehouses from the add selector', async () => {
    await open(); tab('Items (1)');
    fireEvent.click(screen.getByRole('button', { name: 'Add item' }));
    const select = await screen.findByRole('combobox', { name: 'Count item' });
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Location' })).toBeEnabled());
    await choose('Location', 'LOC-001 - Main');
    await waitFor(() => expect(select).toBeEnabled());
    fireEvent.click(select);
    expect(await screen.findByRole('option', { name: /Barcode kit/ })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /Pipe|Other warehouse/ })).not.toBeInTheDocument();
  });
});
