import React, { useState } from 'react';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PhysicalCountCounterSelector } from './PhysicalCountCounterSelector';
import { PhysicalCountCountersPanel } from './PhysicalCountCountersPanel';
import { inventoryManagementService as service, type PhysicalCountDetailDto, type PhysicalCountCounterOptionDto } from '@/services/inventoryManagementService';

vi.mock('sonner', () => ({ toast: { success: vi.fn() } }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: {
  getPhysicalCountCounterOptions: vi.fn(), updatePhysicalCountCounters: vi.fn(),
} }));
const ama: PhysicalCountCounterOptionDto = { employeeId: 'employee-1', employeeNumber: 'EMP-001', employeeName: 'Ama Counter', userId: 'user-1', canAssign: true, hasEmail: true };
const kojo: PhysicalCountCounterOptionDto = { ...ama, employeeId: 'employee-2', employeeNumber: 'EMP-002', employeeName: 'Kojo Counter', userId: 'user-2' };
const blocked: PhysicalCountCounterOptionDto = { ...ama, employeeId: 'employee-3', employeeNumber: 'EMP-003', employeeName: 'Inactive Link', userId: undefined, canAssign: false, ineligibilityReason: 'No active linked user.' };
function Selector({ warehouseId = 'warehouse-1', locationId = 'bin-1' }: { warehouseId?: string; locationId?: string }) {
  const [selected, setSelected] = useState<PhysicalCountCounterOptionDto[]>([]);
  return <PhysicalCountCounterSelector warehouseId={warehouseId} locationId={locationId} selected={selected} onChange={setSelected} />;
}
const detail = (changes: Partial<PhysicalCountDetailDto> = {}): PhysicalCountDetailDto => ({
  id: 'count-1', warehouseId: 'warehouse-1', locationId: 'bin-1', status: 'Draft', rowVersion: 'AQID', canManageCounters: true,
  counters: [{ id: 'assignment-1', ...ama, userId: 'user-1', isActive: true, assignedById: 'manager', assignedAtUtc: '2026-09-27T12:00:00Z', emailNotificationQueued: true }],
  ...changes,
} as PhysicalCountDetailDto);
beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(service.getPhysicalCountCounterOptions).mockResolvedValue([ama, kojo, blocked]);
  vi.mocked(service.updatePhysicalCountCounters).mockResolvedValue();
});

