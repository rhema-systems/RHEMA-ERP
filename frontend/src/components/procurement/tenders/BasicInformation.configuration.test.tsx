import React, { useState } from 'react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import BasicInformation from './BasicInformation';
import type { TenderFormData } from '@/app/procurement/tenders/new/page';

const api = vi.hoisted(() => ({ active: vi.fn(), details: vi.fn() }));
vi.mock('@/services/evaluationTemplateService', () => ({
  evaluationTemplateService: { getActive: api.active, getById: api.details },
}));
const template = (id: string, scoringMethod: string) => ({
  id, scoringMethod, templateName: id, templateCode: id, category: 'Goods', tenderType: 'ITB',
  isActive: true, isDefault: id === 'q', criteria: [], criteriaCount: 0, passingScore: 80, totalWeight: 100,
  technicalWeight: 70, financialWeight: 30, minimumTechnicalScore: 80,
});
const templates = [template('q', 'QCBS'), template('w', 'WeightedAverage'),
  template('s', 'SimpleAverage'), template('p', 'PassFail')];
const initialForm = {
  title: 'Test', tenderType: 'ITB', description: '', currency: 'GHS', estimatedValue: 0,
  submissionDeadline: '', openingDate: '', evaluationTemplateId: null, evaluationTemplateName: '',
  useQCBSEvaluation: false, technicalWeight: 80, financialWeight: 20, minimumTechnicalScore: 70,
} as TenderFormData;

