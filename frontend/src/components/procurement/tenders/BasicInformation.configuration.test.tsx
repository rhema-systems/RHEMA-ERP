import React from 'react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { act, cleanup, render, screen, waitFor } from '@testing-library/react';
import BasicInformation from './BasicInformation';
import type { TenderFormData } from '@/app/procurement/tenders/new/page';

const api = vi.hoisted(() => ({ dropdown: vi.fn(), details: vi.fn() }));
vi.mock('@/services/evaluationTemplateService', () => ({
  evaluationTemplateService: { getForDropdown: api.dropdown, getById: api.details },
}));
const template = (id: string, scoringMethod: string) => ({
  id, scoringMethod, templateName: id, templateCode: id, criteria: [], criteriaCount: 0,
  technicalWeight: 70, financialWeight: 30, minimumTechnicalScore: 80,
});
const form = (evaluationTemplateId: string) => ({
  title: 'Test', tenderType: 'ITB', description: '', currency: 'GHS', estimatedValue: 0,
  submissionDeadline: '', openingDate: '', evaluationTemplateId, useQCBSEvaluation: true,
  technicalWeight: 70, financialWeight: 30, minimumTechnicalScore: 80,
} as TenderFormData);
beforeEach(() => {
  vi.stubGlobal('React', React);
  vi.clearAllMocks();
  api.dropdown.mockResolvedValue([{ id: 'q', templateName: 'QCBS' }, { id: 'w', templateName: 'Weighted' }]);
  api.details.mockImplementation(async (id: string) => template(id, id === 'q' ? 'QCBS' : 'WeightedAverage'));
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('synchronizes both directions and prevents independently toggling an assigned method', async () => {
  const update = vi.fn();
  const rendered = render(<BasicInformation formData={form('q')} updateFormData={update} />);
  await waitFor(() => expect(update).toHaveBeenCalledWith(expect.objectContaining({ useQCBSEvaluation: true })));
  expect((screen.getByRole('checkbox', { name: 'Use QCBS Evaluation Method' }) as HTMLButtonElement).disabled).toBe(true);
  expect((screen.getByLabelText('Technical Weight (%)') as HTMLInputElement).readOnly).toBe(true);
  update.mockClear();
  rendered.rerender(<BasicInformation formData={form('w')} updateFormData={update} />);
  await waitFor(() => expect(update).toHaveBeenCalledWith(expect.objectContaining({ useQCBSEvaluation: false })));
});

it('does not let a late previous selection overwrite the new template settings', async () => {
  let resolveOld!: (value: ReturnType<typeof template>) => void;
  api.details.mockImplementation((id: string) => id === 'q'
    ? new Promise(resolve => { resolveOld = resolve; })
    : Promise.resolve(template('w', 'WeightedAverage')));
  const update = vi.fn();
  const rendered = render(<BasicInformation formData={form('q')} updateFormData={update} />);
  await waitFor(() => expect(api.details).toHaveBeenCalledWith('q'));
  rendered.rerender(<BasicInformation formData={form('w')} updateFormData={update} />);
  await waitFor(() => expect(update).toHaveBeenCalledWith(expect.objectContaining({ useQCBSEvaluation: false })));
  update.mockClear();
  await act(async () => resolveOld(template('q', 'QCBS')));
  expect(update).not.toHaveBeenCalled();
});

it('clears the loading state when selection is removed during a request', async () => {
  api.details.mockReturnValue(new Promise(() => {}));
  const update = vi.fn();
  const rendered = render(<BasicInformation formData={form('q')} updateFormData={update} />);
  expect(await screen.findByText('Loading template details...')).toBeTruthy();
  rendered.rerender(<BasicInformation formData={form('')} updateFormData={update} />);
  await waitFor(() => expect(screen.queryByText('Loading template details...')).toBeNull());
});