describe('physical count employee committee', () => {
  it('uses exact bin scope and lets the manager select multiple named employees and remove one', async () => {
    render(<Selector />);
    fireEvent.click(await screen.findByRole('checkbox', { name: 'Select Ama Counter (EMP-001)' }));
    fireEvent.click(screen.getByRole('checkbox', { name: 'Select Kojo Counter (EMP-002)' }));
    expect(service.getPhysicalCountCounterOptions).toHaveBeenCalledWith('warehouse-1', '', 'bin-1');
    expect(within(screen.getByRole('list', { name: 'Selected counters' })).getAllByRole('listitem')).toHaveLength(2);
    fireEvent.click(screen.getByRole('button', { name: 'Remove Ama Counter' }));
    expect(screen.getByRole('checkbox', { name: 'Select Ama Counter (EMP-001)' })).not.toBeChecked();
    expect(within(screen.getByRole('list', { name: 'Selected counters' })).getAllByRole('listitem')).toHaveLength(1);
  });

  it('disables ineligible employees and explains why without allowing free-text assignment', async () => {
    render(<Selector />);
    expect(await screen.findByRole('checkbox', { name: 'Select Inactive Link (EMP-003)' })).toBeDisabled();
    expect(screen.getByText('No active linked user.')).toBeInTheDocument();
    fireEvent.change(screen.getByPlaceholderText('Search employee name or number'), { target: { value: 'someone@example.com' } });
    await waitFor(() => expect(service.getPhysicalCountCounterOptions).toHaveBeenLastCalledWith('warehouse-1', 'someone@example.com', 'bin-1'));
    expect(screen.queryByRole('list', { name: 'Selected counters' })).not.toBeInTheDocument();
  });

  it('caps a committee at 100 employees while allowing an existing selection to be removed', async () => {
    const selected = Array.from({ length: 100 }, (_, index) => ({ ...ama, employeeId: index ? `selected-${index}` : ama.employeeId }));
    const onChange = vi.fn();
    render(<PhysicalCountCounterSelector warehouseId="warehouse-1" selected={selected} onChange={onChange} />);
    expect(await screen.findByRole('checkbox', { name: 'Select Kojo Counter (EMP-002)' })).toBeDisabled();
    expect(screen.getByRole('checkbox', { name: 'Select Ama Counter (EMP-001)' })).toBeEnabled();
    fireEvent.click(screen.getByRole('checkbox', { name: 'Select Ama Counter (EMP-001)' }));
    expect(onChange.mock.calls[0][0]).toHaveLength(99);
  });

  it('ignores stale employee results from a previous warehouse', async () => {
    let resolveFirst!: (rows: PhysicalCountCounterOptionDto[]) => void;
    vi.mocked(service.getPhysicalCountCounterOptions).mockImplementation(warehouseId => warehouseId === 'warehouse-1'
      ? new Promise(resolve => { resolveFirst = resolve; }) : Promise.resolve([kojo]));
    const { rerender } = render(<Selector />);
    await waitFor(() => expect(service.getPhysicalCountCounterOptions).toHaveBeenCalledOnce());
    rerender(<Selector warehouseId="warehouse-2" locationId="bin-2" />);
    expect(await screen.findByRole('checkbox', { name: 'Select Kojo Counter (EMP-002)' })).toBeInTheDocument();
    await act(async () => resolveFirst([ama]));
    expect(screen.queryByRole('checkbox', { name: 'Select Ama Counter (EMP-001)' })).not.toBeInTheDocument();
  });

  it('shows the server problem and retries lookup while preserving the selected committee', async () => {
    render(<Selector />);
    fireEvent.click(await screen.findByRole('checkbox', { name: 'Select Ama Counter (EMP-001)' }));
    vi.mocked(service.getPhysicalCountCounterOptions).mockRejectedValueOnce({ isAxiosError: true, response: { data: { detail: 'Scope unavailable.', code: 'COUNT_SCOPE' } } });
    fireEvent.change(screen.getByPlaceholderText('Search employee name or number'), { target: { value: 'Kojo' } });
    expect(await screen.findByRole('alert')).toHaveTextContent('Scope unavailable. (COUNT_SCOPE)');
    expect(screen.getByRole('button', { name: 'Remove Ama Counter' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Retry employee search' }));
    expect(await screen.findByRole('checkbox', { name: 'Select Kojo Counter (EMP-002)' })).toBeInTheDocument();
  });

  it.each([{ status: 'InProgress', canManageCounters: true }, { status: 'Draft', canManageCounters: false }])('keeps committee read-only without Draft manager eligibility (%j)', changes => {
    render(<PhysicalCountCountersPanel count={detail(changes)} onSaved={vi.fn()} />);
    expect(screen.getAllByText('Ama Counter').length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Email notification queued/).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: 'Save counters' })).not.toBeInTheDocument();
    expect(service.getPhysicalCountCounterOptions).not.toHaveBeenCalled();
  });

  it('retains removed assignments in history without adding them to the active committee', () => {
    const count = detail({ canManageCounters: false });
    count.counters!.push({ ...count.counters![0], id: 'removed-1', employeeId: kojo.employeeId, employeeName: kojo.employeeName,
      employeeNumber: kojo.employeeNumber, isActive: false, removedAtUtc: '2026-09-27T13:00:00Z', changeReason: 'Committee rotation' });
    render(<PhysicalCountCountersPanel count={count} onSaved={vi.fn()} />);
    expect(screen.getByText('Assignment history')).toBeInTheDocument();
    expect(screen.getByText('Committee rotation')).toBeInTheDocument();
    expect(screen.getAllByText('Kojo Counter')).toHaveLength(1);
    expect(count.counters).toHaveLength(2);
  });

  it('requires an investigation reason for the server-authorized initial legacy recovery committee', async () => {
    const reload = vi.fn().mockResolvedValue(undefined);
    render(<PhysicalCountCountersPanel count={detail({ status: 'UnderInvestigation', counters: [] })} onSaved={reload} />);
    fireEvent.click(await screen.findByRole('checkbox', { name: 'Select Ama Counter (EMP-001)' }));
    expect(screen.getByRole('button', { name: 'Save counters' })).toBeDisabled();
    fireEvent.change(screen.getByLabelText('Investigation reason'), { target: { value: ' Retained recount needed ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save counters' }));
    await waitFor(() => expect(reload).toHaveBeenCalledOnce());
    expect(service.updatePhysicalCountCounters).toHaveBeenCalledWith('count-1', expect.objectContaining({
      employeeIds: ['employee-1'], comment: 'Retained recount needed', rowVersion: 'AQID',
    }));
  });

  it('does not offer investigation recovery without server permission', () => {
    render(<PhysicalCountCountersPanel count={detail({ status: 'UnderInvestigation', canManageCounters: false, counters: [] })} onSaved={vi.fn()} />);
    expect(screen.queryByRole('button', { name: 'Save counters' })).not.toBeInTheDocument();
    expect(service.getPhysicalCountCounterOptions).not.toHaveBeenCalled();
  });

  it('requires at least one counter, sends row version, and reloads after the 204 save', async () => {
    const reload = vi.fn().mockResolvedValue(undefined);
    render(<PhysicalCountCountersPanel count={detail()} onSaved={reload} />);
    fireEvent.click(screen.getByRole('button', { name: 'Remove Ama Counter' }));
    expect(screen.getByRole('button', { name: 'Save counters' })).toBeDisabled();
    fireEvent.click(await screen.findByRole('checkbox', { name: 'Select Kojo Counter (EMP-002)' }));
    fireEvent.change(screen.getByLabelText('Assignment note (optional)'), { target: { value: ' Rotation ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save counters' }));
    await waitFor(() => expect(reload).toHaveBeenCalledOnce());
    expect(service.updatePhysicalCountCounters).toHaveBeenCalledWith('count-1', {
      employeeIds: ['employee-2'], rowVersion: 'AQID', idempotencyKey: expect.any(String), comment: 'Rotation',
    });
  });

  it('retains edits and the same idempotency key when an unchanged failed save is retried', async () => {
    vi.mocked(service.updatePhysicalCountCounters).mockRejectedValueOnce({ isAxiosError: true, response: { data: { detail: 'Notification queue unavailable.', extensions: { code: 'COUNT_SAVE' } } } });
    render(<PhysicalCountCountersPanel count={detail()} onSaved={vi.fn().mockResolvedValue(undefined)} />);
    fireEvent.click(await screen.findByRole('checkbox', { name: 'Select Kojo Counter (EMP-002)' }));
    fireEvent.change(screen.getByLabelText('Assignment note (optional)'), { target: { value: 'Two counters' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save counters' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Notification queue unavailable. (COUNT_SAVE)');
    expect(screen.getByLabelText('Assignment note (optional)')).toHaveValue('Two counters');
    expect(screen.getByRole('button', { name: 'Remove Kojo Counter' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Save counters' }));
    await waitFor(() => expect(service.updatePhysicalCountCounters).toHaveBeenCalledTimes(2));
    expect(vi.mocked(service.updatePhysicalCountCounters).mock.calls[1][1]).toEqual(vi.mocked(service.updatePhysicalCountCounters).mock.calls[0][1]);
  });
});