function Harness({ initial = initialForm, category = 'Goods', onUpdate = vi.fn() }: {
  initial?: TenderFormData; category?: string; onUpdate?: (data: Partial<TenderFormData>) => void;
}) {
  const [form, setForm] = useState(initial);
  return <>
    <BasicInformation formData={form} procurementCategory={category} updateFormData={data => {
      onUpdate(data);
      setForm(current => ({ ...current, ...data }));
    }} />
    <output data-testid="form">{JSON.stringify(form)}</output>
  </>;
}
const savedForm = () => JSON.parse(screen.getByTestId('form').textContent || '{}');
async function openSelect(label: string) {
  fireEvent.keyDown(screen.getByRole('combobox', { name: label }), { key: 'ArrowDown' });
  await screen.findByRole('listbox');
}
async function choose(label: string, option: string) {
  await openSelect(label);
  await act(async () => { fireEvent.click(screen.getByRole('option', { name: option })); });
}
async function loaded() {
  await waitFor(() => expect(screen.queryByText('Loading templates...')).not.toBeInTheDocument());
}
beforeEach(() => {
  vi.stubGlobal('React', React);
  vi.clearAllMocks();
  Element.prototype.scrollIntoView = vi.fn();
  api.active.mockResolvedValue(templates);
  api.details.mockImplementation(async (id: string) => templates.find(value => value.id === id));
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('requires method first and never silently selects the default QCBS template', async () => {
  const update = vi.fn();
  render(<Harness onUpdate={update} />);
  await loaded();
  expect(screen.getByRole('combobox', { name: '2. Evaluation Template' })).toBeDisabled();
  expect(screen.getByRole('combobox', { name: '1. Evaluation Method' })).toHaveTextContent('Choose an evaluation method');
  expect(update).not.toHaveBeenCalled();
  expect(screen.queryByRole('checkbox', { name: 'Use QCBS Evaluation Method' })).not.toBeInTheDocument();
});

it('filters Standard to active supported non-QCBS templates matching category and tender type', async () => {
  api.active.mockResolvedValue([...templates, template('unknown', 'Other'),
    { ...template('inactive', 'WeightedAverage'), isActive: false },
    { ...template('works', 'WeightedAverage'), category: 'Works' },
    { ...template('rfp', 'WeightedAverage'), tenderType: 'RFP' }]);
  render(<Harness category=" Goods " />);
  await loaded();
  await choose('1. Evaluation Method', 'Standard (non-QCBS)');
  await openSelect('2. Evaluation Template');
  expect(screen.getAllByRole('option').map(option => option.textContent)).toEqual([
    'Select a matching template', 'w (w)', 's (s)', 'p (p)',
  ]);
});

it('filters QCBS, applies settings atomically, and clears selection on method changes in both directions', async () => {
  render(<Harness />);
  await loaded();
  await choose('1. Evaluation Method', 'QCBS — Quality and Cost-Based Selection');
  await openSelect('2. Evaluation Template');
  expect(screen.getAllByRole('option').map(option => option.textContent)).toEqual(['Select a matching template', 'q (q)']);
  await act(async () => { fireEvent.click(screen.getByRole('option', { name: 'q (q)' })); });
  expect(savedForm()).toMatchObject({ evaluationTemplateId: 'q', useQCBSEvaluation: true,
    technicalWeight: 70, financialWeight: 30, minimumTechnicalScore: 80 });
  expect(await screen.findByText('QCBS Configuration (Auto-applied)')).toBeInTheDocument();
  expect(screen.queryByRole('spinbutton', { name: 'Technical Weight (%)' })).not.toBeInTheDocument();
  await choose('1. Evaluation Method', 'Standard (non-QCBS)');
  expect(savedForm()).toMatchObject({ evaluationTemplateId: null, evaluationTemplateName: '', useQCBSEvaluation: false });
  expect(screen.queryByText('QCBS Configuration (Auto-applied)')).not.toBeInTheDocument();
  await choose('2. Evaluation Template', 'w (w)');
  expect(savedForm()).toMatchObject({ evaluationTemplateId: 'w', useQCBSEvaluation: false });
  await choose('1. Evaluation Method', 'QCBS — Quality and Cost-Based Selection');
  expect(savedForm()).toMatchObject({ evaluationTemplateId: null, useQCBSEvaluation: true });
});

it('selects an exact Works template without treating legacy Construction or Goods as Works', async () => {
  const works = { ...template('works', 'WeightedAverage'), category: 'Works' };
  api.active.mockResolvedValue([works, ...templates,
    { ...template('legacy-construction', 'WeightedAverage'), category: 'Construction' }]);
  api.details.mockResolvedValue(works);
  render(<Harness category="Works" />);
  await loaded();
  await choose('1. Evaluation Method', 'Standard (non-QCBS)');
  await openSelect('2. Evaluation Template');
  expect(screen.getAllByRole('option').map(option => option.textContent)).toEqual([
    'Select a matching template', 'works (works)',
  ]);
  await act(async () => { fireEvent.click(screen.getByRole('option', { name: 'works (works)' })); });
  await waitFor(() => expect(savedForm()).toMatchObject({ evaluationTemplateId: 'works', useQCBSEvaluation: false }));
});

it('retains an existing compatible draft and restores template-owned weights', async () => {
  render(<Harness initial={{ ...initialForm, evaluationTemplateId: 'q', useQCBSEvaluation: true }} />);
  await loaded();
  await waitFor(() => expect(savedForm()).toMatchObject({ evaluationTemplateId: 'q', technicalWeight: 70, financialWeight: 30 }));
  expect(screen.getByRole('combobox', { name: '1. Evaluation Method' })).toHaveTextContent('QCBS');
});

it('clears a mismatched existing selection without silently changing the chosen method', async () => {
  render(<Harness initial={{ ...initialForm, evaluationTemplateId: 'q', useQCBSEvaluation: false }} />);
  await loaded();
  await waitFor(() => expect(savedForm().evaluationTemplateId).toBeNull());
  expect(savedForm().useQCBSEvaluation).toBe(false);
  expect(screen.getByRole('combobox', { name: '1. Evaluation Method' })).toHaveTextContent('Standard');
});

it('clears selection when the source category or tender type changes', async () => {
  const rendered = render(<Harness initial={{ ...initialForm, evaluationTemplateId: 'w' }} />);
  await loaded();
  rendered.rerender(<Harness initial={{ ...initialForm, evaluationTemplateId: 'w' }} category="Works" />);
  await waitFor(() => expect(savedForm().evaluationTemplateId).toBeNull());
  expect(screen.getByText(/No active non-QCBS template matches Works and ITB/)).toBeInTheDocument();
  rendered.rerender(<Harness />);
  await choose('2. Evaluation Template', 'w (w)');
  await choose('Tender Type *', 'RFP - Request for Proposal');
  await waitFor(() => expect(savedForm().evaluationTemplateId).toBeNull());
  expect(screen.getByText(/No active non-QCBS template matches Goods and RFP/)).toBeInTheDocument();
});

it('keeps the chosen method when no matching template exists', async () => {
  api.active.mockResolvedValue([template('q', 'QCBS')]);
  render(<Harness />);
  await loaded();
  await choose('1. Evaluation Method', 'Standard (non-QCBS)');
  expect(screen.getByRole('combobox', { name: '2. Evaluation Template' })).toBeDisabled();
  expect(screen.getByText(/No active non-QCBS template/)).toBeInTheDocument();
  expect(savedForm()).toMatchObject({ evaluationTemplateId: null, useQCBSEvaluation: false });
});

it('preserves the newest method choice while loading templates', async () => {
  let resolve!: (value: typeof templates) => void;
  api.active.mockReturnValue(new Promise(value => { resolve = value; }));
  render(<Harness />);
  await choose('1. Evaluation Method', 'QCBS — Quality and Cost-Based Selection');
  await choose('1. Evaluation Method', 'Standard (non-QCBS)');
  await act(async () => resolve(templates));
  await openSelect('2. Evaluation Template');
  expect(screen.queryByRole('option', { name: 'q (q)' })).not.toBeInTheDocument();
  expect(savedForm()).toMatchObject({ evaluationTemplateId: null, useQCBSEvaluation: false });
});

it('offers retry after load failure without auto-selecting a template', async () => {
  const error = vi.spyOn(console, 'error').mockImplementation(() => {});
  api.active.mockRejectedValueOnce(new Error('Unavailable')).mockResolvedValueOnce(templates);
  render(<Harness />);
  expect(await screen.findByText('Evaluation templates could not be loaded.')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
  await loaded();
  expect(screen.queryByText('Evaluation templates could not be loaded.')).not.toBeInTheDocument();
  expect(savedForm().evaluationTemplateId).toBeNull();
  error.mockRestore();
});

it('loads criterion names from the detail endpoint rather than the summary list', async () => {
  api.details.mockResolvedValue({ ...templates[1], criteriaCount: 1, criteria: [{
    criterionName: 'Financial Stability', weight: 100, maxScore: 100, isMandatory: true,
  }] });
  render(<Harness initial={{ ...initialForm, evaluationTemplateId: 'w' }} />);
  expect(await screen.findByText('Financial Stability')).toBeInTheDocument();
});

it('discards old detail responses after method change and clears the loading indicator', async () => {
  let resolve!: (value: ReturnType<typeof template>) => void;
  api.details.mockImplementation((id: string) => id === 'q'
    ? new Promise(value => { resolve = value; }) : Promise.resolve(templates[1]));
  render(<Harness initial={{ ...initialForm, evaluationTemplateId: 'q', useQCBSEvaluation: true }} />);
  expect(await screen.findByText('Loading template details...')).toBeInTheDocument();
  await choose('1. Evaluation Method', 'Standard (non-QCBS)');
  expect(screen.queryByText('Loading template details...')).not.toBeInTheDocument();
  await choose('2. Evaluation Template', 'w (w)');
  await screen.findByText('WeightedAverage');
  await act(async () => resolve(templates[0]));
  expect(savedForm()).toMatchObject({ evaluationTemplateId: 'w', useQCBSEvaluation: false });
  expect(screen.queryByText('QCBS Configuration (Auto-applied)')).not.toBeInTheDocument();
});

it('does not override the method when template details have changed since the list loaded', async () => {
  api.details.mockResolvedValue({ ...templates[1], scoringMethod: 'QCBS' });
  render(<Harness initial={{ ...initialForm, evaluationTemplateId: 'w' }} />);
  expect(await screen.findByRole('alert')).toHaveTextContent('The template configuration changed');
  expect(savedForm().useQCBSEvaluation).toBe(false);
  expect(screen.queryByText('QCBS Configuration (Auto-applied)')).not.toBeInTheDocument();
});
