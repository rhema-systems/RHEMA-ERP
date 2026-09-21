import React from 'react';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { ProjectHandoverTab } from './ProjectHandoverTab';
import type { ProjectDetailDto, ProjectHandoverItemDto } from '@/services/projectService';

afterEach(cleanup);
const item: ProjectHandoverItemDto = { id: 'handover-1', projectId: 'project-1', title: 'UAT handover', handoverType: 'PracticalCompletion', status: 'Planned', sortOrder: 4, referenceNumber: 'UAT-PC', notes: 'Retain supporting reference' };
const props = () => ({ project: { commissioningItems: [], handoverItems: [item], unitHandoverBatches: [], buildings: [], floors: [] } as unknown as ProjectDetailDto,
  units: [], commissioningDraft: { title: '' }, setCommissioningDraft: vi.fn(), handoverDraft: { title: '' }, setHandoverDraft: vi.fn(),
  commissioningStatusOptions: ['Planned'], handoverStatusOptions: ['Planned', 'Completed'], handoverTypeOptions: ['PracticalCompletion'],
  formatCatalogLabel: (value?: string | null) => value ?? '', formatDateLabel: (value?: string) => value ?? '',
  onAddHandoverBatch: vi.fn(), onDeleteHandoverBatch: vi.fn(), onAddCommissioningItem: vi.fn(), onDeleteCommissioningItem: vi.fn(),
  editingHandoverItemId: null as string | null, onEditHandoverItem: vi.fn(), onCancelHandoverEdit: vi.fn(), onAddHandoverItem: vi.fn(), onDeleteHandoverItem: vi.fn() });
it('offers editing of the existing handover with its identity and references', () => {
  const input = props(); render(<ProjectHandoverTab {...input} />);
  fireEvent.click(screen.getByRole('button', { name: 'Edit handover' }));
  expect(input.onEditHandoverItem).toHaveBeenCalledWith(item);
  expect(input.onDeleteHandoverItem).not.toHaveBeenCalled();
});
it('shows save and cancel controls for the existing record without offering a duplicate add action', () => {
  const input = { ...props(), editingHandoverItemId: item.id, handoverDraft: { ...item } };
  render(<ProjectHandoverTab {...input} />);
  expect(screen.getByDisplayValue('UAT-PC')).toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Add Handover Item' })).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Save handover changes' }));
  expect(input.onAddHandoverItem).toHaveBeenCalledOnce();
  fireEvent.click(screen.getByRole('button', { name: 'Cancel edit' }));
  expect(input.onCancelHandoverEdit).toHaveBeenCalledOnce();
});
