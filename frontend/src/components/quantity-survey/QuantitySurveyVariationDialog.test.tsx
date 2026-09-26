import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { QuantitySurveyVariationDialog } from './QuantitySurveyVariationDialog';
import { quantitySurveyVariationService as service } from '@/services/quantity-survey-variation.service';

vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: () => true }) }));
vi.mock('@/services/quantity-survey-variation.service', () => ({ quantitySurveyVariationService: { workspace: vi.fn(), save: vi.fn() } }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
afterEach(() => { cleanup(); vi.clearAllMocks(); });
Object.defineProperty(Element.prototype, 'scrollIntoView', { configurable: true, value: vi.fn() });
Object.defineProperty(Element.prototype, 'hasPointerCapture', { configurable: true, value: () => false });
Object.defineProperty(Element.prototype, 'setPointerCapture', { configurable: true, value: vi.fn() });
Object.defineProperty(Element.prototype, 'releasePointerCapture', { configurable: true, value: vi.fn() });

async function open() {
  vi.mocked(service.workspace).mockResolvedValue({ contracts: [{ id: 'contract', number: 'CTR-UAT', title: 'Works', contractorId: 'partner', contractor: 'UAT Contractor', contractSum: 10000, currency: 'GHS' }],
    siteInstructions: [], changeRequests: [], variations: [], boqLines: [{ id: 'line', versionId: 'boq', lineKey: 'key', reference: '1.01', description: 'Excavation', quantity: 10, unitRate: 1000, currency: 'GHS' }] });
  render(<QuantitySurveyVariationDialog projectId="project" />);
  fireEvent.click(screen.getByRole('button', { name: 'Governed variation' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Refresh' })).toBeEnabled());
}
async function choose(label: string, option: string) {
  const picker = within(screen.getByText(label).parentElement!).getByRole('combobox');
  fireEvent.keyDown(picker, { key: 'Enter' });
  fireEvent.click(await screen.findByRole('option', { name: option }));
}
function fill(label: string, value: string, role = 'textbox') {
  fireEvent.change(within(screen.getByText(label).parentElement!).getByRole(role), { target: { value } });
}

it('blocks a missing governed source with persistent guidance instead of submitting an empty GUID', async () => {
  await open();
  await choose('Source type', 'Site instruction');
  expect(screen.getByDisplayValue('No eligible source record available')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Save Draft' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Select an available governed source record');
  expect(service.save).not.toHaveBeenCalled();
});

it('hides optional daywork types from a fresh standard variation', async () => {
  await open();
  fireEvent.keyDown(within(screen.getByText('Commercial record type').parentElement!).getByRole('combobox'), { key: 'Enter' });
  expect(await screen.findByRole('option', { name: 'Scope Change' })).toBeInTheDocument();
  expect(screen.queryByRole('option', { name: 'Daywork' })).not.toBeInTheDocument();
  expect(screen.queryByRole('option', { name: 'Additional Work' })).not.toBeInTheDocument();
});

it('saves direct variations with null source references and preserves input and request identity on failure', async () => {
  await open();
  await choose('Active Works contract', 'CTR-UAT · UAT Contractor');
  await choose('Approved BoQ line', '1.01 · Excavation');
  fill('Title', 'UAT additional excavation');
  fill('Reason and scope', 'Synthetic test of one additional unit');
  fill('Quantity change', '1', 'spinbutton');
  fill('Line valuation reason', 'Approved BOQ rate');
  vi.mocked(service.save).mockRejectedValue(new Error('Configured evidence is unavailable.'));
  fireEvent.click(screen.getByRole('button', { name: 'Save Draft' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Configured evidence is unavailable.');
  expect(screen.getByDisplayValue('UAT additional excavation')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Save Draft' }));
  await waitFor(() => expect(service.save).toHaveBeenCalledTimes(2));
  expect(vi.mocked(service.save).mock.calls[0][1]).toMatchObject({ sourceType: 'directVariation', variationType: 'ScopeChange', siteInstructionId: null, changeRequestId: null });
  expect(vi.mocked(service.save).mock.calls[1]).toEqual(vi.mocked(service.save).mock.calls[0]);
});
